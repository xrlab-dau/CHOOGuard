"""Sample local video frames with actual source PTS and inexpensive quality hints."""

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import subprocess

import cv2


def sha(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for data in iter(lambda: stream.read(1024*1024), b''):
            h.update(data)
    return h.hexdigest()


def sample(video, output, times, width=960):
    if output.exists() or output.is_symlink():
        raise FileExistsError('Use a new extraction directory')
    if (not 1 <= len(times) <= 80 or type(width) is not int or not 320 <= width <= 1920
            or any(isinstance(t, bool) or not math.isfinite(t) or t < 0 for t in times)
            or any(a >= b for a, b in zip(times, times[1:]))
            or len({round(t * 1000) for t in times}) != len(times)):
        raise ValueError('Invalid bounded, unique, ordered sampling request')
    source_sha = sha(video)
    probe = json.loads(subprocess.check_output([
        'ffprobe','-v','error','-select_streams','v:0','-show_streams','-show_format','-of','json',str(video)], timeout=30))
    stream = probe['streams'][0]
    start = float(stream.get('start_time', 0))
    duration = float(stream.get('duration', probe['format']['duration']))
    if not math.isfinite(start) or not math.isfinite(duration) or duration <= 0 or times[-1] >= duration:
        raise ValueError('Invalid source duration or requested timestamp')
    output.mkdir(parents=True)
    frames = []
    for seconds in times:
        if not 0 <= seconds < duration:
            raise ValueError('Requested timestamp is outside video duration')
        target = output/f't{round(seconds*1000):09d}.jpg'
        command = ['ffmpeg','-hide_banner','-loglevel','info','-nostdin','-threads','2',
                   '-ss',f'{seconds:.6f}','-copyts','-i',str(video),'-map','0:v:0','-frames:v','1',
                   '-vf',f'scale={width}:-2,showinfo','-q:v','3','-fps_mode','passthrough','-n',str(target)]
        run = subprocess.run(command, capture_output=True, text=True, timeout=40, check=True)
        match = re.search(r'n:\s*0\s+pts:\s*\S+\s+pts_time:([\d.]+)',run.stderr)
        if not match or not target.is_file():
            raise ValueError('No source timestamp or decoded frame')
        actual = float(match.group(1))
        if (not math.isfinite(actual) or abs(actual - start - seconds) > .2
                or (frames and actual <= frames[-1]['sourcePtsSeconds'])):
            raise ValueError('Decoded timestamp differs unexpectedly from request')
        image = cv2.imread(str(target))
        if image is None:
            raise ValueError('Decoded frame is not readable')
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        frames.append({'file':target.name,'requestedSeconds':seconds,'sourcePtsSeconds':actual,
                       'sha256':sha(target),'bytes':target.stat().st_size,
                       'width':image.shape[1],'height':image.shape[0],
                       'laplacianVariance':float(cv2.Laplacian(gray, cv2.CV_64F).var()),
                       'meanLuma':float(gray.mean()),'darkPixelFraction':float((gray<12).mean()),
                       'status':'sampled_not_geometry_or_privacy_approved'})
    if sha(video) != source_sha:
        raise ValueError('Source changed during extraction')
    result={'schemaVersion':1,'sourceVideoSha256':source_sha,'sourceDurationSeconds':duration,
            'sourceVideoPath':os.path.relpath(video.resolve(), output.resolve()),
            'sourceStartPtsSeconds':start,
            'sourceDimensions':[probe['streams'][0]['width'],probe['streams'][0]['height']],
            'sampleWidth':width,'frames':frames,'frameCount':len(frames),
            'scope':'Overview thumbnails; source PTS preserved, downsampled JPEG. Quality hints are not reconstruction acceptance.'}
    (output/'samples.json').write_text(json.dumps(result,indent=2)+'\n')
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--video',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--interval',type=float,default=30)
    parser.add_argument('--times',help='Comma-separated explicit source seconds')
    parser.add_argument('--width',type=int,default=960)
    args=parser.parse_args()
    if args.times:
        times=[float(x) for x in args.times.split(',')]
    else:
        duration=float(subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration',
                                               '-of','default=noprint_wrappers=1:nokey=1',str(args.video)]))
        if args.interval<=0:
            raise ValueError('Interval must be positive')
        times=[x*args.interval for x in range(math.ceil(duration/args.interval))]
    if not 1<=len(times)<=80 or not 320<=args.width<=1920:
        raise ValueError('Bounded inspection accepts 1-80 samples at 320-1920px width')
    result=sample(args.video,args.output,times,args.width)
    print(json.dumps({'frames':result['frameCount'],'sourceDuration':result['sourceDurationSeconds']}))


if __name__=='__main__':
    main()

"""Extract exactly four frames and scan the entire short interval for hard cuts locally.

No visual/privacy acceptance: scene-score detection can miss fades or edits. Raw images
and video remain local; stdout contains only scalar diagnostics and hashes.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess

from sample_video_frames import sample, sha


def prepare(video, output, times):
    if len(times) != 4 or not 0 < times[-1] - times[0] <= 10:
        raise ValueError('Exactly four ordered frames spanning at most ten seconds')
    result = sample(video, output, times)
    start = result['frames'][0]['sourcePtsSeconds']
    end = result['frames'][-1]['sourcePtsSeconds']
    command = ['ffmpeg', '-hide_banner', '-nostdin', '-loglevel', 'info', '-threads', '2',
               '-ss', str(times[0]), '-t', str(end - start + .1), '-copyts', '-i', str(video),
               '-an', '-vf', "scale=320:-2,select='gt(scene,0.3)',showinfo", '-f', 'null', '-']
    run = subprocess.run(command, capture_output=True, text=True, timeout=90, check=True)
    cuts = [float(t) for t in re.findall(r'pts_time:([\d.]+)', run.stderr)]
    if sha(video) != result['sourceVideoSha256']:
        raise ValueError('Source changed during cut scan')
    result['sequenceVerification'] = {
        'method': 'ffmpeg-scene-score', 'threshold': .3, 'cutCount': len(cuts),
        'cutPtsSeconds': cuts, 'startPtsSeconds': start, 'endPtsSeconds': end,
        'sourceVideoSha256': result['sourceVideoSha256'],
        'command': command[:command.index(str(video))] + ['<local-source>'] + command[command.index(str(video)) + 1:],
        'visualReview': 'not_performed',
        'limits': 'Hard-cut heuristic only; fades, occlusion and low-parallax degeneracy need separate review.'}
    (output / 'samples.json').write_text(json.dumps(result, indent=2) + '\n')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--video', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--times', required=True)
    args = parser.parse_args()
    result = prepare(args.video, args.output, [float(t) for t in args.times.split(',')])
    print(json.dumps({'frames': result['frameCount'], 'check': result['sequenceVerification']}))

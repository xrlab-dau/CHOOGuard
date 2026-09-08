#!/usr/bin/env python3
"""Check per-object visual provenance and actual generated details; never certify a facility twin."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
from urllib.parse import urlsplit

ROOT=Path(__file__).resolve().parents[2]

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def local_path(root,relative):
    if not isinstance(relative,str) or not relative or '\\' in relative or ':' in relative:
        raise ValueError('invalid relative source path')
    p=PurePosixPath(relative)
    if p.is_absolute() or '..' in p.parts:raise ValueError('source path escapes repository')
    result=(root/relative).resolve();result.relative_to(root.resolve())
    if not result.is_file():raise ValueError('source file is missing: '+relative)
    return result

def public_url(value):
    try:
        u=urlsplit(value)
        return u.scheme=='https' and bool(u.netloc) and not u.username and not u.password
    except (TypeError,ValueError):return False

def validate(registry,manifest,root=ROOT,inventory=None,require_reviewed=False):
    errors=[]; rows=registry.get('assets',[]); generated=manifest.get('assets',[])
    ids=[a.get('id') for a in rows]; models={a.get('id'):a for a in generated}
    if len(set(ids))!=len(ids):errors.append('Duplicate asset reference IDs')
    if set(ids)!=set(models):errors.append('Reference/model coverage differs: '+str(sorted(set(ids)^set(models))))
    if registry.get('version')!=1:errors.append('Unknown reference registry version')
    used={a.get('assetId') for a in inventory.get('instances',[])} if inventory is not None else None
    if used is not None:
        for identifier in used-set(models):errors.append('Unregistered scene geometry family: '+str(identifier))
    dependencies=dict(manifest.get('sourceModuleSha256',{}))
    if manifest.get('generatorSha256'):dependencies['scripts/art/build_station_assets.py']=manifest['generatorSha256']
    for path,expected in dependencies.items():
        try:
            if digest(local_path(root,path))!=expected:errors.append('Regenerate models after source change: '+path)
        except ValueError as error:errors.append(str(error))
    referenced=reviewed=physical=virtual=catalog=0
    for row in rows:
        identifier=row.get('id','?'); model=models.get(identifier)
        if model is None:continue
        kind=row.get('kind');usage=row.get('usage')
        if kind not in {'station_specific','category_proxy','virtual_guidance','structural_base'}:
            errors.append(identifier+': classify the reference scope')
        if usage not in {'scene','virtual','catalog'}:errors.append(identifier+': classify actual usage')
        if kind=='virtual_guidance':virtual+=1
        elif usage=='scene':physical+=1
        if usage=='catalog':catalog+=1
        if used is not None and usage!='catalog' and identifier not in used:errors.append(identifier+': absent from scene inventory')
        if kind!='station_specific' and row.get('stationInstallationVerified'):
            errors.append(identifier+': a proxy or virtual marker cannot prove station installation')
        if not row.get('dimensionsBasis'):errors.append(identifier+': distinguish authored dimensions from published or surveyed values')
        references=row.get('references',[]); valid_refs=bool(references)
        for reference in references:
            images=reference.get('imageUrls',[]);sha=reference.get('imageSha256',[])
            good=public_url(reference.get('pageUrl')) and bool(images) and all(public_url(x) for x in images)
            good=good and bool(sha) and all(isinstance(x,str) and re.fullmatch('[0-9a-f]{64}',x) for x in sha)
            good=good and bool(re.fullmatch(r'\d{4}-\d{2}-\d{2}',reference.get('inspectedOn','')))
            good=good and len(reference.get('observedFeatures',[]))>=2
            if not good:errors.append(identifier+': missing inspected image, source or concrete observations')
            valid_refs=valid_refs and good
        if not references:errors.append(identifier+': no per-object image reference')
        if valid_refs:referenced+=1
        try:
            local_path(root,row.get('module'))
            if row.get('module')!=model.get('sourceModule'):errors.append(identifier+': source module binding differs')
            model_path=local_path(root,model['file'])
            if digest(model_path)!=model['sha256']:errors.append(identifier+': generated model digest changed')
        except (ValueError,KeyError) as error:errors.append(identifier+': '+str(error))
        components=row.get('requiredComponents',[])
        if not components:errors.append(identifier+': no authored feature binding')
        for component in components:
            if model.get('components',{}).get(component,{}).get('count',0)<1:
                errors.append(identifier+': reference detail absent from authored geometry: '+component)
        review=row.get('visualReview',{})
        captures=review.get('captureSha256',[])
        complete=review.get('status')=='reviewed' and review.get('assetSha256')==model.get('sha256') and len(set(captures))>=2
        complete=complete and all(isinstance(x,str) and re.fullmatch('[0-9a-f]{64}',x) for x in captures) and bool(review.get('scope'))
        if complete:reviewed+=1
        if require_reviewed and not complete:errors.append(identifier+': current front/side visual review is missing')
    return {'scope':'per_object_reference_and_authored_geometry','errors':errors,'assetCount':len(models),
            'referencedAssets':referenced,'visuallyReviewedAssets':reviewed,'physicalSceneAssets':physical,
            'virtualGuidanceAssets':virtual,'catalogOnlyAssets':catalog,'sceneInventoryChecked':inventory is not None,
            'facilityFidelityValidated':False,'procedureSuitabilityValidated':False,
            'note':'Image provenance, modeled feature names and visual inspection do not establish surveyed geometry or approved employee procedures.'}

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--registry',default='foundation/art/object-references.json')
    p.add_argument('--manifest',default='foundation/art/asset-manifest.json')
    p.add_argument('--inventory');p.add_argument('--require-reviewed',action='store_true');p.add_argument('--asset',help='Include the exact reference/context record for one asset ID')
    a=p.parse_args()
    try:
        registry=json.loads((ROOT/a.registry).read_text())
        report=validate(registry,json.loads((ROOT/a.manifest).read_text()),ROOT,
                        json.loads(Path(a.inventory).read_text()) if a.inventory else None,a.require_reviewed)
        if a.asset:
            record=next((row for row in registry['assets'] if row['id']==a.asset),None)
            if record is None:report['errors'].append('Unknown asset: '+a.asset)
            else:report['assetContext']=record
    except (ValueError,OSError,TypeError) as error:
        report={'errors':[str(error)],'facilityFidelityValidated':False}
    print(json.dumps(report,ensure_ascii=False,indent=2));return int(bool(report['errors']))

if __name__=='__main__':raise SystemExit(main())

#!/usr/bin/env python3
"""Check per-object visual provenance and actual generated details; never certify a facility twin."""
import argparse
from datetime import date
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
    try:
        result=(root/relative).resolve();result.relative_to(root.resolve())
    except (OSError,RuntimeError) as error:
        raise ValueError('source path cannot be resolved: '+relative) from error
    if not result.is_file():raise ValueError('source file is missing: '+relative)
    return result

def public_url(value):
    if not isinstance(value,str) or any(character.isspace() for character in value):return False
    try:
        u=urlsplit(value);port=u.port
        return u.scheme=='https' and bool(u.hostname) and not u.username and not u.password and (port is None or 0<=port<=65535)
    except (TypeError,ValueError):return False

def valid_inspection_date(value):
    if not isinstance(value,str) or not re.fullmatch(r'\d{4}-\d{2}-\d{2}',value):return False
    try:date.fromisoformat(value)
    except ValueError:return False
    return True

def identified_rows(entries,label,id_key,errors):
    if not isinstance(entries,list):
        errors.append(label+' must be a list')
        return []
    rows=[]
    for entry in entries:
        if not isinstance(entry,dict) or not isinstance(entry.get(id_key),str) or not entry[id_key].strip():
            errors.append(label+': each row requires an object with a nonempty string '+id_key)
            continue
        rows.append(entry)
    return rows

def validate(registry,manifest,root=ROOT,inventory=None,require_reviewed=False):
    errors=[]
    if not isinstance(registry,dict):
        errors.append('Reference registry must be an object');registry={}
    if not isinstance(manifest,dict):
        errors.append('Model manifest must be an object');manifest={}
    rows=identified_rows(registry.get('assets',[]),'Asset references','id',errors)
    generated=identified_rows(manifest.get('assets',[]),'Generated models','id',errors)
    if not rows:errors.append('Reference registry must contain at least one asset')
    if not generated:errors.append('Model manifest must contain at least one asset')
    ids=[a.get('id') for a in rows]; models={a.get('id'):a for a in generated}
    if len(set(ids))!=len(ids):errors.append('Duplicate asset reference IDs')
    if len(models)!=len(generated):errors.append('Duplicate generated model IDs')
    if set(ids)!=set(models):errors.append('Reference/model coverage differs: '+str(sorted(set(ids)^set(models))))
    if registry.get('version')!=1:errors.append('Unknown reference registry version')
    used=None
    if inventory is not None:
        if not isinstance(inventory,dict):
            errors.append('Scene inventory must be an object');inventory={}
        used={a['assetId'] for a in identified_rows(inventory.get('instances',[]),'Scene instances','assetId',errors)}
        unsupported=inventory.get('unsupportedMeshes',[])
        if not isinstance(unsupported,list):
            errors.append('Unsupported scene meshes must be a list');unsupported=[]
        for path in unsupported:
            if not isinstance(path,str) or not path.strip():
                errors.append('Unsupported scene mesh requires a nonempty hierarchy path')
            else:errors.append('Unsupported scene geometry: '+path)
    if used is not None:
        for identifier in used-set(models):errors.append('Unregistered scene geometry family: '+str(identifier))
    dependencies=manifest.get('sourceModuleSha256',{})
    if not isinstance(dependencies,dict):
        errors.append('Source module digests must be an object');dependencies={}
    dependencies=dict(dependencies)
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
        if not isinstance(kind,str) or kind not in {'station_specific','category_proxy','virtual_guidance','structural_base'}:
            errors.append(identifier+': classify the reference scope')
        if not isinstance(usage,str) or usage not in {'scene','virtual','catalog'}:errors.append(identifier+': classify actual usage')
        if kind=='virtual_guidance':virtual+=1
        elif usage=='scene':physical+=1
        if usage=='catalog':catalog+=1
        if used is not None and usage!='catalog' and identifier not in used:errors.append(identifier+': absent from scene inventory')
        if kind!='station_specific' and row.get('stationInstallationVerified'):
            errors.append(identifier+': a proxy or virtual marker cannot prove station installation')
        basis=row.get('dimensionsBasis')
        if not isinstance(basis,str) or not basis.strip():errors.append(identifier+': distinguish authored dimensions from published or surveyed values')
        references=row.get('references',[])
        if not isinstance(references,list):
            errors.append(identifier+': references must be a list of inspected image records')
            references=[]
        valid_refs=bool(references)
        for reference in references:
            if not isinstance(reference,dict):
                errors.append(identifier+': image reference must be an object')
                valid_refs=False
                continue
            images=reference.get('imageUrls',[]);sha=reference.get('imageSha256',[])
            inspected=reference.get('inspectedOn');features=reference.get('observedFeatures',[])
            good=public_url(reference.get('pageUrl')) and isinstance(images,list) and bool(images) and all(isinstance(x,str) and public_url(x) for x in images)
            good=good and isinstance(sha,list) and len(sha)==len(images) and all(isinstance(x,str) and re.fullmatch('[0-9a-f]{64}',x) for x in sha)
            good=good and valid_inspection_date(inspected)
            good=good and isinstance(features,list) and len(features)>=2 and all(isinstance(x,str) and x.strip() for x in features)
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
        if not isinstance(components,list):
            errors.append(identifier+': required components must be a list');components=[]
        if not components:errors.append(identifier+': no authored feature binding')
        model_components=model.get('components',{})
        if not isinstance(model_components,dict):
            errors.append(identifier+': generated components must be an object');model_components={}
        for component in components:
            if not isinstance(component,str) or not component.strip():
                errors.append(identifier+': required component must be a nonempty string')
                continue
            detail=model_components.get(component,{})
            count=detail.get('count',0) if isinstance(detail,dict) else 0
            if type(count) is not int or count<1:
                errors.append(identifier+': reference detail absent from authored geometry: '+component)
        review=row.get('visualReview',{})
        if not isinstance(review,dict):
            errors.append(identifier+': visual review must be an object');review={}
        captures=review.get('captureSha256',[])
        complete=review.get('status')=='reviewed' and review.get('assetSha256')==model.get('sha256') and isinstance(captures,list)
        complete=complete and all(isinstance(x,str) and re.fullmatch('[0-9a-f]{64}',x) for x in captures) and len(set(captures))>=2
        complete=complete and isinstance(review.get('scope'),str) and bool(review['scope'].strip())
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
        inventory=json.loads(Path(a.inventory).read_text()) if a.inventory else None
        if a.inventory and inventory is None:raise ValueError('Scene inventory must be an object')
        report=validate(registry,json.loads((ROOT/a.manifest).read_text()),ROOT,inventory,a.require_reviewed)
        if a.asset:
            entries=registry.get('assets',[]) if isinstance(registry,dict) else []
            if not isinstance(entries,list):entries=[]
            record=next((row for row in entries if isinstance(row,dict) and row.get('id')==a.asset),None)
            if record is None:report['errors'].append('Unknown asset: '+a.asset)
            else:report['assetContext']=record
    except (ValueError,OSError,TypeError) as error:
        report={'errors':[str(error)],'facilityFidelityValidated':False}
    print(json.dumps(report,ensure_ascii=False,indent=2));return int(bool(report['errors']))

if __name__=='__main__':raise SystemExit(main())

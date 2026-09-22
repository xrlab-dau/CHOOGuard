import pathlib,sys,json
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';sys.path.insert(0,str(D/'importer-isolated'));from sketchup_importer import sketchup
m=sketchup.Model.from_file(str(D/'station-extracted/부산역(수정).skp'));e=m.entities
for i in [56,0,0,12]:e=list(e.groups)[i].entities
j={'instances':[{'name':i.name,'definition':i.definition.name} for i in e.instances],'groups':[g.name for g in e.groups]};(D/'source-letter-definitions.json').write_text(json.dumps(j,ensure_ascii=False,indent=2));print(j)

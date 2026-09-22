import sys,pathlib,json
D=pathlib.Path.cwd()/'asset-library/research-public/2026-09-21/quality-a/station-model-source';sys.path.insert(0,str(D/'importer-isolated'));from sketchup_importer import sketchup
m=sketchup.Model.from_file(str(D/'station-extracted/부산역(수정).skp'))
g={'componentNames':[c.name for c in m.component_definitions],'materials':m.NumMaterials(),'definitions':m.NumComponentDefinitions(),'scenes':[s.name for s in m.scenes],'layers':[l.name for l in m.layers],'rootGroups':[g.name for g in m.entities.groups],'rootInstances':[{'name':i.name,'definition':i.definition.name,'transform':i.transform} for i in m.entities.instances]};(D/'station-sdk-structure.json').write_text(json.dumps(g,ensure_ascii=False,indent=2));print(json.dumps(g,ensure_ascii=False)[:3000])

from pathlib import Path
import shutil,json,hashlib
r=Path('.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25').resolve();o=r/'storyway-north';s=o/'sources';a='Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-north';imports=[]
def stage(src,dest):
 src=Path(src);dst=s/'construction'/dest;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst);imports.append({'source':str(dst),'destination':dest,'sha256':hashlib.sha256(dst.read_bytes()).hexdigest()});return a+'/'+dest
models={}
models['shelf']=stage(r/'pharmacy/sources/store-shelves/SingleFace-Shelf.fbx','Models/SingleFace-Shelf.fbx')
models['fridge']=stage(r/'free-assets/commercial-refrigerator/CommercialRefrigerator-Empty.fbx','Models/Commercial-Fridge.fbx')
models['pos']=stage(r/'free-assets/pos-terminal/POS-Terminal.fbx','Models/POS-Terminal.fbx')
models['water']=stage(r/'subway/sources/free-models/water_bottle/WaterBottle.fbx','Models/WaterBottle.fbx')
models['counter']=stage(r/'american-trailer/sources/free-models/counter/counter-source-unity.fbx','Models/Checkout-Counter.fbx')
for name in ['soda-can','soda-bottle','bag-flat','chocolate-wrapper','cup','chips','cereal']:
 f=r/f'free-assets/kenney-food-kit/Models/FBX format/{name}.fbx'
 if f.exists():models[name]=stage(f,f'Models/Kenney/{name}.fbx')
tex={}
for name,src in {
 'kenney':r/'free-assets/kenney-food-kit/Models/FBX format/Textures/colormap.png',
 'fridge_base':r/'free-assets/commercial-refrigerator/textures/Image_9.png',
 'fridge_inside':r/'free-assets/commercial-refrigerator/textures/Image_6.png',
 'fridge_metal':r/'free-assets/commercial-refrigerator/textures/URP_Case_MetallicSmoothness.png',
 'fridge_inside_metal':r/'free-assets/commercial-refrigerator/textures/URP_Interior_MetallicSmoothness.png',
 'pos_base':r/'free-assets/pos-terminal/textures/Image_0.png',
 'pos_normal':r/'free-assets/pos-terminal/textures/Image_2.png',
 'pos_normal2':r/'free-assets/pos-terminal/textures/Image_3.png',
 'water_base':r/'subway/sources/free-models/water_bottle/textures/Image_0.png',
 'water_normal':r/'subway/sources/free-models/water_bottle/textures/Image_2.png',
}.items():tex[name]=stage(src,'Textures/'+name+'.png')
(o/'PREPARED-SOURCE-IMPORTS.json').write_text(json.dumps({'imports':imports,'models':models,'textures':tex},indent=2))

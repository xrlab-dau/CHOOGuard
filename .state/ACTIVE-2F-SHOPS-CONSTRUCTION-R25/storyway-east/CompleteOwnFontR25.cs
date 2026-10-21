using System;using System.IO;using System.Linq;using System.Collections.Generic;
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
using TMPro;using UnityEngine.TextCore.LowLevel;using Newtonsoft.Json.Linq;
public static class CompleteOwnFontR25 {
 const string Root="/Users/um-yunsang/CHOOGuard",Thread="classic:/root/r25_east_storyway",Scope="C180_R25_STORYWAY_EAST_FONT_002";
 static void Guard(){var a=JObject.Parse(File.ReadAllText(Root+"/.state/WRITER-AUTHORITY.json"));if((string)a["status"]!="ACTIVE"||(bool?)a["Native_inflight"]!=true||(string)a["sole_writer_thread"]!=Thread||(string)a["scope"]!=Scope)throw new Exception("Exactownstorefontauthorrequired");var g=JObject.Parse(File.ReadAllText(Root+"/"+(string)a["delegated_author_lease"]));if((string)g["grant_nonce"]!=(string)a["scene_worker_grant_nonce"])throw new Exception("Exactnonce required");}
 public static string Apply(){Guard();var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/ChooGuard/Scenes/FpsStation.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("StoppedFpsStationrequired");
  var own=GameObject.Find("FPSWorld/Architecture/2F/MAIN2F_R25_SOURCE_BOUND_SPECIALIST_SHOPS/R25_storyway-east_SOURCE_BOUND_MODEL");if(own==null)throw new Exception("Exactsavedownrootrequired");
  var texts=own.GetComponentsInChildren<TextMeshPro>(true);string all=string.Join("",texts.Select(x=>System.Text.RegularExpressions.Regex.Replace(x.text,"<[^>]+>","")));string chars=new string(all.Distinct().ToArray());
  string path="Assets/ChooGuard/Art/StationInterior/R25_Shops/storyway-east/OWN_COMPLETE_REQUIRED_CHARACTERS_FONT_R25.asset";if(File.Exists(Root+"/"+path))throw new Exception("Noautomaticfontreplay");
  var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf");if(source==null)throw new Exception("OwnsourceOTFrequired");
  var font=TMP_FontAsset.CreateFontAsset(source,90,9,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);font.name="R25_STORYWAY_EAST_COMPLETE_OBSERVED_SIGN_CHARACTERS";
  string missing;bool added=font.TryAddCharacters(chars,out missing);if(!added&&!string.IsNullOrEmpty(missing))throw new Exception("OriginalOTFmissingrequiredcharacters:"+missing);
  font.atlasPopulationMode=AtlasPopulationMode.Static;AssetDatabase.CreateAsset(font,path);
  foreach(var tex in font.atlasTextures){if(tex!=null&&!AssetDatabase.Contains(tex)){tex.name="R25_STORYWAY_EAST_OWN_SIGN_ATLAS";tex.hideFlags=HideFlags.None;AssetDatabase.AddObjectToAsset(tex,font);}}
  if(font.material!=null&&!AssetDatabase.Contains(font.material)){font.material.name="R25_STORYWAY_EAST_OWN_SIGN_MATERIAL";font.material.hideFlags=HideFlags.None;AssetDatabase.AddObjectToAsset(font.material,font);}
  Undo.RecordObjects(texts.Cast<UnityEngine.Object>().ToArray(),"R25ownsourceobservedsignfont");
  foreach(var text in texts){text.font=font;text.fontSharedMaterial=font.material;EditorUtility.SetDirty(text);}
  EditorUtility.SetDirty(font);Guard();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Ownfontsavefailed");
  var r=new JObject{["status"]="OWN_REQUIRED_SIGN_CHARACTERS_AUTHORED_AND_SAVED",["utc"]=DateTime.UtcNow.ToString("o"),["author"]=Thread,["scope"]=Scope,["font_asset"]=path,["original_source_font_fixed"]=true,["required_characters"]=chars,["missing_characters"]=missing,["atlas_count"]=font.atlasTextures.Length,["own_text_objects"]=texts.Length,["SceneQA_tests_Play_capture_reopen"]=false};File.WriteAllText(Root+"/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/storyway-east/APPLIED-OWN-FONT-R25.json",r.ToString());return r.ToString();
 }
}

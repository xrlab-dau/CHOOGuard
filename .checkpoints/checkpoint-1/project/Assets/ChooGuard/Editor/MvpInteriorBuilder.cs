using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor
{
 // Public guides locate zones, but do not verify individual furnishings, partitions, columns or canopy positions.
 // Preserve the separately built source floors and models; remove the former game-authored reconstruction.
 public static class MvpInteriorBuilder
 {
  const string InteriorRoot = "공개 안내도 기반 내부 · 미터 시각 재구성";
  const string CanopyRoot = "승강장 구조 · 파생 캐노피";

  // Legacy call surface used by MvpStationBuilder. Footprints cannot establish a measured interior fit-out.
  public static void Build(Transform[] floors, List<Vector2> footprint, List<Vector2> balcony)
  {
   if(floors==null)throw new ArgumentNullException(nameof(floors));
   int removed=0;
   foreach(var floor in floors)if(floor!=null)removed+=RemoveChildrenNamed(floor,InteriorRoot);
   Debug.Log("MVP unverified interior reconstruction removed="+removed+"; new furnishings require exact-object site-placement evidence.");
  }

  [MenuItem("ChooGuard/MVP/Remove Unverified Interior Reconstruction")]
  public static void UpgradeExistingFunctionalZones()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("편집 모드에서 내부 구획을 정리하세요.");
   var scene=SceneManager.GetSceneByPath(MvpWorkspaceBuilder.ScenePath);
   if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("훈련 장면을 먼저 여세요.");
   var station=scene.GetRootGameObjects().Select(g=>g.GetComponent<MvpStationView>()).FirstOrDefault(v=>v!=null);
   if(station==null||station.FloorRoots==null||station.FloorRoots.Length!=3)throw new InvalidOperationException("기존 역사 세 층이 필요합니다.");
   Build(station.FloorRoots,null,null);
   foreach(var floor in station.FloorRoots)if(floor!=null)Platform(floor,null);
   BuildWorldHubs(station.transform);
   EditorSceneManager.MarkSceneDirty(scene);
   // Root owns the scene save and rendered verification; existing source floors, shells and controls are retained.
  }

  public static void Platform(Transform floor,List<Vector2> poly)
  {
   if(floor!=null)RemoveChildrenNamed(floor,CanopyRoot);
   // A platform polygon supplies no observed canopy, column or safety-line placement.
  }

  public static void BuildWorldHubs(Transform parent)
  {
   if(parent==null)throw new ArgumentNullException(nameof(parent));
   RemoveChildrenNamed(parent,"부산항 국제여객터미널 · 공개홀 및 훈련용 내부");
   RemoveChildrenNamed(parent,"자갈치시장 · 공개홀 및 훈련용 내부");
   // Source landmark centroids/envelopes locate the exterior, not terminal or market room partitions and fixtures.
  }

  public static Transform BuildTrainingHub(Transform parent,string title,Vector3 sourceAnchor,float footprintWidth,float footprintDepth,bool market)
  {
   throw new InvalidOperationException("훈련용 임의 내부는 생성하지 않습니다. 실제 오브젝트와 현장 위치를 확인하는 근거가 필요합니다.");
  }

  static int RemoveChildrenNamed(Transform parent,string name)
  {
   int removed=0;
   for(int i=parent.childCount-1;i>=0;i--)
   {
    var child=parent.GetChild(i);
    if(child.name!=name)continue;
    Object.DestroyImmediate(child.gameObject);removed++;
   }
   return removed;
  }
 }
}

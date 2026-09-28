using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public static class PopulateRealAssets
{
    const string BenchPrefab = "Assets/ChooGuard/Art/StationInterior/Props/StationBench4.fbx";
    const string KioskPrefab = "Assets/ChooGuard/Art/StationInterior/Props/InfoKiosk.fbx";
    const string AtmPrefab = "Assets/ChooGuard/Art/StationInterior/Props/AtmKiosk.fbx";
    const string TrashPrefab = "Assets/ChooGuard/Art/StationInterior/Props/TrashCan.fbx";
    const string BakeryPrefab = "Assets/ChooGuard/Art/StationInterior/Props/BakeryDisplay.fbx";
    const string ShelfPrefab = "Assets/ChooGuard/Art/StationInterior/Props/StoreShelf.fbx";

    public static void Main(string[] args)
    {
        var benchGo = AssetDatabase.LoadAssetAtPath<GameObject>(BenchPrefab);
        var kioskGo = AssetDatabase.LoadAssetAtPath<GameObject>(KioskPrefab);
        var atmGo = AssetDatabase.LoadAssetAtPath<GameObject>(AtmPrefab);
        var trashGo = AssetDatabase.LoadAssetAtPath<GameObject>(TrashPrefab);
        var bakeryGo = AssetDatabase.LoadAssetAtPath<GameObject>(BakeryPrefab);
        var shelfGo = AssetDatabase.LoadAssetAtPath<GameObject>(ShelfPrefab);

        if (benchGo == null || kioskGo == null || atmGo == null || trashGo == null)
            throw new InvalidOperationException("필수 프랍 FBX 로드 실패");

        var propsRoot = GameObject.Find("부산역 역사 내부 · 집기");
        if (propsRoot == null) throw new InvalidOperationException("집기 루트 없음");

        int benchSwapped = 0, kioskSwapped = 0, atmSwapped = 0, trashSwapped = 0;

        // 1. 벤치 교체
        var benchGroup = propsRoot.transform.Find("bench3");
        if (benchGroup != null)
        {
            foreach (Transform t in benchGroup)
            {
                // 기존 큐브 프리미티브 자식들 제거
                var kill = new List<GameObject>();
                foreach (Transform c in t) kill.Add(c.gameObject);
                foreach (var g in kill) UnityEngine.Object.DestroyImmediate(g);

                // 실제 3D 벤치 모델 부착
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(benchGo);
                inst.name = "Model";
                inst.transform.SetParent(t, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                benchSwapped++;
            }
        }

        // 2. 키오스크 교체
        var kioskGroup = propsRoot.transform.Find("info_kiosk");
        if (kioskGroup != null)
        {
            foreach (Transform t in kioskGroup)
            {
                var kill = new List<GameObject>();
                foreach (Transform c in t) kill.Add(c.gameObject);
                foreach (var g in kill) UnityEngine.Object.DestroyImmediate(g);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(kioskGo);
                inst.name = "Model";
                inst.transform.SetParent(t, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                kioskSwapped++;
            }
        }

        // 3. 발권기 교체
        var tvmGroup = propsRoot.transform.Find("ticket_vending_machine");
        if (tvmGroup != null)
        {
            foreach (Transform t in tvmGroup)
            {
                var kill = new List<GameObject>();
                foreach (Transform c in t) kill.Add(c.gameObject);
                foreach (var g in kill) UnityEngine.Object.DestroyImmediate(g);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(atmGo);
                inst.name = "Model";
                inst.transform.SetParent(t, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                atmSwapped++;
            }
        }

        // 4. 쓰레기통 교체
        var trashGroup = propsRoot.transform.Find("trash_bin");
        if (trashGroup != null)
        {
            foreach (Transform t in trashGroup)
            {
                var kill = new List<GameObject>();
                foreach (Transform c in t) kill.Add(c.gameObject);
                foreach (var g in kill) UnityEngine.Object.DestroyImmediate(g);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(trashGo);
                inst.name = "Model";
                inst.transform.SetParent(t, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = new Vector3(0.007f, 0.007f, 0.007f); // cm -> m
                trashSwapped++;
            }
        }

        // 5. 점포 내부 채우기 (2F 매장 전면 유리 뒤편)
        int bakeryPlaced = 0, shelfPlaced = 0;
        var finish = GameObject.Find("부산역 역사 내부 · 마감");
        if (finish != null && bakeryGo != null && shelfGo != null)
        {
            var lvl2 = finish.transform.Find("2F");
            var stores = lvl2 != null ? lvl2.Find("2F 임대매장(실면적)") : null;
            if (stores != null)
            {
                // 점포별 바닥 위치를 찾아 내부에 디스플레이 배치
                var storePos = new Dictionary<string, Transform>();
                foreach (Transform t in stores)
                {
                    if (t.name.EndsWith("바닥", StringComparison.Ordinal))
                    {
                        string sname = t.name.Substring(0, t.name.Length - 3);
                        storePos[sname] = t;
                    }
                }

                foreach (var kv in storePos)
                {
                    string nm = kv.Key;
                    var floorTr = kv.Value;
                    Vector3 pos = floorTr.position + Vector3.up * 0.05f;
                    Quaternion rot = floorTr.rotation;

                    if (nm.Contains("제과") || nm.Contains("빵") || nm.Contains("도넛") || nm.Contains("크리스피") || nm.Contains("서브웨이"))
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(bakeryGo);
                        inst.name = "BakeryDisplayInterior";
                        inst.transform.position = pos;
                        inst.transform.rotation = rot;
                        inst.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
                        inst.transform.SetParent(floorTr.parent, true);
                        bakeryPlaced++;
                    }
                    else if (nm.Contains("편의점") || nm.Contains("스토리웨이") || nm.Contains("올리브영") || nm.Contains("약국") || nm.Contains("특산품"))
                    {
                        // 선반 2열 배치
                        for (int row = -1; row <= 1; row += 2)
                        {
                            var inst = (GameObject)PrefabUtility.InstantiatePrefab(shelfGo);
                            inst.name = "StoreShelfInterior_" + row;
                            Vector3 offset = rot * new Vector3(0f, 0f, row * 1.5f);
                            inst.transform.position = pos + offset;
                            inst.transform.rotation = rot;
                            inst.transform.localScale = Vector3.one;
                            inst.transform.SetParent(floorTr.parent, true);
                            shelfPlaced++;
                        }
                    }
                }
            }
        }

        // 6. 2F 중앙 기둥 4면에 대형 LED 디지털 사이니지 장착
        int adsPlaced = 0;
        var interior = GameObject.Find("부산역 역사 내부");
        var cols = interior != null ? interior.transform.Find("2F · 지상 2층/기둥 격자") : null;
        if (cols != null)
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var adMat = new Material(unlit);
            adMat.name = "AdScreen_DigitalSignage";
            // KTX/코레일 블루 LED 발광색
            adMat.SetColor("_BaseColor", new Color(0.15f, 0.55f, 0.95f, 1f));

            foreach (Transform c in cols)
            {
                // 중앙 대합실 영역의 기둥만 선택 (u -40~40, v -35~0)
                float cu = c.position.x * Mathf.Sin(16.2f * Mathf.Deg2Rad) + c.position.z * Mathf.Cos(16.2f * Mathf.Deg2Rad);
                float cv = c.position.x * Mathf.Cos(16.2f * Mathf.Deg2Rad) - c.position.z * Mathf.Sin(16.2f * Mathf.Deg2Rad);
                if (cu < -35f || cu > 35f || cv < -35f || cv > 0f) continue;

                // 기둥 높이 약 4.0m 지점(눈높이 바로 위)에 4면 대형 스크린 장착
                for (int side = 0; side < 4; side++)
                {
                    float angle = side * 90f + 16.2f;
                    var q = Quaternion.Euler(0f, angle, 0f);
                    Vector3 nrm = q * Vector3.forward;

                    var adScreen = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    adScreen.name = "Column_LED_AdScreen_" + side;
                    adScreen.transform.SetParent(c, false);
                    adScreen.transform.localPosition = new Vector3(0f, 1.2f, 0f) + nrm * 0.55f;
                    adScreen.transform.localRotation = Quaternion.Euler(0f, angle + 180f, 0f);
                    adScreen.transform.localScale = new Vector3(1.8f, 1.1f, 1f);

                    var mr = adScreen.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = adMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    UnityEngine.Object.DestroyImmediate(adScreen.GetComponent<Collider>());
                    adsPlaced++;
                }
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        string report = string.Format("POPULATE_REAL_ASSETS: Benches={0}, Kiosks={1}, ATMs={2}, Trash={3}, BakeryDisplays={4}, Shelves={5}, ColumnAdScreens={6}",
            benchSwapped, kioskSwapped, atmSwapped, trashSwapped, bakeryPlaced, shelfPlaced, adsPlaced);
        Debug.Log(report);
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/populate-assets.txt"), report);
    }
}

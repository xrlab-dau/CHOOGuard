using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace ChooGuard.App.Fps.Work
{
    // 한 설비의 점검 결과. 플레이어가 기재한 판정과 월드가 실제로 어떻게 됐는지를 함께 남긴다 —
    // 둘이 다를 수 있고(오판정), 다음 회차는 그 차이를 알아야 한다.
    [Serializable]
    public class FacilityOutcome
    {
        public string facilityId;            // 씬에서 유일한 유닛 이름. 교체해도 바뀌지 않는다.
        public string serial;                // 점검 종료 시점의 고유번호(교체됐다면 교체품 번호)
        public string replacedFromSerial;    // 교체 전 번호. 비어 있으면 교체되지 않은 것이다.
        public string verdict;               // NOT_RECORDED / FIT / UNFIT — 플레이어가 기재한 것
        public bool tagAttached;
        public bool repairOrderIssued;
        public bool serviceCompleted;        // 기술자가 실제로 교체했는가
        public bool resultWitnessed;         // 플레이어가 결과를 확인했는가
        public string closedAtUtc;
    }

    [Serializable]
    public class TutorialCarryoverData
    {
        public int version=1;
        public string procedureId="";
        public string savedAtUtc="";
        public List<FacilityOutcome> facilities=new List<FacilityOutcome>();

        public FacilityOutcome Find(string facilityId)
        {
            if(string.IsNullOrEmpty(facilityId)||facilities==null)return null;
            foreach(var f in facilities)if(f!=null&&f.facilityId==facilityId)return f;
            return null;
        }
    }

    // 튜토리얼 결과의 지속 저장소.
    //
    // ChooGuard.Persistence 를 쓰지 않는 이유를 기록해 둔다. SqliteProvider 는 주석에
    // "Currently macOS only; other ABIs fail closed" 라고 적혀 있어 Windows 에서 열리지 않고,
    // SqliteRunStore 는 ICommitMaterializer·readSet 검증·blob 복호를 요구하는 운영 커밋 저장소라
    // 점검 결과 보관과 목적이 다르다. 여기서 필요한 것은 작은 결과 기록 하나다.
    //
    // 읽기는 절대 던지지 않는다 — 저장 파일이 없거나 깨졌다고 튜토리얼을 시작하지 못하면 안 된다.
    // 쓰기는 임시 파일에 먼저 쓰고 교체한다. 도중에 죽어도 이전 기록이 남는다.
    public static class TutorialCarryoverStore
    {
        public const int CurrentVersion=1;
        private const string DefaultRelativePath="chooguard/tutorial-carryover.json";

        // 시험이 임시 경로를 꽂는다. 비어 있으면 기본 경로를 쓴다.
        public static string OverridePath="";

        // UnityEngine 를 명시한다 — 이 네임스페이스가 ChooGuard.App.* 안이라
        // 그냥 Application 이라고 쓰면 프로젝트의 ChooGuard.Application 이 먼저 잡힌다.
        public static string ResolvedPath
            =>string.IsNullOrEmpty(OverridePath)
                ? Path.Combine(UnityEngine.Application.persistentDataPath,DefaultRelativePath)
                : OverridePath;

        public static TutorialCarryoverData Load()
        {
            var path=ResolvedPath;
            try
            {
                if(!File.Exists(path))return new TutorialCarryoverData();
                var text=File.ReadAllText(path);
                var data=JsonUtility.FromJson<TutorialCarryoverData>(text);
                // 알 수 없는 판본을 조용히 받아들이지 않는다. 다만 시작을 막지도 않는다 —
                // 비어 있는 것으로 읽고 한 번만 알린다.
                if(data==null||data.version!=CurrentVersion)
                {
                    Debug.LogWarning("[이월] 읽을 수 없는 기록이라 비어 있는 것으로 시작합니다 · version="+(data==null?"null":data.version.ToString())+" · "+path);
                    return new TutorialCarryoverData();
                }
                if(data.facilities==null)data.facilities=new List<FacilityOutcome>();
                return data;
            }
            catch(Exception ex)
            {
                Debug.LogWarning("[이월] 기록을 읽지 못해 비어 있는 것으로 시작합니다 · "+ex.Message+" · "+path);
                return new TutorialCarryoverData();
            }
        }

        public static bool Save(TutorialCarryoverData data)
        {
            if(data==null)return false;
            var path=ResolvedPath;
            string temp=null;
            try
            {
                var directory=Path.GetDirectoryName(path);
                if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
                data.version=CurrentVersion;
                data.savedAtUtc=DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                temp=path+".writing";
                File.WriteAllText(temp,JsonUtility.ToJson(data,true));
                // File.Replace 는 대상이 있어야 한다. 첫 저장이면 그냥 옮긴다.
                if(File.Exists(path))File.Replace(temp,path,null);
                else File.Move(temp,path);
                return true;
            }
            catch(Exception ex)
            {
                Debug.LogError("[이월] 기록을 쓰지 못했습니다 · "+ex.Message+" · "+path);
                try { if(temp!=null&&File.Exists(temp))File.Delete(temp); } catch { /* 정리 실패는 삼킨다 */ }
                return false;
            }
        }

        // 한 설비의 결과를 갱신해 저장한다. 같은 설비를 다시 점검하면 최신 결과로 덮는다.
        public static bool Record(string procedureId,FacilityOutcome outcome)
        {
            if(outcome==null||string.IsNullOrEmpty(outcome.facilityId))return false;
            var data=Load();
            if(!string.IsNullOrEmpty(procedureId))data.procedureId=procedureId;
            var existing=data.Find(outcome.facilityId);
            if(existing!=null)data.facilities.Remove(existing);
            data.facilities.Add(outcome);
            return Save(data);
        }

        // 시험과 '처음부터 다시' 용도. 파일이 없으면 성공으로 본다.
        public static bool Clear()
        {
            var path=ResolvedPath;
            try { if(File.Exists(path))File.Delete(path); return true; }
            catch(Exception ex){ Debug.LogWarning("[이월] 기록을 지우지 못했습니다 · "+ex.Message); return false; }
        }
    }
}

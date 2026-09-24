using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
namespace ChooGuard.App.Fps
{
    // 첫 씬에서 실제 플레이 씬으로 넘어간다. 이것이 없으면 빌드 설정에 두 씬이 등록돼 있어도
    // 빌드해서 실행했을 때 Bootstrap 에 머물러 튜토리얼에 도달하지 못한다(2026-09-24 확인).
    //
    // 씬 이름으로 부른다 — 빌드 인덱스는 씬을 추가·재정렬할 때 조용히 어긋난다.
    public sealed class SceneEntryPoint : MonoBehaviour
    {
        public string SceneName="FpsStation";
        // 시작하자마자 넘기지 않는다. 첫 화면이 두 프레임 만에 사라지면 표시할 것을 표시할 수 없고,
        // 실제로 기존 시험(CSBOOT0101)이 카메라·캔버스를 찾지 못해 깨졌다(2026-09-24).
        public bool LoadOnStart;
        public bool WaitForInput=true;

        private void Start(){ if(LoadOnStart)Load(); }

        // 아무 입력에나 넘어가지 않는다. 이 씬에는 Canvas·EventSystem 이 있고, 클릭을 가로채면
        // 그 UI 를 쓰는 쪽이 통째로 사라진다 — 실제로 입력 라우터 시험이 이 때문에 깨졌다(2026-09-24).
        // 확인 키만 받는다. 버튼을 붙일 때는 WaitForInput 을 끄고 Load() 를 직접 연결하면 된다.
        private void Update()
        {
            if(!WaitForInput||loaded)return;
            var keyboard=Keyboard.current;
            if(keyboard==null)return;
            if(keyboard.enterKey.wasPressedThisFrame||keyboard.numpadEnterKey.wasPressedThisFrame)Load();
        }

        private bool loaded;

        // 실패를 삼키지 않는다. 빌드 설정에서 빠지면 조용히 아무 일도 안 일어나는 대신 크게 알린다.
        public bool Load()
        {
            if(string.IsNullOrWhiteSpace(SceneName))
            {
                Debug.LogError("[진입] 이동할 씬 이름이 비었습니다.",this);
                return false;
            }
            if(SceneUtility.GetBuildIndexByScenePath(SceneName)<0
               &&SceneUtility.GetBuildIndexByScenePath("Assets/ChooGuard/Scenes/"+SceneName+".unity")<0)
            {
                Debug.LogError("[진입] 빌드 설정에 없는 씬입니다 · "+SceneName,this);
                return false;
            }
            loaded=true;                       // 한 프레임에 두 번 부르지 않는다
            SceneManager.LoadScene(SceneName);
            return true;
        }
    }
}

#if UNITY_INCLUDE_TESTS
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using ChooGuard.Presentation.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class CSPLAY0102Tests
    {
        private GameObject root;
        private InputContextRouter router;
        private InputActionAsset source;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("입력 테스트", typeof(EventSystem), typeof(InputSystemUIInputModule));
            router = root.AddComponent<InputContextRouter>();
            source = InputActionAsset.FromJson(File.ReadAllText(Path.Combine(global::UnityEngine.Application.dataPath,
                "ChooGuard/Presentation/Input/Operations.inputactions")));
            router.Configure(root.GetComponent<EventSystem>(), root.GetComponent<InputSystemUIInputModule>(), source);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                // EditMode에서는 네이티브 생명주기 대신 관리 코드 콜백을 직접 호출한다.
                if (router != null) InvokeRouterCallback("OnDisable");
            }
            finally
            {
                // 부분 설정 또는 콜백 실패에도 두 객체의 정리를 시도하며 예외는 숨기지 않는다.
                try { if (root != null) Object.DestroyImmediate(root); }
                finally { if (source != null) Object.DestroyImmediate(source); }
            }
        }

        private void InvokeRouterCallback(string methodName)
        {
            var method = typeof(InputContextRouter).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"라우터 콜백을 찾을 수 없다: {methodName}");
            try
            {
                method.Invoke(router, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                // 반사 호출 래퍼가 아니라 실제 콜백 예외와 원래 스택을 전달한다.
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        [Test]
        public void InjectedComposition_DisablesGame_NotUi_AndDoesNotStoreCompositionText()
        {
            router.SetCompositionActive(true);
            Assert.That(router.RuntimeActions.FindActionMap("UI").enabled, Is.True);
            Assert.That(router.RuntimeActions.FindActionMap("Game").enabled, Is.False);
            Assert.That(router.KeyboardOwner, Is.EqualTo(InputContext.Text));
        }

        [Test]
        public void InjectedComposition_EscapeCannotAlsoCloseModal()
        {
            var modal = new GameObject("모달");
            try
            {
                router.OpenModal(modal, null);
                router.SetCompositionActive(true);
                Assert.That(router.RouteCancel(), Is.EqualTo(InputContext.Text));
                router.SetCompositionActive(false);
                Assert.That(router.RouteCancel(), Is.EqualTo(InputContext.Text), "같은 프레임 조합 종료를 모달 취소로 재해석하면 안 된다");
                Assert.That(router.ActiveModal, Is.SameAs(modal));
                Assert.That(modal.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(modal); }
        }

        [Test]
        public void ApplicationFocusLoss_CancelsCapture_DisablesBothMaps_AndOrphanUpDoesNotRoute()
        {
            var routed = 0;
            router.BackgroundInput += _ => routed++;
            router.RoutePointerDown(17, new Vector2(-100, -100));
            Assert.That(routed, Is.EqualTo(1));
            routed = 0;
            router.SetApplicationFocus(false);
            router.RoutePointerUp(17, Vector2.zero);
            router.SetApplicationFocus(true);
            router.RoutePointerUp(17, Vector2.zero);
            Assert.That(routed, Is.Zero);
            Assert.That(router.CapturedPointerCount, Is.Zero);
            Assert.That(router.RuntimeActions.FindActionMap("UI").enabled, Is.True);
        }

        [Test]
        public void CloneAndModule_ShareRuntimeOnly_AndExplicitDisableCallbackReleasesOwnership()
        {
            var module = root.GetComponent<InputSystemUIInputModule>();
            var runtime = router.RuntimeActions;
            Assert.That(runtime, Is.Not.SameAs(source));
            Assert.That(module.actionsAsset, Is.SameAs(runtime));
            Assert.That(module.leftClick.action.actionMap.asset, Is.SameAs(runtime));
            Assert.That(module.cancel, Is.Null, "router owns cancel; TMP keeps its native Escape handling");
            router.enabled = false;
            // 명시적 관리 코드 콜백 단위 시험이며 실제 활성화 생명주기는 PlayMode에서 검증한다.
            InvokeRouterCallback("OnDisable");
            Assert.That(router.RuntimeActions, Is.Null);
            Assert.That(runtime == null, Is.True);
            Assert.That(source.enabled, Is.False);
            Assert.That(module.enabled, Is.False);
            router.enabled = true;
            InvokeRouterCallback("OnEnable");
            Assert.That(router.RuntimeActions, Is.Not.Null);
            Assert.That(module.actionsAsset, Is.SameAs(router.RuntimeActions));
        }

        [Test]
        public void DuplicateDownOrOrphanRelease_DoesNotEmitAdditionalBackgroundInput()
        {
            var routed = 0;
            router.BackgroundInput += _ => routed++;
            var outside = new Vector2(-100, -100);
            router.RoutePointerDown(10, outside);
            Assert.That(router.RoutePointerDown(10, outside), Is.EqualTo(InputContext.None));
            router.RoutePointerUp(10, outside);
            Assert.That(router.RoutePointerUp(10, outside), Is.EqualTo(InputContext.None));
            Assert.That(routed, Is.EqualTo(2));
        }

        [Test]
        public void ModalCancel_HidesRoot_RestoresSelection_AndBlocksSameFrameShortcut()
        {
            var previous = new GameObject("previous");
            var modal = new GameObject("modal");
            try
            {
                root.GetComponent<EventSystem>().SetSelectedGameObject(previous);
                router.OpenModal(modal, null);
                Assert.That(router.RouteCancel(), Is.EqualTo(InputContext.Modal));
                Assert.That(router.ActiveModal, Is.Null);
                Assert.That(modal.activeSelf, Is.False);
                Assert.That(root.GetComponent<EventSystem>().currentSelectedGameObject, Is.SameAs(previous));
                Assert.That(router.RouteCancel(), Is.EqualTo(InputContext.None));
                Assert.That(router.TryRouteShortcut(RoutedInputKind.Pause, Vector2.zero), Is.False);
            }
            finally { Object.DestroyImmediate(previous); Object.DestroyImmediate(modal); }
        }

        [Test]
        public void CompositionEndAndFocusRestore_BlockSameFrameShortcuts()
        {
            router.SetCompositionActive(true);
            Assert.That(router.TryRouteShortcut(RoutedInputKind.Move, Vector2.one), Is.False);
            Assert.That(router.TryRouteShortcut(RoutedInputKind.Pause, Vector2.zero), Is.False);
            router.SetCompositionActive(false);
            Assert.That(router.TryRouteShortcut(RoutedInputKind.Pause, Vector2.zero), Is.False);
            router.SetApplicationFocus(false);
            Assert.That(router.RuntimeActions.FindActionMap("UI").enabled, Is.False);
            Assert.That(router.RuntimeActions.FindActionMap("Game").enabled, Is.False);
            router.SetApplicationFocus(true);
            Assert.That(router.TryRouteShortcut(RoutedInputKind.Pause, Vector2.zero), Is.False);
        }

        [Test]
        public void OperationsAsset_OnlyUiOwnsCancel_AndAllUiPointerActionsArePassThrough()
        {
            var ui = source.FindActionMap("UI", true);
            var game = source.FindActionMap("Game", true);
            foreach (var name in new[] { "Point", "Click", "RightClick", "MiddleClick", "ScrollWheel" })
                Assert.That(ui.FindAction(name, true).type, Is.EqualTo(InputActionType.PassThrough));
            Assert.That(ui.FindAction("Cancel", true).bindings.Select(x => x.path), Does.Contain("<Keyboard>/escape"));
            Assert.That(game.FindAction("Cancel"), Is.Null);
            Assert.That(game.FindAction("Move", true).bindings.Any(x => x.path == "<Keyboard>/w"), Is.True);
            Assert.That(game.FindAction("Pause", true).bindings.Any(x => x.path == "<Keyboard>/space"), Is.True);
            Assert.That(source.enabled, Is.False, "라우터는 공유 원본 asset이 아닌 인스턴스만 활성화한다");
        }
    }
}
#endif

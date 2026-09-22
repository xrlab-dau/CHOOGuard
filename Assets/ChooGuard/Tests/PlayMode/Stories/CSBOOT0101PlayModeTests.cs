#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ChooGuard.Presentation.Input;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode.Stories
{
    public sealed class CSBOOT0101PlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapScene_RendersStaticMarkerAndUsesInputSystem()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Assert.That(canvases.Length, Is.EqualTo(1));
            Assert.That(canvases[0].isActiveAndEnabled, Is.True);
            Assert.That(canvases[0].renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            Assert.That(systems.Length, Is.EqualTo(1));
            Assert.That(systems[0].isActiveAndEnabled, Is.True);
            Assert.That(systems[0].currentInputModule, Is.TypeOf<InputSystemUIInputModule>());
            Assert.That(Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None), Is.Empty);
            var modules = Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None);
            Assert.That(modules.Length, Is.EqualTo(1));
            var module = modules[0];
            Assert.That(module.isActiveAndEnabled, Is.True);
            Assert.That(module.actionsAsset, Is.Not.Null);
            var routers = Object.FindObjectsByType<InputContextRouter>(FindObjectsSortMode.None);
            Assert.That(routers.Length, Is.EqualTo(1));
            Assert.That(routers[0].gameObject, Is.SameAs(systems[0].gameObject));
            Assert.That(module.actionsAsset, Is.SameAs(routers[0].RuntimeActions));
            Assert.That(module.cancel, Is.Null, "Router alone owns Escape; TMP uses native cancellation");
            var cancel = routers[0].RuntimeActions.FindAction("UI/Cancel", true);
            Assert.That(cancel.enabled, Is.True);
            Assert.That(cancel.bindings.Any(x => x.effectivePath == "<Keyboard>/escape"), Is.True);
#if UNITY_EDITOR
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/ChooGuard/Presentation/Input/Operations.inputactions");
            Assert.That(source, Is.Not.Null);
            Assert.That(module.actionsAsset, Is.Not.SameAs(source));
            Assert.That(source.actionMaps.Any(map => map.enabled), Is.False);
#endif
            foreach (var reference in new[] { module.point, module.leftClick, module.submit })
            {
                Assert.That(reference, Is.Not.Null);
                Assert.That(reference.action, Is.Not.Null);
                Assert.That(reference.action.enabled, Is.True);
                Assert.That(reference.action.bindings.Count, Is.GreaterThan(0));
                Assert.That(reference.action.actionMap.asset, Is.EqualTo(module.actionsAsset));
            }
            var markers = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            Assert.That(markers.Length, Is.EqualTo(1));
            var marker = markers[0];
            Assert.That(marker.text, Is.EqualTo("CHOOGuard bootstrap"));
            Assert.That(marker.isActiveAndEnabled, Is.True);
            Assert.That(marker.font, Is.Not.Null);
            Assert.That(marker.font, Is.EqualTo(TMP_Settings.defaultFontAsset));
            Assert.That(marker.font.name, Is.EqualTo("ChooGuard Bootstrap SDF"));
            Assert.That(marker.font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(marker.fontSharedMaterial, Is.EqualTo(marker.font.material));
            Assert.That(marker.font.atlasTexture, Is.Not.Null);
            Assert.That(marker.fontSharedMaterial.mainTexture, Is.EqualTo(marker.font.atlasTexture));
            Assert.That(marker.fontSharedMaterial.shader, Is.Not.Null);
            Assert.That(marker.fontSharedMaterial.shader.name, Is.EqualTo("TextMeshPro/Mobile/Distance Field"));
            Assert.That(marker.fontSharedMaterial.shader.isSupported, Is.True);
            foreach (var character in marker.text.Distinct()) Assert.That(marker.font.HasCharacter(character, false, false), Is.True);
            marker.ForceMeshUpdate();
            Assert.That(marker.textInfo.characterCount, Is.EqualTo(marker.text.Length));
            Assert.That(marker.textInfo.characterInfo.Take(marker.textInfo.characterCount).Count(x => x.isVisible), Is.EqualTo(marker.text.Count(x => !char.IsWhiteSpace(x))));
            Assert.That(marker.textInfo.meshInfo.Sum(x => x.vertexCount), Is.GreaterThan(0));
            Assert.That(marker.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif

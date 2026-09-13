using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class SmokeRenderingTests
    {
        [UnityTest] public IEnumerator NativePixelsFollowLayerOpticalDepthAndOpaqueDistance()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a native graphics device; headless transport is not renderer acceptance.");
            var shader = Shader.Find("ChooGuard/ConservativeSmoke"); Assert.That(shader, Is.Not.Null); Assert.That(shader.isSupported, Is.True);
            var cameraObject = new GameObject("SmokeFixtureCamera"); var camera = cameraObject.AddComponent<Camera>();
            var volume = GameObject.CreatePrimitive(PrimitiveType.Cube); var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var smoke = new Material(shader); var opaque = new Material(Shader.Find("Standard"));
            var target = new RenderTexture(64,64,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(64,64,TextureFormat.RGBAFloat,false,true); var previous = RenderTexture.active;
            const int layer = 31;
            try
            {
                volume.layer = wall.layer = layer; volume.GetComponent<Collider>().enabled = wall.GetComponent<Collider>().enabled = false;
                volume.GetComponent<Renderer>().sharedMaterial = smoke; wall.GetComponent<Renderer>().sharedMaterial = opaque;
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.cullingMask = 1 << layer; camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.depthTextureMode = DepthTextureMode.Depth;
                camera.allowHDR = true; camera.targetTexture = target; camera.fieldOfView = 45; camera.aspect = 1;
                target.Create(); wall.SetActive(false); var fog = new Color(.23f,.24f,.25f,1); smoke.SetColor("_FogColor",fog);
                Color Read()
                {
                    camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,64,64),0,0); pixels.Apply();
                    return (pixels.GetPixel(31,31)+pixels.GetPixel(31,32)+pixels.GetPixel(32,31)+pixels.GetPixel(32,32))*.25f;
                }
                for (var test = 0; test < 5; test++)
                {
                    var shift = test == 3 ? 20 : 0; var height = test == 4 ? 6 : 4; var eyeY = test == 1 ? 3 : test == 4 ? 2 : 1;
                    camera.transform.position = new Vector3(shift,eyeY,-10); camera.transform.rotation = Quaternion.identity;
                    volume.transform.position = new Vector3(shift,height*.5f,0); volume.transform.localScale = new Vector3(4,height,4);
                    smoke.SetVector("_CellDims",new Vector4(4,height,4,0)); smoke.SetVector("_Layer",new Vector4(test == 4 ? 3 : 2,test == 4 ? 2 : 0,test == 4 ? .5f : 0,0));
                    smoke.SetVector("_Extinction",new Vector4(.25f,.5f,0,0));
                    wall.transform.position = new Vector3(shift,eyeY,.5f); wall.transform.localScale = new Vector3(20,20,1); wall.SetActive(test == 2);
                    volume.SetActive(false); yield return null; var baseline = Read();
                    volume.SetActive(true); yield return null; var actual = Read();
                    var distance = test == 2 ? 2f : 4f; var depth = distance*(test == 1 ? .5f : .25f); var transmission = Mathf.Exp(-depth);
                    var expected = fog+(baseline-fog)*transmission;
                    Assert.That(actual.r,Is.EqualTo(expected.r).Within(.008),"red case "+test);
                    Assert.That(actual.g,Is.EqualTo(expected.g).Within(.008),"green case "+test);
                    Assert.That(actual.b,Is.EqualTo(expected.b).Within(.008),"blue case "+test);
                }
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; target.Release();
                Object.Destroy(cameraObject); Object.Destroy(volume); Object.Destroy(wall); Object.Destroy(smoke); Object.Destroy(opaque); Object.Destroy(target); Object.Destroy(pixels);
            }
            yield return null;
        }
    }
}

// Modified from Unity Per-Object Outline RenderGraph example, 919617c847e41fc0f397220ccd4f32bb81b7210d.
// Copyright 2023 Unity Technologies; Apache-2.0, ThirdParty/Unity-Outline-License.md + Apache-2.0.txt.
// Resource input/dependency contract adapted from URP17.3 FullScreenPassRendererFeature,
// package 7865b6b91f8a, Unity Companion License (source-available, NOT OSS).
// Changes: one exact renderer, alpha-aware submesh materials, cleared mask, resolved depth sampling,
// no stencil writes/color readback, camera registry, bounded hard edge and owned-resource disposal.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
namespace ChooGuard.App.Fps.TrainDeparture
{
    public sealed class DepartureOutlineFeature : ScriptableRendererFeature
    {
        [Tooltip("Assign explicitly so shaders survive player stripping.")]
        public Shader MaskShader, EdgeShader;
        public Color EdgeColor = new Color(.25f, .9f, 1f, 1);
        [Range(1, 3)] public int Width = 2;
        OutlinePass pass;
        Material edge;
        readonly Dictionary<Material, Material> masks = new Dictionary<Material, Material>();
        readonly List<Material> selected = new List<Material>();
        public override void Create()
        {
            Release();
            if (MaskShader == null || EdgeShader == null) return;
            edge = CoreUtils.CreateEngineMaterial(EdgeShader);
            pass = new OutlinePass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            // No camera color sampling: alpha blend into active color, including backbuffer.
            pass.requiresIntermediateTexture = false;
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || edge == null || renderingData.cameraData.cameraType != CameraType.Game ||
                !DepartureVisualFocus.TryGet(renderingData.cameraData.camera, out var focus)) return;
            var record = focus.Index.SelectedRecord;
            if (record == null || record.Renderer != focus.Hit.Renderer || record.Surfaces == null) return;
            selected.Clear();
            foreach (var surface in record.Surfaces)
            {
                if (!surface.Supported || surface.Source == null) return;
                if (!masks.TryGetValue(surface.Source, out var mask))
                { mask = CoreUtils.CreateEngineMaterial(MaskShader); masks.Add(surface.Source, mask); }
                surface.BindMask(mask); selected.Add(mask);
            }
            edge.SetInt("_Spread", Mathf.Clamp(Width, 1, 3)); edge.SetColor("_EdgeColor", EdgeColor);
            pass.SourceMesh = record.StaticBatchSource ? record.SourceMesh : null;
            pass.Target = record.Renderer; pass.Materials = selected; pass.SubmeshCount = record.Mesh.Submeshes.Length; pass.Edge = edge;
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) { Release(); }
        void Release()
        {
            foreach (var mask in masks.Values) CoreUtils.Destroy(mask);
            masks.Clear(); selected.Clear(); CoreUtils.Destroy(edge); edge = null; pass = null;
        }
        sealed class OutlinePass : ScriptableRenderPass
        {
            public Renderer Target;
            public Mesh SourceMesh;
            public List<Material> Materials;
            public int SubmeshCount;
            public Material Edge;
            sealed class MaskData { public Renderer Target; public Mesh SourceMesh; public Matrix4x4 SourceToWorld; public List<Material> Materials; public int SubmeshCount; }
            sealed class BlitData { public TextureHandle Source, Destination; public Material Material; public int Pass; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                if (!resources.cameraDepthTexture.IsValid() || !resources.activeColorTexture.IsValid() || Target == null) return;
                var desc = camera.cameraTargetDescriptor;
                desc.depthBufferBits = 0; desc.depthStencilFormat = GraphicsFormat.None;
                desc.msaaSamples = 1; desc.bindMS = false; desc.graphicsFormat = GraphicsFormat.R8_UNorm;
                var mask = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "Departure focus mask", true);
                desc.graphicsFormat = GraphicsFormat.R8G8_UNorm;
                var horizontal = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "Departure focus horizontal", true);
                using (var builder = graph.AddRasterRenderPass<MaskData>("Departure focus selected submeshes", out var data))
                {
                    data.Target = Target; data.SourceMesh = SourceMesh; data.SourceToWorld = Target.transform.localToWorldMatrix; data.Materials = Materials; data.SubmeshCount = SubmeshCount;
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderFunc((MaskData d, RasterGraphContext ctx) =>
                    {
                        if (d.Target == null) return;
                        for (int i = 0; i < d.Materials.Count; i++)
                        {
                            // URP17.3 DecalDrawSystem.Draw command pattern (Unity Companion License).
                            // Original source mesh is object-local; DrawRenderer would address combined batch submeshes.
                            if (d.SourceMesh != null) ctx.cmd.DrawMesh(d.SourceMesh, d.SourceToWorld, d.Materials[i], Mathf.Min(i, d.SubmeshCount - 1), 0);
                            else ctx.cmd.DrawRenderer(d.Target, d.Materials[i], Mathf.Min(i, d.SubmeshCount - 1), 0);
                        }
                    });
                }
                AddBlit(graph, mask, horizontal, 0, "Departure focus horizontal", AccessFlags.Write);
                // ReadWrite preserves destination for fixed-function alpha blending. Never sample active color.
                AddBlit(graph, horizontal, resources.activeColorTexture, 1, "Departure focus edge composite", AccessFlags.ReadWrite);
            }
            void AddBlit(RenderGraph graph, TextureHandle source, TextureHandle destination, int index, string label, AccessFlags access)
            {
                using (var builder = graph.AddRasterRenderPass<BlitData>(label, out var data))
                {
                    data.Source = source; data.Destination = destination; data.Material = Edge; data.Pass = index;
                    builder.UseTexture(source, AccessFlags.Read); builder.SetRenderAttachment(destination, 0, access);
                    builder.SetRenderFunc((BlitData d, RasterGraphContext ctx) =>
                    {
                        bool flip = ctx.GetTextureUVOrigin(in d.Source) != ctx.GetTextureUVOrigin(in d.Destination);
                        Blitter.BlitTexture(ctx.cmd, d.Source, flip ? new Vector4(1,-1,0,1) : new Vector4(1,1,0,0), d.Material, d.Pass);
                    });
                }
            }
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BillDev.DistanceFog
{
    public sealed class DistanceFogFeature : ScriptableRendererFeature
    {
        private const string FOG_SHADER = "Hidden/BillDev/DistanceFog";

        private static readonly int PID_FogColor = Shader.PropertyToID("_FogColor");
        private static readonly int PID_FogDensity = Shader.PropertyToID("_FogDensity");
        private static readonly int PID_FogStart = Shader.PropertyToID("_FogStart");
        private static readonly int PID_FogEnd = Shader.PropertyToID("_FogEnd");
        private static readonly int PID_SkyFogAmount = Shader.PropertyToID("_SkyFogAmount");

        private FogPass _fogPass;

        private sealed class FogPass : ScriptableRenderPass, System.IDisposable
        {
            private Material _material;

            private sealed class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle target;
            }

            public FogPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

            private bool TryUpdateMaterial()
            {
                var settings = VolumeManager.instance.stack.GetComponent<DistanceFogVolume>();
                if (settings == null || !settings.IsActive()) return false;

                if (_material == null)
                {
                    var shader = Shader.Find(FOG_SHADER);
                    if (shader == null) return false;
                    _material = CoreUtils.CreateEngineMaterial(shader);
                }

                _material.SetColor(PID_FogColor, settings.fogColor.value);
                _material.SetFloat(PID_FogDensity, settings.density.value);
                _material.SetFloat(PID_FogStart, settings.fogStart.value);
                _material.SetFloat(PID_FogEnd, settings.fogEnd.value);
                _material.SetFloat(PID_SkyFogAmount, settings.skyFogAmount.value);

                return true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!TryUpdateMaterial()) return;

                var cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType == CameraType.Preview) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var source = resourceData.activeColorTexture;

                var desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;

                var temp = renderGraph.CreateTexture(new TextureDesc(desc)
                {
                    name = "BillFogTemp",
                    clearBuffer = false,
                });

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("BillDev DistanceFog", out var pd))
                {
                    pd.material = _material;
                    pd.source = source;
                    pd.target = temp;

                    builder.UseTexture(pd.source, AccessFlags.Read);
                    builder.SetRenderAttachment(pd.target, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("BillDev FogCopyBack", out var pd))
                {
                    pd.source = temp;
                    pd.target = source;

                    builder.UseTexture(pd.source, AccessFlags.Read);
                    builder.SetRenderAttachment(pd.target, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                    });
                }
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
        }

        public override void Create()
        {
            _fogPass = new FogPass();
            _fogPass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var settings = VolumeManager.instance.stack.GetComponent<DistanceFogVolume>();
            if (settings == null || !settings.IsActive()) return;
            renderer.EnqueuePass(_fogPass);
        }

        protected override void Dispose(bool disposing)
        {
            _fogPass?.Dispose();
        }
    }
}

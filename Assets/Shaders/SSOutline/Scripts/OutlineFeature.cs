using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BillDev.SSOutline
{
    public sealed class OutlineFeature : ScriptableRendererFeature
    {
        public static event Action<RasterCommandBuffer, LayerMask> OnRenderFoliageMask;

        [SerializeField] private bool halfResMasks = true;

        private const string OUTLINE_SHADER = "Hidden/BillDev/SSOutline";
        private const string MASK_SHADER = "Hidden/BillDev/SelectionMask";

        private static readonly int PID_Thickness = Shader.PropertyToID("_Thickness");
        private static readonly int PID_Color = Shader.PropertyToID("_OutlineColor");
        private static readonly int PID_DepthThresh = Shader.PropertyToID("_DepthThreshold");
        private static readonly int PID_NormalThresh = Shader.PropertyToID("_NormalThreshold");
        private static readonly int PID_DepthViewBias = Shader.PropertyToID("_DepthViewBias");
        private static readonly int PID_NormalViewBias = Shader.PropertyToID("_NormalViewBias");
        private static readonly int PID_Intensity = Shader.PropertyToID("_OutlineIntensity");
        private static readonly int PID_DebugMode = Shader.PropertyToID("_DebugMode");
        private static readonly int PID_FadeStart = Shader.PropertyToID("_FadeStart");
        private static readonly int PID_FadeEnd = Shader.PropertyToID("_FadeEnd");
        private static readonly int PID_VRFade = Shader.PropertyToID("_VRPeripheryFade");
        private static readonly int PID_SelectionMask = Shader.PropertyToID("_SelectionMaskTexture");
        private static readonly int PID_OcclusionMask = Shader.PropertyToID("_OcclusionMaskTexture");

        private sealed class MaskPass : ScriptableRenderPass, IDisposable
        {
            private readonly string _profilerTag;
            private readonly string _textureName;
            private readonly bool _isOcclusion;
            private Material _overrideMaterial;
            private LayerMask _layerMask;
            private bool _halfRes;

            private static readonly ShaderTagId TAG_MASK = new ShaderTagId("SelectionMask");
            private static readonly ShaderTagId TAG_OCCLUDE = new ShaderTagId("OcclusionMask");

            private static readonly ShaderTagId[] TAG_FORWARD =
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
            };

            public TextureHandle MaskTexture { get; private set; }

            public MaskPass(string tag, string texName, bool isOcclusion)
            {
                _profilerTag = tag;
                _textureName = texName;
                _isOcclusion = isOcclusion;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            }

            public void Setup(LayerMask mask, bool halfRes)
            {
                _layerMask = mask;
                _halfRes = halfRes;
                if (_overrideMaterial != null) return;
                var shader = Shader.Find(MASK_SHADER);
                if (shader != null) _overrideMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            private sealed class PassData
            {
                public RendererListHandle dedicatedList;
                public RendererListHandle fallbackList;
                public TextureHandle maskTarget;
                public bool isOcclusion;
                public LayerMask layerMask;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                MaskTexture = TextureHandle.nullHandle;
                if (_layerMask == 0) return;

                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var resourceData = frameData.Get<UniversalResourceData>();

                var desc = cameraData.cameraTargetDescriptor;
                desc.colorFormat = RenderTextureFormat.R8;
                desc.depthBufferBits = 0;

                if (_halfRes)
                {
                    desc.width = Mathf.Max(1, desc.width / 2);
                    desc.height = Mathf.Max(1, desc.height / 2);
                }

                MaskTexture = renderGraph.CreateTexture(new TextureDesc(desc)
                {
                    name = _textureName,
                    clearBuffer = true,
                    clearColor = Color.black,
                });

                var sorting = new SortingSettings(cameraData.camera) { criteria = SortingCriteria.CommonOpaque };
                var filtering = new FilteringSettings(RenderQueueRange.all, _layerMask);

                var dedicatedTag = _isOcclusion ? TAG_OCCLUDE : TAG_MASK;
                var dedicatedRL = renderGraph.CreateRendererList(new RendererListParams(
                    renderingData.cullResults, new DrawingSettings(dedicatedTag, sorting), filtering));

                var fallbackDrawing = new DrawingSettings(TAG_FORWARD[0], sorting)
                {
                    overrideMaterial = _overrideMaterial,
                    overrideMaterialPassIndex = 0,
                };
                for (int i = 1; i < TAG_FORWARD.Length; i++)
                    fallbackDrawing.SetShaderPassName(i, TAG_FORWARD[i]);

                var fallbackRL = renderGraph.CreateRendererList(new RendererListParams(
                    renderingData.cullResults, fallbackDrawing, filtering));

                using var builder = renderGraph.AddRasterRenderPass<PassData>(_profilerTag, out var pd);

                pd.dedicatedList = dedicatedRL;
                pd.fallbackList = fallbackRL;
                pd.maskTarget = MaskTexture;
                pd.isOcclusion = _isOcclusion;
                pd.layerMask = _layerMask;

                builder.UseRendererList(pd.dedicatedList);
                builder.UseRendererList(pd.fallbackList);
                builder.SetRenderAttachment(pd.maskTarget, 0, AccessFlags.Write);

                var sceneDepth = resourceData.activeDepthTexture;
                if (sceneDepth.IsValid())
                    builder.SetRenderAttachmentDepth(sceneDepth, AccessFlags.Read);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(data.dedicatedList);
                    ctx.cmd.DrawRendererList(data.fallbackList);
                    if (!data.isOcclusion) OnRenderFoliageMask?.Invoke(ctx.cmd, data.layerMask);
                });
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_overrideMaterial);
                _overrideMaterial = null;
            }
        }

        private sealed class OutlineCompositePass : ScriptableRenderPass, IDisposable
        {
            private Material _material;
            private MaskPass _selectionPass;
            private MaskPass _occlusionPass;

            private sealed class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle target;
                public TextureHandle selection;
                public TextureHandle occlusion;
            }

            public OutlineCompositePass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

            public void LinkMaskPasses(MaskPass selection, MaskPass occlusion)
            {
                _selectionPass = selection;
                _occlusionPass = occlusion;
            }

            private bool TryUpdateMaterial(UniversalCameraData cameraData)
            {
                var settings = VolumeManager.instance.stack.GetComponent<OutlineVolume>();
                if (settings == null || !settings.IsActive()) return false;

                if (_material == null)
                {
                    var shader = Shader.Find(OUTLINE_SHADER);
                    if (shader == null) return false;
                    _material = CoreUtils.CreateEngineMaterial(shader);
                }

                _material.SetFloat(PID_Thickness, settings.thickness.value);
                _material.SetColor(PID_Color, settings.outlineColor.value);
                _material.SetFloat(PID_DepthThresh, settings.depthThreshold.value);
                _material.SetFloat(PID_NormalThresh, settings.normalThreshold.value);
                _material.SetFloat(PID_DepthViewBias, settings.depthViewBias.value);
                _material.SetFloat(PID_NormalViewBias, settings.normalViewBias.value);
                _material.SetFloat(PID_Intensity, settings.outlineIntensity.value);
                _material.SetFloat(PID_FadeStart, settings.fadeDistanceStart.value);
                _material.SetFloat(PID_FadeEnd, settings.fadeDistanceEnd.value);
                _material.SetFloat(PID_VRFade, settings.vrPeripheryFade.value);
                _material.SetInt(PID_DebugMode, (int)settings.debugMode.value);

                SetKeyword("USE_DEPTH", settings.useDepth.value);
                SetKeyword("USE_NORMALS", settings.useNormals.value);

                SetKeyword("OUTLINE_FULL", settings.mode.value == OutlineVolume.OutlineMode.FullScreen);
                SetKeyword("OUTLINE_SELECTION", settings.mode.value == OutlineVolume.OutlineMode.SelectionOnly);
                SetKeyword("OUTLINE_MIXED", settings.mode.value == OutlineVolume.OutlineMode.Mixed);

                return true;
            }

            private void SetKeyword(string keyword, bool enable)
            {
                if (enable) _material.EnableKeyword(keyword);
                else _material.DisableKeyword(keyword);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                if (!TryUpdateMaterial(cameraData)) return;
                if (cameraData.cameraType == CameraType.Preview) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var source = resourceData.activeColorTexture;

                var desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;

                var temp = renderGraph.CreateTexture(new TextureDesc(desc)
                {
                    name = "BillOutlineTemp",
                    clearBuffer = false,
                });

                var selectionHandle = (_selectionPass?.MaskTexture.IsValid() == true)
                    ? _selectionPass.MaskTexture : TextureHandle.nullHandle;
                var occlusionHandle = (_occlusionPass?.MaskTexture.IsValid() == true)
                    ? _occlusionPass.MaskTexture : TextureHandle.nullHandle;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("BillOutline Composite", out var pd))
                {
                    pd.material = _material;
                    pd.source = source;
                    pd.target = temp;
                    pd.selection = selectionHandle;
                    pd.occlusion = occlusionHandle;

                    builder.UseTexture(pd.source, AccessFlags.Read);
                    if (pd.selection.IsValid()) builder.UseTexture(pd.selection, AccessFlags.Read);
                    if (pd.occlusion.IsValid()) builder.UseTexture(pd.occlusion, AccessFlags.Read);
                    builder.SetRenderAttachment(pd.target, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetTexture(PID_SelectionMask,
                            data.selection.IsValid() ? (Texture)data.selection : Texture2D.blackTexture);
                        data.material.SetTexture(PID_OcclusionMask,
                            data.occlusion.IsValid() ? (Texture)data.occlusion : Texture2D.blackTexture);
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("BillOutline CopyBack", out var pd))
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

        private MaskPass _selectionPass;
        private MaskPass _occlusionPass;
        private OutlineCompositePass _outlinePass;

        public override void Create()
        {
            _selectionPass = new MaskPass("BillOutline SelectionMask", "_SelectionMaskTexture", false);
            _occlusionPass = new MaskPass("BillOutline OcclusionMask", "_OcclusionMaskTexture", true);
            _outlinePass = new OutlineCompositePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var settings = VolumeManager.instance.stack.GetComponent<OutlineVolume>();
            if (settings == null || !settings.IsActive()) return;

            var inputFlags = ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth;
            if (settings.useNormals.value)
                inputFlags |= ScriptableRenderPassInput.Normal;
            _outlinePass.ConfigureInput(inputFlags);

            _outlinePass.LinkMaskPasses(null, null);

            bool needSelection = settings.selectionLayer.value != 0
                && (settings.mode.value == OutlineVolume.OutlineMode.SelectionOnly
                    || settings.mode.value == OutlineVolume.OutlineMode.Mixed);

            bool needOcclusion = settings.occlusionLayer.value != 0;

            if (needSelection)
            {
                _selectionPass.Setup(settings.selectionLayer.value, halfResMasks);
                renderer.EnqueuePass(_selectionPass);
                _outlinePass.LinkMaskPasses(_selectionPass, null);
            }

            if (needOcclusion)
            {
                _occlusionPass.Setup(settings.occlusionLayer.value, halfResMasks);
                renderer.EnqueuePass(_occlusionPass);
                _outlinePass.LinkMaskPasses(
                    needSelection ? _selectionPass : null,
                    _occlusionPass);
            }

            renderer.EnqueuePass(_outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            _selectionPass?.Dispose();
            _occlusionPass?.Dispose();
            _outlinePass?.Dispose();
        }
    }
}

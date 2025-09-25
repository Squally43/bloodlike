// PosterizeFeature.cs (Unity 6.2.4 URP – Compatibility path)

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering; // GraphicsFormat

// OPTIONAL: silence compat-path warnings
#pragma warning disable 0618  // cameraColorTargetHandle, OnCameraSetup/Execute marked obsolete in RG-era
#pragma warning disable 0672  // "overrides obsolete member" noise

namespace WH.Gameplay
{
    public class PosterizeFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public Shader shader;

            [Range(2, 12)] public float levels = 5f;
            [Range(0f, 0.2f)] public float ditherAmp = 0.03f;
            [Range(0.5f, 2f)] public float contrast = 1f;
            [Range(0f, 2f)] public float saturation = 1f;
            [Range(0.5f, 2.2f)] public float gamma = 1f;

            public Texture2D ditherTex;
            public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            public bool onlyGameCamera = true;
        }

        class PosterizePass : ScriptableRenderPass
        {
            static readonly ProfilingSampler Profiler = new ProfilingSampler("Posterize Blit");
            readonly Material material;

            RTHandle tempColor;

            public float Levels, DitherAmp, Contrast, Saturation, Gamma;
            public Texture DitherTex;
            public bool OnlyGameCamera;

            public PosterizePass(Material mat, RenderPassEvent evt)
            {
                material = mat;
                renderPassEvent = evt;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData rd)
            {
                if (OnlyGameCamera && rd.cameraData.cameraType != CameraType.Game)
                    return;

                var desc = rd.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;

                if (desc.graphicsFormat == GraphicsFormat.None)
                    desc.graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);

                // ✔ Correct overload for 6.2.4 (no useDynamicScale arg)
                RenderingUtils.ReAllocateIfNeeded(
                    ref tempColor,
                    desc,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    isShadowMap: false,
                    anisoLevel: 1,
                    mipMapBias: 0f,
                    name: "_PosterizeTemp"
                );
            }

            public override void Execute(ScriptableRenderContext ctx, ref RenderingData rd)
            {
                if (OnlyGameCamera && rd.cameraData.cameraType != CameraType.Game)
                    return;
                if (material == null || tempColor == null)
                    return;

                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, Profiler))
                {
                    var src = rd.cameraData.renderer.cameraColorTargetHandle;
                    if (src == null) { CommandBufferPool.Release(cmd); return; }

                    material.SetFloat("_Levels", Levels);
                    material.SetFloat("_DitherAmp", DitherAmp);
                    material.SetFloat("_Contrast", Contrast);
                    material.SetFloat("_Saturation", Saturation);
                    material.SetFloat("_Gamma", Gamma);
                    if (DitherTex) material.SetTexture("_DitherTex", DitherTex);

                    Blitter.BlitCameraTexture(cmd, src, tempColor, material, 0);
                    Blitter.BlitCameraTexture(cmd, tempColor, src);
                }
                ctx.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd) { }
            public void Dispose() { tempColor?.Release(); }
        }

        public Settings settings = new Settings();
        Material material;
        PosterizePass pass;

        public override void Create()
        {
            material = settings.shader ? CoreUtils.CreateEngineMaterial(settings.shader) : null;
            pass = material ? new PosterizePass(material, settings.passEvent) : null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData rd)
        {
            if (pass == null || material == null) return;

            pass.Levels = settings.levels;
            pass.DitherAmp = settings.ditherAmp;
            pass.Contrast = settings.contrast;
            pass.Saturation = settings.saturation;
            pass.Gamma = settings.gamma;
            pass.DitherTex = settings.ditherTex;
            pass.OnlyGameCamera = settings.onlyGameCamera;

            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
        }
    }
}





using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UniversalUpscaler.Editor
{
    /// <summary>
    /// Runs <see cref="SceneViewUpscalerFeature"/> for scene view cameras without adding it to a renderer asset.
    /// The feature is inserted into the renderer when a scene view camera begins rendering, before URP creates the camera
    /// data, and removed when the camera ends rendering, so game cameras never see it.
    /// </summary>
    [InitializeOnLoad]
    static class SceneViewUpscalerSetup
    {
        static SceneViewUpscalerFeature s_Feature;

        // The renderer the feature was inserted into, so it is removed from the same one even if the asset changed.
        static ScriptableRenderer s_Renderer;

        static SceneViewUpscalerSetup()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.SceneView)
                return;

            // URP always renders the scene view with the default renderer (UniversalRenderPipeline.GetRenderer).
            // Don't use asset.scriptableRenderer here: when the renderer is invalidated it recreates it and reinitializes
            // the GPU Resident Drawer in the middle of this event, and the disposed drawer's OnBeginCameraRendering throws.
            // URP recreates the renderer itself later in this frame, so skip this frame and repaint to set up the next one.
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            if (asset == null)
                return;

            if (!asset.TryGetDefaultRendererWithoutCreating(out ScriptableRenderer renderer))
            {
                RequestRepaint();
                return;
            }

            if (s_Feature == null)
            {
                s_Feature = ScriptableObject.CreateInstance<SceneViewUpscalerFeature>();
                s_Feature.name = nameof(SceneViewUpscalerFeature);
                s_Feature.hideFlags = HideFlags.HideAndDontSave;
            }

            RemoveFromRenderer();
            renderer.InsertTransientRendererFeature(s_Feature);
            s_Renderer = renderer;
        }

        static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.SceneView)
                return;

            RemoveFromRenderer();
        }

        static bool s_RepaintRequested;

        static void RequestRepaint()
        {
            if (s_RepaintRequested)
                return;

            s_RepaintRequested = true;
            EditorApplication.delayCall += () =>
            {
                s_RepaintRequested = false;
                SceneView.RepaintAll();
            };
        }

        static void RemoveFromRenderer()
        {
            s_Renderer?.RemoveTransientRendererFeature(s_Feature);
            s_Renderer = null;
        }

        static void Cleanup()
        {
            RemoveFromRenderer();

            if (s_Feature != null)
                Object.DestroyImmediate(s_Feature);
            s_Feature = null;
        }
    }
}

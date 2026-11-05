using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UniversalUpscaler.Editor
{
    /// <summary>
    /// Applies the upscaler setup URP skips for scene view cameras.
    /// Not added to any renderer asset: <see cref="SceneViewUpscalerSetup"/> inserts it into the renderer only while a
    /// scene view camera renders.
    /// </summary>
    [HideInInspector]
    sealed class SceneViewUpscalerFeature : ScriptableRendererFeature
    {
        public override void Create() { }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {
            if (cameraData.isSceneViewCamera)
                cameraData.SetupSceneViewCamera();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }
    }
}

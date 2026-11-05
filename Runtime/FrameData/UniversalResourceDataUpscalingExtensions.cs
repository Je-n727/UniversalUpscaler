using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Extension methods that let upscalers replace the camera resources URP keeps at the pre-upscale resolution.
    /// </summary>
    public static class UniversalResourceDataUpscalingExtensions
    {
        /// <summary>
        /// Sets <see cref="UniversalResourceData.cameraDepthTexture"/>, whose setter is internal.
        /// Use it after upscaling so the passes that follow read depth at the display resolution.
        /// </summary>
        /// <param name="resourceData">Resource data of the camera being rendered.</param>
        /// <param name="depthTexture">The new camera depth texture.</param>
        public static void SetCameraDepthTexture(this UniversalResourceData resourceData, TextureHandle depthTexture)
        {
            resourceData.cameraDepthTexture = depthTexture;
        }
    }
}

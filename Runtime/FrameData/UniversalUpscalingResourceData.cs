using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Optional per-frame inputs for upscalers, written by the passes that produce them.
    /// Upscalers check <c>frameData.Contains&lt;UniversalUpscalingResourceData&gt;()</c> and fall back when a handle is not valid.
    /// </summary>
    /// <example>
    /// <code>
    /// var upscalingData = frameData.GetOrCreate&lt;UniversalUpscalingResourceData&gt;();
    /// upscalingData.reflectionHitDistance = hitDistanceTexture;
    /// </code>
    /// </example>
    public class UniversalUpscalingResourceData : ContextItem
    {
        /// <summary>
        /// Distance from the surface to the reflection hit, in world units, at the pre-upscale (render) resolution.
        /// The red channel is read; 0 means no hit. Used by DLSS Ray Reconstruction as the specular guide.
        /// </summary>
        public TextureHandle reflectionHitDistance = TextureHandle.nullHandle;

        /// <inheritdoc />
        public override void Reset()
        {
            reflectionHitDistance = TextureHandle.nullHandle;
        }
    }
}

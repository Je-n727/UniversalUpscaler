namespace UnityEngine.Rendering.Universal
{
    public abstract partial class ScriptableRenderer
    {
        /// <summary>
        /// Adds a renderer feature that is not part of the <see cref="ScriptableRendererData"/> asset.
        /// The feature is inserted first, so the features that follow see the camera data it changes in
        /// <see cref="ScriptableRendererFeature.OnCameraPreCull"/>. Remove it with <see cref="RemoveTransientRendererFeature"/>.
        /// </summary>
        /// <param name="feature">The feature to add. It is not created or disposed by the renderer.</param>
        internal void InsertTransientRendererFeature(ScriptableRendererFeature feature)
        {
            if (feature == null || m_RendererFeatures.Contains(feature))
                return;

            m_RendererFeatures.Insert(0, feature);
        }

        /// <summary>
        /// Removes a renderer feature added with <see cref="InsertTransientRendererFeature"/>.
        /// </summary>
        /// <param name="feature">The feature to remove.</param>
        internal void RemoveTransientRendererFeature(ScriptableRendererFeature feature)
        {
            m_RendererFeatures.Remove(feature);
        }
    }
}

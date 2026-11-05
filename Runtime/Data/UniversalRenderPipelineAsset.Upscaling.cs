namespace UnityEngine.Rendering.Universal
{
    public partial class UniversalRenderPipelineAsset
    {
        /// <summary>
        /// Gets the default renderer only when it already exists and is up to date.
        /// Unlike <see cref="scriptableRenderer"/>, this never recreates the renderer. Recreating it also reinitializes
        /// the GPU Resident Drawer, which must not happen inside a <see cref="RenderPipelineManager"/> callback: the
        /// disposed drawer's callbacks are still invoked for the current event and throw.
        /// </summary>
        /// <param name="renderer">The default renderer, or null when it is missing or about to be recreated.</param>
        /// <returns>True if the renderer exists and its renderer data is not invalidated.</returns>
        internal bool TryGetDefaultRendererWithoutCreating(out ScriptableRenderer renderer)
        {
            renderer = null;

            if (m_RendererDataList == null || m_DefaultRendererIndex < 0 || m_DefaultRendererIndex >= m_RendererDataList.Length)
                return false;

            ScriptableRendererData rendererData = m_RendererDataList[m_DefaultRendererIndex];
            if (rendererData == null || rendererData.isInvalidated)
                return false;

            if (m_Renderers == null || m_DefaultRendererIndex >= m_Renderers.Length)
                return false;

            renderer = m_Renderers[m_DefaultRendererIndex];
            return renderer != null;
        }
    }
}

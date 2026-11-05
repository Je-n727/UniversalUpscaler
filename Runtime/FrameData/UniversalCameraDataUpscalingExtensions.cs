// Derived from UniversalRenderPipeline.cs in com.unity.render-pipelines.universal,
// copyright © Unity Technologies ApS. Licensed under the Unity Companion License (see LICENSE.md).

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Extension methods that apply the camera and upscaler setup URP skips for scene view cameras.
    /// The private helpers mirror the functions of the same name in <see cref="UniversalRenderPipeline"/>.
    /// </summary>
    public static class UniversalCameraDataUpscalingExtensions
    {
        static readonly ProfilingSampler s_SetupSceneViewCameraSampler = new ProfilingSampler("SetupSceneViewCamera");

        /// <summary>
        /// Re-applies the upscaler setup that <see cref="UniversalRenderPipeline"/> skips for scene view cameras.
        /// Call from <see cref="ScriptableRendererFeature.OnCameraPreCull"/>, which runs after the camera data is
        /// initialized but before UpdateTemporalAATargets, RTHandles.SetReferenceSize and culling.
        /// </summary>
        /// <param name="cameraData">Camera data passed to <see cref="ScriptableRendererFeature.OnCameraPreCull"/>.</param>
        public static void SetupSceneViewCamera(in this CameraData cameraData)
        {
            cameraData.universalCameraData.SetupSceneViewCamera();
        }

        /// <summary>
        /// Re-applies the upscaler setup that <see cref="UniversalRenderPipeline"/> skips for scene view cameras.
        /// </summary>
        /// <param name="cameraData">Camera data of the camera being rendered.</param>
        public static void SetupSceneViewCamera(this UniversalCameraData cameraData)
        {
            if (!cameraData.isSceneViewCamera || cameraData.renderer == null)
                return;

            using var profScope = new ProfilingScope(s_SetupSceneViewCameraSampler);

            Camera camera = cameraData.camera;
            camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData);

            // Same order as UniversalRenderPipeline.RenderSingleCamera: CreateCameraData -> InitializeAdditionalCameraData
            CreateCameraData(camera, additionalCameraData, cameraData);
            InitializeAdditionalCameraData(camera, additionalCameraData, cameraData);
        }

        /// <summary>
        /// Mirrors UniversalRenderPipeline.CreateCameraData.
        /// URP excludes scene view cameras from the upscaler-driven resolution (UpscalerDrivesCameraResolution).
        /// </summary>
        static void CreateCameraData(Camera camera, UniversalAdditionalCameraData additionalCameraData, UniversalCameraData cameraData)
        {
            var renderer = cameraData.renderer;

            InitializeStackedCameraData(camera, additionalCameraData, cameraData);

            ///////////////////////////////////////////////////////////////////
            // Descriptor settings                                            /
            ///////////////////////////////////////////////////////////////////

            // If upscaling is active, set the scaled width and height
            UniversalRenderPipeline.InitializeScaledDimensions(camera, cameraData);
            
            IUpscaler activeUpscaler = UniversalRenderPipeline.upscaling?.activeUpscaler;
            bool upscalerDictatesResolution = TryGetUpscalerDictatedResolution(cameraData, activeUpscaler, out var upscalerRenderSize);
            if (upscalerDictatesResolution)
            {
                cameraData.scaledWidth = upscalerRenderSize.x;
                cameraData.scaledHeight = upscalerRenderSize.y;
            }
            else
            {
                // Upscaler doesn't dictate the resolution but may constrain it to a supported range; clamp into it.
                var clamped = ClampRenderSizeToUpscalerBounds(cameraData, activeUpscaler,
                    new Vector2Int(cameraData.scaledWidth, cameraData.scaledHeight));
                cameraData.scaledWidth = clamped.x;
                cameraData.scaledHeight = clamped.y;
            }

            int msaaSamples = UniversalRenderPipeline.GetIntermediateTexturesMSAA(cameraData, renderer);

            cameraData.cameraTargetDescriptor = UniversalRenderPipeline.CreateRenderTextureDescriptor(camera, cameraData, msaaSamples);
        }

        /// <summary>
        /// Mirrors UniversalRenderPipeline.InitializeStackedCameraData.
        /// URP ignores the scene view's additional camera data (stop NaN, dithering, anti-aliasing), forces renderScale
        /// to 1, and only treats 100% render scale as upscaling for game cameras.
        /// </summary>
        static void InitializeStackedCameraData(Camera baseCamera, UniversalAdditionalCameraData baseAdditionalCameraData, UniversalCameraData cameraData)
        {
            var settings = UniversalRenderPipeline.asset;
            var supportedRenderingFeatures = cameraData.renderer.supportedRenderingFeatures;

            ///////////////////////////////////////////////////////////////////
            // Environment and Post-processing settings                       /
            ///////////////////////////////////////////////////////////////////

            // The game camera branch, without the settings that stay fixed for the scene view: the volume layer mask
            // and trigger (mirrored from the main camera by GetVolumeLayerMaskAndTrigger, already applied to the volume
            // stack at this point), XR rendering and HDR output.
            if (baseAdditionalCameraData != null)
            {
                cameraData.isStopNaNEnabled = baseAdditionalCameraData.stopNaN && SystemInfo.graphicsShaderLevel >= 35;
                cameraData.isDitheringEnabled = baseAdditionalCameraData.dithering;
                cameraData.antialiasing = baseAdditionalCameraData.antialiasing;
                cameraData.antialiasingQuality = baseAdditionalCameraData.antialiasingQuality;
            }

            ///////////////////////////////////////////////////////////////////
            // Settings that control output of the camera                     /
            ///////////////////////////////////////////////////////////////////

            if (!supportedRenderingFeatures.antiAliasing)
                cameraData.antialiasing = AntialiasingMode.None;

            // ImageUpscalingFilter is deprecated, we now track by upscaler id.
            // The active upscaler is the highest priority one supported on this device, so resolve from it rather than
            // from the serialized selection.
            IUpscaler activeUpscaler = UniversalRenderPipeline.upscaling?.activeUpscaler;

            // Discard variations lesser than kRenderScaleThreshold.
            const float kRenderScaleThreshold = 0.05f;
            float renderScale = baseAdditionalCameraData != null && baseAdditionalCameraData.sceneViewRenderScale > 0f
                            ? Mathf.Clamp(baseAdditionalCameraData.sceneViewRenderScale, UniversalRenderPipeline.minRenderScale, UniversalRenderPipeline.maxRenderScale)
                            : settings.renderScale;

            // A temporal upscaler (DLSS, FSR2/3, ...) only runs in ImageScalingMode.Upscaling, and it also skips the URP TAA
            // pass, yet the jitter keeps coming from the upscaler. Supersampling (> 1) would select Downscaling, leaving the
            // jitter unresolved (visible shaking) and, with a Fixed constraint, rendering at the upscaler's lower resolution.
            if (activeUpscaler != null && activeUpscaler.isTemporal)
                renderScale = Mathf.Min(renderScale, 1.0f);

            bool disableRenderScale = (Mathf.Abs(1.0f - renderScale) < kRenderScaleThreshold) || !supportedRenderingFeatures.upscaling;
            cameraData.renderScale = disableRenderScale ? 1.0f : renderScale;
            string selectedUpscalerId = activeUpscaler != null ? activeUpscaler.upscalerId : UniversalRenderPipeline.k_UpscalerId_Auto;
            string resolvedUpscalerId = ResolveAutoUpscaler(cameraData.pixelWidth, cameraData.pixelHeight, cameraData.renderScale, selectedUpscalerId);
            cameraData.resolvedUpscalerHash = Shader.PropertyToID(resolvedUpscalerId);

            bool upscalerSupportsTemporalAntiAliasing = activeUpscaler != null && activeUpscaler.isTemporal;
            bool upscalerSupportsSharpening = activeUpscaler != null && activeUpscaler.supportsSharpening;

            if (cameraData.renderScale > 1.0f)
            {
                cameraData.imageScalingMode = ImageScalingMode.Downscaling;
            }
            else if ((cameraData.renderScale < 1.0f) || (upscalerSupportsTemporalAntiAliasing || upscalerSupportsSharpening))
            {
                // When certain upscalers are requested, we still consider 100% render scale an upscaling operation.
                // URP limits this to game view cameras; the scene view follows the same rule here.
                cameraData.imageScalingMode = ImageScalingMode.Upscaling;

                // We force temporal anti-aliasing on when it's a prerequisite.
                if (upscalerSupportsTemporalAntiAliasing)
                {
                    cameraData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                }
            }
            else
            {
                cameraData.imageScalingMode = ImageScalingMode.None;
            }
        }

        /// <summary>
        /// Mirrors the upscaler parts of UniversalRenderPipeline.InitializeAdditionalCameraData.
        /// URP ran it for the scene view while imageScalingMode and antialiasing were None, so the upscaler histories
        /// were never requested and the jitter stayed identity.
        /// </summary>
        static void InitializeAdditionalCameraData(Camera camera, UniversalAdditionalCameraData additionalCameraData, UniversalCameraData cameraData)
        {
            // NOTE: TAA depends on XR modifications of cameraTargetDescriptor.
            if (additionalCameraData != null)
                UpdateTemporalAAData(cameraData, additionalCameraData);

            if (!cameraData.IsTemporalAAEnabled())
                return;

            // Time.frameCount does not advance on editor-only scene view repaints, which freezes the jitter sequence.
            // Drive the TAA frame index from the motion vector frame index, which advances once per editor repaint.
            // cameraData.taaSettings is this frame's copy, so the upscaler's io.frameIndex follows it as well.
            int motionVectorFrameIndex = Unity.RenderPipelines.Core.Runtime.Shared.RendererUpdateManagerBridge.GetMotionVectorFrameIndex();
            cameraData.taaSettings.jitterFrameCountOffset = motionVectorFrameIndex - Time.frameCount;

            // TemporalAA.Render treats a frame as new only when the history version differs from Time.frameCount.
            // On editor-only scene view repaints it does not, so the history copy is skipped and the stale history stays
            // blended in. motionVectorsPersistentData is updated after OnCameraPreCull, so its last index is still the
            // previous repaint here: when it differs, mark the history as one frame old so TAA resolves a new frame.
            MotionVectorsPersistentData motionData = additionalCameraData != null ? additionalCameraData.motionVectorsPersistentData : null;
            if (cameraData.taaHistory != null && motionData != null
                && motionData.lastFrameIndex != motionVectorFrameIndex
                && cameraData.taaHistory.GetAccumulationVersion() == Time.frameCount)
            {
                cameraData.taaHistory.SetAccumulationVersion(0, Time.frameCount - 1);
            }

            // The scene view is always a base camera, so the overlay aspect patch of the projection is not needed.
            Matrix4x4 projectionMatrix = camera.projectionMatrix;

            Matrix4x4 jitterMat = Matrix4x4.identity;
            // Depends on the cameraTargetDesc, size and MSAA also XR modifications of those.
            IUpscaler activeUpscaler = UniversalRenderPipeline.upscaling?.activeUpscaler;
            // Only a temporal upscaler drives the jitter sequence.
            if (cameraData.IsTemporalAAEnabled() && activeUpscaler != null && activeUpscaler.isTemporal)
            {
                // Upscalers compute jitter with resolution-dependent parameters.
                int taaFrameIndex = TemporalAA.CalculateTaaFrameIndex(ref cameraData.taaSettings);
                float actualWidth = cameraData.cameraTargetDescriptor.width;
                float upscaleRatio = cameraData.pixelWidth / actualWidth;

                activeUpscaler.CalculateJitter(taaFrameIndex, upscaleRatio, out Vector2 subpixelJitter, out bool allowScaling);

                // Note: DLSS/FSR2 set allowScaling=false, so jitterScale is not applied
                if (allowScaling)
                    subpixelJitter *= cameraData.taaSettings.jitterScale;

                // Store for later use by upscaler during post-process
                cameraData.subpixelJitter = subpixelJitter;

                jitterMat = TemporalAA.CalculateJitterMatrix(cameraData, subpixelJitter);
            }
            else if (cameraData.IsSTPEnabled())
            {
                jitterMat = TemporalAA.CalculateJitterMatrix(cameraData, StpUtils.s_JitterFunc);
            }
            else
            {
                jitterMat = TemporalAA.CalculateJitterMatrix(cameraData, TemporalAA.s_JitterFunc);
            }
            cameraData.SetViewProjectionAndJitterMatrix(camera.worldToCameraMatrix, projectionMatrix, jitterMat);
        }

        static void UpdateTemporalAAData(UniversalCameraData cameraData, UniversalAdditionalCameraData additionalCameraData)
        {
            if (cameraData.IsSTPEnabled())
            {
                additionalCameraData.historyManager.RequestAccess<StpHistory>();
                cameraData.stpHistory = additionalCameraData.historyManager.GetHistoryForWrite<StpHistory>();
            }

            bool needsAlphaUpscaleHistory = cameraData.isAlphaOutputEnabled;
            IUpscaler activeUpscaler = UniversalRenderPipeline.upscaling?.activeUpscaler;
            needsAlphaUpscaleHistory &= activeUpscaler != null && activeUpscaler.isTemporal && !activeUpscaler.supportsAlphaUpscaling;
            if (needsAlphaUpscaleHistory)
            {
                additionalCameraData.historyManager.RequestAccess<AlphaUpscaleHistory>();
                cameraData.alphaUpscaleHistory = additionalCameraData.historyManager.GetHistoryForWrite<AlphaUpscaleHistory>();
            }
        }

        /// <summary>
        /// Mirrors UniversalRenderPipeline.UpscalerDrivesCameraResolution without the scene/preview/reflection exclusion.
        /// </summary>
        static bool UpscalerDrivesCameraResolution(UniversalCameraData cameraData, IUpscaler activeUpscaler)
        {
            if (activeUpscaler == null)
                return false;

            return !cameraData.camera.allowDynamicResolution;
        }

        /// <summary>
        /// Mirrors UniversalRenderPipeline.TryGetUpscalerDictatedResolution.
        /// </summary>
        static bool TryGetUpscalerDictatedResolution(UniversalCameraData cameraData, IUpscaler activeUpscaler, out Vector2Int renderResolution)
        {
            renderResolution = default;

            if (!UpscalerDrivesCameraResolution(cameraData, activeUpscaler))
                return false;

            // Use cameraData.pixelWidth/Height (the viewport size) rather than camera.pixelWidth, to match
            // the display resolution the rest of the pipeline scales from.
            var displayResolution = new Vector2Int(cameraData.pixelWidth, cameraData.pixelHeight);

            // Acquire the per-camera context for the active upscaler.
            // The scene view never renders XR, so the camera id is the view id.
            ulong viewId = EntityId.ToULong(cameraData.camera.GetEntityId());

            var upscaling = UniversalRenderPipeline.upscaling;
            UpscalerOptions options = upscaling.GetGlobalOptions(activeUpscaler);

            // Acquire context early
            upscaling.AcquireContext(viewId, activeUpscaler, options, displayResolution);

            var info = activeUpscaler.GetResolutionInfo(displayResolution, options);

            if (info.constraint != UpscalerResolutionConstraint.Fixed)
                return false;

            renderResolution = info.qualityModeResolution;
            return true;
        }

        /// <summary>
        /// Mirrors UniversalRenderPipeline.ClampRenderSizeToUpscalerBounds.
        /// The once-per-value clamp warning is left to the game view.
        /// </summary>
        static Vector2Int ClampRenderSizeToUpscalerBounds(UniversalCameraData cameraData, IUpscaler activeUpscaler, Vector2Int renderSize)
        {
            if (!UpscalerDrivesCameraResolution(cameraData, activeUpscaler))
                return renderSize;

            // In upscaler terms we have two resolutions: Render Resolution and Display Resolution.
            // In this context, the target(display resolution) is the camera's output viewport.
            var displayResolution = new Vector2Int(cameraData.pixelWidth, cameraData.pixelHeight);

            // Only upscalers actually upscale (render size below the display). At render scale >= 1.0 the image is native
            // or supersampled (downscaled) and the upscaler is inactive, so its range doesn't apply.
            if (renderSize.x >= displayResolution.x && renderSize.y >= displayResolution.y)
                return renderSize;

            // Acquire the per-camera context for the active upscaler.
            // The scene view never renders XR, so the camera id is the view id.
            ulong viewId = EntityId.ToULong(cameraData.camera.GetEntityId());

            var upscaling = UniversalRenderPipeline.upscaling;
            UpscalerOptions options = upscaling.GetGlobalOptions(activeUpscaler);

            // Acquire context early
            upscaling.AcquireContext(viewId, activeUpscaler, options, displayResolution);

            var info = activeUpscaler.GetResolutionInfo(displayResolution, options);

            if (info.constraint != UpscalerResolutionConstraint.Range)
                return renderSize;

            return new Vector2Int(
                Mathf.Clamp(renderSize.x, info.minResolution.x, info.maxResolution.x),
                Mathf.Clamp(renderSize.y, info.minResolution.y, info.maxResolution.y));
        }

        /// <summary>
        /// Mirrors the private UniversalRenderPipeline.ResolveAutoUpscaler.
        /// Resolves the automatic upscaler to the spatial upscaler best suited to the final image size and render scale.
        /// </summary>
        /// <param name="imageSizeX">Width of the final image</param>
        /// <param name="imageSizeY">Height of the final image</param>
        /// <param name="renderScale">Scale being applied to the final image size</param>
        /// <param name="selectedUpscalerId">Id of the active upscaler</param>
        /// <returns>The resolved upscaler id, or the selected id when it isn't the automatic upscaler</returns>
        static string ResolveAutoUpscaler(float imageSizeX, float imageSizeY, float renderScale, string selectedUpscalerId)
        {
            string resolvedUpscalerId = selectedUpscalerId;

            if (selectedUpscalerId == UniversalRenderPipeline.k_UpscalerId_Auto)
            {
                float pixelScale = (1.0f / renderScale);
                bool isIntegerScale = Mathf.Approximately((pixelScale - Mathf.Floor(pixelScale)), 0.0f);

                if (isIntegerScale)
                {
                    float widthScale = (imageSizeX / pixelScale);
                    float heightScale = (imageSizeY / pixelScale);

                    bool isImageCompatible = (Mathf.Approximately((widthScale - Mathf.Floor(widthScale)), 0.0f) &&
                                              Mathf.Approximately((heightScale - Mathf.Floor(heightScale)), 0.0f));

                    resolvedUpscalerId = isImageCompatible ? UniversalRenderPipeline.k_UpscalerId_Point : UniversalRenderPipeline.k_UpscalerId_Linear;
                }
                else
                {
                    resolvedUpscalerId = UniversalRenderPipeline.k_UpscalerId_Linear;
                }
            }

            return resolvedUpscalerId;
        }
    }
}

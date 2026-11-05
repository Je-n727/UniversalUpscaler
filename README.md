# Universal Upscaler

Extends the Universal Render Pipeline (URP) upscaler framework so upscalers also work in the **Scene View**, and adds the extension points that third-party upscalers need.

URP leaves Scene View cameras out of upscaling. It forces their render scale to 1, ignores their anti-aliasing, Stop NaN and dithering settings, and does not let the active upscaler set their resolution. This package applies those settings to the Scene View camera again and adds Scene View–only rendering options to the Scene View camera settings.

## Requirements

| | Version |
|---|---|
| Unity | 6000.7 |
| Universal RP | 17.7.0 |

The package compiles into the URP assemblies through assembly definition references (`.asmref`), so it can use URP internals. Upgrade the package and URP together.

## Installation

Add the package to your project in one of these ways:

- Clone or copy this repository into your project's `Packages/` folder (embedded package).
- In **Window > Package Manager**, click **+ > Install package from git URL…** and enter this repository's URL.

No setup is required. You don't need to add a renderer feature or modify URP. The package takes effect as soon as it is installed and URP is the active render pipeline.

## Scene View settings

Open the **Scene View camera settings** (the camera icon in the Scene View toolbar). The **Rendering (Universal Plus)** section has these options:

| Option | Description |
|---|---|
| **Render Scale** | Scales the Scene View camera's render target, so the Scene View can render at a resolution other than native. Independent of the Render Scale in the URP asset. |
| **Anti-aliasing** | The anti-aliasing mode used by the Scene View camera. |
| **Stop NaNs** | Suppresses NaNs in the Scene View camera's color buffer. |
| **Dithering** | Applies 8-bit dithering to the final render to reduce color banding. |

Settings are stored for each Scene View window.

### Notes

- **Temporal anti-aliasing** in the Scene View requires **Always Refresh** to be enabled in the Scene View's effects menu. The package drives the TAA jitter from the motion vector frame index, so the jitter keeps advancing on editor-only repaints.
- **Temporal upscalers** (upscalers that report `isTemporal`) only run when the camera is upscaling. When one is active:
  - Render Scale is clamped to a maximum of 1. Supersampling (above 1) is ignored.
  - Temporal anti-aliasing is forced on, as it is for game cameras.
- **Fixed resolution:** if the active upscaler fixes the render resolution (`UpscalerResolutionConstraint.Fixed`, for example a quality mode), Render Scale is ignored.
- **Resolution range:** if the active upscaler supports a range of resolutions (`UpscalerResolutionConstraint.Range`), the render size is clamped to that range.
- Render Scale changes smaller than 0.05 from 1 are ignored, as in URP.

## How it works

When a Scene View camera begins rendering (`RenderPipelineManager.beginCameraRendering`), the package inserts a hidden renderer feature at the front of the default renderer's feature list. It removes the feature when the camera ends rendering (`endCameraRendering`). The feature is never saved to a Renderer Data asset, and game cameras never see it.

The feature's `OnCameraPreCull` runs before culling. At this point URP has already created the camera data, but has not yet resized the TAA history, set the `RTHandles` reference size, or culled. The feature calls `SetupSceneViewCamera()`, which repeats the steps of `UniversalRenderPipeline.CreateCameraData` and `InitializeAdditionalCameraData` without URP's Scene View exclusions:

1. Applies the Scene View's anti-aliasing, Stop NaN, dithering and render scale settings.
2. Resolves the image scaling mode, forcing TAA when the active upscaler is temporal.
3. Recomputes the scaled resolution and asks the active upscaler for its resolution constraints.
4. Rebuilds the camera target descriptor.
5. Requests the upscaler histories, then computes the jitter and the jittered projection matrix.

Game cameras are not modified.

## Scripting API

### `UniversalCameraDataUpscalingExtensions`

```csharp
public static void SetupSceneViewCamera(this UniversalCameraData cameraData);
public static void SetupSceneViewCamera(in this CameraData cameraData);
```

Applies the upscaler setup that URP skips for Scene View cameras. Call it from `ScriptableRendererFeature.OnCameraPreCull`. It has no effect on cameras that are not Scene View cameras.

### `UniversalResourceDataUpscalingExtensions`

```csharp
public static void SetCameraDepthTexture(this UniversalResourceData resourceData, TextureHandle depthTexture);
```

Sets `UniversalResourceData.cameraDepthTexture`, whose setter is internal. Call it after an upscaler has produced depth at the display resolution, so the passes that follow read the upscaled depth.

### `UniversalUpscalingResourceData`

Optional per-frame inputs for upscalers, stored in `frameData`. The passes that produce these textures write them. Upscalers read them if they are present, and fall back when a handle is not valid.

| Field | Description |
|---|---|
| `reflectionHitDistance` | Distance from the surface to the reflection hit, in world units, at the render (pre-upscale) resolution. The upscaler reads the red channel; 0 means no hit. |

```csharp
// Producer, for example a screen-space reflection pass
var upscalingData = frameData.GetOrCreate<UniversalUpscalingResourceData>();
upscalingData.reflectionHitDistance = hitDistanceTexture;

// Consumer, for example an upscaler
if (frameData.Contains<UniversalUpscalingResourceData>())
{
    var hitDistance = frameData.Get<UniversalUpscalingResourceData>().reflectionHitDistance;
    if (hitDistance.IsValid())
    {
        // use it
    }
}
```

## License

Code derived from Unity Technologies' render pipeline packages is licensed under the [Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License). All other code is licensed under the MIT License. See [LICENSE.md](LICENSE.md) for the list of files under each license.

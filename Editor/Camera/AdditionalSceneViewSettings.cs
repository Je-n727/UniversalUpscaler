// Derived from HDAdditionalSceneViewSettings.cs in com.unity.render-pipelines.high-definition,
// copyright © Unity Technologies ApS. Licensed under the Unity Companion License (see LICENSE.md).

using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace UniversalUpscaler.Editor
{
    [InitializeOnLoad]
    static class AdditionalSceneViewSettings
    {
        const string k_UXMLResourceGuid = "aed675168a2d73d46991913ab4959b98";

        [Serializable]
        class AdditionalData : SceneView.AdditionalSettings<UniversalRenderPipelineAsset, UniversalAdditionalCameraData>
        {
            [SerializeField] float m_RenderScale;
            [SerializeField] AntialiasingMode m_Antialiasing;
            [SerializeField] bool m_StopNaNs;
            [SerializeField] bool m_Dithering;

            public float renderScale
            {
                get => m_RenderScale;
                set
                {
                    m_RenderScale = value;
                    linkedComponent.sceneViewRenderScale = value;
                }
            }

            public AntialiasingMode antialiasing
            {
                get => m_Antialiasing;
                set
                {
                    m_Antialiasing = value;
                    linkedComponent.antialiasing = value;
                }
            }

            public bool stopNaNs
            {
                get => m_StopNaNs;
                set
                {
                    m_StopNaNs = value;
                    linkedComponent.stopNaN = value;
                }
            }

            public bool dithering
            {
                get => m_Dithering;
                set
                {
                    m_Dithering = value;
                    linkedComponent.dithering = value;
                }
            }

            public AdditionalData(UniversalAdditionalCameraData component)
            {
                Reset();
                SetLinkedComponent(component);
                Apply();
            }

            internal void SetLinkedComponent(UniversalAdditionalCameraData component)
                => linkedComponent = component;
            
            public override void Apply()
            {
                linkedComponent.sceneViewRenderScale = m_RenderScale;
                linkedComponent.antialiasing = m_Antialiasing;
                linkedComponent.stopNaN = m_StopNaNs;
                linkedComponent.dithering = m_Dithering;
            }

            public override void Reset()
            {
                m_RenderScale = 1f;
                m_Antialiasing = AntialiasingMode.None;
                m_StopNaNs = false;
                m_Dithering = false;
            }
        }

        static AdditionalSceneViewSettings()
        {
            SceneView.onCameraCreated += EnsureAdditionalData;
            RenderPipelineManager.activeRenderPipelineCreated += AddHooks;
        }
        
        static AdditionalData GetSettings(SceneView sceneView) 
            => sceneView.GetAdditionalSettings<AdditionalData>();
        
        static void EnsureAdditionalData(SceneView sceneView)
        {
            if (!sceneView.camera.TryGetComponent(out UniversalAdditionalCameraData universalAdditionalCameraData))
                universalAdditionalCameraData = sceneView.camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            AdditionalData additionalData = sceneView.GetAdditionalSettings<AdditionalData>();
            if (additionalData == null)
            {
                additionalData = new AdditionalData(universalAdditionalCameraData);
                sceneView.AddAdditionalSettings(additionalData);
            }

            additionalData.SetLinkedComponent(universalAdditionalCameraData);
            additionalData.Apply();
        }

        static void AddHooks()
        {
            if (GraphicsSettings.currentRenderPipelineAssetType != typeof(UniversalRenderPipelineAsset))
                return;
            SceneViewCameraWindow.createAdditionalSettingsGUI += CreateGUI;
            SceneViewCameraWindow.bindAdditionalSettings += Bind;
            RenderPipelineManager.activeRenderPipelineDisposed += RemoveHooks;
        }

        public static void RemoveHooks()
        {
            SceneViewCameraWindow.createAdditionalSettingsGUI -= CreateGUI;
            SceneViewCameraWindow.bindAdditionalSettings -= Bind;
            RenderPipelineManager.activeRenderPipelineDisposed -= RemoveHooks;
        }

        static VisualElement CreateGUI(SceneView sceneView)
        {
            var root = new VisualElement();
            var resourcePath = AssetDatabase.GUIDToAssetPath(k_UXMLResourceGuid);
            var visualTreeAsset = (VisualTreeAsset)EditorResources.Load<UnityEngine.Object>(resourcePath, isRequired: true);
            visualTreeAsset.CloneTree(root);

            var renderScale = root.Q<Slider>("RenderScale");
            Assert.IsNotNull(renderScale);
            var antiAliasing = root.Q<EnumField>("AntiAliasing");
            Assert.IsNotNull(antiAliasing);
            var taaWarning = root.Q<HelpBox>("TAAWarning");
            Assert.IsNotNull(taaWarning);
            var stopNaNs = root.Q<Toggle>("StopNaNs");
            Assert.IsNotNull(stopNaNs);
            var dithering = root.Q<Toggle>("Dithering");
            Assert.IsNotNull(dithering);
            
            // Links between VisualElement and callbacks
            renderScale.RegisterValueChangedCallback(evt =>
            {
                GetSettings(sceneView).renderScale = evt.newValue;
                sceneView.Repaint();
            });
            
            antiAliasing.RegisterValueChangedCallback(evt =>
            {
                AntialiasingMode newValue = (AntialiasingMode)evt.newValue;
                GetSettings(sceneView).antialiasing = newValue;
                taaWarning.style.display = newValue == AntialiasingMode.TemporalAntiAliasing ? DisplayStyle.Flex : DisplayStyle.None;
                sceneView.Repaint();
            });
            
            stopNaNs.RegisterValueChangedCallback(evt =>
            {
                GetSettings(sceneView).stopNaNs = evt.newValue;
                sceneView.Repaint();
            });
            
            dithering.RegisterValueChangedCallback(evt =>
            {
                GetSettings(sceneView).dithering = evt.newValue;
                sceneView.Repaint();
            });
            
            return root;
        }

        static void Bind(SceneView sceneView, VisualElement root)
        {
            var renderScale = root.Q<Slider>("RenderScale");
            var antiAliasing = root.Q<EnumField>("AntiAliasing");
            var taaWarning = root.Q<HelpBox>("TAAWarning");
            var stopNaNs = root.Q<Toggle>("StopNaNs");
            var dithering = root.Q<Toggle>("Dithering");

            var settings = GetSettings(sceneView);
            taaWarning.style.display = settings.antialiasing == AntialiasingMode.TemporalAntiAliasing ? DisplayStyle.Flex : DisplayStyle.None;

            renderScale.SetValueWithoutNotify(settings.renderScale);
            antiAliasing.SetValueWithoutNotify(settings.antialiasing);
            stopNaNs.SetValueWithoutNotify(settings.stopNaNs);
            dithering.SetValueWithoutNotify(settings.dithering);
        }
    }
}

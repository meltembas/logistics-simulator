using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace Logistics.EditorTools
{
    /// <summary>
    /// Reproducible project setup: rendering quality (URP asset, SSAO, post volume, HDRI sky), UI assets
    /// (font asset, theme, PanelSettings) and the Main scene. Run 1 → 2 → 3 from Tools ▸ Logistics.
    /// </summary>
    public static class SceneSetup
    {
        const string SettingsDir = "Assets/_Project/Settings";
        const string UiDir = "Assets/_Project/UI";
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        const string ConfigPath = "Assets/_Project/Data/GameConfig.asset";
        const string VolumePath = SettingsDir + "/CityVolume.asset";
        const string SkyPath = "Assets/_Project/Materials/Sky.mat";
        const string HdriPath = "Assets/ThirdParty/PolyHaven/kloofendal_48d_partly_cloudy_puresky/kloofendal_48d_partly_cloudy_puresky_2k.hdr";
        const string FontPath = UiDir + "/Fonts/Barlow-SemiBold SDF.asset";

        [MenuItem("Tools/Logistics/2 Configure Rendering and UI Assets")]
        public static void ConfigureRendering()
        {
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(UiDir + "/Fonts");

            // URP asset (the active PC quality level)
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.msaaSampleCount = 4;
                urp.shadowDistance = 340f;
                urp.shadowCascadeCount = 4;
                urp.cascade4Split = new Vector3(0.07f, 0.2f, 0.47f);
                urp.supportsCameraDepthTexture = true;
                urp.supportsHDR = true;
                var so = new SerializedObject(urp);
                var res = so.FindProperty("m_MainLightShadowmapResolution");
                if (res != null) res.intValue = 4096;
                var soft = so.FindProperty("m_SoftShadowsSupported");
                if (soft != null) soft.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(urp);

                // SSAO tuned for a camera 50–350 m away
                var renderer = urp.rendererDataList != null && urp.rendererDataList.Length > 0 ? urp.rendererDataList[0] : null;
                if (renderer != null)
                    foreach (var f in renderer.rendererFeatures)
                    {
                        if (f == null || f.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                        var fso = new SerializedObject(f);
                        Set(fso, "m_Settings.Intensity", 0.85f);
                        Set(fso, "m_Settings.Radius", 0.9f);
                        Set(fso, "m_Settings.DirectLightingStrength", 0.3f);
                        Set(fso, "m_Settings.Falloff", 800f);
                        fso.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(f);
                    }
            }

            // Post-processing profile (values from the art bible's mood; DoF focus is driven at runtime)
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            Override<Tonemapping>(profile, t => t.mode.Override(TonemappingMode.ACES));
            Override<Bloom>(profile, b => { b.intensity.Override(0.3f); b.threshold.Override(1.1f); b.scatter.Override(0.62f); });
            Override<ColorAdjustments>(profile, c => { c.saturation.Override(8f); c.contrast.Override(5f); c.postExposure.Override(0.3f); });
            Override<WhiteBalance>(profile, w => w.temperature.Override(5f));
            Override<Vignette>(profile, v => { v.intensity.Override(0.2f); v.smoothness.Override(0.45f); });
            Override<DepthOfField>(profile, d =>
            {
                d.mode.Override(DepthOfFieldMode.Bokeh);
                d.focusDistance.Override(150f);
                d.focalLength.Override(90f);
                d.aperture.Override(4f);
                d.bladeCount.Override(6);
            });
            Override<ShadowsMidtonesHighlights>(profile, s =>
            {
                s.shadows.Override(new Vector4(0.96f, 0.99f, 1.06f, 0f));
            });
            EditorUtility.SetDirty(profile);

            // HDRI skybox
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Panoramic")) { name = "Sky" };
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(HdriPath));
            sky.SetFloat("_Mapping", 1f);
            sky.SetFloat("_ImageType", 0f);
            sky.SetFloat("_Exposure", 1.0f);
            sky.SetFloat("_Rotation", 40f);
            EditorUtility.SetDirty(sky);

            // UI: font asset, theme, panel settings
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(FontPath);
            if (font == null)
            {
                var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/Barlow/Barlow-SemiBold.ttf");
                font = FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic, true);
                font.name = "Barlow-SemiBold SDF";
                AssetDatabase.CreateAsset(font, FontPath);
                foreach (var tex in font.atlasTextures)
                {
                    if (tex == null) continue;
                    tex.name = font.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(tex, font);
                }
                if (font.material != null)
                {
                    font.material.name = font.name + " Material";
                    AssetDatabase.AddObjectToAsset(font.material, font);
                }
                EditorUtility.SetDirty(font);
            }

            var themePath = UiDir + "/Theme.tss";
            if (!File.Exists(themePath))
            {
                File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath, ImportAssetOptions.ForceSynchronousImport);
            }
            var ussPath = UiDir + "/Game.uss";
            var uss = File.ReadAllText(ussPath);
            if (!uss.Contains("-unity-font-definition"))
            {
                uss = uss.Replace("    color: var(--text);\n    font-size: 16px;\n}",
                    "    color: var(--text);\n    font-size: 16px;\n    -unity-font-definition: url(\"project://database/" + FontPath + "\");\n}");
                File.WriteAllText(ussPath, uss);
                AssetDatabase.ImportAsset(ussPath);
            }

            var psPath = UiDir + "/PanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(psPath);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, psPath);
            }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            EditorUtility.SetDirty(ps);

            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), ConfigPath);

            AssetDatabase.SaveAssets();
            Debug.Log("[Logistics] Rendering and UI assets configured.");
        }

        static void Set(SerializedObject so, string path, float v)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogWarning("[Logistics] Missing property " + path); return; }
            if (p.propertyType == SerializedPropertyType.Float) p.floatValue = v;
            else if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(v);
        }

        static void Override<T>(VolumeProfile profile, System.Action<T> setup) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var c))
            {
                c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            c.active = true;
            setup(c);
            EditorUtility.SetDirty(c);
        }

        [MenuItem("Tools/Logistics/3 Build Main Scene")]
        public static void BuildScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Sun
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Palette.SunKey;
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sunGo.transform.rotation = Quaternion.Euler(42f, 35f, 0f);

            // Lighting environment
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.78f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.Haze;
            RenderSettings.fogStartDistance = 420f;
            RenderSettings.fogEndDistance = 1250f;

            // Camera + Cinemachine
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 2500f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.GetComponent<UniversalAdditionalCameraData>() ?? camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = true;
            var brain = camGo.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.8f);

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);

            var targetGo = new GameObject("CameraTarget");
            var rig = targetGo.AddComponent<CityCameraController>();
            targetGo.AddComponent<CinemachineImpulseSource>();
            var vcamGo = new GameObject("CityCam");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Follow = targetGo.transform;
            var lens = vcam.Lens;
            lens.FieldOfView = 30f;
            lens.NearClipPlane = 1f;
            lens.FarClipPlane = 2500f;
            vcam.Lens = lens;
            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTarget;
            follow.TrackerSettings.PositionDamping = new Vector3(0.25f, 0.25f, 0.25f);
            follow.TrackerSettings.RotationDamping = new Vector3(0.15f, 0.15f, 0.15f);
            follow.FollowOffset = new Vector3(0f, 0f, -150f);
            var rotate = vcamGo.AddComponent<CinemachineRotateWithFollowTarget>();
            rotate.Damping = 0.12f;
            vcamGo.AddComponent<CinemachineImpulseListener>();
            rig.vcam = vcam;
            rig.volume = volume;

            var probeGo = new GameObject("Reflection Probe");
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.hdr = true;
            probe.size = new Vector3(500f, 80f, 400f);

            var audioGo = new GameObject("Audio");
            var audio = audioGo.AddComponent<AudioManager>();

            // UI Toolkit reads pointer input through the Input System UI module when legacy input is off.
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var uiGo = new GameObject("UI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiDir + "/PanelSettings.asset");
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiDir + "/Game.uxml");
            var ui = uiGo.AddComponent<UIController>();

            var gameGo = new GameObject("Game");
            var gm = gameGo.AddComponent<GameManager>();
            gm.config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            gm.materials = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(MaterialBuilder.LibraryPath);
            gm.mainCamera = cam;
            gm.cameraRig = rig;
            gm.reflectionProbe = probe;
            gm.ui = ui;
            gm.audioManager = audio;

            EditorSceneManager.SaveScene(scene, ScenePath);

            // Bake the environment (ambient probe + default reflection) — no lightmaps, just the sky.
            var lightingPath = SettingsDir + "/CityLighting.lighting";
            var lighting = AssetDatabase.LoadAssetAtPath<LightingSettings>(lightingPath);
            if (lighting == null)
            {
                lighting = new LightingSettings { name = "CityLighting" };
                AssetDatabase.CreateAsset(lighting, lightingPath);
            }
            lighting.bakedGI = false;
            lighting.realtimeGI = false;
            Lightmapping.lightingSettings = lighting;
            Lightmapping.Bake();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = scenes;
            Debug.Log("[Logistics] Main scene built: " + ScenePath);
        }
    }
}

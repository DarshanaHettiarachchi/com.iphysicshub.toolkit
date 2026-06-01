using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IPhysicsHub.Toolkit.Editor
{
    internal class ProjectSettingsSyncWindow : ToolkitWindowBase
    {
        private ProjectSettingsProfile _workingProfile;
        private bool _showCapture = true;
        private bool _showApply = true;
        private bool _showMaintainer;
        private bool _maintainerUnlocked;
        private string _maintainerPwd = "";
        private string _savePresetName = "webgl";

        private bool _applyPlayer = true, _applyQuality = true, _applyGraphics = true, _applyLighting = true, _applyBuild = true;
        private string[] _presetNames = Array.Empty<string>();
        private string[] _presetPaths = Array.Empty<string>();
        private int _presetIndex;

        [MenuItem("Tools/iPhysicsHub/Project Settings Sync")]
        private static void Open()
        {
            var w = GetWindow<ProjectSettingsSyncWindow>("Project Settings Sync");
            w.minSize = new Vector2(420, 520);
            w.Show();
        }

        private void OnEnable()
        {
            _collisionPath = null;
            _collisionText = null;
            RefreshPresets();
        }

        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = ToolkitCore.LabelW;

            EditorGUILayout.LabelField("Project Settings Sync", EditorStyles.boldLabel);

            // Apply
            _showApply = EditorGUILayout.Foldout(_showApply, "Apply to Current Project", true);
            if (_showApply)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Package preset:", GUILayout.Width(120));
                    _presetIndex = EditorGUILayout.Popup(_presetIndex, _presetNames);
                    if (GUILayout.Button("↻", GUILayout.Width(26), GUILayout.Height(ToolkitCore.ButtonH)))
                        RefreshPresets();
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Categories to apply:", EditorStyles.miniBoldLabel);
                _applyPlayer = EditorGUILayout.ToggleLeft("Player", _applyPlayer);
                _applyQuality = EditorGUILayout.ToggleLeft("Quality", _applyQuality);
                _applyGraphics = EditorGUILayout.ToggleLeft("Graphics", _applyGraphics);
                _applyLighting = EditorGUILayout.ToggleLeft("Lighting (open scene)", _applyLighting);
                _applyBuild = EditorGUILayout.ToggleLeft("Build", _applyBuild);

                EditorGUILayout.HelpBox("Overwrites the checked project settings (Lighting applies to the open scene).", MessageType.Warning);

                using (new EditorGUI.DisabledScope(!CanApply()))
                {
                    if (GUILayout.Button("Apply Settings", GUILayout.Height(ToolkitCore.ButtonH)))
                        ApplySettings();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // Maintainer
            _showMaintainer = EditorGUILayout.Foldout(_showMaintainer, "Maintainer — Save preset to package", true);
            if (_showMaintainer)
            {
                EditorGUI.indentLevel++;
                if (!_maintainerUnlocked)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _maintainerPwd = EditorGUILayout.PasswordField("Password", _maintainerPwd);
                        if (GUILayout.Button("Enable", GUILayout.Width(90), GUILayout.Height(ToolkitCore.ButtonH)))
                        {
                            if (ToolkitCore.CheckMaintainerPassword(_maintainerPwd, out string msg, out MessageType type))
                            {
                                _maintainerUnlocked = true;
                                _message = msg;
                                _messageType = type;
                            }
                            else
                            {
                                _message = msg;
                                _messageType = type;
                            }
                            _maintainerPwd = "";
                            GUI.FocusControl(null);
                        }
                    }
                    EditorGUILayout.LabelField("Locked — enter the password to enable save.", EditorStyles.miniLabel);
                }
                else
                {
                    // Working profile
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _workingProfile = (ProjectSettingsProfile)EditorGUILayout.ObjectField(
                            "Working profile", _workingProfile, typeof(ProjectSettingsProfile), false);
                        if (GUILayout.Button("Create New", GUILayout.Width(90), GUILayout.Height(ToolkitCore.ButtonH)))
                            CreateNewProfile();
                    }

                    EditorGUILayout.Space(4);

                    // Capture
                    EditorGUILayout.LabelField("Capture from Current Project", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(_workingProfile == null))
                    {
                        if (GUILayout.Button("Capture Settings", GUILayout.Height(ToolkitCore.ButtonH)))
                            CaptureSettings();
                    }
                    if (_workingProfile == null)
                        EditorGUILayout.LabelField("Create or assign a working profile first.", EditorStyles.miniLabel);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Save preset to package", EditorStyles.boldLabel);
                    _savePresetName = EditorGUILayout.TextField("Save as", _savePresetName);
                    using (new EditorGUI.DisabledScope(_workingProfile == null))
                    {
                        if (GUILayout.Button("Save Profile to Templates~", GUILayout.Height(ToolkitCore.ButtonH)))
                            SavePresetToPackage();
                    }
                    if (_workingProfile == null)
                        EditorGUILayout.LabelField("Assign a working profile first.", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            DrawStatus();
        }

        private bool CanApply() => _presetPaths.Length > 0;

        private void CreateNewProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Project Settings Profile",
                "ProjectSettingsProfile",
                "asset",
                "Choose a location for the new profile asset.");
            if (string.IsNullOrEmpty(path)) return;

            var profile = ScriptableObject.CreateInstance<ProjectSettingsProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            _workingProfile = profile;
            _message = "Created new profile: " + path;
            _messageType = MessageType.Info;
        }

        private void RefreshPresets()
        {
            string dir = ToolkitCore.GetTemplatesDir();
            if (dir != null && Directory.Exists(dir))
            {
                _presetPaths = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories);
                _presetNames = _presetPaths.Select(Path.GetFileNameWithoutExtension).ToArray();
            }
            else
            {
                _presetPaths = Array.Empty<string>();
                _presetNames = Array.Empty<string>();
            }
            _presetIndex = Mathf.Clamp(_presetIndex, 0, Math.Max(0, _presetNames.Length - 1));
        }

        private ProjectSettingsProfile LoadPresetProfile()
        {
            if (_presetIndex < 0 || _presetIndex >= _presetPaths.Length) return null;
            string path = _presetPaths[_presetIndex];
            try
            {
                string json = File.ReadAllText(path);
                var profile = ScriptableObject.CreateInstance<ProjectSettingsProfile>();
                EditorJsonUtility.FromJsonOverwrite(json, profile);
                return profile;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProjectSettingsSync] Failed to load preset: {e.Message}");
                return null;
            }
        }

        // QualitySettings' per-platform default level is a serialized map (string -> int) exposed
        // as an array of first/second pairs. Returns the "second" (level) prop for a platform, or null.
        private static SerializedProperty FindPerPlatformQualityProp(SerializedObject so, string platform)
        {
            var mapProp = so.FindProperty("m_PerPlatformDefaultQuality");
            if (mapProp == null || !mapProp.isArray) return null;
            for (int i = 0; i < mapProp.arraySize; i++)
            {
                var pair = mapProp.GetArrayElementAtIndex(i);
                var key = pair.FindPropertyRelative("first");
                if (key != null && key.stringValue == platform)
                    return pair.FindPropertyRelative("second");
            }
            return null;
        }

        // ----- Capture -----

        private void CaptureSettings()
        {
            if (_workingProfile == null) return;

            Undo.RegisterCompleteObjectUndo(_workingProfile, "Capture Project Settings");
            CaptureAll(_workingProfile);
            EditorUtility.SetDirty(_workingProfile);
            AssetDatabase.SaveAssets();
            _message = "Captured current project settings into the working profile.";
            _messageType = MessageType.Info;
        }

        private void CaptureAll(ProjectSettingsProfile profile)
        {
            try { CapturePlayer(profile); } catch (Exception e) { Debug.LogError($"[ProjectSettingsSync] Capture Player failed: {e}"); }
            try { CaptureQuality(profile); } catch (Exception e) { Debug.LogError($"[ProjectSettingsSync] Capture Quality failed: {e}"); }
            try { CaptureGraphics(profile); } catch (Exception e) { Debug.LogError($"[ProjectSettingsSync] Capture Graphics failed: {e}"); }
            try { CaptureLighting(profile); } catch (Exception e) { Debug.LogError($"[ProjectSettingsSync] Capture Lighting failed: {e}"); }
            try { CaptureBuild(profile); } catch (Exception e) { Debug.LogError($"[ProjectSettingsSync] Capture Build failed: {e}"); }
        }

        private void CapturePlayer(ProjectSettingsProfile profile)
        {
            var data = new PlayerSettingsData();
            data.colorSpace = (int)PlayerSettings.colorSpace;

            try { data.defaultScreenWidth = PlayerSettings.defaultWebScreenWidth; } catch { }
            try { data.defaultScreenHeight = PlayerSettings.defaultWebScreenHeight; } catch { }
            try { data.runInBackground = PlayerSettings.runInBackground; } catch { }
            try { data.usePlayerLog = PlayerSettings.usePlayerLog; } catch { }
            try
            {
                var prop = typeof(PlayerSettings).GetProperty("allowUnsafeCode");
                if (prop != null) data.allowUnsafeCode = (bool)prop.GetValue(null);
            }
            catch { }

            var webglGroup = BuildTargetGroup.WebGL;
            try { data.scriptingBackend = PlayerSettings.GetScriptingBackend(webglGroup).ToString(); } catch { }
            try { data.apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(webglGroup).ToString(); } catch { }
            try
            {
                string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(webglGroup);
                data.scriptingDefines = string.IsNullOrEmpty(defines)
                    ? Array.Empty<string>()
                    : defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch { data.scriptingDefines = Array.Empty<string>(); }

            data.webgl = new WebGLSettingsData();
            try { data.webgl.memorySize = PlayerSettings.WebGL.memorySize; } catch { }
            try { data.webgl.compressionFormat = PlayerSettings.WebGL.compressionFormat.ToString(); } catch { }
            try { data.webgl.linkerTarget = PlayerSettings.WebGL.linkerTarget.ToString(); } catch { }
            try { data.webgl.exceptionSupport = PlayerSettings.WebGL.exceptionSupport.ToString(); } catch { }
            try { data.webgl.dataCaching = PlayerSettings.WebGL.dataCaching; } catch { }
            try { data.webgl.decompressionFallback = PlayerSettings.WebGL.decompressionFallback; } catch { }
            try
            {
                var prop = typeof(PlayerSettings.WebGL).GetProperty("powerPreference");
                if (prop != null) data.webgl.powerPreference = prop.GetValue(null).ToString();
            }
            catch { }
            try { data.webgl.threadsSupport = PlayerSettings.WebGL.threadsSupport; } catch { }
            try
            {
                var prop = typeof(PlayerSettings.WebGL).GetProperty("wasmArithmeticExceptions");
                if (prop != null) data.webgl.wasmArithmeticExceptions = prop.GetValue(null).ToString();
            }
            catch { }

            try { data.webgl.emscriptenArgs = PlayerSettings.WebGL.emscriptenArgs; } catch { }
            try { data.webgl.modulesDirectory = PlayerSettings.WebGL.modulesDirectory; } catch { }
            try { data.webgl.template = PlayerSettings.WebGL.template; } catch { }
            try { data.webgl.analyzeBuildSize = PlayerSettings.WebGL.analyzeBuildSize; } catch { }
            try { data.webgl.useEmbeddedResources = PlayerSettings.WebGL.useEmbeddedResources; } catch { }
            try { data.webgl.useWasm = PlayerSettings.WebGL.useWasm; } catch { }
            try { data.webgl.nameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes; } catch { }
            try
            {
                var prop = typeof(PlayerSettings.WebGL).GetProperty("debugSymbolMode");
                if (prop != null) data.webgl.debugSymbolMode = prop.GetValue(null).ToString();
            }
            catch { }
            try { data.webgl.showDiagnostics = PlayerSettings.WebGL.showDiagnostics; } catch { }
            try
            {
                var prop = typeof(PlayerSettings.WebGL).GetProperty("wasmStreaming");
                if (prop != null) data.webgl.wasmStreaming = (bool)prop.GetValue(null);
            }
            catch { }
            try { data.webgl.initialMemorySize = PlayerSettings.WebGL.initialMemorySize; } catch { }
            try { data.webgl.maximumMemorySize = PlayerSettings.WebGL.maximumMemorySize; } catch { }
            try
            {
                var prop = typeof(PlayerSettings.WebGL).GetProperty("memoryGrowthMode");
                if (prop != null) data.webgl.memoryGrowthMode = prop.GetValue(null).ToString();
            }
            catch { }
            try { data.webgl.linearMemoryGrowthStep = PlayerSettings.WebGL.linearMemoryGrowthStep; } catch { }
            try { data.webgl.geometricMemoryGrowthStep = PlayerSettings.WebGL.geometricMemoryGrowthStep; } catch { }
            try { data.webgl.memoryGeometricGrowthCap = PlayerSettings.WebGL.memoryGeometricGrowthCap; } catch { }

            profile.player = data;
        }

        private void CaptureQuality(ProjectSettingsProfile profile)
        {
            var data = new QualitySettingsData();
            data.webglDefaultLevel = QualitySettings.GetQualityLevel();  // fallback

            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qualityAsset != null && qualityAsset.Length > 0)
            {
                var so = new SerializedObject(qualityAsset[0]);

                var webglDefault = FindPerPlatformQualityProp(so, "WebGL");
                if (webglDefault != null) data.webglDefaultLevel = webglDefault.intValue;

                var levelsProp = so.FindProperty("m_QualitySettings");
                if (levelsProp != null)
                {
                    int count = levelsProp.arraySize;
                    var levels = new List<QualityLevelData>();
                    for (int i = 0; i < count; i++)
                    {
                        var levelProp = levelsProp.GetArrayElementAtIndex(i);
                        var level = new QualityLevelData();

                        var p = levelProp.FindPropertyRelative("name");
                        if (p != null) level.name = p.stringValue;

                        p = levelProp.FindPropertyRelative("pixelLightCount");
                        if (p != null) level.pixelLightCount = p.intValue;

                        p = levelProp.FindPropertyRelative("shadows");
                        if (p != null) level.shadows = p.intValue;

                        p = levelProp.FindPropertyRelative("shadowResolution");
                        if (p != null) level.shadowResolution = p.intValue;

                        p = levelProp.FindPropertyRelative("shadowDistance");
                        if (p != null) level.shadowDistance = p.floatValue;

                        p = levelProp.FindPropertyRelative("globalTextureMipmapLimit");
                        if (p != null) level.masterTextureLimit = p.intValue;

                        p = levelProp.FindPropertyRelative("anisotropicTextures");
                        if (p != null) level.anisotropicFiltering = p.intValue;

                        p = levelProp.FindPropertyRelative("antiAliasing");
                        if (p != null) level.antiAliasing = p.intValue;

                        p = levelProp.FindPropertyRelative("vSyncCount");
                        if (p != null) level.vSyncCount = p.intValue;

                        p = levelProp.FindPropertyRelative("lodBias");
                        if (p != null) level.lodBias = p.floatValue;

                        p = levelProp.FindPropertyRelative("maximumLODLevel");
                        if (p != null) level.maximumLODLevel = p.intValue;

                        p = levelProp.FindPropertyRelative("realtimeReflectionProbes");
                        if (p != null) level.realtimeReflectionProbes = p.boolValue;

                        p = levelProp.FindPropertyRelative("skinWeights");
                        if (p != null) level.skinWeights = p.intValue;

                        p = levelProp.FindPropertyRelative("customRenderPipeline");
                        if (p != null && p.objectReferenceValue != null)
                            level.renderPipelineAssetPath = AssetDatabase.GetAssetPath(p.objectReferenceValue);
                        else
                            level.renderPipelineAssetPath = "";

                        p = levelProp.FindPropertyRelative("shadowProjection");
                        if (p != null) level.shadowProjection = p.intValue;

                        p = levelProp.FindPropertyRelative("shadowCascades");
                        if (p != null) level.shadowCascades = p.intValue;

                        p = levelProp.FindPropertyRelative("shadowNearPlaneOffset");
                        if (p != null) level.shadowNearPlaneOffset = p.floatValue;

                        p = levelProp.FindPropertyRelative("shadowCascade2Split");
                        if (p != null) level.shadowCascade2Split = p.floatValue;

                        p = levelProp.FindPropertyRelative("shadowCascade4Split");
                        if (p != null) level.shadowCascade4Split = p.vector3Value;

                        p = levelProp.FindPropertyRelative("shadowmaskMode");
                        if (p != null) level.shadowmaskMode = p.intValue;

                        p = levelProp.FindPropertyRelative("softParticles");
                        if (p != null) level.softParticles = p.boolValue;

                        p = levelProp.FindPropertyRelative("billboardsFaceCameraPosition");
                        if (p != null) level.billboardsFaceCameraPosition = p.boolValue;

                        p = levelProp.FindPropertyRelative("useLegacyDetailDistribution");
                        if (p != null) level.useLegacyDetailDistribution = p.boolValue;

                        p = levelProp.FindPropertyRelative("realtimeGICPUUsage");
                        if (p != null) level.realtimeGICPUUsage = p.intValue;

                        p = levelProp.FindPropertyRelative("enableLODCrossFade");
                        if (p != null) level.enableLODCrossFade = p.boolValue;

                        p = levelProp.FindPropertyRelative("streamingMipmapsActive");
                        if (p != null) level.streamingMipmapsActive = p.boolValue;

                        p = levelProp.FindPropertyRelative("streamingMipmapsAddAllCameras");
                        if (p != null) level.streamingMipmapsAddAllCameras = p.boolValue;

                        p = levelProp.FindPropertyRelative("streamingMipmapsMemoryBudget");
                        if (p != null) level.streamingMipmapsMemoryBudget = p.floatValue;

                        p = levelProp.FindPropertyRelative("streamingMipmapsRenderersPerFrame");
                        if (p != null) level.streamingMipmapsRenderersPerFrame = p.intValue;

                        p = levelProp.FindPropertyRelative("streamingMipmapsMaxLevelReduction");
                        if (p != null) level.streamingMipmapsMaxLevelReduction = p.intValue;

                        p = levelProp.FindPropertyRelative("streamingMipmapsMaxFileIORequests");
                        if (p != null) level.streamingMipmapsMaxFileIORequests = p.intValue;

                        p = levelProp.FindPropertyRelative("particleRaycastBudget");
                        if (p != null) level.particleRaycastBudget = p.intValue;

                        p = levelProp.FindPropertyRelative("asyncUploadTimeSlice");
                        if (p != null) level.asyncUploadTimeSlice = p.intValue;

                        p = levelProp.FindPropertyRelative("asyncUploadBufferSize");
                        if (p != null) level.asyncUploadBufferSize = p.intValue;

                        p = levelProp.FindPropertyRelative("asyncUploadPersistentBuffer");
                        if (p != null) level.asyncUploadPersistentBuffer = p.boolValue;

                        p = levelProp.FindPropertyRelative("resolutionScalingFixedDPIFactor");
                        if (p != null) level.resolutionScalingFixedDPIFactor = p.floatValue;

                        p = levelProp.FindPropertyRelative("terrainQualityOverrides");
                        if (p != null) level.terrainQualityOverrides = p.intValue;

                        p = levelProp.FindPropertyRelative("terrainPixelError");
                        if (p != null) level.terrainPixelError = p.floatValue;

                        p = levelProp.FindPropertyRelative("terrainDetailDensityScale");
                        if (p != null) level.terrainDetailDensityScale = p.floatValue;

                        p = levelProp.FindPropertyRelative("terrainBasemapDistance");
                        if (p != null) level.terrainBasemapDistance = p.floatValue;

                        p = levelProp.FindPropertyRelative("terrainDetailDistance");
                        if (p != null) level.terrainDetailDistance = p.floatValue;

                        p = levelProp.FindPropertyRelative("terrainTreeDistance");
                        if (p != null) level.terrainTreeDistance = p.floatValue;

                        p = levelProp.FindPropertyRelative("terrainBillboardStart");
                        if (p != null) level.terrainBillboardStart = p.floatValue;

                        p = levelProp.FindPropertyRelative("terrainFadeLength");
                        if (p != null) level.terrainFadeLength = p.floatValue;

                        p = levelProp.FindPropertyRelative("terrainMaxTrees");
                        if (p != null) level.terrainMaxTrees = p.intValue;

                        p = levelProp.FindPropertyRelative("excludedTargetPlatforms");
                        if (p != null && p.isArray)
                        {
                            int epCount = p.arraySize;
                            var epList = new List<string>();
                            for (int j = 0; j < epCount; j++)
                            {
                                var epElem = p.GetArrayElementAtIndex(j);
                                if (epElem != null) epList.Add(epElem.stringValue);
                            }
                            level.excludedTargetPlatforms = epList.ToArray();
                        }
                        else
                        {
                            level.excludedTargetPlatforms = Array.Empty<string>();
                        }

                        if (level.name == "Mobile Webgl")
                            levels.Add(level);
                    }
                    data.levels = levels.ToArray();
                    if (data.levels.Length == 1)
                        data.webglDefaultLevel = 0;
                }
            }

            profile.quality = data;
        }

        private void CaptureGraphics(ProjectSettingsProfile profile)
        {
            var data = new GraphicsSettingsData();

            if (GraphicsSettings.defaultRenderPipeline != null)
                data.defaultRenderPipelineAssetPath = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            else
                data.defaultRenderPipelineAssetPath = "";

            data.transparencySortMode = (int)GraphicsSettings.transparencySortMode;
            data.lightsUseLinearIntensity = GraphicsSettings.lightsUseLinearIntensity;

            var graphicsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsAsset != null && graphicsAsset.Length > 0)
            {
                var so = new SerializedObject(graphicsAsset[0]);
                var tierProp = so.FindProperty("m_TierSettings");
                if (tierProp != null)
                {
                    int count = tierProp.arraySize;
                    var tiers = new List<GraphicsTierData>();
                    for (int i = 0; i < count; i++)
                    {
                        var tier = tierProp.GetArrayElementAtIndex(i);
                        var t = new GraphicsTierData();

                        var p = tier.FindPropertyRelative("renderingPath");
                        if (p != null) t.renderingPath = p.intValue;

                        p = tier.FindPropertyRelative("hdrMode");
                        if (p != null) t.hdrMode = p.intValue;

                        p = tier.FindPropertyRelative("reflectionProbeBlending");
                        if (p != null) t.reflectionProbeBlending = p.boolValue;

                        p = tier.FindPropertyRelative("reflectionProbeBoxProjection");
                        if (p != null) t.reflectionProbeBoxProjection = p.boolValue;

                        p = tier.FindPropertyRelative("standardShaderQuality");
                        if (p != null) t.standardShaderQuality = p.intValue;

                        p = tier.FindPropertyRelative("detailNormalMap");
                        if (p != null) t.detailNormalMap = p.intValue;

                        p = tier.FindPropertyRelative("cascadedShadows");
                        if (p != null) t.cascadedShadows = p.boolValue;

                        p = tier.FindPropertyRelative("useSRPBatcher");
                        if (p != null) t.useSRPBatcher = p.boolValue;

                        tiers.Add(t);
                    }
                    data.tierSettings = tiers.ToArray();
                }
            }

            profile.graphics = data;
        }

        private void CaptureLighting(ProjectSettingsProfile profile)
        {
            var data = new LightingEnvironmentData();

            data.ambientMode = (int)RenderSettings.ambientMode;
            data.defaultReflectionMode = (int)RenderSettings.defaultReflectionMode;
            data.defaultReflectionResolution = RenderSettings.defaultReflectionResolution;

            data.ambientLight = RenderSettings.ambientLight;
            data.ambientIntensity = RenderSettings.ambientIntensity;
            data.subtractiveShadowColor = RenderSettings.subtractiveShadowColor;
            data.reflectionIntensity = RenderSettings.reflectionIntensity;
            data.reflectionBounces = RenderSettings.reflectionBounces;
            try { data.reflectionCubemapCompression = (int)LightmapEditorSettings.reflectionCubemapCompression; } catch { }

            profile.lighting = data;
        }

        private void CaptureBuild(ProjectSettingsProfile profile)
        {
            var data = new BuildSettingsData();

            var scenes = EditorBuildSettings.scenes;
            var entries = new List<SceneEntry>();
            foreach (var s in scenes)
            {
                entries.Add(new SceneEntry { path = s.path, enabled = s.enabled });
            }
            data.scenes = entries.ToArray();

            data.development = EditorUserBuildSettings.development;
            data.allowDebugging = EditorUserBuildSettings.allowDebugging;
            data.connectProfiler = EditorUserBuildSettings.connectProfiler;

            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("overrideMaxTextureSize");
                if (prop != null) data.overrideMaxTextureSize = Convert.ToInt32(prop.GetValue(null));
            }
            catch { }

            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("overrideTextureCompression");
                if (prop != null) data.overrideTextureCompression = Convert.ToInt32(prop.GetValue(null));
            }
            catch { }

            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("webGLBuildSubtarget");
                if (prop != null) data.webGLTextureSubtarget = Convert.ToInt32(prop.GetValue(null));
            }
            catch { }

            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("webGLUsePreBuiltUnityEngine");
                if (prop != null) data.webGLUsePreBuiltUnityEngine = (bool)prop.GetValue(null);
            }
            catch { }

            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("buildWithDeepProfilingSupport");
                if (prop != null) data.buildWithDeepProfilingSupport = (bool)prop.GetValue(null);
            }
            catch { }

            profile.build = data;
        }

        // ----- Apply -----

        private void ApplySettings()
        {
            ProjectSettingsProfile source = LoadPresetProfile();
            if (source == null)
            {
                _message = "No preset selected.";
                _messageType = MessageType.Warning;
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "Apply Project Settings?",
                "Overwrite current project settings? Lighting applies to the open scene. A backup asset will be created first.",
                "Apply", "Cancel");
            if (!ok) return;

            // Auto-backup
            try
            {
                var backup = ScriptableObject.CreateInstance<ProjectSettingsProfile>();
                CaptureAll(backup);
                string backupPath = $"Assets/ProjectSettingsBackup_{DateTime.Now:yyyyMMdd_HHmmss}.asset";
                AssetDatabase.CreateAsset(backup, backupPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ProjectSettingsSync] Backup created: {backupPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProjectSettingsSync] Backup failed: {e.Message}");
            }

            Debug.Log($"[ProjectSettingsSync] Applying — Graphics:{_applyGraphics} Quality:{_applyQuality} Build:{_applyBuild} Lighting:{_applyLighting} Player:{_applyPlayer} | Source:{(source != null ? "present" : "null")}");
            AssetDatabase.StartAssetEditing();
            try
            {
                if (_applyGraphics) try { ApplyGraphics(source.graphics); } catch (Exception e) { Debug.LogError($"[ProjectSettingsSync] Apply Graphics failed: {e.Message}"); }
                if (_applyQuality) try { ApplyQuality(source.quality); } catch (Exception e) { Debug.LogError($"[ProjectSettingsSync] Apply Quality failed: {e.Message}"); }
                if (_applyBuild) try { ApplyBuild(source.build); } catch (Exception e) { Debug.LogError($"[ProjectSettingsSync] Apply Build failed: {e.Message}"); }
                if (_applyLighting) try { ApplyLighting(source.lighting); } catch (Exception e) { Debug.LogError($"[ProjectSettingsSync] Apply Lighting failed: {e.Message}"); }
                if (_applyPlayer) try { ApplyPlayer(source.player); } catch (Exception e) { Debug.LogError($"[ProjectSettingsSync] Apply Player failed: {e.Message}"); }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                // The preset branch builds a throwaway instance; the working profile is a real asset.
                if (source != null) DestroyImmediate(source);
            }

            AssetDatabase.SaveAssets();
            _message = "Applied settings from profile.";
            _messageType = MessageType.Info;
        }

        private void ApplyPlayer(PlayerSettingsData data)
        {
            if (data == null) return;

            try { PlayerSettings.colorSpace = (ColorSpace)data.colorSpace; } catch { }
            try { PlayerSettings.defaultWebScreenWidth = data.defaultScreenWidth; } catch { }
            try { PlayerSettings.defaultWebScreenHeight = data.defaultScreenHeight; } catch { }
            try { PlayerSettings.runInBackground = data.runInBackground; } catch { }
            try { PlayerSettings.usePlayerLog = data.usePlayerLog; } catch { }
            try
            {
                var prop = typeof(PlayerSettings).GetProperty("allowUnsafeCode");
                if (prop != null) prop.SetValue(null, data.allowUnsafeCode);
            }
            catch { }

            var webglGroup = BuildTargetGroup.WebGL;

            // WebGL-specific fields (non-recompile)
            if (data.webgl != null)
            {
                try { PlayerSettings.WebGL.memorySize = data.webgl.memorySize; } catch { }
                try { PlayerSettings.WebGL.compressionFormat = (WebGLCompressionFormat)Enum.Parse(typeof(WebGLCompressionFormat), data.webgl.compressionFormat); } catch { }
                try { PlayerSettings.WebGL.linkerTarget = (WebGLLinkerTarget)Enum.Parse(typeof(WebGLLinkerTarget), data.webgl.linkerTarget); } catch { }
                try { PlayerSettings.WebGL.exceptionSupport = (WebGLExceptionSupport)Enum.Parse(typeof(WebGLExceptionSupport), data.webgl.exceptionSupport); } catch { }
                try { PlayerSettings.WebGL.dataCaching = data.webgl.dataCaching; } catch { }
                try { PlayerSettings.WebGL.decompressionFallback = data.webgl.decompressionFallback; } catch { }
                try
                {
                    var prop = typeof(PlayerSettings.WebGL).GetProperty("powerPreference");
                    if (prop != null && !string.IsNullOrEmpty(data.webgl.powerPreference))
                    {
                        var val = Enum.Parse(prop.PropertyType, data.webgl.powerPreference);
                        prop.SetValue(null, val);
                    }
                }
                catch { }
                try { PlayerSettings.WebGL.threadsSupport = data.webgl.threadsSupport; } catch { }
                try
                {
                    var prop = typeof(PlayerSettings.WebGL).GetProperty("wasmArithmeticExceptions");
                    if (prop != null && !string.IsNullOrEmpty(data.webgl.wasmArithmeticExceptions))
                    {
                        var val = Enum.Parse(prop.PropertyType, data.webgl.wasmArithmeticExceptions);
                        prop.SetValue(null, val);
                    }
                }
                catch { }

                try { PlayerSettings.WebGL.emscriptenArgs = data.webgl.emscriptenArgs; } catch { }
                try { PlayerSettings.WebGL.modulesDirectory = data.webgl.modulesDirectory; } catch { }
                try { PlayerSettings.WebGL.template = data.webgl.template; } catch { }
                try { PlayerSettings.WebGL.analyzeBuildSize = data.webgl.analyzeBuildSize; } catch { }
                try { PlayerSettings.WebGL.useEmbeddedResources = data.webgl.useEmbeddedResources; } catch { }
                try { PlayerSettings.WebGL.useWasm = data.webgl.useWasm; } catch { }
                try { PlayerSettings.WebGL.nameFilesAsHashes = data.webgl.nameFilesAsHashes; } catch { }
                try
                {
                    var prop = typeof(PlayerSettings.WebGL).GetProperty("debugSymbolMode");
                    if (prop != null && !string.IsNullOrEmpty(data.webgl.debugSymbolMode))
                    {
                        var val = Enum.Parse(prop.PropertyType, data.webgl.debugSymbolMode);
                        prop.SetValue(null, val);
                    }
                }
                catch { }
                try { PlayerSettings.WebGL.showDiagnostics = data.webgl.showDiagnostics; } catch { }
                try
                {
                    var prop = typeof(PlayerSettings.WebGL).GetProperty("wasmStreaming");
                    if (prop != null) prop.SetValue(null, data.webgl.wasmStreaming);
                }
                catch { }
                try { PlayerSettings.WebGL.initialMemorySize = data.webgl.initialMemorySize; } catch { }
                try { PlayerSettings.WebGL.maximumMemorySize = data.webgl.maximumMemorySize; } catch { }
                try
                {
                    var prop = typeof(PlayerSettings.WebGL).GetProperty("memoryGrowthMode");
                    if (prop != null && !string.IsNullOrEmpty(data.webgl.memoryGrowthMode))
                    {
                        var val = Enum.Parse(prop.PropertyType, data.webgl.memoryGrowthMode);
                        prop.SetValue(null, val);
                    }
                }
                catch { }
                try { PlayerSettings.WebGL.linearMemoryGrowthStep = data.webgl.linearMemoryGrowthStep; } catch { }
                try { PlayerSettings.WebGL.geometricMemoryGrowthStep = data.webgl.geometricMemoryGrowthStep; } catch { }
                try { PlayerSettings.WebGL.memoryGeometricGrowthCap = data.webgl.memoryGeometricGrowthCap; } catch { }
            }

            // Recompile-triggering fields LAST
            try
            {
                if (!string.IsNullOrEmpty(data.scriptingBackend))
                    PlayerSettings.SetScriptingBackend(webglGroup, (ScriptingImplementation)Enum.Parse(typeof(ScriptingImplementation), data.scriptingBackend));
            }
            catch { }

            try
            {
                if (!string.IsNullOrEmpty(data.apiCompatibilityLevel))
                    PlayerSettings.SetApiCompatibilityLevel(webglGroup, (ApiCompatibilityLevel)Enum.Parse(typeof(ApiCompatibilityLevel), data.apiCompatibilityLevel));
            }
            catch { }

            try
            {
                if (data.scriptingDefines != null)
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(webglGroup, string.Join(";", data.scriptingDefines));
            }
            catch { }
        }

        private void ApplyQuality(QualitySettingsData data)
        {
            if (data == null || data.levels == null) return;

            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qualityAsset == null || qualityAsset.Length == 0)
            {
                Debug.LogError("[ProjectSettingsSync] QualitySettings.asset not found.");
                return;
            }

            var so = new SerializedObject(qualityAsset[0]);
            var levelsProp = so.FindProperty("m_QualitySettings");
            if (levelsProp == null)
            {
                Debug.LogError("[ProjectSettingsSync] m_QualitySettings property not found.");
                return;
            }
            if (data.levels == null || data.levels.Length == 0)
            {
                Debug.LogWarning("[ProjectSettingsSync] No quality levels in preset.");
                return;
            }

            Debug.Log($"[ProjectSettingsSync] ApplyQuality: project has {levelsProp.arraySize} levels, preset has {data.levels.Length} levels.");

            for (int i = 0; i < data.levels.Length; i++)
            {
                var level = data.levels[i];
                int projectIndex = -1;

                // Find existing level by name in the project
                for (int j = 0; j < levelsProp.arraySize; j++)
                {
                    var nameProp = levelsProp.GetArrayElementAtIndex(j).FindPropertyRelative("name");
                    if (nameProp != null && nameProp.stringValue == level.name)
                    {
                        projectIndex = j;
                        break;
                    }
                }

                // Not found → append
                if (projectIndex < 0)
                {
                    levelsProp.arraySize++;
                    projectIndex = levelsProp.arraySize - 1;
                    Debug.Log($"[ProjectSettingsSync] Appended '{level.name}' at index {projectIndex}. New array size: {levelsProp.arraySize}");
                }
                else
                {
                    Debug.Log($"[ProjectSettingsSync] Updating existing '{level.name}' at index {projectIndex}.");
                }

                var levelProp = levelsProp.GetArrayElementAtIndex(projectIndex);

                var p = levelProp.FindPropertyRelative("name");
                if (p != null) p.stringValue = level.name;

                p = levelProp.FindPropertyRelative("pixelLightCount");
                if (p != null) p.intValue = level.pixelLightCount;

                p = levelProp.FindPropertyRelative("shadows");
                if (p != null) p.intValue = level.shadows;

                p = levelProp.FindPropertyRelative("shadowResolution");
                if (p != null) p.intValue = level.shadowResolution;

                p = levelProp.FindPropertyRelative("shadowDistance");
                if (p != null) p.floatValue = level.shadowDistance;

                p = levelProp.FindPropertyRelative("globalTextureMipmapLimit");
                if (p != null) p.intValue = level.masterTextureLimit;

                p = levelProp.FindPropertyRelative("anisotropicTextures");
                if (p != null) p.intValue = level.anisotropicFiltering;

                p = levelProp.FindPropertyRelative("antiAliasing");
                if (p != null) p.intValue = level.antiAliasing;

                p = levelProp.FindPropertyRelative("vSyncCount");
                if (p != null) p.intValue = level.vSyncCount;

                p = levelProp.FindPropertyRelative("lodBias");
                if (p != null) p.floatValue = level.lodBias;

                p = levelProp.FindPropertyRelative("maximumLODLevel");
                if (p != null) p.intValue = level.maximumLODLevel;

                p = levelProp.FindPropertyRelative("realtimeReflectionProbes");
                if (p != null) p.boolValue = level.realtimeReflectionProbes;

                p = levelProp.FindPropertyRelative("skinWeights");
                if (p != null) p.intValue = level.skinWeights;

                p = levelProp.FindPropertyRelative("customRenderPipeline");
                if (p != null && !string.IsNullOrEmpty(level.renderPipelineAssetPath))
                {
                    var rpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(level.renderPipelineAssetPath);
                    if (rpAsset != null)
                        p.objectReferenceValue = rpAsset;
                    else
                        Debug.LogWarning($"[ProjectSettingsSync] Quality level {projectIndex} render pipeline asset missing: {level.renderPipelineAssetPath}");
                }

                p = levelProp.FindPropertyRelative("shadowProjection");
                if (p != null) p.intValue = level.shadowProjection;

                p = levelProp.FindPropertyRelative("shadowCascades");
                if (p != null) p.intValue = level.shadowCascades;

                p = levelProp.FindPropertyRelative("shadowNearPlaneOffset");
                if (p != null) p.floatValue = level.shadowNearPlaneOffset;

                p = levelProp.FindPropertyRelative("shadowCascade2Split");
                if (p != null) p.floatValue = level.shadowCascade2Split;

                p = levelProp.FindPropertyRelative("shadowCascade4Split");
                if (p != null) p.vector3Value = level.shadowCascade4Split;

                p = levelProp.FindPropertyRelative("shadowmaskMode");
                if (p != null) p.intValue = level.shadowmaskMode;

                p = levelProp.FindPropertyRelative("softParticles");
                if (p != null) p.boolValue = level.softParticles;

                p = levelProp.FindPropertyRelative("billboardsFaceCameraPosition");
                if (p != null) p.boolValue = level.billboardsFaceCameraPosition;

                p = levelProp.FindPropertyRelative("useLegacyDetailDistribution");
                if (p != null) p.boolValue = level.useLegacyDetailDistribution;

                p = levelProp.FindPropertyRelative("realtimeGICPUUsage");
                if (p != null) p.intValue = level.realtimeGICPUUsage;

                p = levelProp.FindPropertyRelative("enableLODCrossFade");
                if (p != null) p.boolValue = level.enableLODCrossFade;

                p = levelProp.FindPropertyRelative("streamingMipmapsActive");
                if (p != null) p.boolValue = level.streamingMipmapsActive;

                p = levelProp.FindPropertyRelative("streamingMipmapsAddAllCameras");
                if (p != null) p.boolValue = level.streamingMipmapsAddAllCameras;

                p = levelProp.FindPropertyRelative("streamingMipmapsMemoryBudget");
                if (p != null) p.floatValue = level.streamingMipmapsMemoryBudget;

                p = levelProp.FindPropertyRelative("streamingMipmapsRenderersPerFrame");
                if (p != null) p.intValue = level.streamingMipmapsRenderersPerFrame;

                p = levelProp.FindPropertyRelative("streamingMipmapsMaxLevelReduction");
                if (p != null) p.intValue = level.streamingMipmapsMaxLevelReduction;

                p = levelProp.FindPropertyRelative("streamingMipmapsMaxFileIORequests");
                if (p != null) p.intValue = level.streamingMipmapsMaxFileIORequests;

                p = levelProp.FindPropertyRelative("particleRaycastBudget");
                if (p != null) p.intValue = level.particleRaycastBudget;

                p = levelProp.FindPropertyRelative("asyncUploadTimeSlice");
                if (p != null) p.intValue = level.asyncUploadTimeSlice;

                p = levelProp.FindPropertyRelative("asyncUploadBufferSize");
                if (p != null) p.intValue = level.asyncUploadBufferSize;

                p = levelProp.FindPropertyRelative("asyncUploadPersistentBuffer");
                if (p != null) p.boolValue = level.asyncUploadPersistentBuffer;

                p = levelProp.FindPropertyRelative("resolutionScalingFixedDPIFactor");
                if (p != null) p.floatValue = level.resolutionScalingFixedDPIFactor;

                p = levelProp.FindPropertyRelative("terrainQualityOverrides");
                if (p != null) p.intValue = level.terrainQualityOverrides;

                p = levelProp.FindPropertyRelative("terrainPixelError");
                if (p != null) p.floatValue = level.terrainPixelError;

                p = levelProp.FindPropertyRelative("terrainDetailDensityScale");
                if (p != null) p.floatValue = level.terrainDetailDensityScale;

                p = levelProp.FindPropertyRelative("terrainBasemapDistance");
                if (p != null) p.floatValue = level.terrainBasemapDistance;

                p = levelProp.FindPropertyRelative("terrainDetailDistance");
                if (p != null) p.floatValue = level.terrainDetailDistance;

                p = levelProp.FindPropertyRelative("terrainTreeDistance");
                if (p != null) p.floatValue = level.terrainTreeDistance;

                p = levelProp.FindPropertyRelative("terrainBillboardStart");
                if (p != null) p.floatValue = level.terrainBillboardStart;

                p = levelProp.FindPropertyRelative("terrainFadeLength");
                if (p != null) p.floatValue = level.terrainFadeLength;

                p = levelProp.FindPropertyRelative("terrainMaxTrees");
                if (p != null) p.intValue = level.terrainMaxTrees;

                p = levelProp.FindPropertyRelative("excludedTargetPlatforms");
                if (p != null && p.isArray)
                {
                    int epCount = level.excludedTargetPlatforms != null ? level.excludedTargetPlatforms.Length : 0;
                    p.arraySize = epCount;
                    for (int j = 0; j < epCount; j++)
                    {
                        var epElem = p.GetArrayElementAtIndex(j);
                        if (epElem != null) epElem.stringValue = level.excludedTargetPlatforms[j];
                    }
                }
            }

            // Pin the WebGL per-platform default (the green-checkmark level), not just the editor's
            // current level, so the choice survives editor restarts and builds.
            // Dynamically resolve "Mobile Webgl" by scanning the project array.
            int targetDefault = data.webglDefaultLevel;
            for (int i = 0; i < levelsProp.arraySize; i++)
            {
                var nameProp = levelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (nameProp != null && nameProp.stringValue == "Mobile Webgl")
                {
                    targetDefault = i;
                    break;
                }
            }

            var webglDefault = FindPerPlatformQualityProp(so, "WebGL");
            if (webglDefault != null)
            {
                webglDefault.intValue = targetDefault;
                Debug.Log($"[ProjectSettingsSync] WebGL default quality set to index {targetDefault}.");
            }
            else
            {
                Debug.LogWarning("[ProjectSettingsSync] Could not find WebGL per-platform quality property.");
            }

            so.ApplyModifiedProperties();
            Debug.Log("[ProjectSettingsSync] ApplyModifiedProperties called for QualitySettings.");

            try { QualitySettings.SetQualityLevel(targetDefault); Debug.Log($"[ProjectSettingsSync] QualitySettings.SetQualityLevel({targetDefault}) succeeded."); } catch (Exception e) { Debug.LogWarning($"[ProjectSettingsSync] SetQualityLevel failed: {e.Message}"); }
        }

        private void ApplyGraphics(GraphicsSettingsData data)
        {
            if (data == null) return;

            if (!string.IsNullOrEmpty(data.defaultRenderPipelineAssetPath))
            {
                var rpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(data.defaultRenderPipelineAssetPath);
                if (rpAsset != null)
                    GraphicsSettings.defaultRenderPipeline = rpAsset;
                else
                    Debug.LogWarning($"[ProjectSettingsSync] RenderPipelineAsset missing: {data.defaultRenderPipelineAssetPath}");
            }

            try { GraphicsSettings.transparencySortMode = (TransparencySortMode)data.transparencySortMode; } catch { }
            try { GraphicsSettings.lightsUseLinearIntensity = data.lightsUseLinearIntensity; } catch { }

            var graphicsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsAsset != null && graphicsAsset.Length > 0)
            {
                var so = new SerializedObject(graphicsAsset[0]);
                var tierProp = so.FindProperty("m_TierSettings");
                if (tierProp != null && data.tierSettings != null)
                {
                    for (int i = 0; i < data.tierSettings.Length && i < tierProp.arraySize; i++)
                    {
                        var tier = tierProp.GetArrayElementAtIndex(i);
                        var t = data.tierSettings[i];

                        var p = tier.FindPropertyRelative("renderingPath");
                        if (p != null) p.intValue = t.renderingPath;

                        p = tier.FindPropertyRelative("hdrMode");
                        if (p != null) p.intValue = t.hdrMode;

                        p = tier.FindPropertyRelative("reflectionProbeBlending");
                        if (p != null) p.boolValue = t.reflectionProbeBlending;

                        p = tier.FindPropertyRelative("reflectionProbeBoxProjection");
                        if (p != null) p.boolValue = t.reflectionProbeBoxProjection;

                        p = tier.FindPropertyRelative("standardShaderQuality");
                        if (p != null) p.intValue = t.standardShaderQuality;

                        p = tier.FindPropertyRelative("detailNormalMap");
                        if (p != null) p.intValue = t.detailNormalMap;

                        p = tier.FindPropertyRelative("cascadedShadows");
                        if (p != null) p.boolValue = t.cascadedShadows;

                        p = tier.FindPropertyRelative("useSRPBatcher");
                        if (p != null) p.boolValue = t.useSRPBatcher;
                    }
                    so.ApplyModifiedProperties();
                }
            }
        }

        private void ApplyBuild(BuildSettingsData data)
        {
            if (data == null || data.scenes == null) return;

            // Scene paths are project-specific. Only apply scenes that exist here, and never clear
            // an existing build list when none of the profile's scenes resolve (e.g. applying a
            // package preset, or a profile captured in another project).
            var scenes = data.scenes
                .Where(s => !string.IsNullOrEmpty(s.path) && File.Exists(Path.Combine(Directory.GetCurrentDirectory(), s.path)))
                .Select(s => new EditorBuildSettingsScene(s.path, s.enabled))
                .ToArray();
            if (scenes.Length > 0)
                EditorBuildSettings.scenes = scenes;
            else if (data.scenes.Length > 0)
                Debug.LogWarning("[ProjectSettingsSync] None of the profile's build scenes exist in this project; left Scenes In Build unchanged.");

            EditorUserBuildSettings.development = data.development;
            EditorUserBuildSettings.allowDebugging = data.allowDebugging;
            EditorUserBuildSettings.connectProfiler = data.connectProfiler;

            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("overrideMaxTextureSize");
                if (prop != null) prop.SetValue(null, data.overrideMaxTextureSize);
            }
            catch { }

            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("overrideTextureCompression");
                if (prop != null) prop.SetValue(null, data.overrideTextureCompression);
            }
            catch { }

            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("webGLBuildSubtarget");
                if (prop != null) prop.SetValue(null, (UnityEditor.WebGLTextureSubtarget)data.webGLTextureSubtarget);
            }
            catch { }

            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("webGLUsePreBuiltUnityEngine");
                if (prop != null) prop.SetValue(null, data.webGLUsePreBuiltUnityEngine);
            }
            catch { }

            try
            {
                var prop = typeof(EditorUserBuildSettings).GetProperty("buildWithDeepProfilingSupport");
                if (prop != null) prop.SetValue(null, data.buildWithDeepProfilingSupport);
            }
            catch { }
        }

        private void ApplyLighting(LightingEnvironmentData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[ProjectSettingsSync] ApplyLighting: data is null.");
                return;
            }

            Debug.Log($"[ProjectSettingsSync] ApplyLighting: ambientMode={data.ambientMode}, defaultReflectionMode={data.defaultReflectionMode}, defaultReflectionResolution={data.defaultReflectionResolution}");

            try { RenderSettings.ambientMode = (AmbientMode)data.ambientMode; Debug.Log($"[ProjectSettingsSync] ambientMode set to {data.ambientMode}"); } catch (Exception e) { Debug.LogWarning($"[ProjectSettingsSync] ambientMode failed: {e.Message}"); }
            try { RenderSettings.defaultReflectionMode = (DefaultReflectionMode)data.defaultReflectionMode; Debug.Log($"[ProjectSettingsSync] defaultReflectionMode set to {data.defaultReflectionMode}"); } catch (Exception e) { Debug.LogWarning($"[ProjectSettingsSync] defaultReflectionMode failed: {e.Message}"); }
            RenderSettings.defaultReflectionResolution = data.defaultReflectionResolution;
            Debug.Log($"[ProjectSettingsSync] defaultReflectionResolution set to {data.defaultReflectionResolution}");

            try { RenderSettings.ambientLight = data.ambientLight; Debug.Log($"[ProjectSettingsSync] ambientLight set to {data.ambientLight}"); } catch (Exception e) { Debug.LogWarning($"[ProjectSettingsSync] ambientLight failed: {e.Message}"); }
            try { RenderSettings.ambientIntensity = data.ambientIntensity; Debug.Log($"[ProjectSettingsSync] ambientIntensity set to {data.ambientIntensity}"); } catch (Exception e) { Debug.LogWarning($"[ProjectSettingsSync] ambientIntensity failed: {e.Message}"); }
            try { RenderSettings.subtractiveShadowColor = data.subtractiveShadowColor; Debug.Log($"[ProjectSettingsSync] subtractiveShadowColor set to {data.subtractiveShadowColor}"); } catch (Exception e) { Debug.LogWarning($"[ProjectSettingsSync] subtractiveShadowColor failed: {e.Message}"); }
            try { RenderSettings.reflectionIntensity = data.reflectionIntensity; Debug.Log($"[ProjectSettingsSync] reflectionIntensity set to {data.reflectionIntensity}"); } catch (Exception e) { Debug.LogWarning($"[ProjectSettingsSync] reflectionIntensity failed: {e.Message}"); }
            try { RenderSettings.reflectionBounces = data.reflectionBounces; Debug.Log($"[ProjectSettingsSync] reflectionBounces set to {data.reflectionBounces}"); } catch (Exception e) { Debug.LogWarning($"[ProjectSettingsSync] reflectionBounces failed: {e.Message}"); }
            try { LightmapEditorSettings.reflectionCubemapCompression = (ReflectionCubemapCompression)data.reflectionCubemapCompression; Debug.Log($"[ProjectSettingsSync] reflectionCubemapCompression set to {(ReflectionCubemapCompression)data.reflectionCubemapCompression}"); } catch (Exception e) { Debug.LogWarning($"[ProjectSettingsSync] reflectionCubemapCompression failed: {e.Message}"); }

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    EditorSceneManager.MarkSceneDirty(scene);
            }
            Debug.Log("[ProjectSettingsSync] ApplyLighting finished, scenes marked dirty.");
        }

        // ----- Maintainer -----

        private void SavePresetToPackage()
        {
            if (_workingProfile == null) return;

            string dir = ToolkitCore.GetTemplatesDir();
            if (dir == null)
            {
                _message = "Could not resolve the package path.";
                _messageType = MessageType.Error;
                return;
            }

            string name = string.IsNullOrWhiteSpace(_savePresetName) ? "preset" : _savePresetName.Trim();
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                _message = "Preset name contains invalid characters.";
                _messageType = MessageType.Error;
                return;
            }

            string fileName = name + ".json";
            string filePath = Path.Combine(dir, fileName);

            try
            {
                string json = EditorJsonUtility.ToJson(_workingProfile, true);
                File.WriteAllText(filePath, json);
                RefreshPresets();
                _message = $"Saved preset to {fileName} in Templates~.";
                _messageType = MessageType.Info;
            }
            catch (Exception e)
            {
                _message = "Save failed — the package may be read-only. " + e.Message;
                _messageType = MessageType.Error;
            }
        }
    }
}

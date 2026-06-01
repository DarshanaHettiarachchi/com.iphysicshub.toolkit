using System;
using UnityEngine;

namespace IPhysicsHub.Toolkit.Editor
{
    [CreateAssetMenu(fileName = "ProjectSettingsProfile", menuName = "iPhysicsHub/Project Settings Profile")]
    public class ProjectSettingsProfile : ScriptableObject
    {
        public PlayerSettingsData player;
        public QualitySettingsData quality;
        public GraphicsSettingsData graphics;
        public LightingEnvironmentData lighting;
        public BuildSettingsData build;
    }

    [Serializable]
    public class PlayerSettingsData
    {
        // Note: productName / companyName / bundleVersion are intentionally NOT synced —
        // they are per-project identity, not shared best-practice settings.
        public int colorSpace;
        public int defaultScreenWidth;
        public int defaultScreenHeight;
        public bool runInBackground;
        public bool usePlayerLog;
        public bool allowUnsafeCode;
        public string scriptingBackend;
        public string apiCompatibilityLevel;
        public string[] scriptingDefines;
        public WebGLSettingsData webgl;
    }

    [Serializable]
    public class WebGLSettingsData
    {
        public int memorySize;
        public string compressionFormat;
        public string linkerTarget;
        public string exceptionSupport;
        public bool dataCaching;
        public bool decompressionFallback;
        public string powerPreference;
        public bool threadsSupport;
        public string wasmArithmeticExceptions;
    }

    [Serializable]
    public class QualitySettingsData
    {
        public QualityLevelData[] levels;
        public int webglDefaultLevel;
    }

    [Serializable]
    public class QualityLevelData
    {
        public string name;
        public int pixelLightCount;
        public int shadows;
        public int shadowResolution;
        public float shadowDistance;
        public int masterTextureLimit;
        public int anisotropicFiltering;
        public int antiAliasing;
        public int vSyncCount;
        public float lodBias;
        public int maximumLODLevel;
        public bool realtimeReflectionProbes;
        public int skinWeights;
        public string renderPipelineAssetPath;
        public string[] excludedTargetPlatforms;
    }

    [Serializable]
    public class GraphicsSettingsData
    {
        public string defaultRenderPipelineAssetPath;
        public int transparencySortMode;
        public bool lightsUseLinearIntensity;
        public GraphicsTierData[] tierSettings;
    }

    [Serializable]
    public class GraphicsTierData
    {
        public int renderingPath;
        public int hdrMode;
        public bool reflectionProbeBlending;
        public bool reflectionProbeBoxProjection;
        public int standardShaderQuality;
        public int detailNormalMap;
        public bool cascadedShadows;
        public bool useSRPBatcher;
    }

    [Serializable]
    public class LightingEnvironmentData
    {
        public int ambientMode;                  // Environment Lighting > Source
        public int defaultReflectionMode;         // Environment Reflections > Source
        public int defaultReflectionResolution;   // Environment Reflections > Resolution

        public Color ambientLight;                // Environment Lighting > Ambient Color
        public float ambientIntensity;             // Environment Lighting > Intensity Multiplier
        public Color subtractiveShadowColor;      // Realtime Shadow Color
        public float reflectionIntensity;         // Environment Reflections > Intensity Multiplier
        public int reflectionBounces;             // Environment Reflections > Bounces
        public int reflectionCubemapCompression;  // Environment Reflections > Compression (enum as int)
    }

    [Serializable]
    public class BuildSettingsData
    {
        public SceneEntry[] scenes;
        public bool development;
        public bool allowDebugging;
        public bool connectProfiler;
        public int overrideMaxTextureSize;
        public int overrideTextureCompression;
    }

    [Serializable]
    public class SceneEntry
    {
        public string path;
        public bool enabled;
    }
}

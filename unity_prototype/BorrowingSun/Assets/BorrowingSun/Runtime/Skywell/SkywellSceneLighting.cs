using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowingSun
{
    /// <summary>The world sun shared by rendered shadows and the collector's physical sunlight query.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SkywellSceneLighting : MonoBehaviour
    {
        [Header("世界背景 · 在 Game 视图预览")]
        [InspectorName("背景相机")]
        public Camera backgroundCamera;
        [InspectorName("背景色")]
        [Tooltip("场景空白处的纯色背景；修改后自动预览。地面/建筑的整体色调请调下方环境光。编辑模式修改后保存场景。")]
        public Color backgroundColor = new Color(.30f,.36f,.42f,1);
        [Header("世界太阳 · 视觉与采光共用")]
        [Tooltip("场景唯一 Directional Light；采光庭读取同一方向并检查山体遮挡。日光本身不会直接给村庄奖励。")]
        public Light sunLight;
        [Tooltip("X 为太阳高度角，Y 为绕山谷方向；Unity 光线沿该旋转的 forward 传播。")]
        public Vector3 sunEulerAngles = new Vector3(38,120,0);
        [Range(0,5)] public float sunIntensity = 1.65f;
        public Color sunColor = new Color(1,.87f,.68f);
        [Range(0,1)] public float shadowStrength = 1;
        [Header("背阴填充 · Flat 环境光")]
        [Tooltip("直接乘到 Flat ambientColor；不使用在此模式下无效的环境贴图强度。")]
        [Range(0,2)] public float ambientIntensity = .65f;
        public Color ambientColor = new Color(.35f,.46f,.61f);
        private volatile bool pending;
        private (Color background, Color ambient, Color sun, float intensity)? runtimeLighting;
        private void OnEnable(){ApplySettings();}
        // OnValidate can run outside the main thread. Only request the next editor/player update.
        private void OnValidate(){pending=true;}
        private void Update(){if(pending)ApplySettings();}
        internal void SetRuntimeLighting(Color background, Color ambient, Color sun, float intensity)
        {
            runtimeLighting = (background, ambient, sun, intensity);
            ApplySettings();
        }
        internal void RestoreAuthoredLighting()
        {
            if (!runtimeLighting.HasValue) return;
            runtimeLighting = null;
            ApplySettings();
        }
        [ContextMenu("应用日光参数")]
        public void ApplySettings()
        {
            pending=false;
            if(backgroundCamera)
            {
                if(backgroundCamera.clearFlags!=CameraClearFlags.SolidColor)backgroundCamera.clearFlags=CameraClearFlags.SolidColor;
                var background=runtimeLighting?.background??backgroundColor;
                if(!SameRgb(backgroundCamera.backgroundColor,background))backgroundCamera.backgroundColor=background;
            }
            if(!sunLight||sunLight.type!=LightType.Directional)return;
            var rotation=Quaternion.Euler(sunEulerAngles);
            float intensity=Mathf.Max(0,runtimeLighting?.intensity??sunIntensity),strength=Mathf.Clamp01(shadowStrength);
            var ambient=runtimeLighting?.ambient??ambientColor*Mathf.Max(0,ambientIntensity);
            var directLight=runtimeLighting?.sun??sunColor;
            // ExecuteAlways also runs when leaving Play Mode. Avoid dirtying unchanged author-scene objects.
            if(Quaternion.Angle(sunLight.transform.rotation,rotation)>.001f)sunLight.transform.rotation=rotation;
            if(!Mathf.Approximately(sunLight.intensity,intensity))sunLight.intensity=intensity;
            if(!SameRgb(sunLight.color,directLight))sunLight.color=directLight;
            if(sunLight.shadows!=LightShadows.Soft)sunLight.shadows=LightShadows.Soft;
            if(!Mathf.Approximately(sunLight.shadowStrength,strength))sunLight.shadowStrength=strength;
            if(RenderSettings.sun!=sunLight)RenderSettings.sun=sunLight;
            if(RenderSettings.ambientMode!=AmbientMode.Flat)RenderSettings.ambientMode=AmbientMode.Flat;
            if(!SameRgb(RenderSettings.ambientLight,ambient))RenderSettings.ambientLight=ambient;
        }
        // Lighting is RGB; Unity can normalize the unused alpha channel when reading RenderSettings back.
        private static bool SameRgb(Color a,Color b)=>Mathf.Approximately(a.r,b.r)&&Mathf.Approximately(a.g,b.g)&&Mathf.Approximately(a.b,b.b);
    }
}

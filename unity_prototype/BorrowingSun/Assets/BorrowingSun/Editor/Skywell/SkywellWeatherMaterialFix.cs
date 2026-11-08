using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>Repairs only weather particle materials in the saved, migrated L5 author scene.</summary>
    public static class SkywellWeatherMaterialFix
    {
        private const string ScenePath="Assets/BorrowingSun/Scenes/05_Skywell.unity";
        private const string MaterialPath="Assets/BorrowingSun/Materials/SkywellLocalWeatherSoft.mat";
        private const string TexturePath="Assets/BorrowingSun/Materials/SkywellLocalWeatherSoftMask.asset";
        private const string ShaderName="Universal Render Pipeline/Particles/Unlit";

        [MenuItem("Borrowing Sun/Skywell/Repair local weather particle material")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=ScenePath||scene.isDirty)
                throw new InvalidOperationException("Exit Play Mode and open the saved, clean 05_Skywell author scene before repairing its weather materials.");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="L5_Skywell");
            var court=root.GetComponentsInChildren<SkywellController>(true).Single();
            var zones=court.Consoles.Select(s=>s?s.Zone:null).ToArray();
            if(zones.Length!=4||zones.Any(z=>!z)||zones.Distinct().Count()!=4)
                throw new InvalidOperationException("Expected four distinct migrated local weather zones.");
            var rain=root.GetComponentsInChildren<SkywellRainFeedback>(true).Single().RainParticles;
            if(!rain||zones[0].RainParticles!=rain)
                throw new InvalidOperationException("Tower zone must reuse its existing authored rain system.");
            var renderer=rain.GetComponent<ParticleSystemRenderer>();
            var shader=Shader.Find(ShaderName);
            if(!renderer||!shader)throw new InvalidOperationException("Required rain renderer or URP particle shader is unavailable.");
            foreach(var zone in zones)
                if(new SerializedObject(zone).FindProperty("weatherMaterial")==null)
                    throw new InvalidOperationException("Weather material field is missing on "+zone.name);

            string workspace=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string backup=Path.Combine(workspace,"backups","2026-09-24_before_weather_material_fix_"+stamp);
            Directory.CreateDirectory(backup);
            foreach(string asset in new[]{ScenePath,MaterialPath,TexturePath})
            {
                string file=Path.Combine(project,asset);
                if(File.Exists(file))File.Copy(file,Path.Combine(backup,Path.GetFileName(file)));
                if(File.Exists(file+".meta"))File.Copy(file+".meta",Path.Combine(backup,Path.GetFileName(file)+".meta"));
            }
            string prior=string.Join("\n",zones.Select(z=>z.name+" previous material: "+AssetDatabase.GetAssetPath(new SerializedObject(z).FindProperty("weatherMaterial").objectReferenceValue)));
            prior+="\nauthored rain previous material: "+AssetDatabase.GetAssetPath(renderer.sharedMaterial);

            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if(!texture)
            {
                if(AssetDatabase.LoadMainAssetAtPath(TexturePath))throw new InvalidOperationException("Weather mask asset path contains another asset type.");
                texture=new Texture2D(64,64,TextureFormat.RGBA32,false,true){name="SkywellLocalWeatherSoftMask",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                var pixels=new Color32[64*64];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {
                    float px=(x+.5f)/32-1,py=(y+.5f)/32-1;
                    float alpha=Mathf.Pow(Mathf.Clamp01(1-px*px-py*py),2);
                    pixels[y*64+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha*255));
                }
                texture.SetPixels32(pixels);texture.Apply(false,false);AssetDatabase.CreateAsset(texture,TexturePath);
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(!material)
            {
                if(AssetDatabase.LoadMainAssetAtPath(MaterialPath))throw new InvalidOperationException("Weather material path contains another asset type.");
                material=new Material(shader){name="SkywellLocalWeatherSoft"};AssetDatabase.CreateAsset(material,MaterialPath);
            }
            Undo.RecordObject(material,"Configure transparent weather particles");
            material.shader=shader;material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ColorMode",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",(float)CullMode.Off);material.SetFloat("_AlphaClip",0);
            material.SetFloat("_SoftParticlesEnabled",0);material.SetFloat("_CameraFadingEnabled",0);material.SetFloat("_DistortionEnabled",0);
            material.DisableKeyword("_ALPHATEST_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.DisableKeyword("_ALPHAMODULATE_ON");
            material.DisableKeyword("_SOFTPARTICLES_ON");material.DisableKeyword("_FADING_ON");material.DisableKeyword("_DISTORTION_ON");
            material.DisableKeyword("_COLOROVERLAY_ON");material.DisableKeyword("_COLORCOLOR_ON");material.DisableKeyword("_COLORADDSUBDIFF_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=(int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly",false);material.SetShaderPassEnabled("DepthNormalsOnly",false);material.SetShaderPassEnabled("ShadowCaster",false);
            EditorUtility.SetDirty(material);

            Undo.RecordObjects(zones.Cast<Object>().Append(renderer).ToArray(),"Bind local weather particle material");
            foreach(var zone in zones)
            {
                var data=new SerializedObject(zone);data.FindProperty("weatherMaterial").objectReferenceValue=material;
                data.ApplyModifiedProperties();EditorUtility.SetDirty(zone);
            }
            renderer.sharedMaterial=material;EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Unable to save the repaired Skywell author scene.");
            string report=Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/weather-material-fix.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report,DateTime.UtcNow.ToString("O")+"\nWEATHER_MATERIAL_FIX_SAVED\nScene: "+ScenePath+"\nMaterial: "+MaterialPath+"\nMask: "+TexturePath+"\nShader: "+shader.name+"\n4 zone bindings + existing tower rain renderer; original WeatherCircuit material unchanged.\nTransparent particle vertex color and radial alpha; depth writes off. No gameplay or transform changes.\nBackup: "+backup+"\n"+prior+"\nRendering still requires Play Mode/build visual inspection.\n");
            Debug.Log("Skywell weather materials repaired and author scene saved: "+report);
        }
    }
}

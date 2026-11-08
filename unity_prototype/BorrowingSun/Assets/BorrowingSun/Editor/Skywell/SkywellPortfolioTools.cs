using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BorrowingSun.Editor
{
    public static partial class SkywellBuilder
    {
        [Serializable] private sealed class PortfolioView {public string id,purpose;public Vector3 position,target;public float fieldOfView=50,orthographicSize;}
        [Serializable] private sealed class PortfolioViews {public string scene,coordinateConvention;public PortfolioView[] views;}
        [MenuItem("Borrowing Sun/Skywell/Portfolio/Capture six art views")]
        public static void CapturePortfolioViews()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Capture saved initial scene in Edit Mode.");
            var folder=Path.Combine(Root,"portfolio/L5_Skywell/04_Visual/Whitebox_Views");Directory.CreateDirectory(folder);
            var views=new[]{
                new PortfolioView{id="01_Tower_Hero",purpose="主塔与三山院；构图与组团总览",position=new Vector3(64,65,49),target=new Vector3(0,28,108)},
                new PortfolioView{id="02_Entrance_Low",purpose="从西货庭仰看主塔；进入场景",position=new Vector3(-33,29,87),target=new Vector3(1,29,110)},
                new PortfolioView{id="03_Mirror_Gallery",purpose="北院采光与镜轨；建筑与机关结合",position=new Vector3(-25,39,118),target=new Vector3(-2,28,132)},
                new PortfolioView{id="04_Underwater_Refuge",purpose="水下舱、柱体与远端设备间；照明构图",position=new Vector3(27,26,55),target=new Vector3(-4,11,84)},
                new PortfolioView{id="05_Valley_Lookback",purpose="第一关与跨谷轨道；整城空间兑现",position=new Vector3(67,74,-35),target=new Vector3(2,22,58)},
                new PortfolioView{id="06_Handover_Station",purpose="花庭前站；交付终点与回望",position=new Vector3(-40,44,-13),target=new Vector3(-24,35,2)}
            };
            foreach(var view in views)CapturePortfolioCamera(Path.Combine(folder,view.id+".png"),view);
            File.WriteAllText(Path.Combine(folder,"Camera_Manifest.json"),JsonUtility.ToJson(new PortfolioViews{scene=ScenePath,coordinateConvention="Unity world metres: X east, Y up, Z north. Initial saved whitebox; art camera, not gameplay camera.",views=views},true));
        }
        private static void CapturePortfolioCamera(string path,PortfolioView view)
        {
            var go=new GameObject("Temporary_portfolio_camera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.CopyFrom(Camera.main);
            go.AddComponent<UniversalAdditionalCameraData>();cam.enabled=false;cam.orthographic=view.orthographicSize>0;cam.orthographicSize=Mathf.Max(1,view.orthographicSize);cam.fieldOfView=view.fieldOfView;cam.aspect=16f/9;cam.nearClipPlane=.1f;cam.farClipPlane=1000;
            cam.transform.SetPositionAndRotation(view.position,Quaternion.LookRotation(view.target-view.position,Vector3.up));
            var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);rt.Create();var previous=RenderTexture.active;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try {RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
            finally {RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);}
        }
        [MenuItem("Borrowing Sun/Skywell/Portfolio/Build macOS playable")]
        public static void BuildPortfolioPlayer()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before build.");
            var folder=Path.Combine(Root,"portfolio/L5_Skywell/08_Playable");Directory.CreateDirectory(folder);
            string previousName=PlayerSettings.productName;BuildReport result;
            try
            {
                PlayerSettings.productName="借晴 · 天井跨谷";
                result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Path.Combine(folder,"Skywell_Playtest.app"),target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            }
            finally {PlayerSettings.productName=previousName;}
            var s=result.summary;
            File.WriteAllText(Path.Combine(folder,"Build_Result.txt"),$"UTC: {DateTime.UtcNow:O}\nScene: {ScenePath}\nUnity: {Application.unityVersion}\nResult: {s.result}\nErrors: {s.totalErrors}\nWarnings: {s.totalWarnings}\nBytes: {s.totalSize}\nDuration: {s.totalTime}\nOutput: {s.outputPath}\nDevelopment build; local macOS; no published distribution/signing claim.\n");
            if(s.result!=BuildResult.Succeeded)throw new InvalidOperationException("Skywell build failed; inspect Build_Result and Editor log.");
        }
    }
}

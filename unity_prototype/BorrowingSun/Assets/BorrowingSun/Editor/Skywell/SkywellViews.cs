using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowingSun.Editor
{
    public static partial class SkywellBuilder
    {
        private static string SunFolder => Path.Combine(Root,"validation/current/skywell/optics");
        public static void CaptureRainDetail(string stage)
        {
            var c=UnityEngine.Object.FindAnyObjectByType<SkywellController>();
            var folder=System.IO.Path.Combine(Root,"validation/current/skywell/rain");System.IO.Directory.CreateDirectory(folder);
            CapturePortfolioCamera(System.IO.Path.Combine(folder,stage+"-structure.png"),new PortfolioView{position=c.Origin+new Vector3(21,12,-24),target=c.Origin+new Vector3(2,1,-2)});
            var main=Camera.main;
            CapturePortfolioCamera(System.IO.Path.Combine(folder,stage+"-normal-camera.png"),new PortfolioView{position=main.transform.position,target=main.transform.position+main.transform.forward*20,fieldOfView=main.fieldOfView});
        }
        public static void CaptureLiftBaseViews()
        {
            if(SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open Skywell.");
            var c=UnityEngine.Object.FindAnyObjectByType<SkywellController>();
            string stage=EditorApplication.isPlaying?(c.Height>5.9f?"high":"low"):"saved-low";
            var folder=Path.Combine(Root,"validation/current/skywell/lift");Directory.CreateDirectory(folder);
            CapturePortfolioCamera(Path.Combine(folder,stage+"-structure.png"),new PortfolioView{position=c.Origin+new Vector3(25,15,-32),target=c.Origin+new Vector3(0,-2,0)});
            CapturePortfolioCamera(Path.Combine(folder,stage+"-deck.png"),new PortfolioView{position=c.Origin+new Vector3(-15,12,-18),target=c.Origin+new Vector3(0,c.Height+1,0)});
        }
        public static void CaptureSunViews(string stage="current")
        {
            Directory.CreateDirectory(SunFolder);var c=UnityEngine.Object.FindAnyObjectByType<SkywellController>();
            CapturePortfolioCamera(Path.Combine(SunFolder,stage+"-valley.png"),new PortfolioView{position=c.Origin+new Vector3(13,43,-96),target=c.Origin+new Vector3(13,8,8),fieldOfView=57});
            CapturePortfolioCamera(Path.Combine(SunFolder,stage+"-settlement.png"),new PortfolioView{position=c.Origin+new Vector3(28,24,-31),target=c.Origin+new Vector3(53,10,0)});
            CapturePortfolioCamera(Path.Combine(SunFolder,stage+"-tower-sightline.png"),new PortfolioView{position=c.Origin+new Vector3(-10,11,-15),target=c.Origin+new Vector3(48,13,-3),fieldOfView=62});
        }
        public static void CaptureCleanDockViews()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!=ScenePath)
                throw new InvalidOperationException("Capture saved Skywell in Edit Mode.");
            var folder=Path.Combine(Root,"validation/current/skywell/open-docks");Directory.CreateDirectory(folder);
            CapturePortfolioCamera(Path.Combine(folder,"open-docks-overview.png"),new PortfolioView
                {position=new Vector3(-47,65,51),target=new Vector3(0,20,105),fieldOfView=52});
            CapturePortfolioCamera(Path.Combine(folder,"open-central-platform.png"),new PortfolioView
                {position=new Vector3(-22,40,87),target=new Vector3(0,21,107),fieldOfView=50});
        }
    }
}

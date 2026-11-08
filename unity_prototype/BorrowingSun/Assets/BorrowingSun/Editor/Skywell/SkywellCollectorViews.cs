using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun.Editor
{
    public static partial class SkywellChecks
    {
        [MenuItem("Borrowing Sun/Skywell/Review collector from normal player camera")]
        public static void RunCollectorViews()=>StartCheck("SKYWELL_COLLECTOR_NORMAL_VIEWS",CollectorViewSequence());
        private static IEnumerator CollectorViewSequence()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/2026-10-19-object-targeting"));
            Directory.CreateDirectory(folder);
            const string stage="collector-targeting";
            report.Add("VISUAL FIXTURE: place on north bridge, then walk with production keyboard/collisions and use Tab plus held E and an actual screen-positioned pointer click on the collector; starts at the default F camera; pointer helper may use real Z orbit if needed and logs it, with no overview or custom camera pose. No Sunny state injected. Text hidden only for comparison captures.");
            court.ResetCourt();court.Player.Teleport(new Vector3(0,26.03f,119));
            yield return EnsureCameraMode(false);yield return Wait(1.5f);
            yield return CollectorViewCapture(folder,stage+"-01-bridge");
            yield return Walk(new Vector3(0,26.03f,126),"Normal approach across north bridge");
            yield return Wait(1);yield return CollectorViewCapture(folder,stage+"-02-court");
            yield return Walk(new Vector3(-3,26.03f,128),"Enter shared courtyard");
            var station=court.Consoles[1];
            yield return Walk(station.transform.position+new Vector3(0,.03f,-1.3f),"Approach collector weather station");
            yield return Wait(1);yield return CollectorViewCapture(folder,stage+"-03-weather-station");
            Require(station.Zone.Contains(court.Player.FeetPosition),"Player remains within the collector's local weather area.");
            yield return SetWeather(SkywellToken.Wind,1);
            yield return WaitUntil(()=>court.Optics.Collector.FocusIndex==1&&court.Optics.Collector.IsLocked,5,"Local Wind physically turns plate to the central relay stop");
            yield return Wait(1);yield return CollectorViewCapture(folder,stage+"-04-redirected");
            Require(!court.Optics.Collector.Active&&!court.Optics.Lit,"No emitted gameplay beam without local Sunny; silhouette review is an unlit fixture.");
            File.WriteAllLines(Path.Combine(folder,stage+"-normal-camera.txt"),report);
        }
        private static IEnumerator CollectorViewCapture(string folder,string name)
        {
            var camera=court.CameraRig.ViewCamera;
            Require(!court.CameraRig.IsOverview&&!court.CameraRig.IsObserving&&!camera.orthographic&&Mathf.Abs(camera.fieldOfView-50)<.01f,"Normal perspective follow camera: "+name);
            report.Add(name+" feet="+court.Player.FeetPosition.ToString("F3")+" camera="+camera.transform.position.ToString("F3")+" yaw="+court.CameraRig.OrbitYaw+" frontDot="+Vector3.Dot(-court.Optics.Collector.Plate.forward,(camera.transform.position-court.Optics.SourcePoint).normalized).ToString("F3"));
            var hud=Object.FindAnyObjectByType<SkywellHUD>();bool enabled=hud&&hud.enabled;
            try
            {
                if(hud)hud.enabled=false;yield return Wait(.1f);
                Capture(Path.Combine(folder,name+".png"));yield return Wait(.2f);
            }
            finally {if(hud)hud.enabled=enabled;}
        }
    }
}

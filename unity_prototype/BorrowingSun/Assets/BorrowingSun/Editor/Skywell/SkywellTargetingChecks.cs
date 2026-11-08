using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    public static partial class SkywellChecks
    {
        [MenuItem("Borrowing Sun/Skywell/Check held E weather targeting")]
        public static void RunTargeting()=>StartCheck("SKYWELL_WEATHER_TARGETING",TargetingSequence());

        private static SkywellWeatherTarget WeatherTargetForStation(int station)
        {
            var targets=court.WeatherTargets;
            var matches=targets==null?Array.Empty<SkywellWeatherTarget>():targets.Where(t=>t&&t.StationIndex==station).ToArray();
            if(matches.Length!=1)throw new Exception("Expected one authored weather target for station "+station+", found "+matches.Length);
            return matches[0];
        }
        private static SkywellWeatherTarget ClosestWeatherTarget()
        {
            int best=-1;float distance=float.PositiveInfinity;
            for(int i=0;i<court.Consoles.Length;i++)
            {
                var station=court.Consoles[i];if(!station||!station.Zone||!station.Zone.Contains(court.Player.FeetPosition))continue;
                float candidate=Vector3.SqrMagnitude(station.transform.position-court.Player.FeetPosition);
                if(candidate<distance){distance=candidate;best=i;}
            }
            if(best<0)throw new Exception("Weather helper requires the player inside an authored target region; feet="+court.Player.FeetPosition);
            return WeatherTargetForStation(best);
        }
        private static IEnumerator SetWeather(SkywellToken token,int action)
        {
            bool originalShoulder=court.CameraRig.IsShoulderView;
            yield return PickWeather(token);
            var target=ClosestWeatherTarget();var station=court.Consoles[target.StationIndex];
            bool available=court.CanUseToken(token);
            yield return BeginTargeting(target);
            yield return ClickWeatherPointer(target,available,token==SkywellToken.Wind&&action==0);
            if(available)
            {
                Require(station.Token==token,"A real pointer hit applies "+token+" to "+target.DisplayName+".");
                yield return Wait(1.3f);
                if(token==SkywellToken.Wind)Require((station.Zone.WindDirection.x<0?0:1)==action,
                    "The explicit "+(action==1?"left/clockwise":"right/counterclockwise")+" mouse button selects the requested Wind direction on its first hit.");
            }
            yield return ReleaseTargeting();
            Require(!court.WeatherTargeting,"Releasing E ends targeting without another short-E action.");
            yield return EnsureCameraMode(originalShoulder);
        }
        private static IEnumerator WaitForWeatherMechanism(SkywellWeatherTarget target)
        {
            int station=target.StationIndex;
            yield return WaitUntil(()=>!court.Consoles[station].Busy&&(station==0?!court.IsMoving:station==1?court.Optics.Collector.IsLocked:station!=2||court.WindBell.WindSettled),8,"Wait for the current weather mechanism before another target click");
        }
        private static IEnumerator BeginTargeting(SkywellWeatherTarget target)
        {
            // Production camera controls only. No camera pose or player placement in this helper.
            for(int attempt=0;attempt<10;attempt++)
            {
                input=new KeyboardState(Key.E);yield return Wait(court.weatherHoldTime+.12f);
                Require(court.WeatherTargeting,"Holding E opens object targeting.");
                if(TryWeatherPointer(target,out Vector2 point))
                {
                    pointerInput=new MouseState{position=point};yield return Wait(.08f);
                    report.Add("POINTER target="+target.DisplayName+" station="+target.StationIndex+" pixel="+point+" camera="+court.CameraRig.ViewCamera.transform.position);SaveProgress();
                    yield break;
                }
                bool wasShoulder=court.CameraRig.IsShoulderView;
                yield return ReleaseTargeting();
                if(wasShoulder&&court.CanTargetWeather(target))
                    throw new Exception("Automatic shoulder targeting view did not expose target "+target.DisplayName+"; feet="+court.Player.FeetPosition);
                yield return EnsureCameraMode(true);
                if(!court.CanUseToken(court.SelectedWeather))yield return AimShoulderAtWeatherTarget(target);
                report.Add("POINTER VIEW: actual Z selected shoulder; the next held E uses automatic physical-target framing for "+target.DisplayName);SaveProgress();
            }
            throw new Exception("No visible unoccluded pixel on "+target.DisplayName+" from production camera; feet="+court.Player.FeetPosition);
        }
        private static IEnumerator ClickWeatherPointer(SkywellWeatherTarget target,bool requireHover,bool rightButton=false)
        {
            if(!TryWeatherPointer(target,out Vector2 point))throw new Exception("Target moved out of the clickable screen: "+target.DisplayName);
            pointerInput=new MouseState{position=point};yield return Wait(.08f);
            if(requireHover)Require(court.HoveredWeatherTarget==target,"The production camera ray hovers the visible "+target.DisplayName+".");
            pointerInput=new MouseState{position=point,buttons=(ushort)(rightButton?2:1)};yield return Wait(.08f);
            pointerInput=new MouseState{position=point};yield return Wait(.08f);
        }
        private static IEnumerator ReleaseTargeting()
        {
            pointerInput=new MouseState{position=pointerInput.position};input=new KeyboardState();yield return Wait(.15f);
        }
        private static bool TryWeatherPointer(SkywellWeatherTarget target,out Vector2 point)
        {
            var camera=court.CameraRig.ViewCamera;
            foreach(Vector3 world in WeatherSurfaceSamples(target))
            {
                Vector3 screen=camera.WorldToScreenPoint(world);
                if(screen.z<=camera.nearClipPlane||screen.x<12||screen.x>Screen.width-12||screen.y<64||screen.y>Screen.height-64)continue;
                var candidate=new Vector2(screen.x,screen.y);
                if(SkywellHUD.IsWeatherSelectorHit(candidate)||!target.TryHit(camera.ScreenPointToRay(candidate),out _))continue;
                point=candidate;return true;
            }
            point=default;return false;
        }
        private static IEnumerable<Vector3> WeatherSurfaceSamples(SkywellWeatherTarget target)
        {
            foreach(var renderer in target.HighlightRenderers??Array.Empty<Renderer>())
            {
                if(!renderer||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                yield return renderer.bounds.center;
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter&&filter.sharedMesh&&filter.sharedMesh.isReadable)
                {
                    var vertices=filter.sharedMesh.vertices;var triangles=filter.sharedMesh.triangles;
                    int step=Mathf.Max(1,triangles.Length/3/96)*3;
                    for(int i=0;i+2<triangles.Length;i+=step)
                        yield return renderer.transform.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);
                }
                foreach(var point in BoundsSamples(renderer.bounds))yield return point;
            }
            foreach(var collider in target.HitColliders??Array.Empty<Collider>())
                if(collider&&collider.enabled&&collider.gameObject.activeInHierarchy)
                    foreach(var point in BoundsSamples(collider.bounds))yield return point;
        }
        private static IEnumerable<Vector3> BoundsSamples(Bounds bounds)
        {
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                yield return bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)*.8f);
        }
        private static IEnumerator PickWeatherWithPointer(SkywellToken token)
        {
            float scale=Mathf.Clamp(Screen.height/900f,.65f,1.6f);
            Vector2 icon=new Vector2(Screen.width-78*scale,81*scale);
            Require(SkywellHUD.IsWeatherSelectorHit(icon),"Pointer fixture uses the actual weather-icon hit area.");
            pointerInput=new MouseState{position=icon};yield return Wait(.08f);
            pointerInput=new MouseState{position=icon,buttons=1};yield return Wait(court.weatherHoldTime+.15f);
            Require(court.WeatherPickerOpen,"Holding the actual weather icon still opens token selection.");
            int index=(int)token-1;
            Vector2 card=new Vector2(Screen.width-(350-index*110-53)*scale,256.5f*scale);
            pointerInput=new MouseState{position=card,delta=card-icon,buttons=1};yield return Wait(.12f);
            Require(court.WeatherCandidate==token,"Moving the held pointer onto a card previews "+token+".");
            pointerInput=new MouseState{position=card};yield return Wait(.2f);
            Require(!court.WeatherPickerOpen&&!court.WeatherSelectionHeld&&court.SelectedWeather==token,"Releasing the icon selection chooses the card without casting it.");
        }
        private static IEnumerator TargetingSequence()
        {
            report.Add("EXPLICIT TARGETING FIXTURES: player placed in authored tower/collector/village regions and beside the inspection point. Real Tab selection, held E, screen-positioned Mouse left/right clicks, short-E release and Escape/R drive all interactions. Fixed mouse direction is checked against actual tower yaw and collector plate rotation. No TryCastWeather/Request calls or item/repair injections. Complete route separately verifies continuous movement.");
            court.ResetCourt();yield return Wait(.4f);
            Require(court.WeatherTargets!=null&&court.WeatherTargets.Length==5,"Five actual weather objects are bound, including the optional apple tree.");
            for(int i=0;i<court.WeatherTargets.Length;i++)Require(WeatherTargetForStation(i).HighlightRenderers.Any(r=>r),"Weather target "+i+" has visible highlighted geometry.");
            yield return WeatherFixturePlace(0);
            yield return PickWeatherWithPointer(SkywellToken.Wind);
            Require(court.Consoles.All(s=>s.Token==SkywellToken.None),"Pointer card selection alone never casts weather.");
            yield return PickWeather(SkywellToken.Rain);
            yield return Press(Key.E);
            Require(court.Consoles.All(s=>s.Token==SkywellToken.None)&&!court.WeatherTargeting,"Short E release does not cast weather onto a nearby station.");
            var tower=WeatherTargetForStation(0);
            yield return BeginTargeting(tower);
            Capture(Path.Combine(Folder,"targeting-tower-hover.png"));yield return Wait(.15f);
            Require(court.Consoles.All(s=>s.Token==SkywellToken.None),"Holding E only highlights targets; it does not cast by itself.");
            var tokens=court.Consoles.Select(s=>s.Token).ToArray();
            yield return ClickWeatherPointer(tower,true,true);
            Require(tokens.SequenceEqual(court.Consoles.Select(s=>s.Token))&&!court.TowerWeatherZone.IsRain,"Right clicking a real target while Rain is selected never inserts or starts Rain.");
            Vector2 empty=FindEmptyWeatherPixel();pointerInput=new MouseState{position=empty};yield return Wait(.08f);
            Require(!court.HoveredWeatherTarget,"Empty screen space is not a weather object.");
            pointerInput=new MouseState{position=empty,buttons=1};yield return Wait(.08f);pointerInput=new MouseState{position=empty};yield return Wait(.08f);
            Require(tokens.SequenceEqual(court.Consoles.Select(s=>s.Token)),"Clicking empty space does not cast weather.");
            input=new KeyboardState(Key.E,Key.Escape);yield return Wait(.15f);input=new KeyboardState();yield return Wait(.2f);
            Require(!court.WeatherTargeting&&!court.CameraRig.InputSuppressed&&tokens.SequenceEqual(court.Consoles.Select(s=>s.Token)),"Escape cancels targeting without casting or leaving a camera lock.");
            yield return TargetingLifecycleChecks(tower);
            yield return PickWeather(SkywellToken.Sun);yield return BeginTargeting(tower);yield return ClickWeatherPointer(tower,false);yield return ReleaseTargeting();
            Require(!court.CanUseToken(SkywellToken.Sun)&&court.Consoles.All(s=>s.Token==SkywellToken.None),"Clicking a real target with unavailable Sunny never grants or inserts it.");
            yield return SetWeather(SkywellToken.Rain,0);yield return Carry(.5f);yield return WeatherFixtureLeave(0);
            Require(!court.Consoles[0].Zone.IsRain,"Leaving the real region returns Rain and stops its weather without Q.");
            yield return FixedTowerWindButtonChecks();

            yield return WeatherFixturePlace(1);var collector=WeatherTargetForStation(1);var station=court.Consoles[1];
            yield return PickWeather(SkywellToken.Wind);yield return BeginTargeting(collector);yield return ClickWeatherPointer(collector,true);
            Require(station.Token==SkywellToken.Wind&&station.Zone.WindDirection.x>0,"Left click selects clockwise Wind toward the collector relay stop.");
            Require(court.WeatherTargeting,"Successful casting keeps targeting active until E is released.");
            yield return ClickWeatherPointer(collector,true,true);
            Require(station.Zone.WindDirection.x>0,"Opposite-button input during the busy weather transition cannot change the accepted Wind direction.");
            yield return WaitForWeatherMechanism(collector);Quaternion relayPose=court.Optics.Collector.Plate.rotation;
            yield return ClickWeatherPointer(collector,true);yield return WaitForWeatherMechanism(collector);
            Require(station.Zone.WindDirection.x>0&&court.Optics.Collector.FocusIndex==1&&Quaternion.Angle(relayPose,court.Optics.Collector.Plate.rotation)<.1f,"Repeated left click keeps the real plate at its clockwise relay stop without toggling.");
            yield return ClickWeatherPointer(collector,true,true);yield return WaitForWeatherMechanism(collector);
            Quaternion calibrationPose=court.Optics.Collector.Plate.rotation;
            Require(station.Zone.WindDirection.x<0&&court.Optics.Collector.FocusIndex==0&&Quaternion.Angle(relayPose,calibrationPose)>5,"Right click physically returns the mirror to the counterclockwise calibration stop.");
            yield return ClickWeatherPointer(collector,true,true);yield return WaitForWeatherMechanism(collector);
            Require(station.Zone.WindDirection.x<0&&Quaternion.Angle(calibrationPose,court.Optics.Collector.Plate.rotation)<.1f,"Repeated right click stays at the actual calibration pose and never toggles clockwise.");
            yield return ReleaseTargeting();
            Require(!court.Optics.Collector.Active&&!court.Optics.Collector.IsRepaired,"Wind can move the broken mirror without fabricating light or a repair.");
            yield return WeatherFixtureLeave(1);Quaternion held=court.Optics.Collector.Plate.rotation;yield return Wait(.5f);
            Require(!station.Zone.IsWind&&Quaternion.Angle(held,court.Optics.Collector.Plate.rotation)<.01f,"Wind departure leaves the actual physical mirror pose stationary.");
            yield return WeatherFixturePlace(1);yield return PickWeather(SkywellToken.Wind);yield return BeginTargeting(collector);yield return ClickWeatherPointer(collector,true,true);
            Require(station.Zone.WindDirection.x<0,"Right click after departure still selects counterclockwise instead of a default or remembered toggle.");
            yield return WaitForWeatherMechanism(collector);yield return ClickWeatherPointer(collector,true);
            Require(station.Zone.WindDirection.x>0,"Left click explicitly chooses clockwise again after counterclockwise weather resumes.");
            yield return WaitForWeatherMechanism(collector);yield return ReleaseTargeting();
            yield return SetWeather(SkywellToken.Rain,0);yield return PickWeather(SkywellToken.Wind);yield return BeginTargeting(collector);yield return ClickWeatherPointer(collector,true,true);
            Require(station.Zone.WindDirection.x<0,"Replacing Wind with Rain does not change the right button's counterclockwise meaning.");
            yield return WaitForWeatherMechanism(collector);Capture(Path.Combine(Folder,"targeting-collector-hover.png"));yield return Wait(.15f);yield return ReleaseTargeting();

            court.Player.Teleport(court.Story.InteractionPoint(0).position+new Vector3(.9f,.03f,0));yield return Wait(.3f);
            input=new KeyboardState(Key.E);yield return Wait(court.weatherHoldTime+.15f);
            Require(court.WeatherTargeting&&!court.Story.DialogueOpen,"Holding E at the mirror inspection point selects objects without opening dialogue.");
            yield return ReleaseTargeting();Require(!court.Story.DialogueOpen,"Releasing a long hold does not fall through to short-E inspection.");
            input=new KeyboardState(Key.E);yield return Wait(.12f);Require(!court.Story.DialogueOpen,"Inspection waits for short-E release.");
            input=new KeyboardState();yield return Wait(.2f);Require(court.Story.DialogueOpen&&!court.WeatherTargeting,"Short-E release still opens the normal mirror dialogue.");
            yield return Press(Key.Escape);
            Require(!court.Story.DialogueOpen&&!court.WeatherTargeting&&!court.CameraRig.InputSuppressed,"Closing dialogue restores normal control after targeting.");
            yield return BeginTargeting(collector);input=new KeyboardState(Key.E,Key.R);yield return Wait(.2f);input=new KeyboardState();yield return Wait(.3f);
            Require(!court.WeatherTargeting&&!court.HoveredWeatherTarget&&!court.CameraRig.InputSuppressed,"R during targeting clears highlight selection and input locks.");
            CheckReset();
        }
        private static IEnumerator FixedTowerWindButtonChecks()
        {
            yield return WeatherFixturePlace(0);yield return PickWeather(SkywellToken.Wind);
            var target=WeatherTargetForStation(0);
            Require(Mathf.Abs(court.Yaw+90)<.1f,"The real tower starts the button-direction fixture at its west stop.");
            yield return BeginTargeting(target);
            foreach(float expected in new[]{0f,90f,90f,0f,-90f,-90f})
            {
                bool right=expected<court.Yaw||expected==-90;
                float previous=court.Yaw;Quaternion before=court.Rotor.rotation;
                yield return ClickWeatherPointer(target,true,right);yield return WaitForWeatherMechanism(target);
                Require(Mathf.Abs(court.Yaw-expected)<.1f&&Mathf.Abs(Mathf.DeltaAngle(court.Rotor.eulerAngles.y,expected))<.1f,
                    (right?"Right":"Left")+" click moves the real rotor from "+previous+" to "+expected+" degrees with its fixed direction and existing stop limits.");
                Require(Mathf.Abs(expected-previous)<.1f?Quaternion.Angle(before,court.Rotor.rotation)<.1f:Quaternion.Angle(before,court.Rotor.rotation)>89,
                    "The physical rotor transform matches the requested step or bounded end-stop hold.");
                Require((court.Consoles[0].Zone.WindDirection.x<0)==right,"The tower's wind vector retains the clicked button direction after motion stops.");
            }
            yield return ReleaseTargeting();yield return WeatherFixtureLeave(0);
        }
        private static IEnumerator TargetingLifecycleChecks(SkywellWeatherTarget target)
        {
            report.Add("LIFECYCLE FIXTURES: disable/re-enable the actual target and controller components during held-E targeting; invoke the controller's OnApplicationFocus(false) callback explicitly. This is callback injection, not a real OS focus-change test. All fixture state is restored before normal input resumes.");SaveProgress();
            var snapshots=new List<WeatherHighlightSnapshot>();
            foreach(var renderer in target.HighlightRenderers.Where(r=>r).Distinct())
                for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)snapshots.Add(new WeatherHighlightSnapshot(renderer,slot));
            var tokens=court.Consoles.Select(s=>s.Token).ToArray();
            yield return BeginTargeting(target);
            Require(snapshots.Any(s=>!s.Matches()),"Held-E targeting changes actual renderer material-slot overrides before lifecycle checks.");
            try
            {
                target.enabled=false;yield return Wait(.15f);
                Require(snapshots.All(s=>s.Matches()),"Disabling the actual weather-target component restores its original material-slot colors and empty overrides.");
                Require(!court.CanTargetWeather(target)&&court.HoveredWeatherTarget!=target,"A disabled target cannot remain eligible or hovered.");
            }
            finally{target.enabled=true;}
            yield return Wait(.15f);
            Require(court.WeatherTargeting&&snapshots.Any(s=>!s.Matches()),"Re-enabling the target during the same hold restores eligible highlighting through normal controller updates.");
            yield return ReleaseTargeting();
            Require(snapshots.All(s=>s.Matches()),"Releasing E removes the re-enabled target's highlight.");

            yield return BeginTargeting(target);
            var focus=typeof(SkywellController).GetMethod("OnApplicationFocus",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Require(focus!=null,"The controller exposes its Unity focus lifecycle callback for the explicitly injected fixture.");
            focus.Invoke(court,new object[]{false});yield return Wait(.15f);
            Require(!court.WeatherTargeting&&!court.HoveredWeatherTarget&&!court.CameraRig.InputSuppressed,"Injected focus-loss callback cancels targeting and clears hover/camera input lock.");
            Require(snapshots.All(s=>s.Matches())&&tokens.SequenceEqual(court.Consoles.Select(s=>s.Token)),"Injected focus loss restores actual highlight overrides without casting weather.");
            yield return ReleaseTargeting();focus.Invoke(court,new object[]{true});yield return Wait(.15f);
            Require(!court.Story.DialogueOpen&&!court.WeatherTargeting,"Releasing E after focus cancellation does not trigger a short-E dialogue.");

            yield return BeginTargeting(target);
            try
            {
                court.enabled=false;yield return Wait(.15f);
                Require(!court.WeatherTargeting&&!court.HoveredWeatherTarget&&!court.CameraRig.InputSuppressed,"Disabling the actual controller cancels targeting and camera input lock.");
                Require(snapshots.All(s=>s.Matches())&&tokens.SequenceEqual(court.Consoles.Select(s=>s.Token)),"Controller disable restores actual highlight overrides without casting weather.");
            }
            finally{court.enabled=true;}
            yield return ReleaseTargeting();
            yield return BeginTargeting(target);yield return ReleaseTargeting();
            Require(!court.WeatherTargeting&&snapshots.All(s=>s.Matches()),"Held-E targeting and release remain functional after controller re-enable.");
        }
        private sealed class WeatherHighlightSnapshot
        {
            private readonly Renderer renderer;
            private readonly int slot;
            private readonly MaterialPropertyBlock original=new MaterialPropertyBlock();
            private readonly MaterialPropertyBlock current=new MaterialPropertyBlock();
            private static readonly int[] Colors={Shader.PropertyToID("_BaseColor"),Shader.PropertyToID("_Color"),Shader.PropertyToID("_EmissionColor")};
            public WeatherHighlightSnapshot(Renderer renderer,int slot)
            {this.renderer=renderer;this.slot=slot;renderer.GetPropertyBlock(original,slot);}
            public bool Matches()
            {
                if(!renderer)return false;
                renderer.GetPropertyBlock(current,slot);
                if(original.isEmpty!=current.isEmpty)return false;
                foreach(int color in Colors)
                    if(original.HasColor(color)!=current.HasColor(color)||(original.HasColor(color)&&original.GetColor(color)!=current.GetColor(color)))return false;
                return true;
            }
        }
        private static Vector2 FindEmptyWeatherPixel()
        {
            var camera=court.CameraRig.ViewCamera;
            for(int y=2;y<9;y++)for(int x=1;x<10;x++)
            {
                var point=new Vector2(Screen.width*x/10f,Screen.height*y/10f);var ray=camera.ScreenPointToRay(point);
                if(court.WeatherTargets.All(t=>!t||!t.TryHit(ray,out _)))return point;
            }
            throw new Exception("No empty pixel found for the negative pointer fixture.");
        }
    }
}

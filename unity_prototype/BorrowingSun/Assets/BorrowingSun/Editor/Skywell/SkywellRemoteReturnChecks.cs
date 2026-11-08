using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun.Editor
{
    public static partial class SkywellChecks
    {
        public static void RunRemoteReturn()=>StartCheck("SKYWELL_REMOTE_NORTH_RETURN",RemoteNorthReturnSequence());

        private static IEnumerator RemoteNorthReturnSequence()
        {
            report.Add("REMOTE NORTH RETURN FIXTURE: explicit placements inside the boat for actual E Sunny pickup, on the tower for real Rain/Wind preparation, then once on the northern village floor. Tower height and yaw are reached through production weather input and mechanisms. After the northern placement, all travel is actual WASD: no player/rotor placement, height/yaw/ownership injection, recall, jump or success shortcut. Real held E and screen-space left/right mouse hits select the distant tower, outside its weather volume. This is a focused return route, not the complete entrance playthrough or a first-player test.");
            court.ResetCourt();yield return Wait(.4f);
            var tower=WeatherTargetForStation(0);var station=court.Consoles[0];var zone=station.Zone;
            var gangway=court.Rotor.Find("Doorway_gangway");
            Require(gangway&&gangway.GetComponent<Collider>()&&gangway.GetComponent<Collider>().enabled,
                "The authored protruding doorway gangway and its solid collision are retained.");
            Vector3 originalGangwayPosition=gangway.localPosition,originalGangwayScale=gangway.localScale;
            Require(Mathf.Abs(court.weatherCastRange-45)<.001f&&Mathf.Abs(court.remoteWeatherMoveRadius-3.5f)<.001f,
                "This check exercises the authored 45m surface-distance limit and 3.5m remote operating radius.");

            court.Player.Teleport(court.Journey.ReturnShuttle.TransformPoint(new Vector3(0,.03f,-.7f)));
            yield return Wait(.3f);yield return Press(Key.E);
            Require(court.Journey.SunnyTokenObtained&&court.CanUseToken(SkywellToken.Sun),
                "Sunny for the persistence probe comes from the actual boat pickup, never a flag injection.");
            yield return WeatherFixturePlace(0);
            yield return SetWeather(SkywellToken.Rain,0);
            yield return CarryUntil(()=>court.LiftLatched&&Mathf.Abs(court.Height-court.PlatformHighY)<.01f,15,
                "Real collected rain raises and latches the tower at its high level");
            yield return TurnTowerTo(90);
            Require(court.DockedPort==2&&Mathf.Abs(court.Yaw-90)<.1f,"The return problem starts with the actual high tower facing east.");

            Vector3 north=court.Origin+new Vector3(0,court.PlatformHighY+.03f,23);
            court.Player.Teleport(north);yield return Wait(1.5f);
            Require(!court.OnRotor(court.Player.FeetPosition)&&!zone.Contains(court.Player.FeetPosition)&&station.Token==SkywellToken.None,
                "The declared northern-village starting point is supported independently of the rotor and outside the tower weather volume.");
            Require(tower.SurfaceDistance(court.Player.FeetPosition)<court.weatherCastRange,
                "The visible main tower lies inside the real remote surface-distance limit.");
            yield return EnsureCameraMode(true);
            yield return PickWeather(SkywellToken.Wind);
            // Rotate the real shoulder view toward actual mesh samples; no camera transform assignment.
            for(int aim=0;aim<3;aim++)yield return AimShoulderAtWeatherTarget(tower);
            Require(court.CanTargetWeather(tower),"The tower is genuinely visible and eligible from the off-rotor northern court.");
            Capture(Path.Combine(Folder,"remote-return-north-view.png"));yield return Wait(.15f);

            yield return RemoteWeatherDepartureProbe(tower,SkywellToken.Rain,north);
            yield return RemoteWeatherDepartureProbe(tower,SkywellToken.Sun,north);

            Vector3 castPosition=court.Player.FeetPosition;
            yield return PickWeather(SkywellToken.Wind);yield return BeginTargeting(tower);
            yield return ClickWeatherPointer(tower,true);yield return WaitForWeatherMechanism(tower);
            Require(zone.IsWind&&zone.WindDirection.x>0&&Mathf.Abs(court.Yaw-90)<.1f,
                "Remote left click has its fixed clockwise meaning and respects the physical east end stop.");
            yield return ClickWeatherPointer(tower,true,true);yield return WaitForWeatherMechanism(tower);
            Require(zone.IsWind&&zone.WindDirection.x<0&&Mathf.Abs(court.Yaw)<.1f&&court.DockedPort==1,
                "Remote right click turns the actual high tower counterclockwise from east to the northern bridge.");
            Require(!court.OnRotor(court.Player.FeetPosition)&&Vector3.Distance(castPosition,court.Player.FeetPosition)<.04f,
                "The north-village player stays on their own floor while remotely rotating the tower.");
            Capture(Path.Combine(Folder,"remote-return-tower-aligned.png"));yield return Wait(.15f);
            yield return ReleaseTargeting();yield return EnsureCameraMode(false);
            Require(zone.IsWind&&station.Token==SkywellToken.Wind,"Releasing held E removes targeting without withdrawing the remote Wind token.");

            yield return Walk(castPosition+Vector3.back*2.8f,"North court to bridge mouth, still inside the remote operating radius");
            Require(Vector3.Distance(castPosition,court.Player.FeetPosition)<court.remoteWeatherMoveRadius&&station.Token==SkywellToken.Wind&&zone.IsWind,
                "Wind stays active while the player remains within 3.5m of the real remote cast position.");
            yield return Walk(court.Origin+new Vector3(0,court.PlatformHighY+.03f,18.5f),"Leave the remote operating area onto the fixed northern bridge");
            yield return Wait(1.4f);
            Require(Vector3.Distance(castPosition,court.Player.FeetPosition)>court.remoteWeatherMoveRadius&&!zone.Contains(court.Player.FeetPosition)&&
                station.Token==SkywellToken.None&&!zone.IsWind&&court.TokenLocation(SkywellToken.Wind)=="在手中",
                "Walking beyond 3.5m returns remote Wind and stops its weather before entering the tower volume.");
            Require(Mathf.Abs(court.Yaw)<.1f&&court.LiftLatched&&Mathf.Abs(court.Height-court.PlatformHighY)<.01f,
                "Stopped Wind leaves the northern alignment and real high-level latch intact for the return crossing.");

            yield return Walk(court.Origin+new Vector3(0,court.PlatformHighY+.03f,10.6f),"Walk the fixed north bridge to its inner docking tongue");
            yield return Walk(court.Origin+new Vector3(0,court.PlatformHighY+.03f,7.6f),"Cross the physical inner docking tongue to the protruding gangway");
            yield return Walk(Local(0,6.2f),"Walk onto the retained protruding doorway gangway");
            Require(court.OnRotor(court.Player.FeetPosition),"The real capsule reaches the rotating gangway through continuous ground collision.");
            Capture(Path.Combine(Folder,"remote-return-gangway-crossing.png"));yield return Wait(.15f);
            yield return Walk(Local(0,4.8f),"Enter the tower doorway from the northern return bridge");
            yield return Walk(Local(-2.8f,3.9f),"Clear the cart along the inside tower aisle");
            yield return Walk(Local(-2.4f,-.8f),"Finish the north-village return at the main tower aisle");
            Require(court.OnRotor(court.Player.FeetPosition)&&!court.IsMoving&&Mathf.Abs(court.Yaw)<.1f&&court.Player.enabled&&!court.Player.IsSwimming,
                "Actual northern return finishes on the central platform with ordinary movement and no recovery shortcut.");
            Require(gangway.localPosition==originalGangwayPosition&&gangway.localScale==originalGangwayScale&&gangway.GetComponent<Collider>().enabled,
                "The return test did not remove, resize or disable the protruding gangway to make the route pass.");
            Capture(Path.Combine(Folder,"remote-return-main-platform.png"));yield return Wait(.15f);
            report.Add("SKYWELL_REMOTE_NORTH_RETURN_PASSED");SaveProgress();
        }

        private static IEnumerator RemoteWeatherDepartureProbe(SkywellWeatherTarget target,SkywellToken token,Vector3 origin)
        {
            var station=court.Consoles[target.StationIndex];var zone=station.Zone;
            Vector3 castPosition=court.Player.FeetPosition;
            yield return PickWeather(token);yield return BeginTargeting(target);
            yield return ClickWeatherPointer(target,true);yield return Wait(1.4f);yield return ReleaseTargeting();
            Require(station.Token==token&&zone.ActiveWeather==token&&!zone.Contains(court.Player.FeetPosition),
                "Actual remote "+token+" starts on the distant tower while the player stands outside its weather volume.");
            yield return EnsureCameraMode(false);
            yield return Walk(castPosition+Vector3.forward*2.8f,"Remote "+token+": move within the northern operating area");
            Require(Vector3.Distance(castPosition,court.Player.FeetPosition)<court.remoteWeatherMoveRadius&&station.Token==token&&zone.ActiveWeather==token,
                token+" remains held and active inside the 3.5m remote operating area.");
            yield return Walk(castPosition+Vector3.forward*4.5f,"Remote "+token+": walk beyond the operating radius on village ground");
            yield return Wait(1.4f);
            Require(Vector3.Distance(castPosition,court.Player.FeetPosition)>court.remoteWeatherMoveRadius&&station.Token==SkywellToken.None&&court.TokenLocation(token)=="在手中",
                "Leaving the remote operating area returns "+token+" to the hand without Q or a station visit.");
            Require(zone.ActiveWeather==(token==SkywellToken.Sun?SkywellToken.Sun:SkywellToken.None)&&!zone.IsRain&&!zone.IsWind,
                "Remote departure preserves only established Sunny; Rain and Wind are stopped.");
            yield return Walk(origin,"Return across the northern village floor after the "+token+" departure probe");
            yield return Wait(.3f);
        }
    }
}

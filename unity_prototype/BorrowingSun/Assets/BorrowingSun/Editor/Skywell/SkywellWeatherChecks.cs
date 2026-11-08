using System;
using System.Collections;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    public static partial class SkywellChecks
    {
        public static void RunWeatherViews()=>StartCheck("SKYWELL_WEATHER_VIEWS",WeatherViewSequence());
        private static IEnumerator WeatherViewSequence()
        {
            court.ResetCourt();yield return Wait(.4f);
            yield return WeatherTokenStationMatrix();
            court.ResetCourt();yield return Wait(.3f);CheckReset();
        }
        public static void RunRainFeedback()=>StartCheck("SKYWELL_RAIN_FEEDBACK",RainFeedbackSequence());

        private static IEnumerator RainFeedbackSequence()
        {
            court.ResetCourt();yield return Wait(.3f);
            report.Add("WEATHER/Rain fixture: explicitly placed near stations and on safe ground outside their weather regions; actual keyboard selection, held E and pointer clicks, auto-return, mechanical carry and pickup. Radius alone is tuned temporarily. No water, height, yaw, token-acquired or success-state injection. Departure probes are not a continuous walking-route test.");
            var feedback=court.GetComponentInChildren<SkywellRainFeedback>();
            var zone=court.TowerWeatherZone;
            Require(feedback&&zone&&court.Consoles.Length==5,"Five linked stations and existing physical rain feedback are present.");
            Require(court.Consoles.All(s=>s&&s.Zone&&s.Zone.Station==s),"Every station has its own explicitly linked weather zone.");
            Require(court.Consoles.Select(s=>s.Zone).Distinct().Count()==5,"Stations do not share a weather authority.");
            Require(zone.RainParticles==feedback.RainParticles,"Tower reuses the authored rain system without a duplicate shower.");
            Require(court.Rotor.parent==court.OuterLiftRing&&feedback.CounterweightTanks.Length==4&&feedback.Collectors.Length==4,"Four tanks belong to the non-rotating lift; inner cargo platform rotates independently.");
            Require(Mathf.Abs(zone.Radius-12)<.001f&&court.CollectorCoverage==1,"Authored 12m tower zone covers all four collection roofs.");
            Require(!feedback.Raining&&court.WaterLevel==0&&!court.Journey.SunnyTokenObtained&&!court.CanUseToken(SkywellToken.Sun),"Fresh scene has no rain or stored water, and Sunny has not been obtained.");
            CheckCounterweights(feedback,"initial");
            yield return WeatherFixturePlace(0);
            yield return SetWeather(SkywellToken.Sun,0);
            Require(court.Consoles[0].Token==SkywellToken.None&&!zone.IsSunny&&court.WaterLevel==0,"A held-E pointer cast cannot insert Sunny before its boat pickup.");

            var tuning=new SerializedObject(zone);float originalRadius=zone.Radius;
            try
            {
                tuning.FindProperty("radius").floatValue=4;tuning.ApplyModifiedPropertiesWithoutUndo();
                report.Add("TUNING fixture: only tower Zone.radius changed 12→4; visible rain remains local and all collector roofs lie outside.");
                yield return SetWeather(SkywellToken.Rain,0);yield return Wait(.7f);
                Require(zone.RainVisible&&feedback.LiveDrops>0&&court.CollectorCoverage==0&&court.WaterLevel==0,"Small-zone rain is visible but cannot fill outside roofs.");
                Require(zone.Contains(zone.Center+Vector3.right*3.9f)&&!zone.Contains(zone.Center+Vector3.right*4.1f),"Gameplay radius responds to Inspector tuning at the actual boundary.");
                Require(!feedback.FlowDrops.Any(t=>t.gameObject.activeSelf),"Out-of-zone collection roofs show no gutter inflow.");
                yield return WeatherFixtureLeave(0);
            }
            finally
            {
                if(zone){tuning.Update();tuning.FindProperty("radius").floatValue=originalRadius;tuning.ApplyModifiedPropertiesWithoutUndo();}
            }
            Require(court.CollectorCoverage==1,"Restoring the authored radius restores physical collector coverage.");
            yield return WeatherFixturePlace(0);

            // Split selection and station animation so the collection delay is observed before any water is added.
            yield return PickWeather(SkywellToken.Rain);
            var delayTarget=WeatherTargetForStation(0);
            yield return BeginTargeting(delayTarget);yield return ClickWeatherPointer(delayTarget,true);yield return ReleaseTargeting();
            float activateBy=Time.time+2;
            while(court.Consoles[0].Busy&&Time.time<activateBy)yield return null;
            Require(zone.IsRain&&court.WaterLevel<.001f,"Rain starts before any water is collected.");
            float delay=new SerializedObject(court).FindProperty("rainCollectionDelay").floatValue;
            yield return Wait(delay*.45f);
            Require(court.WaterLevel<.001f,"Roof-to-tank collection delay is respected.");
            yield return Carry(delay+.75f);
            Require(feedback.Raining&&feedback.LiveDrops>0&&court.WaterLevel>0&&court.WaterLevel<court.MaxStoredWater&&court.Height>court.PlatformLowY,"Visible local rain and roof flow drive gradual tank filling and lift motion.");
            Require(feedback.FlowDrops.All(t=>t.gameObject.activeSelf),"All covered collection roofs show gutter and downpipe flow.");
            CheckCounterweights(feedback,"filling");
            yield return WeatherFixtureLeave(0);
            float heldWater=court.WaterLevel,heldHeight=court.Height;var heldTank=feedback.CounterweightTanks[0].position;
            yield return Carry(.7f);
            Require(!feedback.Raining&&feedback.LiveDrops==0&&Mathf.Abs(court.WaterLevel-heldWater)<.001f&&Mathf.Abs(court.Height-heldHeight)<.01f&&Vector3.Distance(heldTank,feedback.CounterweightTanks[0].position)<.01f,"Leaving the region stops rain while water, intermediate deck height and counterweight position persist.");
            Require(!feedback.FlowDrops.Any(t=>t.gameObject.activeSelf),"Departure also stops incoming gutter flow.");
            yield return WeatherFixturePlace(0);

            var tankBefore=feedback.CounterweightTanks.Select(t=>t.position).ToArray();
            var cableBefore=feedback.Cables.Select(l=>l.GetPosition(0)).ToArray();
            var pulleyBefore=feedback.Pulleys.Select(t=>t.rotation).ToArray();
            yield return TurnTowerTo(0);
            for(int i=0;i<4;i++)Require(Vector3.Distance(tankBefore[i],feedback.CounterweightTanks[i].position)<.01f&&Vector3.Distance(cableBefore[i],feedback.Cables[i].GetPosition(0))<.01f&&Quaternion.Angle(pulleyBefore[i],feedback.Pulleys[i].rotation)<.01f,"Wind rotates cargo deck without twisting tank, cable or pulley "+i);
            Require(Quaternion.Angle(court.OuterLiftRing.rotation,Quaternion.identity)<.01f&&Mathf.Abs(court.Yaw)<.1f,"Outer lift remains unrotated while the inner platform turns 90 degrees.");
            yield return TurnTowerTo(-90);yield return WeatherFixtureLeave(0);yield return WeatherFixturePlace(0);
            yield return SetWeather(SkywellToken.Rain,0);yield return Carry(6);
            Require(court.LiftLatched&&Mathf.Abs(court.Height-court.PlatformHighY)<.01f&&Mathf.Abs(court.WaterLevel-court.MaxStoredWater)<.01f&&!court.BorrowedSunAtVillage,"Full water raises and mechanically latches the tower without Sunny or borrowed sunlight.");
            CheckCounterweights(feedback,"full");

            // Leaving the tower returns its physical token before it is inserted at another station.
            yield return WeatherFixturePlace(1);yield return SetWeather(SkywellToken.Rain,0);
            Require(court.Consoles[0].Token==SkywellToken.None&&!court.Consoles[0].Zone.IsRain&&court.Consoles[1].Token==SkywellToken.Rain&&court.Consoles[1].Zone.IsRain&&court.Consoles.Count(s=>s.Token==SkywellToken.Rain)==1,"Departure returns Rain; the second station uses the same single token without leaving rain active at the tower.");
            yield return WeatherFixtureLeave(1);yield return WeatherFixturePlace(0);yield return Carry(.7f);
            Require(court.LiftLatched&&Mathf.Abs(court.Height-court.PlatformHighY)<.01f&&Mathf.Abs(court.WaterLevel-court.MaxStoredWater)<.01f,"Removing weather does not release the mechanical high lock or delete stored water.");
            court.Player.Teleport(court.DrainValve.position+court.Rotor.right*1.1f);yield return Wait(.25f);yield return Press(Key.E);yield return Carry(6);
            Require(court.DockedPort==0&&court.WaterLevel<.01f&&!court.LiftLatched,"Existing drain explicitly releases the latch and returns the tower to its low west dock.");
            CheckCounterweights(feedback,"drained");
            court.ResetCourt();yield return Wait(.3f);
            Require(court.WaterLevel==0&&court.BallastAmount==0&&!court.LiftLatched&&!feedback.Raining&&court.Consoles.All(s=>s.Token==SkywellToken.None)&&court.Cart.IsChildOf(court.Rotor),"Full reset clears weather, water, cargo ballast and latch and restores cargo parenting.");
            CheckCounterweights(feedback,"reset");

            yield return WeatherTokenStationMatrix();
            court.ResetCourt();yield return Wait(.3f);
            Require(!court.Journey.SunnyTokenObtained&&!court.CanUseToken(SkywellToken.Sun)&&court.Consoles.All(s=>s.Token==SkywellToken.None),"Full reset returns the physical Sunny pickup and empties all five stations.");
            report.Add("SKYWELL_LOCAL_WEATHER_AND_RAIN_PASSED");SaveProgress();
        }

        private static IEnumerator WeatherTokenStationMatrix()
        {
            var journey=court.Journey;
            report.Add("WEATHER matrix fixture: explicit placement on the authored surface-water refuge dock, inside the boat for Sunny and beside the floating paddle; real E pickups, boat travel and W ladder climbing. Station and region-exit placements isolate weather/ownership rules, not the continuous water-entry route or first-player navigation. The removed entry lift is not used.");
            court.Player.Teleport(journey.RefugePoint+Vector3.up*.03f);yield return Wait(.3f);
            Require(journey.SurfaceConfigured&&journey.Travel==SkywellTravel.Walking&&!court.Player.IsSwimming&&journey.InRefuge,
                "The authored water-level refuge dock provides real dry support without an E lift or an injected travel state.");
            court.Player.Teleport(journey.ReturnShuttle.TransformPoint(new Vector3(0,.03f,-.7f)));yield return Wait(.2f);yield return Press(Key.E);
            Require(journey.SunnyTokenObtained&&court.CanUseToken(SkywellToken.Sun)&&!journey.SunnyToken.gameObject.activeSelf,"Sunny becomes available only through real E pickup, with its world object removed.");
            yield return ExerciseWeatherStation(2);
            court.Player.Teleport(journey.PaddlePickup.position+Vector3.left*.8f);yield return Wait(.1f);yield return Press(Key.E);
            Require(journey.PaddleObtained&&!journey.PaddlePickup.gameObject.activeSelf,"Real E picks up the floating paddle separately from Sunny.");
            court.Player.Teleport(journey.ReturnShuttle.TransformPoint(new Vector3(0,.03f,-.7f)));yield return Wait(.2f);yield return Press(Key.E);
            Require(journey.Travel==SkywellTravel.Returning,"Paddle and Sunny enable the real water-surface boat route.");
            yield return WaitUntil(()=>journey.Travel==SkywellTravel.Docked,20,"Boat docks beside the tower");
            court.Player.Teleport(journey.LadderBottom.position-journey.LadderFacingDirection*1.4f);yield return Wait(.2f);yield return BeginBottomLadderWithW();
            Require(journey.Travel==SkywellTravel.Climbing,"Real W enters the automatic ladder movement state without E.");
            input=new KeyboardState(Key.W);yield return WaitUntil(()=>journey.Travel==SkywellTravel.Walking,15,"W climbs and steps onto the tower");input=new KeyboardState();
            Require(journey.HasReturnedToTower&&court.Player.enabled,"Ladder completion returns normal movement after the boat.");
            foreach(int station in new[]{0,1,3})
            {
                yield return ExerciseWeatherStation(station);
                Require(court.Consoles[2].Zone.IsSunny,"Reusing Sunny elsewhere preserves the refuge's previously established clear weather.");
            }
        }

        private static IEnumerator ExerciseWeatherStation(int station)
        {
            var console=court.Consoles[station];var zone=console.Zone;
            foreach(var token in new[]{SkywellToken.Rain,SkywellToken.Wind,SkywellToken.Sun})
            {
                yield return WeatherFixturePlace(station);
                if(token!=SkywellToken.Wind)
                {
                    yield return PickWeather(token);var target=WeatherTargetForStation(station);
                    var tokens=court.Consoles.Select(s=>s.Token).ToArray();
                    var weather=court.Consoles.Select(s=>s.Zone.ActiveWeather).ToArray();
                    yield return BeginTargeting(target);yield return ClickWeatherPointer(target,true,true);yield return ReleaseTargeting();
                    Require(tokens.SequenceEqual(court.Consoles.Select(s=>s.Token))&&weather.SequenceEqual(court.Consoles.Select(s=>s.Zone.ActiveWeather)),
                        "Right click with available "+token+" selected leaves every token owner and local weather unchanged at station "+station+".");
                }
                float beforeWater=court.WaterLevel,beforeHeight=court.Height,beforeYaw=court.Yaw;
                yield return SetWeather(token,0);yield return Wait(.65f);
                Require(console.Token==token&&zone.ActiveWeather==token,"Station "+station+" accepts available "+token+" and produces that local weather.");
                Require(court.Consoles.Count(s=>s.Token==token)==1,"Exactly one station physically owns "+token+" during insertion at station "+station);
                Require(token==SkywellToken.Rain?zone.RainVisible&&zone.RainParticles.particleCount>0:token==SkywellToken.Wind?zone.WindVisible:zone.IsSunny&&zone.CloudAmount<.05f,"Station "+station+" visibly presents "+token+", even when no local machinery uses it.");
                if(module=="SKYWELL_WEATHER_VIEWS")
                {
                    string views=Path.Combine(Folder,"redesign/weather-views");Directory.CreateDirectory(views);
                    Capture(Path.Combine(views,"station-"+station+"-"+token+".png"));
                    yield return Wait(.4f);
                }
                if(station!=0)Require(Mathf.Abs(court.WaterLevel-beforeWater)<.01f&&Mathf.Abs(court.Height-beforeHeight)<.01f&&Mathf.Abs(court.Yaw-beforeYaw)<.01f,"Remote station "+station+" cannot directly move or fill the central tower.");
                if(station==0&&token==SkywellToken.Sun)Require(Mathf.Abs(court.WaterLevel-beforeWater)<.01f&&Mathf.Abs(court.Height-beforeHeight)<.01f,"Sunny at the tower does not add water or change retained lift height.");
                if(station==2&&token==SkywellToken.Wind)yield return FixedBellWindButtonChecks();
                yield return Press(Key.Q);yield return Wait(.15f);
                Require(console.Token==token&&zone.ActiveWeather==token,"Q does not remove "+token+" or change weather at station "+station);
                yield return WeatherFixtureLeave(station);
                Require(console.Token==SkywellToken.None&&court.TokenLocation(token)=="在手中","Leaving station "+station+" returns "+token+" to the player's inventory without a retrieval key.");
                Require(zone.ActiveWeather==(token==SkywellToken.Sun?SkywellToken.Sun:SkywellToken.None)&&!zone.RainVisible&&!zone.WindVisible,"Departure preserves only Sunny; rain and wind stop at station "+station);
                if(token==SkywellToken.Sun)Require(zone.CloudAmount<.05f,"Sunny remains clear after its physical token returns.");
            }
        }

        private static IEnumerator FixedBellWindButtonChecks()
        {
            var target=WeatherTargetForStation(2);var zone=court.Consoles[2].Zone;
            yield return WaitForWeatherMechanism(target);
            Require(court.WindBell.BellPosition<.01f&&zone.WindDirection.x<0,"The right-button matrix cast moves the real wind bell to its west endpoint.");
            Vector3 west=court.WindBell.LurePosition;
            yield return BeginTargeting(target);yield return ClickWeatherPointer(target,true);yield return WaitForWeatherMechanism(target);
            Require(court.WindBell.BellPosition>.99f&&zone.WindDirection.x>0&&court.WindBell.LurePosition.x-west.x>12.9f,"Left click moves the real linear wind mechanism east along its 13m rail.");
            Vector3 east=court.WindBell.LurePosition;
            yield return ClickWeatherPointer(target,true);yield return WaitForWeatherMechanism(target);
            Require(court.WindBell.BellPosition>.99f&&zone.WindDirection.x>0&&Vector3.Distance(east,court.WindBell.LurePosition)<.01f,"Repeated left click keeps the real bell at its east endpoint.");
            yield return ClickWeatherPointer(target,true,true);yield return WaitForWeatherMechanism(target);
            Require(court.WindBell.BellPosition<.01f&&zone.WindDirection.x<0&&Vector3.Distance(west,court.WindBell.LurePosition)<.01f,"Right click explicitly returns the actual wind bell west without a toggle.");
            yield return ClickWeatherPointer(target,true,true);yield return WaitForWeatherMechanism(target);
            Require(court.WindBell.BellPosition<.01f&&zone.WindDirection.x<0,"Repeated right click preserves the west endpoint and westward wind.");
            yield return ReleaseTargeting();
        }

        private static IEnumerator WeatherFixturePlace(int station)
        {
            Vector3 position=station==0?Local(-2.4f,-.8f):court.Consoles[station].transform.position+(station==2?Vector3.left:Vector3.right)*1.4f;
            court.Player.Teleport(position+Vector3.up*.03f);yield return Wait(.25f);
            Require(court.Consoles[station].Zone.Contains(court.Player.FeetPosition)&&WeatherTargetForStation(station),"Explicit placement lies in weather target region "+station+" with its authored target bound.");
        }

        private static IEnumerator WeatherFixtureLeave(int station)
        {
            var zone=court.Consoles[station].Zone;
            Vector3 safeOutside=station==2?court.Journey.ReturnShuttle.TransformPoint(new Vector3(0,.03f,-.7f)):court.Player.SpawnPosition;
            Require(!zone.Contains(safeOutside),"Authored safe departure fixture lies outside weather region "+station);
            court.Player.Teleport(safeOutside);yield return Wait(1.5f);
            Require(court.Consoles[station].Token==SkywellToken.None,"The normal controller auto-return clears the departed station's physical socket "+station);
        }
    }
}

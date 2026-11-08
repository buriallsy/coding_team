using System;
using System.Collections;
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
        [MenuItem("Borrowing Sun/Skywell/Check apple grove weather and gratitude")]
        public static void RunDeerGrove()=>StartCheck("SKYWELL_DEER_GROVE",DeerGroveSequence());
        private static IEnumerator DeerGroveSequence()
        {
            report.Add("EXPLICIT GROVE FIXTURES: placement on authored ground isolates local growth, fruit collision and gratitude. Weather uses real Tab, held E and screen-positioned left clicks. Sunny is obtained by real E on the authored surface boat; no progress/token/repair injection. This check does not establish continuous discovery or the complete cloud-jump route.");
            court.ResetCourt();yield return Wait(.4f);
            var grove=court.Grove;
            Require(grove&&grove.Ride&&grove.Tree&&grove.CloudPlatforms&&grove.FeedingNeck&&grove.FeedingMuzzle,"The optional grove, tree, deer ride and cloud platforms are explicitly linked.");
            Require(court.Consoles.Length==5&&court.WeatherTargets.Length==5&&grove.Zone==court.Consoles[SkywellDeerGrove.StationIndex].Zone,"The grove appends station 4 without replacing the original four weather owners.");
            var landingRoute=DeerAuthoredCloudRoute(grove);
            CheckGroveReset("initial");
            Vector3 baseScale=grove.Tree.localScale;int sparseCount=grove.VisibleFruitCount;
            var feedingHead=grove.FeedingMuzzle.parent;var feedingNeck=grove.FeedingNeck;
            Vector3 headRest=feedingHead.localPosition,neckRest=feedingNeck.localPosition,neckScaleRest=feedingNeck.localScale;
            Quaternion headRotationRest=feedingHead.localRotation,neckRotationRest=feedingNeck.localRotation;
            var cloudVisuals=grove.CloudPlatforms.GetComponentsInChildren<Renderer>(true).Select(r=>r.transform).Distinct().ToArray();
            var cloudCenters=cloudVisuals.Select(t=>t.position).ToArray();
            var cloudScales=cloudVisuals.Select(t=>t.localScale).ToArray();
            Vector3 cloudRootPosition=grove.CloudPlatforms.position,cloudRootScale=grove.CloudPlatforms.localScale;
            yield return GroveFixturePlace();yield return EnsureCameraMode(false);
            Vector3 orbitBefore=grove.Ride.DeerTransform.position;yield return Wait(.8f);
            Vector3 orbitAfter=grove.Ride.DeerTransform.position;
            float treeDistance=Vector3.ProjectOnPlane(orbitAfter-grove.Tree.position,Vector3.up).magnitude;
            Require(Vector3.Distance(orbitBefore,orbitAfter)>.25f&&Mathf.Abs(orbitAfter.y-orbitBefore.y)<.01f&&treeDistance>3&&treeDistance<4.1f,
                "While waiting, the deer walks visibly around the tree on the island floor, clear of its trunk.");
            yield return Press(Key.E);
            Require(grove.Zone.ActiveWeather==SkywellToken.None&&!grove.SmallFruitDropped,"Short E does not remotely cast on the tree or release apples.");
            yield return CastGrove(SkywellToken.Wind);
            Require(!grove.IsGrown&&grove.SmallFruitDropped&&!grove.MatureFruitDropped&&!grove.Unlocked&&!grove.Ride.IsAvailable,"Wind on the sparse tree releases the existing small fruit without granting the cloud ride.");
            yield return Wait(2);
            Require(!grove.IsThanking&&grove.LandedMatureFruitCount==0,"Sparse fruit does not start the plentiful-fruit gratitude sequence.");
            yield return WeatherFixtureLeave(SkywellDeerGrove.StationIndex);
            Require(!grove.Zone.IsWind&&!grove.Zone.WindVisible&&grove.SmallFruitDropped,"Leaving the real grove zone stops wind while the fallen small fruit stays released.");

            yield return GroveFixturePlace();yield return CastGrove(SkywellToken.Rain);
            yield return WaitUntil(()=>grove.Growth>.1f&&grove.Growth<1,3,"Rain begins visible tree growth");
            yield return WeatherFixtureLeave(SkywellDeerGrove.StationIndex);
            float partial=grove.Growth;yield return Wait(.8f);
            Require(partial>0&&partial<1&&Mathf.Abs(grove.Growth-partial)<.001f&&!grove.Zone.IsRain,"Leaving during rainfall pauses growth at its actual partial size and stops rain.");
            yield return GroveFixturePlace();yield return CastGrove(SkywellToken.Rain);
            yield return WaitUntil(()=>grove.IsGrown,6,"One rain growth cycle completes");
            Require(grove.Tree.localScale.magnitude>baseScale.magnitude*1.05f&&grove.VisibleFruitCount>sparseCount&&!grove.Unlocked,"Rain enlarges the tree and adds visible ripe fruit, without unlocking the deer by itself.");
            Vector3 grown=grove.Tree.localScale;int ripeCount=grove.VisibleFruitCount;
            yield return CastGrove(SkywellToken.Rain);yield return Wait(3);
            Require(grove.IsGrown&&Vector3.Distance(grove.Tree.localScale,grown)<.0001f&&grove.VisibleFruitCount==ripeCount,"A second accepted Rain click adds neither extra size nor duplicate fruit.");
            Capture(Path.Combine(Folder,"deer-grove-grown-fixed.png"));
            yield return WeatherFixtureLeave(SkywellDeerGrove.StationIndex);
            Require(grove.IsGrown&&!grove.Zone.IsRain,"Completed tree growth persists after Rain returns to the player.");

            yield return GroveSunnyPickupFixture();
            yield return WeatherFixturePlace(1);yield return SetWeather(SkywellToken.Sun,0);
            yield return GroveFixturePlace();
            Require(court.Consoles[1].Zone.IsSunny&&court.Consoles[1].Token==SkywellToken.None,"The collector's established Sunny weather remains after leaving with the physical token.");
            var beforeTokens=court.Consoles.Select(s=>s.Token).ToArray();var beforeWeather=court.Consoles.Select(s=>s.Zone.ActiveWeather).ToArray();
            yield return CastGrove(SkywellToken.Sun,false);
            Require(beforeTokens.SequenceEqual(court.Consoles.Select(s=>s.Token))&&beforeWeather.SequenceEqual(court.Consoles.Select(s=>s.Zone.ActiveWeather)),"Rejecting Sun at the shaded tree leaves every physical token and existing local weather unchanged.");
            Require(court.Prompt().Contains("没有可用光源")&&!grove.Zone.IsSunny&&!grove.Unlocked,"Sun reports the unavailable light source and cannot advance the grove or create local sunlight.");

            yield return EnsureCameraMode(true);
            Vector3 deerBefore=grove.Ride.DeerTransform.position;
            yield return CastGrove(SkywellToken.Wind);
            Require(grove.MatureFruitDropped&&!grove.Unlocked&&!grove.Ride.IsAvailable,"Wind detaches ripe apples but does not unlock riding at the moment of the weather click.");
            yield return WaitUntil(()=>grove.LandedMatureFruitCount>0,5,"Ripe fruit reaches an authored collider through actual falling motion");
            yield return WaitUntil(()=>grove.IsThanking,2,"The deer reacts after ripe fruit lands");
            Require(!grove.Ride.IsAvailable,"The approach/eating/reveal sequence precedes ride availability.");
            yield return WaitUntil(()=>Vector3.Distance(grove.FeedingMuzzle.position,grove.FeedingPoint)<.12f&&grove.EatenFruitCount==0,12,"The deer lowers its mouth to the actual landed apple before eating");
            Capture(Path.Combine(Folder,"deer-grove-feeding.png"));
            yield return WaitUntil(()=>grove.EatenFruitCount==1,3,"One ripe apple visibly disappears during chewing");
            Require(grove.LandedMatureFruitCount>0&&!grove.Unlocked,"Eating preserves the genuine landing state and still precedes the cloud reward.");
            yield return WaitUntil(()=>grove.CloudPlatforms.gameObject.activeSelf&&!grove.Unlocked,12,"Observe cloud reveal before riding unlocks");
            Require(Vector3.Distance(grove.CloudPlatforms.position,cloudRootPosition)<.0001f&&Vector3.Distance(grove.CloudPlatforms.localScale,cloudRootScale)<.0001f&&
                cloudVisuals.Select((t,i)=>Vector3.Distance(t.position,cloudCenters[i])<.0001f).All(same=>same),
                "Cloud meshes reveal at their own fixed world centres; the route root never scales or translates.");
            Require(grove.CloudPlatforms.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled)&&cloudVisuals.Select((t,i)=>t.localScale.magnitude<cloudScales[i].magnitude).All(smaller=>smaller),
                "Cloud meshes begin small with no landing collision while they are still appearing.");
            yield return WaitUntil(()=>grove.Unlocked,12,"Deer approaches and eats, then cloud platforms finish appearing");
            Require(Vector3.Distance(deerBefore,grove.Ride.DeerTransform.position)>.25f&&grove.CloudPlatforms.gameObject.activeInHierarchy&&grove.Ride.IsAvailable,"The deer physically approaches the fallen fruit, and visible cloud platforms precede riding availability.");
            for(int i=1;i<landingRoute.Length-1;i++)
            {
                var platform=landingRoute[i];var marker=grove.CloudPlatforms.Find("Landing_"+(i-1));
                Require(platform.enabled&&platform.gameObject.activeInHierarchy,"Revealed cloud "+(i-1)+" enables its authored landing collision.");
                Vector3 top=DeerRoutePlatformTop(platform);
                Require(Vector3.Distance(top-Vector3.up*.03f,marker.position)<.12f&&DeerRouteFootprint(platform,top),
                    "Cloud "+(i-1)+" has a real safe landing at its saved marker.");
                report.Add("CLOUD "+(i-1)+" top="+top+" bounds="+platform.bounds);
            }
            var cloudLandings=landingRoute.Skip(1).Take(landingRoute.Length-2).ToArray();
            Require(cloudLandings.Max(c=>c.bounds.max.y)-cloudLandings.Min(c=>c.bounds.max.y)>.2f&&
                cloudLandings.Max(c=>c.bounds.size.x)-cloudLandings.Min(c=>c.bounds.size.x)>.2f,
                "The four physical cloud landings vary in authored height and width.");
            Vector3 readyDeer=grove.Ride.DeerTransform.position;yield return Wait(.8f);
            Require(Vector3.Distance(readyDeer,grove.Ride.DeerTransform.position)<.001f,"The ready deer stops circling and waits for the rider.");
            Require(grove.EatenFruitCount==1,"Only one mature apple is consumed in the gratitude sequence.");
            int landed=grove.LandedMatureFruitCount;int count=grove.VisibleFruitCount;
            yield return CastGrove(SkywellToken.Wind);yield return Wait(2);
            Require(grove.Unlocked&&grove.VisibleFruitCount==count&&grove.LandedMatureFruitCount==landed,"Repeated Wind does not duplicate apples or restart gratitude.");
            Capture(Path.Combine(Folder,"deer-grove-unlocked-shoulder.png"));
            yield return WeatherFixtureLeave(SkywellDeerGrove.StationIndex);
            Require(!grove.Zone.IsWind&&grove.Unlocked&&grove.Ride.IsAvailable,"Leaving stops Wind and returns its token, while the earned ride remains available.");
            yield return DeerRideInputSequence();
            yield return Press(Key.R);yield return Wait(.4f);CheckGroveReset("after unlock and ride");

            // Reset in-flight progression, then wait beyond all former timers: no delayed unlock is allowed.
            yield return GroveFixturePlace();yield return CastGrove(SkywellToken.Rain);
            yield return WaitUntil(()=>grove.Growth>.1f&&grove.Growth<1,3,"Partial growth for R cancellation fixture");
            yield return Press(Key.R);yield return Wait(4);CheckGroveReset("during growth");
            yield return GroveFixturePlace();yield return CastGrove(SkywellToken.Rain);
            yield return WaitUntil(()=>grove.IsGrown,6,"Regrow through actual Rain before the detached-fruit reset fixture");
            yield return PickWeather(SkywellToken.Wind);var fallingTarget=WeatherTargetForStation(SkywellDeerGrove.StationIndex);
            yield return BeginTargeting(fallingTarget);yield return ClickWeatherPointer(fallingTarget,true);yield return ReleaseTargeting();
            yield return WaitUntil(()=>grove.MatureFruitDropped,3,"Observe the first real frame of released ripe fruit");
            Require(grove.LandedMatureFruitCount==0,"R cancellation fixture reaches falling fruit before a ground hit.");
            yield return Press(Key.R);yield return Wait(4);CheckGroveReset("during fruit fall");
            yield return GroveFixturePlace();yield return CastGrove(SkywellToken.Rain);
            yield return WaitUntil(()=>grove.IsGrown,6,"Grow the tree again using actual weather after R");
            yield return CastGrove(SkywellToken.Wind);
            yield return WaitUntil(()=>grove.IsThanking,5,"Reach actual fruit landing before reset");
            yield return Press(Key.R);yield return Wait(9);CheckGroveReset("during gratitude");
            Require(Vector3.Distance(feedingHead.localPosition,headRest)<.0001f&&Quaternion.Angle(feedingHead.localRotation,headRotationRest)<.01f&&
                Vector3.Distance(feedingNeck.localPosition,neckRest)<.0001f&&Quaternion.Angle(feedingNeck.localRotation,neckRotationRest)<.01f&&Vector3.Distance(feedingNeck.localScale,neckScaleRest)<.0001f,
                "R restores both head and neck local position, rotation and scale after interrupted gratitude.");
            yield return GroveArrivalPrecisionFixture();
        }
        private static IEnumerator GroveArrivalPrecisionFixture()
        {
            var grove=court.Grove;var deer=grove.Ride.DeerTransform;
            report.Add("NUMERIC ARRIVAL FIXTURE: actual Rain, Wind and collider landing establish the progression. Only the unmounted deer pose is explicitly placed near the authored 1.2 m stopping radius to reproduce float-position rounding. No phase, fruit-landing, unlock or timer state is injected.");
            yield return GroveFixturePlace();yield return CastGrove(SkywellToken.Rain);
            yield return WaitUntil(()=>grove.IsGrown,6,"Real Rain prepares the numerical arrival regression");
            yield return PickWeather(SkywellToken.Wind);var target=WeatherTargetForStation(SkywellDeerGrove.StationIndex);
            yield return BeginTargeting(target);yield return ClickWeatherPointer(target,true);yield return ReleaseTargeting();
            yield return WaitUntil(()=>grove.MatureFruitDropped,3,"Actual Wind releases fruit for the arrival regression");
            Vector3 far=grove.Tree.position+Vector3.right*3.5f;far.y=deer.position.y;
            Require(grove.Ride.SetUnriddenPose(far,deer.rotation),"Explicit deer-only placement leaves time for a genuine approach after the fruit lands.");
            yield return WaitUntil(()=>grove.IsApproachingFruit&&grove.LandedMatureFruitCount>0,5,"Actual fruit collision starts the production approach state");
            Vector3 food=grove.FeedingPoint,near=food+Vector3.right*1.2f;near.y=deer.position.y;
            Vector3 toward=Vector3.ProjectOnPlane(food-near,Vector3.up);float distance=toward.magnitude;
            Vector3 roundedStep=near+toward.normalized*(distance-1.2f);
            Require(distance>1.2f&&distance<1.201f&&roundedStep.Equals(near),
                "The fixture reproduces a positive sub-millimetre distance residual whose final float movement rounds to zero.");
            report.Add("ARRIVAL FIXTURE deer="+near.ToString("F9")+" food="+food.ToString("F9")+" distance="+distance.ToString("F9")+" residual="+(distance-1.2f).ToString("G9"));SaveProgress();
            Require(grove.Ride.SetUnriddenPose(near,Quaternion.LookRotation(toward)),"Only the deer pose is placed at the representable arrival boundary.");
            yield return WaitUntil(()=>!grove.IsApproachingFruit&&grove.IsThanking,.5f,"Production Update leaves approach at the float arrival boundary");
            yield return WaitUntil(()=>grove.EatenFruitCount==1,4,"Production feeding still consumes exactly one genuinely landed apple");
            yield return WaitUntil(()=>grove.Unlocked,6,"Normal feeding and reveal complete without changing progression thresholds");
            Require(grove.Ride.IsAvailable&&grove.LandedMatureFruitCount>0,"Arrival tolerance does not bypass real fruit landing, gratitude or ride availability.");
            yield return Press(Key.R);yield return Wait(.4f);CheckGroveReset("after numeric arrival regression");
        }
        private static void CheckGroveReset(string context)
        {
            var g=court.Grove;
            Require(g&&g.Growth==0&&!g.SmallFruitDropped&&!g.MatureFruitDropped&&g.LandedMatureFruitCount==0&&g.EatenFruitCount==0&&!g.IsThanking&&!g.Unlocked,"Grove reset clears growth, detached fruit and pending gratitude: "+context);
            Require(!g.CloudPlatforms.gameObject.activeSelf&&!g.Ride.IsAvailable&&!g.Ride.IsMounted&&g.Zone.ActiveWeather==SkywellToken.None&&g.Zone.Station.Token==SkywellToken.None,"Grove reset hides clouds, returns the deer and clears its weather owner: "+context);
        }
        private static IEnumerator GroveFixturePlace()
        {
            var g=court.Grove;court.Player.Teleport(g.Tree.position+new Vector3(-3,.03f,-2));yield return Wait(.3f);
            Require(g.Zone.Contains(court.Player.FeetPosition)&&WeatherTargetForStation(SkywellDeerGrove.StationIndex),"Explicit fixture is on the authored grove island inside its real weather bounds.");
        }
        private static IEnumerator CastGrove(SkywellToken weather,bool accepted=true)
        {
            bool shoulder=court.CameraRig.IsShoulderView;
            yield return PickWeather(weather);var target=WeatherTargetForStation(SkywellDeerGrove.StationIndex);
            yield return BeginTargeting(target);yield return ClickWeatherPointer(target,true);
            if(accepted)Require(court.Grove.Zone.Station.Token==weather,"A real held-E pixel hit casts "+weather+" on the apple tree.");
            yield return ReleaseTargeting();yield return Wait(1.3f);yield return EnsureCameraMode(shoulder);
        }
        private static IEnumerator GroveSunnyPickupFixture()
        {
            var j=court.Journey;
            Require(j.SurfaceConfigured,"The grove Sunny fixture uses the authored surface route; no inspection-lift transition is required.");
            court.Player.Teleport(j.ReturnShuttle.TransformPoint(new Vector3(0,.03f,-.7f)));yield return Wait(.2f);yield return Press(Key.E);
            Require(j.SunnyTokenObtained&&court.CanUseToken(SkywellToken.Sun),"The genuine boat E pickup supplies Sunny; the fixture never grants it directly.");
            court.Player.Teleport(j.PaddlePickup.position+Vector3.left*.8f);yield return Wait(.1f);yield return Press(Key.E);
            court.Player.Teleport(j.ReturnShuttle.TransformPoint(new Vector3(0,.03f,-.7f)));yield return Wait(.2f);yield return Press(Key.E);
            yield return WaitUntil(()=>j.Travel==SkywellTravel.Docked,20,"Actual boat travel completes for the grove pickup fixture");
            court.Player.Teleport(j.LadderBottom.position-j.LadderFacingDirection*1.4f);yield return Wait(.2f);yield return BeginBottomLadderWithW();
            input=new KeyboardState(Key.W);yield return WaitUntil(()=>j.Travel==SkywellTravel.Walking,15,"W climbs the real ladder before grove weather resumes");input=new KeyboardState();
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class SkywellBoatJourneyTests
    {
        private readonly List<GameObject> objects=new List<GameObject>();
        private GameObject Make(string name){var result=new GameObject(name);objects.Add(result);return result;}
        [TearDown] public void Clean(){foreach(var item in objects)if(item)Object.DestroyImmediate(item);objects.Clear();}

        private SkywellJourney Journey(out CourtPlayer player)
        {
            var owner=Make("court").AddComponent<SkywellController>();
            var camera=Make("camera").AddComponent<Camera>();
            var cameraRig=camera.gameObject.AddComponent<CourtCamera>();
            player=Make("player").AddComponent<CourtPlayer>();
            cameraRig.Initialize(null,player,camera);
            player.InitializeGameplay(owner,camera,Vector3.zero);
            using(var serialized=new SerializedObject(owner))
            {
                serialized.FindProperty("player").objectReferenceValue=player;
                serialized.FindProperty("cameraRig").objectReferenceValue=cameraRig;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var lift=Make("lift").transform;lift.SetParent(owner.transform);
            var refuge=Make("refuge").transform;refuge.position=new Vector3(-18,-5.45f,-14);
            var boat=Make("boat").transform;boat.position=new Vector3(11,-5.45f,-34);
            var sunny=Make("sunny-on-boat").transform;sunny.SetParent(boat,false);
            var paddle=Make("paddle-surface").transform;paddle.position=new Vector3(-3,-5.35f,-25);
            var dock=Make("dock").transform;dock.position=new Vector3(0,-5.45f,-9);
            var bottom=Make("ladder-bottom").transform;bottom.position=new Vector3(0,-5.42f,-7);
            var top=Make("ladder-top").transform;top.position=new Vector3(0,.08f,-7);
            var exit=Make("ladder-exit").transform;exit.position=new Vector3(0,.08f,-4.9f);
            var journey=owner.gameObject.AddComponent<SkywellJourney>();
            journey.Initialize(owner,lift,sunny,refuge,null);
            journey.ConfigureReturnShuttle(boat);journey.ConfigureBoatRoute(paddle,dock,bottom,top,exit);
            var floor=Make("surface-dock-floor").AddComponent<BoxCollider>();
            floor.transform.position=refuge.position-Vector3.up*.15f;floor.size=new Vector3(8,.3f,8);
            journey.ConfigureSurfaceRoute(refuge,new[]{refuge},new Collider[]{floor},new Bounds(new Vector3(-8,-4,-20),new Vector3(65,12,60)),-5.5f,-5.6f);
            var water=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(water);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.position=new Vector3(-3,-5.5f,-22);water.transform.localScale=new Vector3(44,.01f,34);
            journey.ConfigureSurfaceWaterBody(water.GetComponent<Renderer>());
            journey.ResetJourney();
            player.Teleport(refuge.position);
            Assert.IsTrue(journey.SurfaceConfigured&&journey.InRefuge);
            Assert.That(journey.Travel,Is.EqualTo(SkywellTravel.Walking));
            return journey;
        }

        [Test] public void SurfacePaddleDoesNotGrantSunnyAndBoatNeedsBothItems()
        {
            var journey=Journey(out var player);
            player.Teleport(journey.PaddlePickup.position);journey.TryPrimary();
            Assert.IsTrue(journey.PaddleObtained);Assert.IsFalse(journey.SunnyTokenObtained);
            Assert.IsFalse(journey.PaddlePickup.gameObject.activeSelf);
            player.Teleport(journey.ReturnShuttle.position);journey.TryPrimary();
            Assert.IsTrue(journey.SunnyTokenObtained);Assert.IsFalse(journey.SunnyToken.gameObject.activeSelf);
            Assert.That(journey.Travel,Is.EqualTo(SkywellTravel.Walking),"Picking up Sunny does not also launch the boat.");
            journey.TryPrimary();
            Assert.That(journey.Travel,Is.EqualTo(SkywellTravel.Returning));
            Assert.IsFalse(journey.HasReturnedToTower,"The water ride must still be followed by climbing.");
        }

        [Test] public void SunnyIsAvailableBeforePaddleButDoesNotLetBoatDepart()
        {
            var journey=Journey(out var player);
            player.Teleport(journey.ReturnShuttle.position);journey.TryPrimary();journey.TryPrimary();
            Assert.IsTrue(journey.SunnyTokenObtained);Assert.IsFalse(journey.PaddleObtained);
            Assert.That(journey.Travel,Is.EqualTo(SkywellTravel.Walking));
            Assert.IsTrue(player.enabled);
        }

        [Test] public void MissingPhysicalDeckCannotStartBoardingOrGrantSunny()
        {
            var journey=Journey(out var player);
            player.Teleport(journey.ReturnShuttle.position+Vector3.right*1.8f);
            Assert.IsFalse(journey.InReturnCabin);journey.TryPrimary();
            Assert.IsFalse(journey.SunnyTokenObtained);
            Assert.That(journey.Travel,Is.EqualTo(SkywellTravel.Walking));
        }

        [Test] public void RefugeRecoveryKeepsBothItemsButReplayRestoresPickupsAndBoat()
        {
            var journey=Journey(out var player);Vector3 initialBoat=journey.ReturnShuttle.position;
            player.Teleport(journey.PaddlePickup.position);journey.TryPrimary();
            player.Teleport(journey.ReturnShuttle.position);journey.TryPrimary();
            journey.RecoverToRefuge("test fish recovery");
            Assert.That(journey.Recoveries,Is.EqualTo(1));
            Assert.That(Vector3.Distance(player.FeetPosition,journey.RefugePoint+Vector3.up*.05f),Is.LessThan(.01f),"Recovery lands just above the dock collision surface.");
            Assert.IsTrue(journey.PaddleObtained&&journey.SunnyTokenObtained);
            Assert.IsFalse(journey.PaddlePickup.gameObject.activeSelf||journey.SunnyToken.gameObject.activeSelf);
            journey.ReturnShuttle.position=journey.BoatDock.position;
            journey.ResetJourney();
            Assert.IsFalse(journey.PaddleObtained||journey.SunnyTokenObtained||journey.BoatDocked||journey.HasReturnedToTower);
            Assert.IsTrue(journey.PaddlePickup.gameObject.activeSelf&&journey.SunnyToken.gameObject.activeSelf);
            Assert.That(journey.ReturnShuttle.position,Is.EqualTo(initialBoat));
            Assert.That(journey.Travel,Is.EqualTo(SkywellTravel.Walking));
            Assert.IsTrue(player.enabled);
        }

        [Test] public void RealBoatLipSupportsCapsuleBeforeCentreButClosedHullDoesNot()
        {
            var journey=Journey(out var player);
            var floor=Make("boat-floor").AddComponent<BoxCollider>();floor.transform.SetParent(journey.ReturnShuttle,false);
            floor.center=new Vector3(0,-.15f,0);floor.size=new Vector3(3,.3f,3);
            journey.ConfigureSurfaceRoute(journey.SurfaceDock,journey.SurfaceRouteWaypoints,new Collider[]{floor},journey.SurfaceRegion,
                journey.SurfaceWaterLevel,journey.SurfaceFeetHeight);
            Physics.SyncTransforms();
            Vector3 lip=journey.ReturnShuttle.position+new Vector3(0,0,-1.65f);lip.y=journey.SurfaceFeetHeight;
            Assert.IsTrue(Supported(journey,lip),"A genuine floor beneath the front of the capsule stops buoyancy pulling down a boarding step.");
            Vector3 beyond=lip+Vector3.back*.3f;
            Assert.IsFalse(Supported(journey,beyond),"Support must not extend beyond the real foot footprint.");
            var hull=Make("closed-hull").AddComponent<BoxCollider>();hull.transform.SetParent(journey.ReturnShuttle,false);
            hull.center=new Vector3(1.5f,1.5f,0);hull.size=new Vector3(.1f,3,3);Physics.SyncTransforms();
            Vector3 side=journey.ReturnShuttle.position+new Vector3(1.65f,0,0);side.y=journey.SurfaceFeetHeight;
            Assert.IsFalse(Supported(journey,side),"A floor sample across a closed hull cannot grant walking outside the boat.");
        }

        [Test] public void FootprintBeyondDryApronDoesNotExemptTheOrdinaryFallPlane()
        {
            var journey=Journey(out var player);
            var apron=Make("dry-apron-beyond-visible-water").AddComponent<BoxCollider>();
            apron.transform.position=new Vector3(0,journey.SurfaceFeetHeight, -3);apron.size=new Vector3(2,.3f,2);
            journey.ConfigureSurfaceRoute(journey.SurfaceDock,journey.SurfaceRouteWaypoints,new Collider[]{apron},journey.SurfaceRegion,
                journey.SurfaceWaterLevel,journey.SurfaceFeetHeight);
            Physics.SyncTransforms();
            Vector3 edge=new Vector3(0,apron.bounds.max.y+.03f,apron.bounds.max.z-.05f);
            Assert.IsFalse(journey.IsOverSurfaceWater(edge));
            Assert.IsTrue(journey.IsWaterApproach(edge),"Actual centre support on the dry apron remains a safe approach floor.");
            Vector3 beyond=edge+Vector3.forward*.2f;
            Assert.IsTrue(Supported(journey,beyond),"The capsule footprint may still touch the physical ledge after its centre crosses it.");
            Assert.IsFalse(journey.IsWaterApproach(beyond),"A touching footprint outside visible water cannot prevent hazard recovery or overwrite a supported checkpoint.");
        }

        [Test] public void LadderAlignmentMovesGraduallyAndPhysicalBlockReleasesControlInPlace()
        {
            var journey=Journey(out var player);
            player.Teleport(journey.PaddlePickup.position);journey.TryPrimary();
            player.Teleport(journey.ReturnShuttle.position);journey.TryPrimary();journey.TryPrimary();
            for(int i=0;i<300&&journey.Travel==SkywellTravel.Returning;i++)Tick(journey,"TickBoat",.05f);
            Assert.IsTrue(journey.BoatDocked);
            Vector3 start=journey.LadderBottom.position+Vector3.back;
            player.Teleport(start);Assert.IsTrue(AutoClimb(journey,1,Vector3.forward));
            Assert.That(journey.Travel,Is.EqualTo(SkywellTravel.Climbing));
            Assert.That(player.FeetPosition,Is.EqualTo(start),"Walking into the front acquires ladder control without relocating the character.");
            Tick(journey,"TickClimb",0f,.05f);
            Assert.That(Vector3.Distance(start,player.FeetPosition),Is.GreaterThan(.001f).And.LessThan(journey.climbAlignSpeed*.05f+.001f));
            var obstacle=Make("blocked-ladder-approach").AddComponent<BoxCollider>();
            obstacle.transform.position=start+new Vector3(0,1,.5f);obstacle.size=new Vector3(4,3,.2f);Physics.SyncTransforms();
            for(int i=0;i<100&&journey.Travel==SkywellTravel.Climbing;i++)Tick(journey,"TickClimb",0f,.05f);
            Assert.That(journey.Travel,Is.EqualTo(SkywellTravel.Docked));
            Assert.IsTrue(player.enabled,"A blocked grab must return player control instead of freezing forever.");
            Assert.IsFalse(journey.HasReturnedToTower);
            Assert.That(player.FeetPosition.z,Is.LessThan(obstacle.bounds.min.z),"The character remains on the original side of real collision.");
            Assert.That(Vector3.Distance(start,player.FeetPosition),Is.LessThan(.3f),"Cancellation never teleports to a distant safe point.");
        }


        private void BoatHull(SkywellJourney journey)
        {
            var floor=Make("Shuttle_floor").AddComponent<BoxCollider>();floor.transform.SetParent(journey.ReturnShuttle,false);
            floor.center=new Vector3(0,-.15f,0);floor.size=new Vector3(3,.3f,3);
            for(int i=0;i<3;i++)
            {
                var hull=Make("low-visible-gunwale-"+i).AddComponent<BoxCollider>();hull.transform.SetParent(journey.ReturnShuttle,false);
                hull.center=i==2?new Vector3(0,.325f,1.5f):new Vector3(i==0?-1.5f:1.5f,.325f,0);
                hull.size=i==2?new Vector3(3,.65f,.12f):new Vector3(.12f,.65f,3);
            }
            Physics.SyncTransforms();
        }
        [TestCase(1,0)] [TestCase(-1,0)] [TestCase(0,1)] [TestCase(0,-1)]
        public void NearbyBoatEBoardsEverySideWithPhysicalMotionAndNoProgressGrant(float x,float z)
        {
            var journey=Journey(out var player);BoatHull(journey);
            Vector3 start=journey.ReturnShuttle.position+new Vector3(x*2.3f,-.15f,z*2.3f);
            player.Teleport(start);Assert.IsFalse(journey.InReturnCabin);journey.TryPrimary();
            Assert.IsTrue(journey.IsBoarding,"Each reachable side accepts E without an opening/facing requirement.");
            Assert.That(player.FeetPosition,Is.EqualTo(start),"Acquiring boarding never relocates the actor.");
            int frames=0;
            while(journey.IsBoarding&&frames++<250)
            {
                Vector3 before=player.FeetPosition;Tick(journey,"TickBoarding",.04f);
                Assert.That(Vector3.Distance(before,player.FeetPosition),Is.LessThan(journey.boardingMoveSpeed*.04f+.01f),"Every boarding frame uses bounded real collision movement.");
            }
            Assert.That(frames,Is.GreaterThan(15).And.LessThan(250));
            Assert.IsFalse(journey.IsBoarding);Assert.IsTrue(journey.InReturnCabin&&player.enabled);
            Assert.IsFalse(journey.SunnyTokenObtained||journey.PaddleObtained||journey.BoatDocked,"Boarding cannot pick up, depart or finish the boat route.");
            journey.TryPrimary();Assert.IsTrue(journey.SunnyTokenObtained,"The next independent interaction takes Sunny before any paddle requirement.");
            Assert.That(journey.Travel,Is.EqualTo(SkywellTravel.Walking));
        }
        [Test] public void BoatBoardingRejectsFarPositionAndPhysicalWallAcrossMountPath()
        {
            var journey=Journey(out var player);BoatHull(journey);
            Vector3 start=journey.ReturnShuttle.position+new Vector3(3.1f,-.15f,0);
            player.Teleport(start);journey.TryPrimary();Assert.IsFalse(journey.IsBoarding);
            start=journey.ReturnShuttle.position+new Vector3(2.3f,-.15f,0);player.Teleport(start);
            var obstruction=Make("real-wall-between-player-and-boat").AddComponent<BoxCollider>();
            obstruction.transform.position=journey.ReturnShuttle.position+new Vector3(1.7f,2,0);obstruction.size=new Vector3(.12f,4,4);Physics.SyncTransforms();
            journey.TryPrimary();Assert.IsFalse(journey.IsBoarding);Assert.IsTrue(player.enabled);Assert.That(player.FeetPosition,Is.EqualTo(start));
            Object.DestroyImmediate(obstruction.gameObject);Physics.SyncTransforms();
            journey.TryPrimary();Assert.IsTrue(journey.IsBoarding,"Removing the actual obstruction makes the same side usable.");
            journey.enabled=false;Tick(journey,"OnDisable");Assert.IsFalse(journey.IsBoarding);Assert.IsTrue(player.enabled,"The disable cleanup releases movement without teleporting.");
            Assert.That(player.FeetPosition,Is.EqualTo(start));
        }
        [Test] public void BoardingResetReleasesMovementAndRestoresOriginalPickups()
        {
            var journey=Journey(out var player);BoatHull(journey);
            player.Teleport(journey.ReturnShuttle.position+new Vector3(2.3f,-.15f,0));journey.TryPrimary();
            Tick(journey,"TickBoarding",.1f);Assert.IsTrue(journey.IsBoarding);
            journey.ResetJourney();Assert.IsFalse(journey.IsBoarding);Assert.IsTrue(player.enabled);
            Assert.IsTrue(journey.SunnyToken.gameObject.activeSelf&&journey.PaddlePickup.gameObject.activeSelf);
            Assert.IsFalse(journey.SunnyTokenObtained||journey.PaddleObtained||journey.BoatDocked);
        }
        [Test] public void LadderOnlyGrabsFromPhysicalFrontWithTowardLadderWAndNeverE()
        {
            var journey=Journey(out var player);Dock(journey,player);
            Vector3 front=journey.LadderBottom.position-journey.LadderFacingDirection*.7f;
            player.Teleport(front);journey.TryPrimary();Assert.IsFalse(journey.IsClimbing,"E is no longer a ladder trigger.");
            Assert.IsFalse(AutoClimb(journey,-1,journey.LadderFacingDirection));
            Assert.IsFalse(AutoClimb(journey,1,-journey.LadderFacingDirection));
            player.Teleport(journey.LadderBottom.position+journey.LadderFacingDirection*.6f);
            Assert.IsFalse(AutoClimb(journey,1,journey.LadderFacingDirection),"Back-side proximity never attracts the actor through the ladder.");
            player.Teleport(front+Vector3.right*1.2f);Assert.IsFalse(AutoClimb(journey,1,journey.LadderFacingDirection));
            player.Teleport(front);Assert.IsTrue(AutoClimb(journey,1,journey.LadderFacingDirection));
            for(int i=0;i<100&&journey.ClimbPhase==SkywellClimbPhase.Approach;i++)Tick(journey,"TickClimb",1f,.04f);
            Tick(journey,"TickClimb",1f,.04f);
            Assert.IsTrue(journey.IsClimbing);Assert.That(journey.ClimbProgress01,Is.GreaterThan(0));Assert.That(journey.ClimbVelocity,Is.GreaterThan(0));
            Tick(journey,"TickClimb",0f,.04f);Assert.That(journey.ClimbVelocity,Is.EqualTo(0),"Stopping on the ladder pauses the locomotion animation.");
        }
        [Test] public void LadderTopAcceptsSForDescentAndDoesNotRearmAfterLandingUntilRelease()
        {
            var journey=Journey(out var player);Dock(journey,player);
            player.Teleport(journey.LadderExit.position);
            Assert.IsFalse(AutoClimb(journey,1,journey.LadderFacingDirection));
            Assert.IsTrue(AutoClimb(journey,-1,journey.LadderFacingDirection));
            for(int i=0;i<100&&journey.ClimbPhase==SkywellClimbPhase.Approach;i++)Tick(journey,"TickClimb",-1f,.04f);
            Tick(journey,"TickClimb",-1f,.04f);Assert.That(journey.ClimbVelocity,Is.LessThan(0));
            for(int i=0;i<200&&journey.IsClimbing;i++)Tick(journey,"TickClimb",-1f,.04f);
            Assert.IsFalse(journey.IsClimbing);Assert.IsTrue(player.enabled);
            Assert.IsFalse(AutoClimb(journey,1,journey.LadderFacingDirection),"Finishing a transition requires releasing movement before another grab.");
        }
        [Test] public void AutomaticLadderGrabRespectsWeatherAndObservationModalFrames()
        {
            var journey=Journey(out var player);Dock(journey,player);
            player.Teleport(journey.LadderBottom.position-journey.LadderFacingDirection*.7f);
            var owner=journey.GetComponent<SkywellController>();
            var weather=typeof(SkywellController).GetField("weatherClosedFrame",BindingFlags.Instance|BindingFlags.NonPublic);
            weather.SetValue(owner,Time.frameCount);Assert.IsFalse(AutoClimb(journey,1,journey.LadderFacingDirection));
            weather.SetValue(owner,-1);
            var observation=typeof(SkywellController).GetField("observationClosedFrame",BindingFlags.Instance|BindingFlags.NonPublic);
            observation.SetValue(owner,Time.frameCount);Assert.IsFalse(AutoClimb(journey,1,journey.LadderFacingDirection));
            observation.SetValue(owner,-1);Assert.IsTrue(AutoClimb(journey,1,journey.LadderFacingDirection));
        }
        [Test] public void ObstacleEnteringBoardingPathCancelsInPlaceAndReturnsPlayerControl()
        {
            var journey=Journey(out var player);BoatHull(journey);
            Vector3 start=journey.ReturnShuttle.position+new Vector3(2.3f,-.15f,0);
            player.Teleport(start);journey.TryPrimary();Assert.IsTrue(journey.IsBoarding);
            Tick(journey,"TickBoarding",.05f);
            var ceiling=Make("moving-obstacle-over-boarding").AddComponent<BoxCollider>();
            ceiling.transform.position=start+new Vector3(0,2.2f,0);ceiling.size=new Vector3(2,.2f,2);Physics.SyncTransforms();
            for(int i=0;i<100&&journey.IsBoarding;i++)Tick(journey,"TickBoarding",.04f);
            Assert.IsFalse(journey.IsBoarding);Assert.IsTrue(player.enabled);Assert.IsFalse(journey.SunnyTokenObtained||journey.BoatDocked);
            Assert.That(Vector3.Distance(start,player.FeetPosition),Is.LessThan(.55f),"A new obstacle stops the physical motion rather than relocating to the boat or refuge.");
        }
        private static void Dock(SkywellJourney journey,CourtPlayer player)
        {
            player.Teleport(journey.PaddlePickup.position);journey.TryPrimary();
            player.Teleport(journey.ReturnShuttle.position);journey.TryPrimary();journey.TryPrimary();
            for(int i=0;i<300&&journey.Travel==SkywellTravel.Returning;i++)Tick(journey,"TickBoat",.05f);
            Assert.IsTrue(journey.BoatDocked);
        }
        private static bool AutoClimb(SkywellJourney journey,float axis,Vector3 cameraForward)=>(bool)typeof(SkywellJourney)
            .GetMethod("TryAutoClimb",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(journey,new object[]{axis,cameraForward});

        private static bool Supported(SkywellJourney journey,Vector3 feet)=>(bool)typeof(SkywellJourney)
            .GetMethod("HasSurfaceSupport",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(journey,new object[]{feet});
        // EditMode drives production ticks explicitly; Play coverage separately uses real keys and frame updates.
        private static void Tick(SkywellJourney journey,string method,params object[] arguments)=>typeof(SkywellJourney)
            .GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(journey,arguments);
    }
}

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    public static partial class SkywellChecks
    {
        public static void RunReturnCabinChecks()=>StartCheck("SKYWELL_BOAT_AND_LADDER",ReturnCabinSequence());
        private static IEnumerator ReturnCabinSequence()
        {
            court.ResetCourt();yield return Wait(.3f);var j=court.Journey;
            report.Add("SURFACE/BOAT/LADDER FIXTURE: explicit unmounted placements on the authored dock, water, four boat sides, floating paddle, live-fish contact and ladder test apron. Real E boards from all four sides while facing away, then separately collects Sunny/rows. Actual W automatically grabs the front ladder; S descends. One temporary solid obstruction tests rejection, and is removed. No owned flag, travel-state, buoyancy, animation or success injection. Not a continuous entrance route.");
            Require(j.SurfaceConfigured&&j.SurfaceDock&&j.SurfaceWalkable.Length>0,"The saved scene supplies the open dock and real dry-floor references.");
            yield return EnsureCameraMode(false);
            court.Player.Teleport(j.SurfaceDock.position+Vector3.up*.03f);yield return Wait(.3f);
            Require(j.InRefuge&&j.Travel==SkywellTravel.Walking&&!court.Player.IsSwimming&&!j.SunnyTokenObtained&&!j.PaddleObtained,"The open surface dock supports ordinary walking without granting either pickup.");
            yield return SurfaceRampReturnFixture();
            yield return SurfaceNorthBoundaryFixture();
            yield return SurfaceSwimInputFixture();
            var cabin=j.ReturnShuttle;Vector3 start=cabin.position;
            var approaches=new[]{new Vector3(-2.25f,0,0),new Vector3(2.25f,0,0),new Vector3(0,0,-2.25f),new Vector3(0,0,2.25f)};
            for(int side=0;side<approaches.Length;side++)
            {
                Vector3 approach=cabin.TransformPoint(approaches[side]);approach.y=j.SurfaceFeetHeight;
                court.Player.Teleport(approach);court.GetComponent<SkywellFish>().ResetFish();yield return Wait(.2f);
                navigationError=Vector3.zero;
                yield return MoveInDirection(cabin.TransformDirection(approaches[side]).normalized,.08f);
                Require(Vector3.Dot(court.Player.FacingDirection,Vector3.ProjectOnPlane(cabin.position-court.Player.FeetPosition,Vector3.up).normalized)<-.6f,
                    "Boat-side fixture "+side+" faces away through real movement; boarding must not require a facing cone.");
                Require(!j.InReturnCabin&&court.Player.IsSurfaceSwimming&&(j.CurrentPrompt??"").Contains("上船"),"Near boat side "+side+" exposes E boarding while still swimming outside the hull.");
                yield return BoardBoatFromNearby("E boards from side "+side);
                Require(!j.SunnyTokenObtained&&!j.PaddleObtained&&Vector3.Distance(start,cabin.position)<.01f,"Boarding side "+side+" neither collects either item nor starts rowing.");
            }
            Require(j.CurrentPrompt.Contains("晴牌"),"Sunny is offered as a separate aboard-boat action before obtaining the paddle.");
            yield return Press(Key.E);
            Require(j.SunnyTokenObtained&&court.CanUseToken(SkywellToken.Sun)&&!j.SunnyToken.gameObject.activeSelf&&!j.PaddleObtained,"Actual E takes Sunny independently of boarding and of the floating paddle.");
            yield return Press(Key.E);
            Require(j.Travel==SkywellTravel.Walking&&Vector3.Distance(start,cabin.position)<.01f&&!j.PaddleObtained,"Sunny alone does not move the boat; the paddle is still required.");
            Capture(Path.Combine(Folder,"boat-sunny-before-paddle.png"));yield return Wait(1);
            Require(j.Recoveries==0&&!court.GetComponent<SkywellFish>().PlayerVisible,"The physically supported boat remains outside swimmer detection.");
            Vector3 boatFront=cabin.TransformPoint(new Vector3(0,0,-2.5f));boatFront.y=j.SurfaceFeetHeight;
            yield return MonitorBoatThreshold(Walk(boatFront,"Leave the boat across its low front lip"),"Leave boat");
            Require(!j.InRefuge&&court.Player.IsSwimming&&Mathf.Abs(court.Player.FeetPosition.y-j.SurfaceFeetHeight)<.04f,"Walking off the deck resumes stable swimming at the actual waterline.");
            yield return BoatBlockedBoardingFixture();
            report.Add("EXPLICIT BOAT BOUNDS PROBES: beside the hull, under its floor and above its interaction volume cannot grant dry-boat interior state. These are immediate read-only volume probes, not travel routes.");
            foreach(var offset in new[]{new Vector3(1.9f,.03f,0),new Vector3(0,-.5f,0),new Vector3(0,3.2f,0)})
            {court.Player.Teleport(cabin.TransformPoint(offset));Require(!j.InReturnCabin,"Outside boat bounds is not dry interior: "+offset);}
            Vector3 paddleApproach=j.PaddlePickup.position+Vector3.left*.8f;paddleApproach.y=j.SurfaceFeetHeight;
            court.Player.Teleport(paddleApproach);yield return Wait(.2f);yield return Press(Key.E);yield return Wait(.2f);
            Require(j.PaddleObtained&&!j.PaddlePickup.gameObject.activeSelf&&j.SunnyTokenObtained,"Actual nearby E picks up the separate floating paddle without changing Sunny ownership.");

            report.Add("EXPLICIT FISH CONTACT FIXTURE: Sunny and paddle were acquired by real E; player and live fish are placed in clear water to verify warning, capture and recovery retention.");
            var fish=court.GetComponent<SkywellFish>();int recoveries=j.Recoveries;
            Vector3 contact=court.Origin+new Vector3(-11,0,-20);contact.y=j.SurfaceFeetHeight;court.Player.Teleport(contact);
            fish.ResetFish();fish.Body.SetPositionAndRotation(new Vector3(contact.x,fish.PatrolHeight,contact.z-.6f),Quaternion.identity);Physics.SyncTransforms();
            yield return WaitUntil(()=>j.Recoveries>recoveries,fish.warningDuration+4,"Live fish catches exposed swimmer through normal warning and contact");
            Require(j.InRefuge&&!court.Player.IsSwimming&&j.SunnyTokenObtained&&j.PaddleObtained&&!j.SunnyToken.gameObject.activeSelf&&!j.PaddlePickup.gameObject.activeSelf,"Fish contact returns to the real open dock and preserves both pickups without collecting either again.");
            Require(Vector3.Distance(court.Player.FeetPosition,j.SurfaceDock.position)<.15f,"Fish recovery uses the authored surface dock, never a submerged chamber.");

            court.Player.Teleport(boatFront);yield return Wait(.2f);yield return BoardBoatFromNearby("Board after fish recovery");
            Require(j.SunnyTokenObtained&&j.PaddleObtained&&j.CurrentPrompt.Contains("划船"),"Both retained pickups enable separate rowing after recovery and boarding.");
            yield return Press(Key.E);Require(j.Travel==SkywellTravel.Returning&&!court.Player.IsSwimming,"A separate E begins surface rowing.");
            float boatY=cabin.position.y,maxVerticalDrift=0,deadline=Time.time+20;
            while(j.Travel==SkywellTravel.Returning&&Time.time<deadline)
            {maxVerticalDrift=Mathf.Max(maxVerticalDrift,Mathf.Abs(cabin.position.y-boatY));yield return null;}
            Require(j.Travel==SkywellTravel.Docked&&j.BoatDocked&&maxVerticalDrift<.01f&&Vector3.Distance(cabin.position,j.BoatDock.position)<.05f,"Boat follows the water surface and stops at the authored tower-side dock.");
            Require(!j.HasReturnedToTower&&court.Player.enabled,"Docking does not skip the ladder or finish tower return.");
            yield return LadderBackplateAndSideFixture();
            yield return Walk(j.LadderBottom.position-j.LadderFacingDirection*1.4f,"Walk from the boat to the ladder's front approach");
            yield return BeginBottomLadderWithW();
            float climbStart=court.Player.FeetPosition.y;
            input=new KeyboardState(Key.W);yield return ClimbAnimationMotionSample(.8f,1);input=new KeyboardState();
            Require(j.Travel==SkywellTravel.Climbing&&court.Player.FeetPosition.y>climbStart+.4f&&!j.HasReturnedToTower,"Holding W raises the real CharacterController without teleporting to the platform.");
            yield return ClimbAnimationPauseSample();
            float beforeDown=court.Player.FeetPosition.y;input=new KeyboardState(Key.S);yield return ClimbAnimationMotionSample(.4f,-1);input=new KeyboardState();
            Require(court.Player.FeetPosition.y<beforeDown-.2f,"S reverses actual ladder movement and its retargeted animation.");
            input=new KeyboardState(Key.W);yield return WaitUntil(()=>j.Travel==SkywellTravel.Walking,15,"W reaches the upper landing and steps off");input=new KeyboardState();
            Require(j.HasReturnedToTower&&j.SunnyTokenObtained&&j.PaddleObtained&&court.Player.enabled&&Vector3.Distance(court.Player.FeetPosition,j.LadderExit.position)<.5f,"Only stepping onto the tower completes return and preserves both pickups.");
            yield return Wait(.4f);Require(ClimbVisual().ClimbPoseWeight<.02f,"Reaching the upper landing blends out climbing and restores ordinary humanoid animation.");
            yield return EnsureCameraMode(true);yield return FaceNavigationDirection(j.LadderFacingDirection);
            input=new KeyboardState(Key.S);yield return WaitUntil(()=>j.Travel==SkywellTravel.Climbing,3,"S walks backward from the top and automatically takes the ladder without E");
            yield return WaitUntil(()=>j.Travel==SkywellTravel.Docked,15,"Continued S returns to the boat-side ladder landing");input=new KeyboardState();
            Require(court.Player.enabled,"Descending the ladder restores normal movement at the dock.");
            yield return EnsureCameraMode(false);
            yield return Walk(j.LadderBottom.position-j.LadderFacingDirection*1.4f,"Step away before checking interrupted automatic ladder approach");
            yield return BeginBottomLadderWithW(false);
            Vector3 interrupted=court.Player.FeetPosition;j.enabled=false;input=new KeyboardState();yield return Wait(.3f);
            Require(court.Player.enabled&&!j.ControlsMovement&&!court.Player.IsSwimming&&court.Player.PlanarVelocity.sqrMagnitude<.001f&&ClimbVisual().ClimbPoseWeight<.02f,
                "Disabling Journey during automatic ladder approach releases movement, animation and stored velocity.");
            Require(Vector3.ProjectOnPlane(court.Player.FeetPosition-interrupted,Vector3.up).magnitude<.04f,
                "Disabled ladder approach leaves the character at the actual interruption point without a positional snap.");
            j.enabled=true;yield return Wait(.15f);
            yield return Walk(j.LadderBottom.position-j.LadderFacingDirection*1.4f,"Step away after re-enabling Journey");
            yield return BeginBottomLadderWithW(false);
            yield return Press(Key.R);yield return Wait(.3f);CheckReset();
            Require(court.Player.enabled&&!j.ControlsMovement&&!court.Player.IsSwimming&&court.Player.PlanarVelocity.sqrMagnitude<.001f&&ClimbVisual().ClimbPoseWeight<.02f,
                "Actual R during automatic ladder approach clears assisted travel, climbing animation and velocity.");
            Require(Vector3.Distance(start,cabin.position)<.01f&&!j.PaddleObtained&&j.PaddlePickup.gameObject.activeSelf&&!j.SunnyTokenObtained&&j.SunnyToken.gameObject.activeSelf,"Full replay restores the boat and both world pickups.");
        }

        private static IEnumerator BoardBoatFromNearby(string label)
        {
            var j=court.Journey;Vector3 previous=court.Player.FeetPosition;float previousTime=Time.time,began=Time.time,maxExcess=0;bool sawBoarding=false;
            string excessFrame="";var previousTravel=j.Travel;
            Require(!j.InReturnCabin&&!j.ControlsMovement,label+": start outside the actual boat interior without assisted travel.");
            input=new KeyboardState(Key.E);
            while(Time.time<began+8)
            {
                // This driver (-300) samples before Journey (-90). The next observation
                // contains motion executed later in this frame, using this frame's delta.
                float movementDelta=Time.deltaTime;
                yield return null;
                Vector3 now=court.Player.FeetPosition;
                float excess=Vector3.Distance(now,previous)-(j.boardingMoveSpeed+.1f)*movementDelta;
                if(excess>maxExcess){maxExcess=excess;excessFrame=$" from={previous:F4} to={now:F4} movementDt={movementDelta:F5} observationDt={Time.time-previousTime:F5} travel={previousTravel}->{j.Travel} swim={court.Player.IsSwimming}";}
                previous=now;previousTime=Time.time;previousTravel=j.Travel;
                sawBoarding|=j.IsBoarding;
                if(Time.time>began+.12f)input=new KeyboardState();
                if(sawBoarding&&!j.IsBoarding)break;
            }
            input=new KeyboardState();yield return Wait(.2f);
            Require(sawBoarding&&!j.IsBoarding&&j.InReturnCabin&&!court.Player.IsSwimming&&court.Player.enabled,label+": actual E completes the physical boarding arc and returns deck walking.");
            Require(maxExcess<.035f,label+": measured frame displacement stays within configured boarding speed plus collision tolerance; no destination snap (excess="+maxExcess.ToString("F4")+excessFrame+").");
        }

        private static IEnumerator BoatBlockedBoardingFixture()
        {
            var j=court.Journey;var cabin=j.ReturnShuttle;
            Vector3 approach=cabin.TransformPoint(new Vector3(0,0,-2.5f));approach.y=j.SurfaceFeetHeight;
            court.Player.Teleport(approach);yield return Wait(.2f);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="TEST_ONLY_boarding_obstruction";wall.hideFlags=HideFlags.HideAndDontSave;
            wall.transform.SetPositionAndRotation(cabin.TransformPoint(new Vector3(0,1.7f,-1.8f)),cabin.rotation);wall.transform.localScale=new Vector3(4,5,.25f);Physics.SyncTransforms();
            report.Add("BLOCKED BOARDING FIXTURE: a temporary real 4 x 5 x 0.25 m wall is placed between the near bow approach and the deck. It must reject E and is removed in finally; no runtime collision filtering is changed.");
            try
            {
                Vector3 before=court.Player.FeetPosition;yield return Press(Key.E);yield return Wait(.3f);
                Require(!j.IsBoarding&&!j.InReturnCabin&&!j.ControlsMovement&&Vector3.Distance(before,court.Player.FeetPosition)<.1f,"Nearby E cannot board through the temporary solid wall or relocate the swimmer.");
            }
            finally{UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();}
            yield return BoardBoatFromNearby("The same near approach boards after the real obstruction is removed");
            Vector3 far=cabin.TransformPoint(new Vector3(0,0,-5));far.y=j.SurfaceFeetHeight;court.Player.Teleport(far);yield return Wait(.2f);
            yield return Press(Key.E);Require(!j.IsBoarding&&!j.InReturnCabin&&!j.ControlsMovement,"E outside boat reach cannot start remote boarding.");
        }

        private static IEnumerator BeginBottomLadderWithW(bool finishApproach=true)
        {
            var j=court.Journey;yield return FaceNavigationDirection(j.LadderFacingDirection);
            float startY=court.Player.FeetPosition.y;bool returned=j.HasReturnedToTower;input=new KeyboardState(Key.W);
            yield return WaitUntil(()=>j.Travel==SkywellTravel.Climbing,3,"Actual forward W automatically takes the front ladder without E");
            input=new KeyboardState();
            Require(Mathf.Abs(court.Player.FeetPosition.y-startY)<.16f&&j.HasReturnedToTower==returned,"Automatic grab starts at the actual ladder foot without skipping climb height.");
            if(finishApproach)yield return WaitUntil(()=>Vector3.Distance(court.Player.FeetPosition,j.LadderBottom.position)<.065f,3,"Automatic approach reaches the real ladder foot through CharacterController collision");
        }

        private static IEnumerator LadderBackplateAndSideFixture()
        {
            var j=court.Journey;Vector3 facing=j.LadderFacingDirection,side=Vector3.Cross(Vector3.up,facing);
            bool shoulder=court.CameraRig.IsShoulderView;yield return EnsureCameraMode(true);yield return FaceNavigationDirection(facing);
            court.Player.Teleport(j.LadderBottom.position-facing*.65f-side*.45f);yield return Wait(.2f);
            input=new KeyboardState(Key.D);yield return Wait(.18f);input=new KeyboardState();yield return Wait(.15f);
            Require(j.Travel!=SkywellTravel.Climbing&&court.Player.enabled,"Sideways D across the front apron does not automatically attach to the ladder.");
            court.Player.Teleport(j.LadderBottom.position-facing*.5f);yield return Wait(.2f);
            yield return FaceNavigationDirection(Vector3.Cross(facing,Vector3.up));
            Vector3 before=court.Player.FeetPosition;float furthest=Vector3.Dot(before-j.LadderBottom.position,facing);
            input=new KeyboardState(Key.D);float until=Time.time+.8f;
            while(Time.time<until){yield return null;furthest=Mathf.Max(furthest,Vector3.Dot(court.Player.FeetPosition-j.LadderBottom.position,facing));}
            input=new KeyboardState();yield return Wait(.2f);
            Require(j.Travel!=SkywellTravel.Climbing&&Vector3.Dot(court.Player.FeetPosition-before,facing)>.08f,"Actual lateral-key movement approaches the ladder backplate while the W grab condition stays inactive.");
            Require(furthest<.5f,"The real authored backplate stops the player capsule before it can walk behind the ladder; furthest depth="+furthest.ToString("F3")+" m.");
            var backing=court.GetComponentsInChildren<Collider>().FirstOrDefault(c=>c.name=="Ladder_solid_backing"&&c.enabled&&!c.isTrigger);
            Require(backing&&backing.Raycast(new Ray(court.Player.FeetPosition+Vector3.up*.8f,facing),out var hit,.55f)&&hit.distance>.05f,
                "The stopped capsule is directly against the authored solid backing face, not held by an invisible gameplay threshold.");
            yield return EnsureCameraMode(shoulder);
        }

        private static StandardHumanoidVisual ClimbVisual()
        {
            var visual=court.Player.GetComponentInChildren<StandardHumanoidVisual>(true);
            Require(visual&&visual.Animator&&visual.Animator.isHuman&&visual.ClimbAnimation,"The actual player has a valid Humanoid Avatar and the imported retargeted climbing clip.");
            return visual;
        }
        private static IEnumerator ClimbAnimationMotionSample(float seconds,int direction)
        {
            var visual=ClimbVisual();var animator=visual.Animator;
            var bones=new[]{HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg};
            var transforms=bones.Select(animator.GetBoneTransform).ToArray();Require(transforms.All(t=>t),"Real humanoid hand/leg chains are mapped for the climbing animation sample.");
            var first=transforms.Select(t=>t.localRotation).ToArray();float[] changes=new float[first.Length];float began=Time.time,cycle=visual.ClimbCycleTime;
            while(Time.time<began+seconds){yield return null;for(int i=0;i<first.Length;i++)changes[i]=Mathf.Max(changes[i],Quaternion.Angle(first[i],transforms[i].localRotation));}
            Require(visual.ClimbPoseWeight>.95f&&Mathf.Abs(visual.ClimbCycleTime-cycle)>.01f,"The imported climb cycle actually advances while "+(direction>0?"W ascends":"S descends")+" instead of only owning a clip reference.");
            Require(changes.Take(2).Max()>4&&changes.Skip(2).Max()>4,"Retargeted arm and leg bones visibly change during real "+(direction>0?"ascent":"descent")+"; maximum degrees="+string.Join(", ",changes.Select(v=>v.ToString("F2")))+".");
        }
        private static IEnumerator ClimbAnimationPauseSample()
        {
            input=new KeyboardState();yield return Wait(.3f);var visual=ClimbVisual();float cycle=visual.ClimbCycleTime;Vector3 feet=court.Player.FeetPosition;
            yield return Wait(.3f);
            Require(Mathf.Abs(visual.ClimbCycleTime-cycle)<.01f&&Vector3.Distance(feet,court.Player.FeetPosition)<.02f,"Releasing movement holds both the real climbing pose and capsule position instead of playing an idle walking clip.");
            Capture(Path.Combine(Folder,"boat-ladder-retargeted-climb.png"));
            SkywellClimbAnimationSetup.CaptureCurrentClimbViews(Folder);
        }

        private static IEnumerator MonitorBoatThreshold(IEnumerator movement,string label)
        {
            var steps=new Stack<IEnumerator>();steps.Push(movement);
            bool swimming=court.Player.IsSurfaceSwimming;int transitions=0;
            Vector3 previous=court.Player.FeetPosition;float maxVerticalStep=0,maxPlanarExcess=0;
            string worstPlanarFrame="";
            while(steps.Count>0)
            {
                var step=steps.Peek();
                if(!step.MoveNext()){steps.Pop();continue;}
                if(step.Current is IEnumerator nested){steps.Push(nested);continue;}
                // The driver samples before Player/Journey Update. The next observed
                // movement was executed with this frame's delta, not the next one's.
                float movementDelta=Time.deltaTime;
                yield return step.Current;
                Vector3 now=court.Player.FeetPosition;
                if(court.Player.IsSurfaceSwimming!=swimming){transitions++;swimming=court.Player.IsSurfaceSwimming;}
                maxVerticalStep=Mathf.Max(maxVerticalStep,Mathf.Abs(now.y-previous.y));
                float planarExcess=Vector3.ProjectOnPlane(now-previous,Vector3.up).magnitude-4.25f*movementDelta;
                if(planarExcess>maxPlanarExcess){maxPlanarExcess=planarExcess;worstPlanarFrame=$" from={previous:F4} to={now:F4} movementDt={movementDelta:F5}";}
                previous=now;
            }
            Require(transitions==1,label+": one stable swim/walk handoff across the physical opening, without state oscillation.");
            Require(maxVerticalStep<.26f&&maxPlanarExcess<.04f,label+": actual frame displacement stays within the small boarding step and movement speed; no snap to a destination (vertical="+maxVerticalStep.ToString("F4")+", planarExcess="+maxPlanarExcess.ToString("F4")+worstPlanarFrame+").");
        }

        private static IEnumerator SurfaceSwimInputFixture()
        {
            var j=court.Journey;var fish=court.GetComponent<SkywellFish>();
            Vector3 sample=court.Origin+new Vector3(-8,0,-17);sample.y=j.SurfaceFeetHeight;
            float baseline=0;
            var variants=new[]{new KeyboardState(Key.W),new KeyboardState(Key.W,Key.Space),new KeyboardState(Key.W,Key.LeftCtrl)};
            var labels=new[]{"W","W + Space","W + Left Ctrl"};
            for(int test=0;test<variants.Length;test++)
            {
                input=new KeyboardState();court.Player.Teleport(sample);fish.ResetFish();yield return Wait(.25f);
                Require(court.Player.IsSwimming&&j.Travel==SkywellTravel.Swimming,"The declared clear-water placement enters actual surface swimming for "+labels[test]);
                Vector3 before=court.Player.FeetPosition;float began=Time.time,maximumHeightError=0;input=variants[test];
                while(Time.time<began+.6f)
                {
                    maximumHeightError=Mathf.Max(maximumHeightError,Mathf.Abs(court.Player.FeetPosition.y-j.SurfaceFeetHeight));
                    yield return null;
                }
                input=new KeyboardState();float speed=Vector3.ProjectOnPlane(court.Player.FeetPosition-before,Vector3.up).magnitude/Mathf.Max(.001f,Time.time-began);
                if(test==0)baseline=speed;
                Require(speed>2.8f&&speed<3.5f&&Mathf.Abs(speed-baseline)<.15f,"Actual "+labels[test]+" preserves normal horizontal swimming speed; measured "+speed.ToString("F3")+" m/s.");
                Require(maximumHeightError<.04f&&Mathf.Abs(court.Player.FeetPosition.y-j.SurfaceFeetHeight)<.04f,"Actual "+labels[test]+" cannot dive or rise away from the configured waterline.");
                yield return Wait(.1f);
            }
            var cover=court.GetComponentsInChildren<Collider>(true).FirstOrDefault(c=>c.name=="Opaque_filter_column_hides_swimmer"&&c.enabled&&c.gameObject.activeInHierarchy&&c.bounds.max.y>j.SurfaceWaterLevel+.5f);
            Require(cover,"An authored solid cover column extends above the surface swimmer.");
            Bounds bounds=cover.bounds;Vector3 beside=new Vector3(bounds.min.x-.65f,j.SurfaceFeetHeight,bounds.center.z);
            input=new KeyboardState();court.Player.Teleport(beside);fish.ResetFish();yield return Wait(.25f);
            Require(!fish.Clear(beside+Vector3.up*.35f,new Vector3(bounds.max.x+1,j.SurfaceFeetHeight+.35f,bounds.center.z),.08f),"The authored column blocks the actual fish sight cast at the surface body's height.");
            var body=court.Player.GetComponent<CharacterController>();
            Require(body,"The cover fixture measures the actual player capsule against the authored mesh.");
            float radius=body.radius*Mathf.Max(Mathf.Abs(body.transform.lossyScale.x),Mathf.Abs(body.transform.lossyScale.z));
            float startDistance=Vector3.ProjectOnPlane(court.Player.FeetPosition-bounds.center,Vector3.up).magnitude;
            float minimumClearance=float.PositiveInfinity,end=Time.time+.8f;bool foundSideEveryFrame=true,swamEveryFrame=true;
            navigationError=Vector3.zero;
            while(Time.time<end)
            {
                Direction(Vector3.ProjectOnPlane(bounds.center-court.Player.FeetPosition,Vector3.up).normalized);yield return null;
                Vector3 center=body.transform.TransformPoint(body.center);
                Vector3 outward=Vector3.ProjectOnPlane(center-bounds.center,Vector3.up).normalized;
                float reach=bounds.extents.magnitude+radius+1;RaycastHit hit=default;
                bool side=outward.sqrMagnitude>.9f&&center.y>bounds.min.y&&center.y<bounds.max.y&&
                    cover.Raycast(new Ray(center+outward*reach,-outward),out hit,reach*2);
                if(side)
                {
                    side=Mathf.Abs(hit.normal.y)<.1f;
                    // The real mesh face normal measures capsule clearance; an AABB face would reject legal cylinder sliding.
                    minimumClearance=Mathf.Min(minimumClearance,Vector3.Dot(center-hit.point,hit.normal));
                }
                foundSideEveryFrame&=side;swamEveryFrame&=court.Player.IsSurfaceSwimming;
            }
            input=new KeyboardState();yield return Wait(.15f);
            float endDistance=Vector3.ProjectOnPlane(court.Player.FeetPosition-bounds.center,Vector3.up).magnitude;
            Require(foundSideEveryFrame&&swamEveryFrame&&minimumClearance>=radius-body.skinWidth-.015f,"Actual swim input toward the column preserves capsule clearance from its real mesh every sampled frame; minimum="+minimumClearance.ToString("F3")+", radius="+radius.ToString("F3")+", skin="+body.skinWidth.ToString("F3")+".");
            Require(startDistance-endDistance>.1f&&minimumClearance<=radius+body.skinWidth+.08f,"The capsule actually approaches and contacts the column instead of passing the check from a distance; approach="+(startDistance-endDistance).ToString("F3")+" m.");
            Require(j.Recoveries==0,"The clear-water movement and cover fixtures caused no recovery.");
            court.Player.Teleport(j.SurfaceDock.position+Vector3.up*.03f);yield return Wait(.25f);
        }

        private static IEnumerator SurfaceNorthBoundaryFixture()
        {
            var j=court.Journey;var player=court.Player;
            var apron=j.SurfaceWalkable.FirstOrDefault(c=>c&&c.name=="Tower_water_landing"&&c.enabled&&c.gameObject.activeInHierarchy);
            var backing=court.GetComponentsInChildren<Collider>().FirstOrDefault(c=>c.name=="Ladder_solid_backing"&&c.enabled&&!c.isTrigger);
            Require(j.SurfaceWaterBody&&apron&&backing,"The boundary fixture uses the actual visible water renderer, tower-side apron and the new solid ladder backing.");
            Bounds floor=apron.bounds;float edge=floor.max.z;
            Vector3 outside=new Vector3(floor.center.x,j.SurfaceFeetHeight,edge+.9f);
            Require(!j.IsOverSurfaceWater(outside)&&!j.IsWaterApproach(outside),"The read-only point beyond the north apron (approximately z103) is neither visible water nor supported approach ground.");
            report.Add("NORTH WATER BOUNDARY INPUT FIXTURE: this revision physically closes the rear of the ladder with a real backing wall, so the former north-edge walk-off is no longer reachable from the apron. After the explicit front-apron placement, actual northward WASD must contact that wall while retaining dry real-floor support, with no swimming, rear penetration or recovery. The previous z103 outside-water/support probe remains read-only; this is not a claim of walking to z103. Visible water ends at z="+j.SurfaceWaterBounds.max.z.ToString("F3")+", apron ends at z="+edge.ToString("F3")+".");SaveProgress();
            input=new KeyboardState();player.Teleport(new Vector3(floor.center.x,floor.max.y+.03f,j.SurfaceWaterBounds.max.z+.1f));yield return Wait(.3f);
            Require(!j.IsOverSurfaceWater(player.FeetPosition)&&j.IsWaterApproach(player.FeetPosition)&&!player.IsSwimming,"The supported front apron beyond the water bounds is dry walking, not buoyancy in air.");
            Vector3 began=player.FeetPosition;int fishRecoveries=j.Recoveries;float peakZ=began.z,end=Time.time+1.4f;bool recovered=false,everSwam=false;
            navigationError=Vector3.zero;
            while(Time.time<end)
            {
                Direction(Vector3.forward);yield return null;
                peakZ=Mathf.Max(peakZ,player.FeetPosition.z);everSwam|=player.IsSurfaceSwimming||player.IsSwimming;
                recovered|=court.Prompt().Contains("已回到最近的安全岸边");
            }
            input=new KeyboardState();yield return Wait(.2f);
            Vector3 stopped=player.FeetPosition;
            bool supported=apron.Raycast(new Ray(stopped+Vector3.up*.22f,Vector3.down),out var support,.52f)&&support.normal.y>.55f;
            bool wallContact=backing.Raycast(new Ray(stopped+Vector3.up*.8f,Vector3.forward),out var hit,.55f)&&hit.distance>.05f;
            Require(!everSwam&&!player.IsSurfaceSwimming&&!player.IsSwimming,"Northward input on the dry apron never enables swimming during the real wall contact.");
            Require(!recovered&&peakZ>began.z+.08f&&peakZ<backing.bounds.min.z&&wallContact&&supported&&Mathf.Abs(stopped.y-floor.max.y)<.1f&&player.enabled,
                "Actual northward movement reaches the solid backing and stops on the supported apron without walking behind it or invoking recovery; peak z="+peakZ.ToString("F3")+", stopped="+stopped+".");
            Require(j.Recoveries==fishRecoveries&&!j.SunnyTokenObtained&&!j.PaddleObtained,"Contacting the backing neither masquerades as fish capture nor grants progression.");
            player.Teleport(j.SurfaceDock.position+Vector3.up*.03f);yield return Wait(.3f);
        }

        private static IEnumerator SurfaceRampReturnFixture()
        {
            var j=court.Journey;var route=j.SurfaceRouteWaypoints;
            report.Add("BIDIRECTIONAL RAMP INPUT FIXTURE: from the preceding declared dock placement, actual camera-relative WASD follows every authored static-ramp waypoint back to the high bank, then down to the dock again. No placement, progress or swimming-state changes during either direction.");SaveProgress();
            for(int i=route.Length-1;i>=0;i--)
            {
                yield return Walk(route[i].position,"Return up static bank route "+i,true);
                Require(!court.Player.IsSurfaceSwimming&&!court.Player.IsSwimming,"The uphill authored ground remains ordinary walking at waypoint "+i);
            }
            Require(court.Player.FeetPosition.y>court.Origin.y-.15f&&court.Player.PuzzleEdgeProtectionEnabled,"The high bank restores ordinary ground height and edge protection.");
            for(int i=0;i<route.Length;i++)
            {
                yield return Walk(route[i].position,"Return down static bank route "+i,true);
                Require(!court.Player.IsSurfaceSwimming&&!court.Player.IsSwimming,"The downhill authored ground remains ordinary walking at waypoint "+i);
            }
            Require(j.InRefuge&&j.Travel==SkywellTravel.Walking&&!j.PaddleObtained&&!j.SunnyTokenObtained,"The continuous round trip returns to the dry dock without changing progression.");
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace BorrowingSun.Editor
{
    public static partial class SkywellChecks
    {
        [MenuItem("Borrowing Sun/Skywell/Continuous deer branch from east village")]
        public static void RunDeerRoute()=>StartCheck("SKYWELL_DEER_ROUTE",DeerRouteSequence(false));
        [MenuItem("Borrowing Sun/Skywell/Continuous shoulder deer branch from east village")]
        public static void RunDeerShoulderRoute()=>StartCheck("SKYWELL_DEER_SHOULDER_ROUTE",DeerRouteSequence(true));

        private static IEnumerator DeerRouteSequence(bool shoulder)
        {
            report.Add("CONTINUOUS BRANCH WITH ONE START FIXTURE: reset, then place the unmounted player once beside the east-village resident. Walk the cave, use real weather, ride outward across four clouds, arrive on the northern floor for automatic dismount, observe the deer hopping back, then freely jump the cloud route on foot and walk the cave back east. All player travel uses production keyboard/mouse and physical collision. No later player placement, growth/unlock/velocity injection or forced success. This is not the complete original entrance route or a declared fall-recovery fixture.");
            report.Add("CAMERA: "+(shoulder?"shoulder, turning through actual mouse input before W":"fixed oblique, real camera-relative WASD combinations")+"; weather must be visible from that selected camera's production targeting frame, with no helper fallback to another view.");
            court.ResetCourt();yield return Wait(.4f);
            var grove=court.Grove;var ride=grove?grove.Ride:null;
            Require(grove&&ride,"The branch has an authored grove and deer ride.");
            var platforms=DeerAuthoredCloudRoute(grove);int cloudCount=platforms.Length-2;
            int northIndex=platforms.Length-1;
            report.Add("SAVED CLOUD ROUTE: "+string.Join(" -> ",platforms.Select(p=>p.name)));
            CheckGroveReset("continuous branch start");
            var root=court.transform.Find("Revision_15_Hidden_deer_grove");
            Require(root,"The incremental scene root exists.");
            var waypoints=DeerAuthoredPathWaypoints(root);
            var cutaway=court.GetComponentsInChildren<SkywellCaveCutaway>(true).SingleOrDefault();
            Require(cutaway,"The authored mountain supplies a cave presentation component.");
            var caveSurfaces=cutaway.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Cave_shell_",StringComparison.Ordinal))
                .Select(t=>t.GetComponent<Renderer>()).ToArray();
            Require(caveSurfaces.Length==5&&caveSurfaces.All(r=>r&&r.shadowCastingMode==ShadowCastingMode.On),
                "The five authored cave surfaces begin with normal visible shadow casting.");
            bool cutawayChecked=false;
            report.Add("SAVED CAVE WAYPOINTS: "+string.Join(" -> ",waypoints.Select(p=>p.name+"="+p.position.ToString("F2"))));
            court.Player.Teleport(new Vector3(53.5f,26.03f,112.5f));
            yield return Wait(.5f);yield return EnsureCameraMode(shoulder);
            int journeyRecoveries=court.Journey.Recoveries;
            for(int i=0;i<waypoints.Length;i++)
            {
                yield return DeerBranchWalk(waypoints[i].position+Vector3.up*.03f,"cave path "+waypoints[i].name);
                if(!cutawayChecked&&i>=2&&i<waypoints.Length-1&&cutaway.IsInside)
                {
                    yield return DeerRouteCaveViews(cutaway,caveSurfaces,shoulder);
                    cutawayChecked=true;
                }
                if(i==waypoints.Length/3||i==2*waypoints.Length/3){yield return Wait(.3f);Capture(Path.Combine(Folder,"deer-route-"+(shoulder?"shoulder":"fixed")+"-path-"+i+".png"));}
            }
            Require(cutawayChecked,"The actual walking route enters the cave and exercises its fixed/shoulder visibility.");
            RequireCaveRestored(cutaway,caveSurfaces,"outbound cave exit");
            yield return DeerBranchWalk(new Vector3(27.5f,28.03f,134.5f),"apple clearing observation position");
            Require(grove.Zone.Contains(court.Player.FeetPosition)&&!ride.IsAvailable,"Walking the authored cave physically reaches the island before any unlock.");
            Vector3 deerBefore=ride.DeerTransform.position;yield return Wait(.8f);
            Require(Vector3.Distance(deerBefore,ride.DeerTransform.position)>.25f,"The waiting deer circles the tree in the reachable clearing.");
            yield return DeerRouteCast(SkywellToken.Wind,shoulder);
            yield return WaitUntil(()=>grove.SmallFruitDropped,3,"Sparse fruit drops from the actual tree");
            Require(!grove.IsGrown&&!grove.Unlocked&&!ride.IsAvailable,"One sparse apple does not unlock the cloud route.");
            yield return DeerRouteCast(SkywellToken.Rain,shoulder);
            yield return WaitUntil(()=>grove.IsGrown,6,"Real rain grows the tree once and exposes its mature fruit");
            yield return DeerRouteCast(SkywellToken.Wind,shoulder);
            yield return WaitUntil(()=>grove.LandedMatureFruitCount>0,5,"Mature apples fall onto the actual island collider");
            yield return WaitUntil(()=>grove.Unlocked&&ride.IsAvailable,18,"The deer approaches and eats before the cloud route finishes appearing");
            yield return Wait(.4f);Capture(Path.Combine(Folder,"deer-route-"+(shoulder?"shoulder":"fixed")+"-island.png"));
            yield return DeerRouteAroundTree(ride.MountPoint.position+Vector3.up*.03f,false,"approach the waiting deer");
            yield return Press(Key.E);yield return Wait(.3f);
            Require(ride.IsMounted&&court.Player.ExternalMotionOwnedBy(ride),"Real short E mounts the deer reached on foot.");
            // Stay south of the real trunk; do not draw a direct chord through the tree.
            yield return DeerRouteAroundTree(new Vector3(28.5f,28.03f,135),true,"south-east clearing");
            yield return DeerRouteDrive(new Vector3(25,28.03f,135),"south clearing");
            yield return DeerRouteDrive(new Vector3(21,28.03f,135),"west cloud approach");
            int recoveries=ride.RecoveryCount;
            for(int i=0;i<cloudCount;i++)
            {
                yield return DeerRouteJump(platforms[i],platforms[i+1],"outbound cloud "+i);
                if(i==0)yield return CloudJumpComfort(platforms[i+1],false);
            }
            Require(ride.RecoveryCount==recoveries,cloudCount+" outbound cloud jumps landed without recovery or placement.");
            // Actual stable arrival, not a scripted E press, completes the deer's outward task.
            yield return DeerRouteJump(platforms[cloudCount],platforms[northIndex],"outbound final cloud across the gap to north village");
            yield return WaitUntil(()=>ride.FootRouteUnlocked&&!ride.IsMounted,3,"Stable northern arrival automatically dismounts and unlocks foot travel");
            Require(ride.LastSafePlatform==platforms[northIndex]&&DeerRouteFootprint(platforms[northIndex],court.Player.FeetPosition),"Automatic dismount leaves the traveller on the original northern floor.");
            Require(!court.Player.ExternalMotionOwnedBy(ride),"Northern arrival releases riding without an E interaction.");
            Capture(Path.Combine(Folder,"deer-route-"+(shoulder?"shoulder":"fixed")+"-north-village.png"));
            Require(court.CameraRig.IsShoulderView==shoulder,"Arrival and dismount retain the chosen gameplay camera.");
            yield return DeerRouteObserveReturn(platforms);
            var foot=ride.FootDriver;
            Require(foot&&ride.FootRouteUnlocked,"The earned cloud route exposes its player-controlled foot driver.");
            int footRecoveries=foot.RecoveryCount;
            yield return DeerRouteJump(platforms[northIndex],platforms[cloudCount],"on foot from north onto final cloud",null,true);
            yield return CloudJumpComfort(platforms[cloudCount],true);
            for(int i=cloudCount;i>0;i--)yield return DeerRouteJump(platforms[i],platforms[i-1],"on foot return cloud "+(i-1),null,true);
            Require(foot.LastSafePlatform==platforms[0]&&foot.RecoveryCount==footRecoveries&&ride.RecoveryCount==recoveries,
                "The actual on-foot reverse cloud route reaches the island without recovery or remounting.");
            Require(!ride.IsMounted&&grove.Unlocked&&grove.IsGrown&&ride.FootRouteUnlocked,"On-foot return preserves the earned tree and foot-route state.");
            Require(Vector3.ProjectOnPlane(court.Player.FeetPosition-ride.DeerTransform.position,Vector3.up).magnitude>1.5f,
                "The parked deer leaves clear space for the traveller's actual island landing.");
            Require(court.Journey.Recoveries==journeyRecoveries,"The whole branch caused no unrelated journey rescue.");
            Require(court.CameraRig.IsShoulderView==shoulder,"The complete outward and return traversal preserves its selected camera mode.");
            yield return Wait(.4f);Capture(Path.Combine(Folder,"deer-route-"+(shoulder?"shoulder":"fixed")+"-returned.png"));
            yield return DeerRouteAroundTree(new Vector3(27.5f,28.03f,134.5f),false,"return to cave-path entrance");
            for(int i=waypoints.Length-1;i>=0;i--)
                yield return DeerBranchWalk(waypoints[i].position+Vector3.up*.03f,"return cave path "+waypoints[i].name);
            Require(court.Journey.Recoveries==journeyRecoveries,"The authored cave walking path is also traversable downhill without rescue.");
            Require(!foot.IsActive&&!court.Player.ExternalMotionOwnedBy(foot)&&!court.Player.IsExternallyDriven,
                "Walking off the island and back through the cave releases foot-route ownership to ordinary player movement before reset.");
            Require(foot.RecoveryCount==0,
                "The entire continuous foot return, including the island-to-cave transition, causes no foot-driver recovery before reset.");
            RequireCaveRestored(cutaway,caveSurfaces,"return to the east-village cave mouth");
            yield return Press(Key.R);yield return Wait(.4f);CheckGroveReset("continuous bidirectional branch reset");
            Require(!court.Player.IsExternallyDriven&&!court.CameraRig.IsShoulderView,"Final real R releases riding and restores the fixed gameplay camera.");
            Require(!ride.FootRouteUnlocked&&!ride.IsReturning&&ride.ReturnLandingCount==0&&!foot.IsActive&&!foot.IsAirborne,
                "Final real R clears the earned foot route, deer return and airborne foot ownership.");
            RequireCaveRestored(cutaway,caveSurfaces,"final real R");
        }

        private static IEnumerator CloudJumpComfort(Collider surface,bool onFoot)
        {
            var ride=court.Grove.Ride;var foot=ride.FootDriver;var actor=court.Player;var rig=court.CameraRig;
            Func<bool> grounded=()=>onFoot?foot.IsGrounded:ride.IsGrounded;
            Func<int> recoveries=()=>onFoot?foot.RecoveryCount:ride.RecoveryCount;
            Vector3 center=DeerRoutePlatformTop(surface);int before=recoveries();
            string label=onFoot?"on-foot":"riding";
            report.Add("JUMP COMFORT "+label+": real input on the cloud reached by the continuous route; centre walking, neutral buffered hop, mouse observation and late edge jump. No teleport, velocity write, unlock injection or destination magnet.");
            yield return DeerRouteDrive(center,label+" centre for controls check",onFoot);
            input=new KeyboardState();pointerInput=new MouseState{position=pointerInput.position};yield return Wait(.3f);
            Require(rig.PlatformingViewActive,"Cloud framing is requested by the active "+label+" motion owner.");
            if(rig.IsShoulderView)
            {
                Require(rig.ViewCamera.fieldOfView>63,"Local shoulder framing widens the landing view.");
                Vector3 facing=actor.FacingDirection;Quaternion oldRotation=rig.ViewCamera.transform.rotation;
                pointerInput=new MouseState{position=pointerInput.position,delta=new Vector2(30/.14f,0)};yield return null;
                pointerInput=new MouseState{position=pointerInput.position};yield return Wait(.08f);
                Require(Vector3.Angle(facing,actor.FacingDirection)<.1f&&Quaternion.Angle(oldRotation,rig.ViewCamera.transform.rotation)>20,
                    "Looking around a stationary cloud does not rotate or move the character.");
            }
            Capture(Path.Combine(Folder,"jump-controls-"+(rig.IsShoulderView?"shoulder":"fixed")+"-"+label+".png"));
            float y=actor.FeetPosition.y;
            yield return Press(Key.Space);
            Require(!grounded()&&actor.FeetPosition.y>y+.1f,"Neutral Space starts a real "+label+" hop.");
            float previousY=actor.FeetPosition.y;
            double deadline=EditorApplication.timeSinceStartup+2;
            while(true)
            {
                float current=actor.FeetPosition.y;
                if(current<previousY&&current<y+.65f)break;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Buffered hop never descended: "+label);
                previousY=current;yield return null;
            }
            input=new KeyboardState(Key.Space);yield return Wait(.025f);input=new KeyboardState();
            yield return WaitUntil(()=>actor.FeetPosition.y>y+.9f,.6f,"A pre-landing Space press is buffered into the next physical "+label+" jump");
            yield return WaitUntil(()=>grounded(),2,"The buffered hop lands again");yield return Wait(.2f);
            Require(recoveries()==before&&Vector3.ProjectOnPlane(actor.FeetPosition-center,Vector3.up).magnitude<.5f,
                "Buffered hopping retains real local support without drift or an air jump.");
            // Leave the SOUTH rim through actual movement, then press late and steer back.
            Vector3 outward=Vector3.back;
            yield return FaceNavigationDirection(outward);navigationError=Vector3.zero;Direction(outward);
            deadline=EditorApplication.timeSinceStartup+3;
            while(ride.TryRouteCenterSupport(actor.FeetPosition,out _))
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Cloud rim was not reached: "+label);
                yield return null;
            }
            yield return Wait(.025f);
            float edgeY=actor.FeetPosition.y;
            navigationError=Vector3.zero;
            if(rig.IsShoulderView)input=new KeyboardState(Key.S);else Direction(-outward);
            input.Press(Key.Space);yield return Wait(.06f);
            Require(actor.FeetPosition.y>edgeY+.15f&&!grounded(),"Space just after the physical rim still launches "+label+" through coyote time.");
            Require(Vector3.Angle(actor.FacingDirection,-outward)<8,"Turning follows the requested jump direction rather than old outward inertia.");
            // Countersteer briefly so the test lands in the centre, rather than coasting
            // across this small cloud and demanding a full footprint at the opposite rim.
            navigationError=Vector3.zero;
            if(rig.IsShoulderView)input=new KeyboardState(Key.W);else Direction(outward);
            yield return WaitUntil(()=>actor.PlanarVelocity.magnitude<3.2f,.4f,"Real opposite input brakes the airborne traveller");
            input=new KeyboardState();pointerInput=new MouseState{position=pointerInput.position};
            yield return WaitUntil(()=>grounded(),2,"Late jump returns onto the actual cloud collider");yield return Wait(.3f);
            Require(recoveries()==before&&DeerRouteFootprint(surface,actor.FeetPosition),"Late takeoff lands safely without recovery or invisible support.");
            yield return DeerRouteDrive(center,label+" return from rim check",onFoot);
        }

        private static IEnumerator DeerRouteObserveReturn(Collider[] platforms)
        {
            var ride=court.Grove.Ride;var visual=ride.DeerTransform;
            Require(ride.IsReturning&&ride.ReturnLandingCount==0&&!ride.IsMounted,
                "Automatic dismount begins the visible deer return before its first cloud landing.");
            float radius,parkingDistance,walkingSpeed;
            using(var serialized=new SerializedObject(ride))
            {
                radius=serialized.FindProperty("collisionRadius").floatValue;
                parkingDistance=serialized.FindProperty("returnParkingDistance").floatValue;
                walkingSpeed=serialized.FindProperty("moveSpeed").floatValue*.7f;
            }
            Vector3 playerStart=court.Player.FeetPosition,hopStart=visual.position;
            Vector3 parkingStart=Vector3.zero,previousParking=Vector3.zero;
            float previousMovementDelta=Time.deltaTime;
            float peak=hopStart.y,parkingTravel=0;int landed=0,parkingMovingFrames=0;
            bool parkingStayedSupported=true,parkingStayedVisible=true,parkingStayedUnavailable=true,parkingStepsBounded=true;
            double deadline=EditorApplication.timeSinceStartup+25;
            input=new KeyboardState();pointerInput=new MouseState{position=pointerInput.position};
            while(ride.IsReturning)
            {
                if(EditorApplication.timeSinceStartup>deadline)
                    throw new Exception("Deer return did not complete: feet="+visual.position+" landings="+ride.ReturnLandingCount+" blocked="+ride.ReturnBlocked);
                peak=Mathf.Max(peak,visual.position.y);
                RequireDeerReturnLanding();
                ObserveParkingWalk();
                previousMovementDelta=Time.deltaTime;
                yield return null;
            }
            peak=Mathf.Max(peak,visual.position.y);RequireDeerReturnLanding();ObserveParkingWalk();
            Require(landed==platforms.Length-1&&ride.ReturnLandingCount==landed&&ride.IsAvailable,
                "The deer physically lands on all four clouds and the island, then finishes parking before becoming available.");
            Require(parkingMovingFrames>=3&&parkingTravel>=parkingDistance-.1f&&
                Vector3.ProjectOnPlane(visual.position-parkingStart,Vector3.up).magnitude>=parkingDistance-.1f,
                "After its fifth landing the deer visibly walks inward far enough to clear the cloud approach.");
            Require(parkingStayedSupported&&parkingStayedVisible&&parkingStepsBounded,
                "The parking walk stays on five real island support points with visible bounded movement, without teleporting.");
            Require(parkingStayedUnavailable,
                "The deer remains unavailable until the supported parking walk has finished.");
            report.Add("DEER PARKING landing="+parkingStart.ToString("F3")+" parked="+visual.position.ToString("F3")+
                " walked="+parkingTravel.ToString("F3")+" movingFrames="+parkingMovingFrames);SaveProgress();
            Require(Vector3.Distance(playerStart,court.Player.FeetPosition)<.06f&&!ride.IsMounted,
                "The traveller remains on the northern floor while the independent visible deer returns.");
            Capture(Path.Combine(Folder,"deer-route-"+(court.CameraRig.IsShoulderView?"shoulder":"fixed")+"-deer-returned.png"));

            void ObserveParkingWalk()
            {
                if(landed!=platforms.Length-1)return;
                float step=Vector3.Distance(visual.position,previousParking);
                parkingTravel+=step;if(step>.001f)parkingMovingFrames++;
                parkingStepsBounded&=step<=walkingSpeed*Mathf.Max(0,previousMovementDelta)+.05f;
                parkingStayedSupported&=DeerRouteFootprint(platforms[0],visual.position,radius);
                parkingStayedVisible&=visual.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy);
                parkingStayedUnavailable&=!ride.IsReturning||!ride.IsAvailable;
                previousParking=visual.position;
            }

            void RequireDeerReturnLanding()
            {
                if(ride.ReturnLandingCount==landed)return;
                Require(ride.ReturnLandingCount==landed+1,"Deer return reports one actual cloud landing at a time.");
                // Count 1 is the last cloud, count N is the island.
                int index=platforms.Length-1-ride.ReturnLandingCount;
                Collider expected=platforms[index];
                Require(DeerRouteFootprint(expected,visual.position,radius),
                    "Returning deer has five real support points on "+expected.name+".");
                Require(peak>Mathf.Max(hopStart.y,visual.position.y)+.3f&&
                    Vector3.ProjectOnPlane(visual.position-hopStart,Vector3.up).magnitude>.3f,
                    "Returning deer visibly rises and crosses space before landing on "+expected.name+".");
                Require(visual.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy),
                    "Returning deer keeps its visible model active at "+expected.name+".");
                report.Add("DEER RETURN landing="+ride.ReturnLandingCount+" platform="+expected.name+
                    " feet="+visual.position.ToString("F3")+" peak="+peak.ToString("F3"));SaveProgress();
                landed=ride.ReturnLandingCount;hopStart=visual.position;peak=hopStart.y;
                if(index==0)parkingStart=previousParking=visual.position;
            }
        }

        private static IEnumerator DeerRouteCaveViews(SkywellCaveCutaway cutaway,Renderer[] surfaces,bool originalShoulder)
        {
            // Only actual Z input changes the production camera mode. Never hide geometry
            // or write the player's/camera's pose from this visibility check.
            yield return EnsureCameraMode(false);yield return Wait(.25f);
            var hidden=surfaces.Where(r=>r.shadowCastingMode==ShadowCastingMode.ShadowsOnly).ToArray();
            Require(cutaway.IsInside&&cutaway.CutawayCount>0&&hidden.Length==cutaway.CutawayCount,
                "Inside the cave, fixed view makes only its obstructing authored surfaces shadow-only.");
            foreach(var renderer in surfaces)
            {
                var solid=renderer.GetComponent<Collider>();
                Require(renderer.enabled&&solid&&solid.enabled&&!solid.isTrigger,
                    "Cave presentation preserves the visible renderer component and solid collision: "+renderer.name);
            }
            Vector3 feet=court.Player.FeetPosition,camera=court.CameraRig.ViewCamera.transform.position;
            var sightOffsets=new[]{Vector3.up*.18f,Vector3.up*1.1f,Vector3.up*1.85f,
                Vector3.up*.6f+Vector3.left*1.25f,Vector3.up*.6f+Vector3.right*1.25f,
                Vector3.up*.6f+Vector3.forward*1.25f,Vector3.up*.6f+Vector3.back*1.25f};
            foreach(var renderer in hidden)
                Require(sightOffsets.Any(offset=>DeerCaveBlocksSight(renderer.GetComponent<Collider>(),feet+offset,camera)),
                    "A shadow-only cave face still physically blocks a body or nearby-floor sightline: "+renderer.name);
            foreach(float height in new[]{.18f,1.85f})
                Require(!surfaces.Any(r=>r.shadowCastingMode!=ShadowCastingMode.ShadowsOnly&&
                    DeerCaveBlocksSight(r.GetComponent<Collider>(),feet+Vector3.up*height,camera)),
                    "Fixed cave view has no visible solid shell between the camera and "+(height<1?"feet":"head")+".");
            Capture(Path.Combine(Folder,"deer-route-"+(originalShoulder?"shoulder":"fixed")+"-cave-fixed.png"));
            yield return EnsureCameraMode(true);yield return Wait(.25f);
            Require(cutaway.IsInside&&cutaway.CutawayCount==0&&surfaces.All(r=>r.shadowCastingMode==ShadowCastingMode.On),
                "Real Z to shoulder restores all cave surface rendering while the player remains inside.");
            Capture(Path.Combine(Folder,"deer-route-"+(originalShoulder?"shoulder":"fixed")+"-cave-shoulder.png"));
            yield return EnsureCameraMode(originalShoulder);yield return Wait(.25f);
            Require(court.CameraRig.IsShoulderView==originalShoulder&&
                (originalShoulder?cutaway.CutawayCount==0:cutaway.CutawayCount>0),
                "Actual mode switches restore the selected route view and its cave presentation.");
        }

        private static bool DeerCaveBlocksSight(Collider solid,Vector3 from,Vector3 to)
        {
            Vector3 delta=to-from;
            return solid.Raycast(new Ray(from,delta.normalized),out _,delta.magnitude);
        }

        private static void RequireCaveRestored(SkywellCaveCutaway cutaway,Renderer[] surfaces,string context)
        {
            Require(!cutaway.IsInside&&cutaway.CutawayCount==0&&surfaces.All(r=>r.enabled&&r.shadowCastingMode==ShadowCastingMode.On&&
                r.GetComponent<Collider>()&&r.GetComponent<Collider>().enabled&&!r.GetComponent<Collider>().isTrigger),
                "Leaving the cave restores all surface rendering and preserves collision: "+context);
        }

        private static Transform[] DeerAuthoredPathWaypoints(Transform root)
        {
            var path=root.Find("Hidden_path_from_shaded_village");
            Require(path,"The saved scene supplies the cave path waypoint parent.");
            const string prefix="Waypoint_";
            var waypoints=path.Cast<Transform>()
                .Where(t=>t.name.StartsWith(prefix,StringComparison.Ordinal)&&int.TryParse(t.name.Substring(prefix.Length),out _))
                .OrderBy(t=>int.Parse(t.name.Substring(prefix.Length))).ToArray();
            Require(waypoints.Length>=2&&waypoints.Select(t=>int.Parse(t.name.Substring(prefix.Length))).Distinct().Count()==waypoints.Length,
                "The cave path has distinct, numerically ordered authored waypoints.");
            Require(!root.GetComponentsInChildren<Renderer>(true).Any(r=>r.name.StartsWith("Path_cloud_",StringComparison.Ordinal)&&r.enabled&&r.gameObject.activeInHierarchy),
                "The walking approach has no remaining visible path-cloud meshes.");
            return waypoints;
        }

        private static Collider[] DeerAuthoredCloudRoute(SkywellDeerGrove grove)
        {
            var platforms=grove.Ride.SafePlatforms;
            Require(platforms!=null&&platforms.Length>=2&&platforms.All(p=>p)&&platforms.Distinct().Count()==platforms.Length,
                "The saved ride route contains distinct physical landing colliders.");
            int cloudCount=platforms.Length-2;
            Require(cloudCount==4,"The saved route contains four clouds between the island and original northern floor.");
            Require(!platforms[0].transform.IsChildOf(grove.CloudPlatforms)&&!platforms[platforms.Length-1].transform.IsChildOf(grove.CloudPlatforms),
                "Island and northern floor remain separate physical endpoint surfaces.");
            for(int i=0;i<cloudCount;i++)
                Require(platforms[i+1].transform.IsChildOf(grove.CloudPlatforms)&&grove.CloudPlatforms.Find("Landing_"+i),
                    "Saved cloud "+i+" has a landing collider and authored centre marker.");
            return platforms;
        }

        private static IEnumerator DeerRouteCast(SkywellToken weather,bool shoulder)
        {
            yield return PickWeather(weather);
            var target=WeatherTargetForStation(SkywellDeerGrove.StationIndex);
            input=new KeyboardState(Key.E);yield return Wait(court.weatherHoldTime+.15f);
            Require(court.WeatherTargeting&&court.CameraRig.IsShoulderView==shoulder,"Held E retains the selected view while preparing "+weather+".");
            Require(TryWeatherPointer(target,out Vector2 point),"The production "+(shoulder?"automatic shoulder targeting":"fixed")+" view exposes a visible, unoccluded tree pixel.");
            pointerInput=new MouseState{position=point};yield return Wait(.1f);
            yield return ClickWeatherPointer(target,true);
            Require(court.Consoles[4].Token==weather,"A real pointer hit applies "+weather+" in the continuous branch.");
            yield return ReleaseTargeting();yield return Wait(1.3f);
            Require(!court.CameraRig.InteractionFramingActive&&court.CameraRig.IsShoulderView==shoulder,"Releasing E restores the same gameplay view after the tree cast.");
        }

        private static IEnumerator DeerRouteAroundTree(Vector3 target,bool mounted,string label)
        {
            // The deer can finish eating on either side. Walk around the real trunk,
            // instead of giving the input driver an obstructed straight-line waypoint.
            Vector3 center=court.Grove.Tree.position;
            Vector3 from=Vector3.ProjectOnPlane(court.Player.FeetPosition-center,Vector3.up).normalized;
            Vector3 to=Vector3.ProjectOnPlane(target-center,Vector3.up).normalized;
            float angle=Vector3.SignedAngle(from,to,Vector3.up);
            int segments=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(angle)/25));
            for(int i=0;i<=segments;i++)
            {
                Vector3 waypoint=center+Quaternion.AngleAxis(angle*i/segments,Vector3.up)*from*4.5f;
                waypoint.y=target.y;
                if(mounted)yield return DeerRouteDrive(waypoint,label+" around trunk "+i);
                else yield return DeerBranchWalk(waypoint,label+" around trunk "+i);
            }
            if(mounted)yield return DeerRouteDrive(target,label);
            else yield return DeerBranchWalk(target,label);
        }

        private static IEnumerator DeerBranchWalk(Vector3 target,string label)
        {
            var start=court.Player.FeetPosition;float low=Mathf.Min(start.y,target.y)-.15f,high=Mathf.Max(start.y,target.y)+.35f;
            navigationError=Vector3.zero;double deadline=EditorApplication.timeSinceStartup+20;
            while(Vector3.ProjectOnPlane(target-court.Player.FeetPosition,Vector3.up).magnitude>.25f)
            {
                if(EditorApplication.timeSinceStartup>deadline)
                {DiagnoseFloor(target);throw new Exception("Cave-path walk blocked: "+label+" feet="+court.Player.FeetPosition+" target="+target);}
                if(court.Player.FeetPosition.y<low||court.Player.FeetPosition.y>high||court.Player.IsSwimming)
                    throw new Exception("Cave-path traversal left its authored ground: "+label+" feet="+court.Player.FeetPosition);
                Vector3 direction=Vector3.ProjectOnPlane(target-court.Player.FeetPosition,Vector3.up).normalized;
                if(court.CameraRig.IsShoulderView&&Mathf.Abs(NavigationYawError(direction))>.75f){yield return FaceNavigationDirection(direction);continue;}
                Direction(direction);yield return null;
            }
            input=new KeyboardState();yield return Wait(.2f);
            Require(Mathf.Abs(court.Player.FeetPosition.y-target.y)<.15f,"WALK "+label+" reaches its authored height through actual collision.");
        }

        private static IEnumerator DeerRouteDrive(Vector3 target,string label,bool onFoot=false)
        {
            navigationError=Vector3.zero;double deadline=EditorApplication.timeSinceStartup+12;
            while(Vector3.ProjectOnPlane(target-court.Player.FeetPosition,Vector3.up).magnitude>.22f)
            {
                var ride=court.Grove.Ride;
                if(!ride.IsMounted&&!onFoot)
                    throw new Exception("Riding ended before the actual northern arrival during "+label);
                if(onFoot&&(!ride.FootRouteUnlocked||ride.IsMounted))throw new Exception("Foot-route ownership changed during "+label);
                if(EditorApplication.timeSinceStartup>deadline)
                {DiagnoseFloor(target);throw new Exception("Mounted approach blocked: "+label+" feet="+court.Player.FeetPosition+" target="+target);}
                Vector3 delta=Vector3.ProjectOnPlane(target-court.Player.FeetPosition,Vector3.up),direction=delta.normalized;
                if(court.CameraRig.IsShoulderView&&Mathf.Abs(NavigationYawError(direction))>.75f){yield return FaceNavigationDirection(direction);continue;}
                // Brake through released keys near an edge; never modify production velocity.
                // Pulse in seconds, not one frame in four: acceleration at high frame rates
                // can otherwise round each tiny movement away and strand this input driver.
                if(delta.magnitude<.85f&&Time.time%.18f>=.08f)input=new KeyboardState();else Direction(direction);
                yield return null;
            }
            input=new KeyboardState();yield return Wait(.30f);
            var finalRide=court.Grove.Ride;
            Require(onFoot?finalRide.FootDriver.IsGrounded&&finalRide.FootDriver.CanJumpHere:finalRide.IsGrounded,
                (onFoot?"FOOT ":"RIDE ")+label+" settles on actual supported ground.");
        }

        private static Vector3 DeerRoutePlatformTop(Collider platform)
        {
            Bounds bounds=platform.bounds;
            if(!platform.Raycast(new Ray(new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z),Vector3.down),out RaycastHit hit,bounds.size.y+2))
                throw new Exception("No real top surface on "+platform.name);
            return hit.point+Vector3.up*.03f;
        }
        private static bool DeerRouteFootprint(Collider expected,Vector3 feet,float footprintRadius=-1)
        {
            float inset=(footprintRadius>0?footprintRadius:court.Player.GetComponent<CharacterController>().radius)*.8f;
            foreach(var offset in new[]{Vector3.zero,Vector3.left*inset,Vector3.right*inset,Vector3.forward*inset,Vector3.back*inset})
            {
                var hits=Physics.RaycastAll(feet+offset+Vector3.up*.25f,Vector3.down,.65f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                    .Where(h=>!h.transform.IsChildOf(court.Player.transform)).OrderBy(h=>h.distance).ToArray();
                if(hits.Length==0||hits[0].collider!=expected||hits[0].normal.y<.7f||Mathf.Abs(hits[0].point.y-(feet.y-.03f))>.18f)return false;
            }
            return true;
        }
        private static Vector3 DeerRouteLaunch(Collider source,Vector3 direction,float extraInset=.25f)
        {
            Vector3 center=DeerRoutePlatformTop(source);
            float upper=DeerPlatformRadius(source.bounds,direction); // Shared fixture helper gives only a search bound.
            float last=0;
            for(float distance=0;distance<upper;distance+=.025f)
            {
                if(!DeerRouteFootprint(source,center+direction*distance))break;
                last=distance;
            }
            if(last<.8f)throw new Exception("No safe launch footprint on "+source.name);
            return center+direction*Mathf.Max(0,last-extraInset);
        }
        private static bool DeerRouteFootRunupFits(SkywellCloudFootTravel foot,Collider source,Vector3 launch,Vector3 direction,float length)
        {
            Vector3 candidate=launch-direction*length;
            foreach(var offset in new[]{Vector3.zero,Vector3.left*.35f,Vector3.right*.35f,Vector3.forward*.35f,Vector3.back*.35f})
                if(!foot.TryLocalSupport(candidate+offset,out var runupSupport)||runupSupport!=source)return false;
            for(float distance=0;distance<=length+.001f;distance+=.05f)
                if(!foot.TryLocalSupport(launch-direction*distance,out var pathSupport)||pathSupport!=source)return false;
            return true;
        }
        private static Vector3 DeerRouteFootRunup(SkywellCloudFootTravel foot,Collider source,Vector3 launch,Vector3 direction,string label)
        {
            // A large endpoint floor is not a global jumping area. Choose the longest
            // supported run-up within the production apron, allowing for real key-input
            // drift without expanding that area or moving the actor to the chosen point.
            for(float length=1.1f;length>=.45f;length-=.05f)
            {
                Vector3 candidate=launch-direction*length;
                if(!DeerRouteFootRunupFits(foot,source,launch,direction,length))continue;
                report.Add("FOOT RUN-UP "+label+" point="+candidate.ToString("F3")+" length="+length.ToString("F2")+" uses actual local support with 0.35m planning margin.");
                return candidate;
            }
            throw new Exception("No real local foot-support run-up fits the authored endpoint: "+label+" launch="+launch);
        }
        private static void DeerRouteFootEndpointLaunch(SkywellCloudFootTravel foot,Collider source,Collider destination,
            out Vector3 launch,out Vector3 landing,out Vector3 direction,out Vector3 runup)
        {
            // The centre of a broad endpoint floor can be far to one side of its cloud
            // opening. Sample its real surface near that opening instead of projecting
            // an unnecessarily diagonal launch ray from the whole floor's centre.
            Vector3 target=DeerRoutePlatformTop(destination);
            Bounds bounds=source.bounds;
            Vector3 anchor=bounds.ClosestPoint(target);
            float range;
            using(var serialized=new SerializedObject(foot))range=serialized.FindProperty("endpointActivationDistance").floatValue;
            const float step=.2f;
            int cells=Mathf.CeilToInt(range/step);
            var samples=new System.Collections.Generic.List<Vector3>();
            for(int x=-cells;x<=cells;x++)for(int z=-cells;z<=cells;z++)
            {
                Vector3 point=anchor+new Vector3(x*step,0,z*step);
                if(point.x<bounds.min.x||point.x>bounds.max.x||point.z<bounds.min.z||point.z>bounds.max.z)continue;
                samples.Add(point);
            }
            float radius=court.Player.GetComponent<CharacterController>().radius;
            foreach(var sample in samples.OrderBy(p=>Vector3.ProjectOnPlane(p-target,Vector3.up).sqrMagnitude))
            {
                if(!source.Raycast(new Ray(new Vector3(sample.x,bounds.max.y+1,sample.z),Vector3.down),out var hit,bounds.size.y+2)||hit.normal.y<.7f)continue;
                Vector3 candidate=hit.point+Vector3.up*.03f;
                if(!DeerRouteFootprint(source,candidate,radius+.15f)||
                    !foot.TryLocalSupport(candidate,out var actual)||actual!=source)continue;
                Vector3 toward=Vector3.ProjectOnPlane(target-candidate,Vector3.up).normalized;
                Vector3 next=DeerRouteLaunch(destination,-toward,.6f);
                toward=Vector3.ProjectOnPlane(next-candidate,Vector3.up).normalized;
                if(!DeerRouteFootRunupFits(foot,source,candidate,toward,1.1f))continue;
                launch=candidate;landing=next;direction=toward;runup=candidate-toward*1.1f;
                report.Add("FOOT ENDPOINT PLAN source="+source.name+" launch="+launch.ToString("F3")+
                    " runup="+runup.ToString("F3")+" target="+landing.ToString("F3")+
                    " distance="+Vector3.ProjectOnPlane(landing-launch,Vector3.up).magnitude.ToString("F3")+
                    " fullRunup=1.10m; actual top, local support and five-point footprints only.");
                return;
            }
            throw new Exception("No practical endpoint launch with a full supported run-up toward "+destination.name);
        }
        private static IEnumerator DeerRouteJump(Collider source,Collider destination,string label,Vector3? launchOverride=null,bool onFoot=false)
        {
            var ride=court.Grove.Ride;var foot=ride.FootDriver;
            Func<bool> grounded=()=>onFoot?foot.IsGrounded:ride.IsGrounded;
            Func<int> recoveryCount=()=>onFoot?foot.RecoveryCount:ride.RecoveryCount;
            Func<Collider> support=()=>onFoot?foot.LastSafePlatform:ride.LastSafePlatform;
            Vector3 destinationCenter=DeerRoutePlatformTop(destination);
            UnityEngine.Object motion=onFoot?(UnityEngine.Object)foot:ride;
            float airAcceleration,groundAcceleration,gravity,mouseSensitivity;
            using(var serialized=new SerializedObject(motion))
            {
                airAcceleration=(onFoot?foot.JumpAssist:ride.JumpAssist).airBrakingAcceleration;
                groundAcceleration=serialized.FindProperty("groundAcceleration").floatValue;
                gravity=serialized.FindProperty("gravity").floatValue;
            }
            // Read the production integrator only; the input fixture never writes it.
            var verticalField=motion.GetType().GetField("verticalVelocity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            using(var serialized=new SerializedObject(court.CameraRig))mouseSensitivity=serialized.FindProperty("shoulderSensitivity").floatValue;
            Require(airAcceleration>0&&groundAcceleration>0&&gravity>0&&verticalField!=null,
                "The authored "+(onFoot?"foot":"riding")+" motion exposes readable flight and ground braking parameters.");
            Vector3 direction=Vector3.ProjectOnPlane(destinationCenter-DeerRoutePlatformTop(source),Vector3.up).normalized;
            Vector3 launch=launchOverride??DeerRouteLaunch(source,direction);
            if(launchOverride.HasValue)direction=Vector3.ProjectOnPlane(destinationCenter-launch,Vector3.up).normalized;
            // Cross the actual gap to an inset, five-point safe patch on the near side.
            // Requiring the platform centre would test unnecessary extra flight distance
            // instead of whether its authored landing surface can be reached.
            Vector3 next=DeerRouteLaunch(destination,-direction,.6f);
            direction=Vector3.ProjectOnPlane(next-launch,Vector3.up).normalized;
            Vector3 runup=launch-direction*1.1f;
            if(onFoot)
            {
                var route=ride.SafePlatforms;
                if(!launchOverride.HasValue&&(source==route[0]||source==route[route.Length-1]))
                    DeerRouteFootEndpointLaunch(foot,source,destination,out launch,out next,out direction,out runup);
                else runup=DeerRouteFootRunup(foot,source,launch,direction,label);
            }
            Require(DeerRouteFootprint(source,launch)&&DeerRouteFootprint(source,runup),"Actual five-point source footprint supports the launch and run-up: "+label);
            Require(DeerRouteFootprint(destination,next),"The target landing uses the actual five-point destination footprint: "+label);
            yield return DeerRouteDrive(runup,label+" run-up",onFoot);
            yield return FaceNavigationDirection(direction);
            navigationError=Vector3.zero;int recoveries=recoveryCount();
            double deadline=EditorApplication.timeSinceStartup+4;
            while(Vector3.Dot(launch-court.Player.FeetPosition,direction)>.06f)
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Run-up did not reach launch: "+label);
                Direction(direction);yield return null;
            }
            Require(grounded()&&DeerRouteFootprint(source,court.Player.FeetPosition),"The moving "+(onFoot?"traveller":"deer")+" reaches a real safe launch footprint: "+label);
            if(onFoot)Require(foot.CanJumpHere,"The actual foot launch remains inside the production local jump area: "+label);
            report.Add("JUMP TAKEOFF "+label+" feet="+court.Player.FeetPosition.ToString("F3")+
                " velocity="+court.Player.PlanarVelocity.ToString("F3")+" plannedLaunch="+launch.ToString("F3")+
                " target="+next.ToString("F3"));SaveProgress();
            float launchY=court.Player.FeetPosition.y;
            Direction(direction);input.Press(Key.Space);yield return Wait(.075f);
            Require(!grounded()&&court.Player.FeetPosition.y>launchY+.15f,"Space produces an actual airborne ascent: "+label);
            if(onFoot)Require(foot.IsActive&&foot.IsAirborne&&court.Player.ExternalMotionOwnedBy(foot)&&!ride.IsMounted,
                "Real Space uses the foot driver and the single player collider, without remounting: "+label);
            float peak=court.Player.FeetPosition.y;deadline=EditorApplication.timeSinceStartup+3;
            bool braking=false;
            float landingRadius=court.Player.GetComponent<CharacterController>().radius;
            while(!grounded())
            {
                if(recoveryCount()!=recoveries)throw new Exception("A missed jump recovered during "+label);
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Jump never reached physical ground: "+label);
                peak=Mathf.Max(peak,court.Player.FeetPosition.y);
                Vector3 delta=Vector3.ProjectOnPlane(next-court.Player.FeetPosition,Vector3.up);
                Vector3 velocity=Vector3.ProjectOnPlane(court.Player.PlanarVelocity,Vector3.up);
                float speed=velocity.magnitude;
                // Solve the remaining descending flight to the real destination top,
                // then brake at its actual ground acceleration. Air-only braking
                // overestimates travel when the faster ground brake begins mid-stop.
                float vertical=(float)verticalField.GetValue(motion);
                float discriminant=vertical*vertical+2*gravity*(court.Player.FeetPosition.y-next.y);
                float flightTime=discriminant>=0?Mathf.Max(0,(vertical+Mathf.Sqrt(discriminant))/gravity):float.PositiveInfinity;
                float airBrakeTime=Mathf.Min(flightTime,speed/airAcceleration);
                float landingSpeed=Mathf.Max(0,speed-airAcceleration*airBrakeTime);
                float stoppingDistance=speed*airBrakeTime-.5f*airAcceleration*airBrakeTime*airBrakeTime+
                    landingSpeed*landingSpeed/(2*groundAcceleration);
                float remaining=speed>.05f?Vector3.Dot(delta,velocity/speed):delta.magnitude;
                Vector3 predictedStop=court.Player.FeetPosition+velocity.normalized*stoppingDistance;
                predictedStop.y=next.y;
                // Forward distance alone can trigger braking while sideways momentum
                // still points off the platform. Keep correcting through real input until
                // the predicted stopped footprint fits, with a small extra planning margin.
                // Final acceptance still uses the unchanged actual five-point footprint.
                bool safeStop=DeerRouteFootprint(destination,predictedStop,landingRadius+.15f);
                if(!braking&&safeStop&&(remaining<=stoppingDistance+.08f||delta.magnitude<.12f))
                {
                    braking=true;
                    report.Add("JUMP BRAKE "+label+" feet="+court.Player.FeetPosition.ToString("F3")+
                        " velocity="+velocity.ToString("F3")+" target="+next.ToString("F3")+
                        " remaining="+remaining.ToString("F3")+" stoppingDistance="+stoppingDistance.ToString("F3")+
                        " predictedStop="+predictedStop.ToString("F3")+" vertical="+vertical.ToString("F3")+
                        " flightTime="+flightTime.ToString("F3")+" landingSpeed="+landingSpeed.ToString("F3"));
                }
                if(braking)
                {
                    input=new KeyboardState();pointerInput=new MouseState{position=pointerInput.position};
                }
                else
                {
                    // Correct accumulated eight-way input drift toward the real safe patch.
                    // A small lateral-velocity lead counters sideways momentum without
                    // changing the intended landing or any acceptance tolerance.
                    Vector3 toward=delta.normalized;
                    Vector3 lateral=velocity-toward*Vector3.Dot(velocity,toward);
                    Vector3 steering=(delta-lateral*.2f).normalized;
                    Steer(steering);
                }
                yield return null;
            }
            input=new KeyboardState();pointerInput=new MouseState{position=pointerInput.position};
            Vector3 touchdown=court.Player.FeetPosition,touchdownVelocity=court.Player.PlanarVelocity;
            // A physical touchdown can precede full support. Use brief real key
            // pulses toward the centre; never accept the rim as a safe landing.
            // This remains below both drivers' unsupported-ground recovery window.
            double correctionDeadline=EditorApplication.timeSinceStartup+.55;
            while(grounded())
            {
                Vector3 velocity=Vector3.ProjectOnPlane(court.Player.PlanarVelocity,Vector3.up);
                Vector3 stop=court.Player.FeetPosition+velocity.normalized*(velocity.sqrMagnitude/(2*groundAcceleration));
                stop.y=next.y;
                if(DeerRouteFootprint(destination,court.Player.FeetPosition)&&
                    DeerRouteFootprint(destination,stop,landingRadius+.1f))break;
                if(recoveryCount()!=recoveries)throw new Exception("Landing correction recovered during "+label);
                if(EditorApplication.timeSinceStartup>correctionDeadline)break;
                if(velocity.magnitude>1.25f)
                {input=new KeyboardState();pointerInput=new MouseState{position=pointerInput.position};}
                else Steer(Vector3.ProjectOnPlane(destinationCenter-court.Player.FeetPosition,Vector3.up).normalized);
                yield return null;
            }
            input=new KeyboardState();pointerInput=new MouseState{position=pointerInput.position};
            yield return Wait(.35f);
            report.Add("JUMP LANDING "+label+" touchdown="+touchdown.ToString("F3")+
                " velocity="+touchdownVelocity.ToString("F3")+" settled="+court.Player.FeetPosition.ToString("F3")+
                " target="+next.ToString("F3")+" centre="+destinationCenter.ToString("F3")+" lastSafe="+(support()?support().name:"none"));SaveProgress();
            Require(recoveryCount()==recoveries&&support()==destination&&DeerRouteFootprint(destination,court.Player.FeetPosition),"Real landing footprint belongs to "+destination.name+" without recovery: "+label);
            report.Add("JUMP "+label+" source="+source.name+" destination="+destination.name+" launch="+launch+" landed="+court.Player.FeetPosition+" peak="+peak.ToString("F2"));SaveProgress();
            Require(court.Player.FeetPosition.y>=next.y-.12f,"Landing height matches the actual destination: "+label);

            void Steer(Vector3 steering)
            {
                if(!court.CameraRig.IsShoulderView){Direction(steering);return;}
                ValidateShoulderNavigation();
                if(Cursor.lockState!=CursorLockMode.Locked)
                    throw new Exception("Shoulder jump lost real mouse control: "+NavigationInputState());
                float turn=Mathf.Clamp(NavigationYawError(steering),-20,20);
                pointerInput=new MouseState{position=pointerInput.position,
                    delta=new Vector2(turn/Mathf.Max(.001f,mouseSensitivity),0)};
                input=new KeyboardState(Key.W);
            }
        }
    }
}

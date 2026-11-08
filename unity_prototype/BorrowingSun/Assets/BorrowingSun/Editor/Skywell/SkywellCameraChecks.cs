using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    public static partial class SkywellChecks
    {
        [MenuItem("Borrowing Sun/Skywell/Check fixed and shoulder camera input")]
        public static void RunCameraModes()=>StartCheck("SKYWELL_CAMERA_MODES",CameraModesSequence());

        private static IEnumerator EnsureCameraMode(bool shoulder)
        {
            Require(court.CameraRig.FixedAndShoulderEnabled,"The L5 camera uses its authored fixed/shoulder opt-in profile.");
            if(court.CameraRig.IsShoulderView!=shoulder)yield return Press(Key.Z);
            yield return Wait(.4f);
            Require(court.CameraRig.IsShoulderView==shoulder,"Real Z input selects "+(shoulder?"shoulder":"fixed")+" gameplay view.");
        }
        private static IEnumerator CameraMousePulse(Vector2 delta,ushort buttons=0,Vector2 scroll=default)
        {
            // Exactly one input frame. Persisting a nonzero delta during Wait would invent repeated motion.
            Vector2 center=new Vector2(Screen.width*.5f,Screen.height*.5f);
            pointerInput=new MouseState{position=center,delta=delta,buttons=buttons,scroll=scroll};yield return null;
            pointerInput=new MouseState{position=center};yield return Wait(.2f);
        }
        private static IEnumerator AimShoulderAtWeatherTarget(SkywellWeatherTarget target)
        {
            var rig=court.CameraRig;var camera=rig.ViewCamera;
            Require(rig.IsShoulderView,"Target framing uses the actual shoulder mode.");
            if(Cursor.lockState!=CursorLockMode.Locked)
            {
                yield return CameraMousePulse(Vector2.zero,1);yield return Wait(.2f);
            }
            Require(Cursor.lockState==CursorLockMode.Locked,"Shoulder target framing has acquired the real game pointer lock.");
            Vector3 aim=Vector3.zero;float score=float.PositiveInfinity;
            foreach(Vector3 world in WeatherSurfaceSamples(target))
            {
                Vector3 direction=world-camera.transform.position;
                if(direction.sqrMagnitude<.01f||!target.TryHit(new Ray(camera.transform.position,direction),out _))continue;
                float candidate=Vector3.Angle(camera.transform.forward,direction);
                if(candidate<score){score=candidate;aim=direction.normalized;}
            }
            if(float.IsPositiveInfinity(score))throw new Exception("No physically visible target surface for shoulder framing: "+target.DisplayName);
            float sensitivity;Vector2 limits;
            using(var serialized=new SerializedObject(rig))
            {sensitivity=serialized.FindProperty("shoulderSensitivity").floatValue;limits=serialized.FindProperty("shoulderPitchLimits").vector2Value;}
            float desiredYaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;
            float desiredPitch=Mathf.Clamp(-Mathf.Asin(aim.y)*Mathf.Rad2Deg,Mathf.Min(limits.x,limits.y),Mathf.Max(limits.x,limits.y));
            float yaw=Mathf.Clamp(Mathf.DeltaAngle(camera.transform.eulerAngles.y,desiredYaw),-75,75);
            float pitch=Mathf.Clamp(Mathf.DeltaAngle(camera.transform.eulerAngles.x,desiredPitch),-30,30);
            yield return CameraMousePulse(new Vector2(yaw,-pitch)/Mathf.Max(.001f,sensitivity));
            report.Add("MOUSE AIM target="+target.DisplayName+" yawDelta="+yaw.ToString("F2")+" pitchDelta="+pitch.ToString("F2")+"; calculated from actual pose, never assigned to camera transform.");SaveProgress();
        }
        private static bool SameCameraPose(Vector3 position,Quaternion rotation,float fov,float positionTolerance=.025f)
        {
            var camera=court.CameraRig.ViewCamera;
            return Vector3.Distance(position,camera.transform.position)<positionTolerance&&Quaternion.Angle(rotation,camera.transform.rotation)<.03f&&Mathf.Abs(fov-camera.fieldOfView)<.01f;
        }
        private static void RequireShoulderFacing(string label)
        {
            // Retain this shared helper's signature for the riding checks. The current
            // contract is independent orbit, not the older camera-owned strafe heading.
            Require(court.CameraRig.IsShoulderView&&!court.Player.ViewFacingActive,
                label+": free shoulder orbit leaves character facing owned by locomotion.");
        }
        private static IEnumerator CameraModesSequence()
        {
            report.Add("EXPLICIT CAMERA FIXTURES: authored tower/collector/story/observation placements. Real Z, Mouse delta, Tab, held E and pointer hits drive modes and interactions. Mouse deltas may be calculated from actual camera angles; no camera Transform, private look angle, weather or item state is assigned. The near-wall check creates one temporary solid BoxCollider and cleans it up; it is not a sweep of every authored wall. Cursor tests require the focused actual GameView.");
            court.ResetCourt();yield return Wait(.4f);yield return WeatherFixturePlace(1);yield return Wait(1.5f);
            var rig=court.CameraRig;var camera=rig.ViewCamera;
            Require(rig.FixedAndShoulderEnabled&&!rig.IsShoulderView,"L5 reset starts in the opted-in fixed gameplay mode.");
            Vector3 entryFacing=court.Player.FacingDirection;
            input=new KeyboardState(Key.Z);yield return Wait(.3f);
            Require(rig.IsShoulderView,"A real Z press changes fixed view to shoulder view.");
            Require(Vector3.Angle(entryFacing,court.Player.FacingDirection)<.1f,"Entering shoulder view uses the character's current facing without turning the character toward an old camera yaw.");
            Require(Vector3.Angle(entryFacing,Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up))<.1f,
                "The first shoulder entry after reset begins behind the character's existing heading.");
            RequireShoulderFacing("First real Z entry");
            yield return Wait(.7f);Require(rig.IsShoulderView,"Continuing to hold Z does not repeatedly toggle the mode.");
            input=new KeyboardState();yield return Wait(.2f);yield return Press(Key.Z);yield return Wait(.7f);
            Require(!rig.IsShoulderView,"A separate Z press returns to fixed view.");
            Capture(Path.Combine(Folder,"camera-fixed-view.png"));yield return Wait(.15f);

            Vector3 fixedPosition=camera.transform.position;Quaternion fixedRotation=camera.transform.rotation;float fixedFov=camera.fieldOfView;
            input=new KeyboardState(Key.X,Key.F,Key.C,Key.V);yield return Wait(.5f);input=new KeyboardState();yield return Wait(.2f);
            yield return CameraMousePulse(new Vector2(120,-65),2,new Vector2(0,120));
            Require(!rig.IsShoulderView&&!rig.IsOverview&&!rig.IsObserving&&SameCameraPose(fixedPosition,fixedRotation,fixedFov),"Removed X/F/C/V, right-button motion and wheel input cannot orbit, zoom or replace the fixed L5 view.");

            yield return EnsureCameraMode(true);Require(Cursor.lockState==CursorLockMode.Locked,"Shoulder entry locks the actual game cursor.");
            Quaternion shoulderBefore=camera.transform.rotation;Vector3 facingBeforeLook=court.Player.FacingDirection;
            yield return CameraMousePulse(new Vector2(70,25));
            Require(Quaternion.Angle(shoulderBefore,camera.transform.rotation)>3,"Raw mouse movement without a mouse button changes shoulder look.");
            Require(Vector3.Angle(facingBeforeLook,court.Player.FacingDirection)<.1f,"Stationary mouse orbit does not turn the character.");
            RequireShoulderFacing("Independent mouse orbit");
            Capture(Path.Combine(Folder,"camera-shoulder-view.png"));yield return Wait(.15f);
            yield return ShoulderFreeOrbitFixture();
            yield return ShoulderFacingMovementFixture();
            yield return ShoulderFacingDisableLifecycleFixture();
            yield return WeatherFixturePlace(1);yield return EnsureCameraMode(true);
            yield return Press(Key.Escape);Require(rig.IsShoulderView&&Cursor.lockState!=CursorLockMode.Locked,"Escape releases the pointer while retaining shoulder mode.");
            yield return CameraMousePulse(Vector2.zero,1);yield return Wait(.2f);
            Require(Cursor.lockState==CursorLockMode.Locked,"A real plain-world click resumes shoulder pointer look.");

            yield return PickWeather(SkywellToken.Rain);
            Quaternion beforeTargetingRotation=camera.transform.rotation;Vector3 beforeTargetingPosition=camera.transform.position;float beforeTargetingFov=camera.fieldOfView;
            var target=WeatherTargetForStation(1);yield return BeginTargeting(target);
            Require(rig.InteractionFramingActive&&Vector3.Distance(beforeTargetingPosition,camera.transform.position)>3,"Held E automatically pulls the shoulder camera out to frame available physical objects.");
            Require(rig.IsShoulderView&&Cursor.lockState!=CursorLockMode.Locked,"Held E frees the pointer in shoulder view.");
            Vector3 frozenPosition=camera.transform.position,frozenFacing=court.Player.FacingDirection;Quaternion frozenRotation=camera.transform.rotation;float frozenFov=camera.fieldOfView;
            input=new KeyboardState(Key.E,Key.Z);yield return CameraMousePulse(new Vector2(160,-90),0,new Vector2(0,120));
            input=new KeyboardState(Key.E);yield return Wait(.15f);
            Require(rig.IsShoulderView&&SameCameraPose(frozenPosition,frozenRotation,frozenFov),"Pointer movement, Z and wheel cannot move the frozen camera during weather targeting.");
            Require(Vector3.Angle(frozenFacing,court.Player.FacingDirection)<.1f,"Targeting pointer movement also leaves the character's facing unchanged.");
            yield return ClickWeatherPointer(target,true);
            yield return Wait(1.3f); // Observe the existing 1.2-second weather transition, as in SetWeather.
            Require(court.Consoles[1].Token==SkywellToken.Rain&&court.Consoles[1].Zone.IsRain,"A real visible surface click casts Rain while shoulder targeting is frozen.");
            Require(SameCameraPose(frozenPosition,frozenRotation,frozenFov),"Successful weather casting keeps the targeting pose frozen.");
            Capture(Path.Combine(Folder,"camera-shoulder-weather-targeting.png"));yield return ReleaseTargeting();yield return Wait(.3f);
            Require(rig.IsShoulderView&&!rig.InteractionFramingActive&&Cursor.lockState==CursorLockMode.Locked&&Quaternion.Angle(beforeTargetingRotation,camera.transform.rotation)<.03f,"Releasing E resumes shoulder look at the same yaw/pitch and locks the pointer.");
            Require(SameCameraPose(beforeTargetingPosition,beforeTargetingRotation,beforeTargetingFov,.05f),
                "Releasing E restores the stationary traveller's original orbit position and projection.");
            RequireShoulderFacing("After held-E release");
            Require(Vector3.Angle(frozenFacing,court.Player.FacingDirection)<.1f,"Leaving weather targeting restores orbit without turning the stationary character.");
            yield return CameraMousePulse(new Vector2(-55,0));
            Require(Quaternion.Angle(beforeTargetingRotation,camera.transform.rotation)>3,"New mouse movement works after held-E targeting ends.");
            yield return WeatherFixtureLeave(1);

            yield return AutomaticTargetFramingFixture();
            yield return ShoulderNearWallFixture();
            yield return CameraModalRestorationFixture(false);
            yield return CameraModalRestorationFixture(true);
            yield return Press(Key.R);yield return Wait(.4f);
            Require(!rig.IsShoulderView&&Cursor.lockState!=CursorLockMode.Locked&&!rig.InputSuppressed,"R restores fixed view and releases the pointer without a modal lock.");
            CheckReset();
        }
        private static IEnumerator ShoulderFreeOrbitFixture()
        {
            var rig=court.CameraRig;var camera=rig.ViewCamera;
            float sensitivity;Vector2 pitchLimits;
            using(var serialized=new SerializedObject(rig))
            {sensitivity=serialized.FindProperty("shoulderSensitivity").floatValue;pitchLimits=serialized.FindProperty("shoulderPitchLimits").vector2Value;}
            Vector3 feet=court.Player.FeetPosition,facing=court.Player.FacingDirection;
            Quaternion original=camera.transform.rotation;
            float yaw=camera.transform.eulerAngles.y,pitch=Mathf.DeltaAngle(0,camera.transform.eulerAngles.x);
            report.Add("FREE ORBIT FIXTURE: six real 60-degree mouse yaw steps make a full turn; real vertical mouse input reaches both authored pitch limits. The traveller remains stationary, and solid collision still limits the camera boom.");SaveProgress();
            for(int i=0;i<6;i++)
            {
                yield return CameraMousePulse(new Vector2(60/Mathf.Max(.001f,sensitivity),0));
                Require(Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.y,yaw+60*(i+1)))<.1f,
                    "Real mouse yaw freely orbits past "+(60*(i+1))+" degrees without a heading clamp.");
                Require(Vector3.Distance(feet,court.Player.FeetPosition)<.04f&&Vector3.Angle(facing,court.Player.FacingDirection)<.1f,
                    "Stationary orbit changes neither character position nor facing at step "+i);
                RequireShoulderCameraOutsideSolids("horizontal orbit step "+i);
                if(i==2)Capture(Path.Combine(Folder,"camera-shoulder-front-orbit.png"));
            }
            Require(Quaternion.Angle(original,camera.transform.rotation)<.03f,"A real full turn returns to the original camera orientation.");
            float min=Mathf.Clamp(Mathf.Min(pitchLimits.x,pitchLimits.y),-85,85);
            float max=Mathf.Clamp(Mathf.Max(pitchLimits.x,pitchLimits.y),-85,85);
            yield return CameraMousePulse(new Vector2(0,-(max+20-pitch)/Mathf.Max(.001f,sensitivity)));
            Require(Mathf.Abs(Mathf.DeltaAngle(0,camera.transform.eulerAngles.x)-max)<.1f,
                "Downward mouse orbit stops at the authored upper pitch limit.");
            Vector3 high=camera.transform.position;
            RequireShoulderCameraOutsideSolids("downward orbit limit");
            yield return CameraMousePulse(new Vector2(0,-(min-20-max)/Mathf.Max(.001f,sensitivity)));
            Require(Mathf.Abs(Mathf.DeltaAngle(0,camera.transform.eulerAngles.x)-min)<.1f,
                "Upward mouse orbit stops at the authored lower pitch limit.");
            Require(Mathf.Abs(high.y-camera.transform.position.y)>.2f,
                "Pitch moves the camera vertically around the traveller instead of only swivelling its aim.");
            RequireShoulderCameraOutsideSolids("upward orbit limit near the floor");
            Require(Vector3.Distance(feet,court.Player.FeetPosition)<.04f&&Vector3.Angle(facing,court.Player.FacingDirection)<.1f,
                "Vertical orbit leaves the stationary character unchanged.");
            yield return CameraMousePulse(new Vector2(0,-(pitch-min)/Mathf.Max(.001f,sensitivity)));
            Require(Quaternion.Angle(original,camera.transform.rotation)<.03f,"Real mouse input restores the original yaw and pitch after the limit checks.");
        }
        private static void RequireShoulderCameraOutsideSolids(string label)
        {
            bool clear=true;
            foreach(var solid in Physics.OverlapSphere(court.CameraRig.ViewCamera.transform.position,.05f,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(!solid.transform.IsChildOf(court.Player.transform)){clear=false;break;}
            Require(clear,"The camera stays outside solid scene geometry during "+label+".");
        }

        private static IEnumerator AutomaticTargetFramingFixture()
        {
            report.Add("AUTO FRAMING FIXTURE: explicit placements in each authored weather region; real Z/Tab/held E. No manual mouse aiming, camera pose assignment or artificial target hit. Escape and release must return the saved shoulder heading.");
            int[] stations={0,1,3,4,2};
            foreach(int station in stations)
            {
                if(station==2)
                {
                    // The surface dock uses ordinary supported Walking. The next declared
                    // placement fixture supplies its physical location, without a lift interaction.
                    court.ResetCourt();yield return Wait(.4f);
                    Require(court.Journey.SurfaceConfigured,"The authored surface-dock route is available for its camera fixture.");
                }
                yield return WeatherFixturePlace(station);yield return EnsureCameraMode(true);
                yield return PickWeather(SkywellToken.Rain);
                var rig=court.CameraRig;var camera=rig.ViewCamera;var target=WeatherTargetForStation(station);
                Quaternion before=camera.transform.rotation;Vector3 facing=court.Player.FacingDirection;
                input=new KeyboardState(Key.E);yield return Wait(court.weatherHoldTime+.2f);
                Require(court.WeatherTargeting&&rig.InteractionFramingActive,"Actual held E automatically opens the framing view at station "+station);
                Require(TryWeatherPointer(target,out _),"Automatic framing exposes a real visible clickable surface at station "+station+" without manual aiming.");
                Require(Vector3.Angle(facing,court.Player.FacingDirection)<.1f,"Automatic target framing does not rotate the player at station "+station);
                Capture(Path.Combine(Folder,"camera-auto-target-"+station+".png"));yield return Wait(.1f);
                input=new KeyboardState(Key.E,Key.Escape);yield return Wait(.15f);input=new KeyboardState();yield return Wait(.3f);
                Require(!court.WeatherTargeting&&!rig.InteractionFramingActive&&rig.IsShoulderView&&Quaternion.Angle(before,camera.transform.rotation)<.03f,"Escape restores the saved shoulder heading at station "+station);
                yield return WeatherFixtureLeave(station);
            }
            court.ResetCourt();yield return Wait(.4f);
        }
        private static IEnumerator ShoulderFacingDisableLifecycleFixture()
        {
            report.Add("PLAY LIFECYCLE FIXTURE: actual CourtPlayer disable/re-enable on the dry platform. The camera may freely orbit a stationary disabled traveller; it must never acquire movement-facing ownership or snap back on re-enable. No reflected lifecycle invocation or facing assignment.");SaveProgress();
            yield return EnsureCameraMode(true);
            var actor=court.Player;var rig=court.CameraRig;
            RequireShoulderFacing("Before actual movement disable");
            Vector3 before=actor.FacingDirection;
            Quaternion disabledOrbit=rig.ViewCamera.transform.rotation;
            try
            {
                actor.enabled=false;
                Require(!actor.ViewFacingActive,"Actual Play OnDisable immediately releases the player's view-facing binding.");
                yield return Wait(.3f);
                Require(!actor.ViewFacingActive,"The active shoulder camera does not reacquire movement binding while the player component remains disabled.");
                Require(Vector3.Angle(before,actor.FacingDirection)<.1f,"Disabling the traveller preserves its actual character facing.");
                Quaternion orbit=rig.ViewCamera.transform.rotation;
                yield return CameraMousePulse(new Vector2(140,35));
                Require(rig.IsShoulderView&&Quaternion.Angle(orbit,rig.ViewCamera.transform.rotation)>10,
                    "The mouse can orbit the disabled stationary traveller without a character-heading snap.");
                Require(Vector3.Angle(before,actor.FacingDirection)<.1f,"Orbiting during disabled travel still preserves character facing.");
                disabledOrbit=rig.ViewCamera.transform.rotation;
            }
            finally{actor.enabled=true;}
            yield return Wait(.3f);
            RequireShoulderFacing("After actual movement re-enable");
            Require(Vector3.Angle(before,actor.FacingDirection)<.1f,"Re-enabled movement leaves the stationary traveller's heading unchanged.");
            Require(Quaternion.Angle(disabledOrbit,rig.ViewCamera.transform.rotation)<.03f,
                "Re-enabling the traveller does not replace the freely chosen orbit.");
        }
        private static IEnumerator ShoulderFacingMovementFixture()
        {
            report.Add("FREE ORBIT MOVEMENT FIXTURE: one explicit dry-entrance placement. Real fixed-view S changes character facing; returning with Z preserves the earlier orbit. Real W/S/A/D follow camera-planar axes and turn the character toward actual travel, through existing floor support and collision. No facing, camera or position correction during the steps.");SaveProgress();
            Quaternion storedOrbit=court.CameraRig.ViewCamera.transform.rotation;
            yield return EnsureCameraMode(false);
            court.Player.Teleport(court.Player.SpawnPosition);yield return Wait(.6f);
            input=new KeyboardState(Key.S);yield return Wait(.16f);input=new KeyboardState();yield return Wait(.2f);
            Vector3 currentFacing=court.Player.FacingDirection;
            Require(!court.Player.ViewFacingActive,"Fixed view releases the camera-facing movement binding.");
            yield return EnsureCameraMode(true);
            Require(Vector3.Angle(currentFacing,court.Player.FacingDirection)<.1f,"Re-entering shoulder mode does not rotate the character.");
            Require(Quaternion.Angle(storedOrbit,court.CameraRig.ViewCamera.transform.rotation)<.03f,
                "Z restores the saved independent orbit despite character turns in fixed view.");
            RequireShoulderFacing("Z after fixed-view movement");
            Vector3 forward=Vector3.ProjectOnPlane(court.CameraRig.ViewCamera.transform.forward,Vector3.up).normalized,right=Vector3.Cross(Vector3.up,forward);
            Key[] keys={Key.W,Key.S,Key.A,Key.D};Vector3[] directions={forward,-forward,-right,right};
            for(int i=0;i<keys.Length;i++)
            {
                Vector3 before=court.Player.FeetPosition;input=new KeyboardState(keys[i]);yield return Wait(.18f);input=new KeyboardState();yield return Wait(.2f);
                Vector3 moved=Vector3.ProjectOnPlane(court.Player.FeetPosition-before,Vector3.up);
                float along=Vector3.Dot(moved,directions[i]);Vector3 across=moved-directions[i]*along;
                Require(along>.3f&&across.magnitude<.12f,"Actual "+keys[i]+" movement follows its camera-planar axis through the CharacterController.");
                Require(Vector3.Angle(moved,court.Player.FacingDirection)<1,
                    "Actual "+keys[i]+" movement turns character facing toward physical travel instead of running backwards.");
                Require(Quaternion.Angle(storedOrbit,court.CameraRig.ViewCamera.transform.rotation)<.03f,
                    "Character movement does not overwrite the chosen camera orbit: "+keys[i]);
                RequireShoulderFacing("After "+keys[i]+" movement");
            }
        }
        private static IEnumerator CameraModalRestorationFixture(bool story)
        {
            if(story)yield return StoryFixturePlace(0);
            else{court.Player.Teleport(court.Observation.transform.position+new Vector3(0,.03f,-.8f));yield return Wait(.5f);}
            yield return EnsureCameraMode(true);yield return CameraMousePulse(new Vector2(40,-20));
            var rig=court.CameraRig;var camera=rig.ViewCamera;Quaternion before=camera.transform.rotation;Vector3 facing=court.Player.FacingDirection;
            yield return Press(Key.E);yield return Wait(.5f);
            Require(story?court.Story.DialogueOpen:court.Observation.IsOpen,(story?"Story":"Observation")+" opens through actual short E.");
            Require(rig.IsShoulderView&&Cursor.lockState!=CursorLockMode.Locked,"The modal preserves shoulder selection and frees the pointer.");
            input=new KeyboardState(Key.Z);yield return CameraMousePulse(new Vector2(150,60));input=new KeyboardState();yield return Wait(.15f);
            Require(rig.IsShoulderView,"Z beneath the modal cannot change the selected gameplay mode.");
            Require(Vector3.Angle(facing,court.Player.FacingDirection)<.1f,"Mouse movement beneath the modal does not turn the character.");
            input=new KeyboardState(Key.Escape,Key.Z);yield return Wait(.15f);input=new KeyboardState();yield return Wait(.5f);
            Require(!court.Story.DialogueOpen&&!court.Observation.IsOpen&&!rig.InputSuppressed&&rig.IsShoulderView,"Closing-frame Escape+Z closes only the modal and preserves shoulder mode.");
            Require(Quaternion.Angle(before,camera.transform.rotation)<.03f,"The modal restores actual gameplay yaw and pitch after mouse movement and a closing-frame Z press.");
            RequireShoulderFacing("After "+(story?"Story":"Observation")+" close");
            yield return CameraMousePulse(new Vector2(35,0));
            Require(Quaternion.Angle(before,camera.transform.rotation)>2,"Shoulder mouse look resumes after modal closure.");
        }
        private static IEnumerator ShoulderNearWallFixture()
        {
            // Safe authoring platform, then a declared temporary physical occluder; no camera pose injection.
            yield return WeatherFixturePlace(1);yield return EnsureCameraMode(true);yield return Wait(.7f);
            var rig=court.CameraRig;var camera=rig.ViewCamera;float height;
            using(var serialized=new SerializedObject(rig))height=serialized.FindProperty("shoulderHeight").floatValue;
            Vector3 pivot=court.Player.FeetPosition+Vector3.up*height,clear=camera.transform.position;
            Vector3 boom=clear-pivot;
            for(int attempt=0;attempt<8&&boom.magnitude<=1.3f;attempt++)
            {
                float sensitivity;
                using(var serialized=new SerializedObject(rig))sensitivity=serialized.FindProperty("shoulderSensitivity").floatValue;
                yield return CameraMousePulse(new Vector2(45/Mathf.Max(.001f,sensitivity),0));
                clear=camera.transform.position;boom=clear-pivot;
            }
            Require(boom.magnitude>1.3f,"The near-wall fixture begins with a measurable unobstructed shoulder boom after real mouse framing.");
            GameObject wall=new GameObject("Camera_check_temporary_wall"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                wall.transform.SetPositionAndRotation(pivot+boom.normalized*.9f,Quaternion.LookRotation(boom.normalized,Vector3.up));
                var box=wall.AddComponent<BoxCollider>();box.size=new Vector3(3,3,.16f);Physics.SyncTransforms();yield return Wait(.4f);
                float projected=Vector3.Dot(camera.transform.position-pivot,boom.normalized);
                Require(projected<.82f&&projected>=-.01f,"An actual solid wall shortens the shoulder boom on the player side of the obstacle.");
                Require(Vector3.Distance(box.ClosestPoint(camera.transform.position),camera.transform.position)>.05f,"The camera remains outside the actual oriented near-wall collider with visible clearance.");
                Capture(Path.Combine(Folder,"camera-near-wall-fixture.png"));
            }
            finally{UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();}
            yield return Wait(.4f);
            Require(Vector3.Distance(clear,camera.transform.position)<.04f,"Removing the declared fixture restores the original shoulder boom.");
        }
    }
}

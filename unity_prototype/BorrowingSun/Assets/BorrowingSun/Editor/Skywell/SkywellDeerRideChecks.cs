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
        private static IEnumerator DeerRideInputSequence()
        {
            var ride=court.Grove.Ride;var actor=court.Player;var body=actor.GetComponent<CharacterController>();
            report.Add("DEER RIDE INPUT FIXTURE: after real weather/gratitude unlock, one explicit unmounted placement reaches the authored deer interaction point. All mounted movement, jumping, airborne E refusal and intentional fall thereafter use actual WASD/Space/E/R; no mounted teleport, progress injection or velocity assignment. One actual island-to-first-cloud jump is verified; later clouds are not covered by this fixture.");SaveProgress();
            Require(ride.IsAvailable&&ride.SafePlatforms.Length>=4,"Riding starts only after the actual grove unlock and authored cloud collision are available.");
            float normalRadius=body.radius,normalHeight=body.height;
            yield return EnsureCameraMode(false);
            actor.Teleport(ride.MountPoint.position+Vector3.up*.03f);yield return Wait(.3f);
            yield return Press(Key.E);yield return Wait(.3f);
            Require(ride.IsMounted&&actor.enabled&&actor.ExternalMotionOwnedBy(ride),"Actual short E mounts the deer and delegates motion while keeping player input enabled.");
            Require(actor.GetComponentsInChildren<CharacterController>().Length==1,"Mounted collision uses the existing single CharacterController.");
            var humanoid=actor.GetComponentInChildren<StandardHumanoidVisual>();
            Require(humanoid&&humanoid.IsRiding,"The existing humanoid uses the saddle pose while mounted.");
            var island=ride.SafePlatforms[0];var firstCloud=ride.SafePlatforms[1];
            Vector3 center=island.bounds.center;center.y=island.bounds.max.y+.03f;
            // The south side of the authored tree is a clear run-up area, away from its real trunk collider.
            yield return DeerDriveTo(center+new Vector3(2.5f,0,-2),"clear south side of grove");
            yield return DeerDriveTo(center+new Vector3(0,0,-2),"open run-up area");
            Vector3 before=actor.FeetPosition;
            input=new KeyboardState(Key.D);yield return Wait(.15f);input=new KeyboardState();yield return Wait(.25f);
            Require(Vector3.ProjectOnPlane(actor.FeetPosition-before,Vector3.up).magnitude>.25f&&ride.IsMounted,"Fixed-view real movement rides the deer rather than leaving the rider behind.");
            yield return EnsureCameraMode(true);yield return FaceNavigationDirection(Vector3.back);
            Vector3 viewForward=Vector3.ProjectOnPlane(court.CameraRig.ViewCamera.transform.forward,Vector3.up).normalized;
            Quaternion cameraBefore=court.CameraRig.ViewCamera.transform.rotation;before=actor.FeetPosition;
            input=new KeyboardState(Key.S);yield return Wait(.15f);input=new KeyboardState();yield return Wait(.25f);
            Require(Vector3.Dot(actor.FeetPosition-before,viewForward)<-.2f
                &&Vector3.Angle(actor.FacingDirection,-viewForward)<8
                &&Quaternion.Angle(cameraBefore,court.CameraRig.ViewCamera.transform.rotation)<.1f,
                "Free-shoulder S rides along camera-back and turns the deer with movement, without rotating the camera.");
            RequireShoulderFacing("Mounted shoulder movement");
            yield return EnsureCameraMode(false);
            Vector3 next=firstCloud.bounds.center;next.y=firstCloud.bounds.max.y+.03f;
            Vector3 direction=Vector3.ProjectOnPlane(next-center,Vector3.up).normalized;
            float radius=DeerPlatformRadius(island.bounds,direction);
            Vector3 launch=center+direction*(radius-.85f),runup=launch-direction*1.4f;
            yield return DeerDriveTo(runup,"first cloud run-up");
            yield return EnsureCameraMode(true);yield return FaceNavigationDirection(direction);
            int recoveries=ride.RecoveryCount;
            double deadline=EditorApplication.timeSinceStartup+3;
            input=new KeyboardState(Key.W);
            while(Vector3.Dot(launch-actor.FeetPosition,direction)>.04f)
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Deer run-up did not reach the real launch edge.");
                yield return null;
            }
            float launchY=actor.FeetPosition.y;
            input=new KeyboardState(Key.W,Key.Space);yield return Wait(.10f);
            Require(ride.IsMounted&&!ride.IsGrounded&&actor.FeetPosition.y>launchY+.2f,"Real Space produces a controllable airborne jump from the physical island.");
            input=new KeyboardState(Key.W,Key.E);yield return Wait(.06f);input=new KeyboardState(Key.W);yield return Wait(.06f);
            Require(ride.IsMounted,"Actual short E while airborne refuses to dismount the rider.");
            yield return WaitUntil(()=>ride.IsGrounded,2,"The first jump lands through the existing character collision body");
            input=new KeyboardState();yield return Wait(.35f);
            Require(ride.RecoveryCount==recoveries&&ride.LastSafePlatform==firstCloud,"The actual jump reaches the first cloud and records its real stable support without recovery.");
            RequireShoulderFacing("After first cloud landing");
            Capture(Path.Combine(Folder,"deer-first-cloud-shoulder.png"));

            // A real unsupported step tests recovery; it does not assign a falling pose or call recovery.
            yield return FaceNavigationDirection(Vector3.back);
            input=new KeyboardState(Key.W);yield return WaitUntil(()=>ride.RecoveryCount>recoveries,4,"Walking beyond cloud collision actually falls and recovers to the last safe cloud");
            input=new KeyboardState();yield return Wait(.35f);
            Require(ride.IsMounted&&ride.IsGrounded&&ride.LastSafePlatform==firstCloud,"Fall recovery preserves riding and returns to the most recent safe cloud, not the original island.");
            yield return Press(Key.E);yield return Wait(.2f);
            Require(!ride.IsMounted&&!actor.IsExternallyDriven&&!humanoid.IsRiding,"Real grounded E restores normal movement and removes the saddle pose.");
            Require(Mathf.Abs(body.radius-normalRadius)<.001f&&Mathf.Abs(body.height-normalHeight)<.001f,"Dismount restores the original walking capsule dimensions.");
            yield return Press(Key.E);yield return Wait(.2f);
            Require(ride.IsMounted,"The parked deer can be mounted again through real short E.");
            report.Add("RIDE OWNER LIFECYCLE FIXTURE: disable only the actual SkywellController component during Play after real E remount. Ride must release on the next game frame; no availability assignment, reflected lifecycle invocation or direct release call is used.");SaveProgress();
            yield return Wait(.8f);
            float mountedCameraHeight=court.CameraRig.ViewCamera.transform.position.y-actor.FeetPosition.y;
            float ridingCameraOffset;
            using(var serialized=new SerializedObject(ride))ridingCameraOffset=serialized.FindProperty("cameraHeightOffset").floatValue;
            int disabledAt=Time.frameCount;
            try
            {
                court.enabled=false;
                yield return WaitUntil(()=>Time.frameCount>disabledAt&&!ride.IsMounted,1,"The disabled gameplay owner releases the active ride on a real game frame");
                Require(actor.enabled&&!actor.IsExternallyDriven&&!humanoid.IsRiding,"Disabling the owner releases motion ownership and saddle presentation while normal player movement remains enabled.");
                Require(Mathf.Abs(body.radius-normalRadius)<.001f&&Mathf.Abs(body.height-normalHeight)<.001f,"Owner disable restores the original walking capsule dimensions.");
            }
            finally{court.enabled=true;}
            // Controller disable freezes the camera pose. Verify the cleared riding offset only
            // after the owner's normal pointer lifecycle has resumed camera following.
            yield return Wait(.8f);
            float walkingCameraHeight=court.CameraRig.ViewCamera.transform.position.y-actor.FeetPosition.y;
            Require(court.CameraRig.IsShoulderView&&Mathf.Abs(mountedCameraHeight-walkingCameraHeight-ridingCameraOffset)<.08f,
                "After owner resume, the actual free-orbit camera loses only the riding height offset while keeping its observed pitch.");
            yield return Press(Key.E);yield return Wait(.2f);
            Require(ride.IsAvailable&&ride.IsMounted&&actor.ExternalMotionOwnedBy(ride),"Re-enabling the owner permits actual short E to remount the already earned deer.");
            yield return Press(Key.R);yield return Wait(.4f);
            Require(!ride.IsMounted&&!ride.IsAvailable&&!actor.IsExternallyDriven&&actor.enabled&&!humanoid.IsRiding,"Real R while riding releases motion ownership, clears riding presentation and resets deer availability.");
            Require(Mathf.Abs(body.radius-normalRadius)<.001f&&Mathf.Abs(body.height-normalHeight)<.001f,"R restores the ordinary walking collision profile.");
        }
        private static float DeerPlatformRadius(Bounds bounds,Vector3 direction)
        {
            float x=Mathf.Max(.1f,bounds.extents.x),z=Mathf.Max(.1f,bounds.extents.z);
            return 1/Mathf.Sqrt(direction.x*direction.x/(x*x)+direction.z*direction.z/(z*z));
        }
        private static IEnumerator DeerDriveTo(Vector3 target,string label)
        {
            navigationError=Vector3.zero;double deadline=EditorApplication.timeSinceStartup+10;
            while(Vector3.ProjectOnPlane(target-court.Player.FeetPosition,Vector3.up).magnitude>.32f)
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Deer ground movement blocked: "+label+" feet="+court.Player.FeetPosition+" target="+target);
                if(!court.Grove.Ride.IsMounted)throw new Exception("Riding ended during "+label);
                Direction(Vector3.ProjectOnPlane(target-court.Player.FeetPosition,Vector3.up).normalized);yield return null;
            }
            input=new KeyboardState();yield return Wait(.28f);
            Require(court.Grove.Ride.IsGrounded,"Real ground riding reaches "+label+" on existing support.");
        }
    }
}

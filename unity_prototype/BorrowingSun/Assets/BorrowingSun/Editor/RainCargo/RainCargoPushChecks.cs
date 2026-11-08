using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    /// <summary>Explicit state fixtures followed by actual keyboard and continuously measured push assistance.</summary>
    public static class RainCargoPushChecks
    {
        private static RainCargoController court;
        private static IEnumerator sequence;
        private static Keyboard keyboard, oldKeyboard;
        private static KeyboardState requested;
        private static bool pending, observing, held, finalPrepared, capturedHumanPush, visualRun, lifecycleDisabledController;
        private static float movingPushTime;
        private static Key observedKey;
        private static int serial, queued, downs, ups;
        private static Vector3 offset;
        private static readonly List<string> report = new List<string>();
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/rain-cargo-push"));

        [MenuItem("Borrowing Sun/Rain Cargo/Run assisted push checks")]
        public static void Run()=>StartChecks(false);

        [MenuItem("Borrowing Sun/Rain Cargo/Capture active reverse push and idle")]
        public static void RunVisual()=>StartChecks(true);

        private static void StartChecks(bool visualOnly)
        {
            if(sequence!=null)throw new InvalidOperationException("Push checks are already running.");
            if(!EditorApplication.isPlaying||EditorApplication.isPaused||Time.timeScale<=0)
                throw new InvalidOperationException("Unpaused Play Mode is required.");
            if(!Application.isFocused)throw new InvalidOperationException("Activate Unity and click its Game view before running real-input checks.");
            court=UnityEngine.Object.FindAnyObjectByType<RainCargoController>();
            if(court==null)throw new InvalidOperationException("Rain cargo scene is required.");
            offset=court.Bindings.offset;serial=queued=0;pending=observing=finalPrepared=capturedHumanPush=lifecycleDisabledController=false;visualRun=visualOnly;movingPushTime=0;report.Clear();Directory.CreateDirectory(Output);
            report.Add((visualRun?"Reverse-push visual check":"Assisted cargo pushing")+" / real Dynamic keyboard input / "+DateTime.UtcNow.ToString("O"));
            report.Add("SETUP entries explicitly position the actor and assign reachable puzzle states. Every measured approach, push and ride uses real Update; no Teleport inside measured segments.");
            report.Add("This replaces the old stationary-player rejection expectation with authored automatic approach and following. Original reports remain separate.");
            report.Add("Cleanup resets the puzzle and leaves the player beside the western starting cart. This is a test baseline, not restored user progress.");
            oldKeyboard=Keyboard.current;
            try
            {
                foreach(var w in Resources.FindObjectsOfTypeAll<EditorWindow>())if(w.GetType().Name=="GameView"){w.Focus();break;}
                keyboard=InputSystem.AddDevice<Keyboard>("RainCargoPushValidationKeyboard");keyboard.MakeCurrent();
                InputSystem.onBeforeUpdate+=BeforeInput;InputSystem.onAfterUpdate+=AfterInput;
                AssemblyReloadEvents.beforeAssemblyReload+=Abort;EditorApplication.playModeStateChanged+=PlayChanged;
                sequence=visualRun?VisualPushCheck():Check();EditorApplication.update+=Tick;
            }
            catch(Exception e){Finish(false,e);}
        }

        private static IEnumerator VisualPushCheck()
        {
            Setup(new RainCargoState(),World(-16,15.06f,-1.35f),"Visual-only baseline beside the western cart");
            foreach(var step in Wait(.25f))yield return step;
            // Keep the already-reviewed forward-push image and full-suite report intact.
            capturedHumanPush=true;
            foreach(var step in Push(1,1,"Normal E reaches A before the reverse-push capture"))yield return step;
            float movingSeconds=0,lastTime=Time.unscaledTime;int lastFrame=-1;bool reverseCaptured=false;
            foreach(var step in Push(-1,0,"Normal Q returns to the western starting stop"))
            {
                if(lastFrame!=Time.frameCount)
                {
                    if(court.Player.IsPushingCart&&court.Player.PlanarVelocity.magnitude>.1f)
                        movingSeconds+=Mathf.Max(0,Time.unscaledTime-lastTime);
                    lastTime=Time.unscaledTime;lastFrame=Time.frameCount;
                    if(!reverseCaptured&&movingSeconds>=.5f&&court.Player.IsPushingCart&&court.Player.PlanarVelocity.magnitude>.1f)
                    {CaptureMovingHumanoid("06-q-active-human-push.png","ACTIVE REVERSE HUMAN PUSH");reverseCaptured=true;}
                }
                yield return step;
            }
            Require(reverseCaptured,"Reverse-push capture never observed half a second of actual pushing movement");
            foreach(var step in Wait(.5f))yield return step;
            var visual=court.Player.GetComponentInChildren<StandardHumanoidVisual>();
            Require(visual!=null&&!court.IsCartAssisting&&!court.Player.IsPushingCart&&!court.Player.IsAutoPositioning
                &&court.Player.PlanarVelocity.magnitude<.1f&&visual.Animator.GetFloat("Speed")<.2f&&visual.PushPoseWeight<.05f,
                "Arriving back at the western stop did not restore idle and release the push pose");
            report.Add($"IDLE RESTORED: actor={court.Player.FeetPosition-offset:F3}; speed={court.Player.PlanarVelocity.magnitude:F3}; Animator.Speed={visual.Animator.GetFloat("Speed"):F3}; IKweight={visual.PushPoseWeight:F3}; cart={court.State.CartStop}");Flush();
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"07-returned-idle.png"));foreach(var step in Wait(.25f))yield return step;
            foreach(var step in LifecyclePush())yield return step;
            Setup(new RainCargoState(),World(-16,15.06f,-1.35f),"Final western baseline after the isolated lifecycle fixture");
            foreach(var step in Wait(.25f))yield return step;
            finalPrepared=true;
        }

        private static IEnumerable<object> LifecyclePush()
        {
            var evidence=new List<string>{"Lifecycle fixture / real E0->1 / "+DateTime.UtcNow.ToString("O"),
                "Controller disable/enable and Player.Recover are explicit test lifecycle operations, not player key presses. Recover's relocation is recorded outside continuous-motion measurement."};
            File.WriteAllLines(Path.Combine(Output,"lifecycle-checks.txt"),evidence);
            var motion=new MotionProbe();
            foreach(var step in Press(Key.E)){Measure(motion,"Lifecycle push start");yield return step;}
            double deadline=EditorApplication.timeSinceStartup+15;
            while(!court.Player.IsPushingCart||court.Player.PlanarVelocity.magnitude<.1f)
            {Require(EditorApplication.timeSinceStartup<deadline,"Lifecycle fixture did not enter real pushing");Measure(motion,"Lifecycle push start");yield return null;}
            Require(court.State.CartStop==0&&court.State.PendingCartStop==1,"Lifecycle fixture lost its initial destination");
            Vector3 pausedCart=court.Bindings.cart.position;
            lifecycleDisabledController=true;court.enabled=false;
            Require(!court.Player.IsAutoPositioning&&!court.Player.IsPushingCart,"Controller disable retained movement ownership");
            foreach(var step in Wait(.15f))yield return step;
            Require(court.State.PendingCartStop==1&&Vector3.Distance(pausedCart,court.Bindings.cart.position)<.01f,"Disabled controller advanced or discarded its cargo destination");
            evidence.Add($"PASS controller disabled: cart={pausedCart-offset:F3}; PendingCartStop=1; actor control released");File.WriteAllLines(Path.Combine(Output,"lifecycle-checks.txt"),evidence);
            court.enabled=true;lifecycleDisabledController=false;
            deadline=EditorApplication.timeSinceStartup+15;
            while(!court.Player.IsPushingCart||court.Player.PlanarVelocity.magnitude<.1f)
            {Require(EditorApplication.timeSinceStartup<deadline,"Controller re-enable did not restore real pushing");Measure(motion,"Controller resume");yield return null;}
            Vector3 before=court.Player.FeetPosition;court.Player.Recover();
            evidence.Add($"EXPLICIT Recover: actor {before-offset:F3}->{court.Player.FeetPosition-offset:F3}; pending={court.State.PendingCartStop}");File.WriteAllLines(Path.Combine(Output,"lifecycle-checks.txt"),evidence);
            Require(!court.Player.IsAutoPositioning&&!court.Player.IsPushingCart&&court.State.PendingCartStop==1,"Recover did not release actor control while retaining its pending cargo destination");
            // Recover intentionally relocates to the last safe point. Resume measurements start after that explicit operation.
            motion=new MotionProbe();deadline=EditorApplication.timeSinceStartup+15;bool resumed=false;
            while(court.IsCartAssisting||court.State.CartInMotion||court.State.CartStop!=1)
            {
                Require(EditorApplication.timeSinceStartup<deadline,"Pushing did not finish after the explicit Recover; "+Diagnostic());
                resumed|=court.Player.IsPushingCart&&court.Player.PlanarVelocity.magnitude>.1f;
                Measure(motion,"Resume after Recover");yield return null;
            }
            Require(resumed&&!court.Player.IsAutoPositioning&&!court.Player.IsPushingCart,"Recover resumed no real movement or left automatic control locked");
            evidence.Add($"PASS enabled/resumed/recovered push reaches the same stop1; actor={court.Player.FeetPosition-offset:F3}; max actor/cart steps={motion.maxActorStep:F3}/{motion.maxCartStep:F3}m");
            evidence.Add("RAIN_CARGO_PUSH_LIFECYCLE_PASSED");File.WriteAllLines(Path.Combine(Output,"lifecycle-checks.txt"),evidence);
            report.Add("PASS Explicit controller disable/enable and Player.Recover resume the same E0->1 push without control lock; see lifecycle-checks.txt");Flush();
        }

        private static IEnumerator Check()
        {
            Vector3[] sides={new Vector3(-14,15.06f,0),new Vector3(-18,15.06f,0),new Vector3(-16,15.06f,1.35f),new Vector3(-16,15.06f,-1.35f)};
            string[] names={"front","rear","north side","south side"};
            for(int i=0;i<sides.Length;i++)
            {
                Setup(new RainCargoState(),offset+sides[i],"Near starting cart from "+names[i]);
                foreach(var step in Wait(.25f))yield return step;
                if(i==0){ScreenCapture.CaptureScreenshot(Path.Combine(Output,"01-near-front.png"));foreach(var step in Wait(.2f))yield return step;}
                foreach(var step in Push(1,1,"E from "+names[i],i==0))yield return step;
                if(i==0)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output,"02-followed-onto-A.png"));foreach(var step in Wait(.2f))yield return step;
                    foreach(var step in Push(-1,0,"Q reverses from the completed push position"))yield return step;
                }
            }

            Setup(LoadedAt(2),World(4,15.06f,-1.35f),"Full tank at the freight-ramp corner, actor close beside it");
            foreach(var step in Wait(.25f))yield return step;
            foreach(var step in Push(1,3,"Turn and push continuously down the freight ramp"))yield return step;
            Require(court.State.TankFull,"The slope discarded the stored rain");
            foreach(var step in Push(1,4,"Reposition from the ramp end and turn into the covered street"))yield return step;
            foreach(var step in Push(-1,3,"Q reverses through the freight ramp's open rail end"))yield return step;
            foreach(var step in Push(1,4,"E returns through the repaired ramp-end route"))yield return step;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"03-through-freight-street.png"));foreach(var step in Wait(.2f))yield return step;
            foreach(var step in Push(1,5,"Push onto the low B platform through its narrow connection"))yield return step;
            foreach(var step in Push(-1,4,"Q returns the loaded cart through B's low connection"))yield return step;
            foreach(var step in Push(1,5,"E returns the loaded cart to B"))yield return step;
            Require(court.State.TankFull&&court.Support(court.Player.FeetPosition)==5,"Automatic following did not leave the actor on B with the full cart");
            var ride=new MotionProbe();
            foreach(var step in Press(Key.E)){Measure(ride,"Unload and ride B");yield return step;}
            double rideEnd=EditorApplication.timeSinceStartup+8;
            while(!court.State.IsBHigh||court.State.Transferring)
            {
                Require(EditorApplication.timeSinceStartup<rideEnd,"B unload/ride timed out");Measure(ride,"Unload and ride B");yield return null;
            }
            Require(court.State.TankWater<.001f&&Mathf.Abs(court.Player.FeetPosition.y-offset.y-15)<.15f,"Empty tank and raised B must carry the actor to the upper dock");
            report.Add("PASS Actual E transfers water and carries the pushing actor up with B");Flush();
            foreach(var step in Refused(Key.Q,"Raised B cannot reverse the cart into the lower street"))yield return step;
            foreach(var step in Push(1,6,"Push across the raised B to the upper dock"))yield return step;
            foreach(var step in Push(1,7,"Turn at the upper dock and push to the final receiver"))yield return step;
            Require(court.State.Completed&&capturedHumanPush,"The assisted cargo route or its actual moving-humanoid capture did not complete");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"04-final-delivery.png"));foreach(var step in Wait(.2f))yield return step;

            var highA=new RainCargoState();AdvanceCart(highA);highA.InsertCard(0);highA.ToggleRain(0);highA.Advance(10);highA.RemoveCard(0);
            Setup(highA,World(-6,18.06f,-1.35f),"High A, empty cart1, near its side");foreach(var step in Wait(.25f))yield return step;
            foreach(var step in Refused(Key.E,"E cannot move a high A cart onto the lower dock"))yield return step;
            var emptyB=new RainCargoState();for(int i=0;i<5;i++)AdvanceCart(emptyB);
            Setup(emptyB,World(28,12.06f,16.65f),"Empty cart on low B");foreach(var step in Wait(.25f))yield return step;
            foreach(var step in Refused(Key.E,"E cannot raise B with an empty tank"))yield return step;

            Setup(new RainCargoState(),World(-14,15.06f,0),"Reset during automatic approach or pushing");foreach(var step in Wait(.25f))yield return step;
            foreach(var step in Press(Key.E))yield return step;
            Require(court.IsCartAssisting||court.State.CartInMotion,"Reset probe did not enter an active assistance phase");
            foreach(var step in Press(Key.R))yield return step;
            Require(!court.IsCartAssisting&&!court.Player.IsAutoPositioning&&!court.Player.IsPushingCart&&!court.State.CartInMotion
                &&court.State.CartStop==0&&court.State.IsALow&&court.State.IsBLow&&court.State.CardStation==-1&&court.State.TankWater==0,"R did not clear movement ownership and puzzle state");
            Vector3 afterReset=court.Player.FeetPosition;SetInput(Key.W);foreach(var step in WaitInput(serial,.25f))yield return step;SetInput();foreach(var step in WaitInput(serial,.12f))yield return step;
            Require(Vector3.Distance(afterReset,court.Player.FeetPosition)>.3f,"Manual walking remained locked after R");
            report.Add("PASS R interrupts assistance, resets cargo/puzzle state and restores actual WASD movement");Flush();

            Setup(new RainCargoState(),World(-16,15.06f,-1.35f),"Final western-cart test baseline");foreach(var step in Wait(.25f))yield return step;
            Require(!court.IsCartAssisting&&!court.Player.IsAutoPositioning&&!court.Player.IsPushingCart,"Final baseline retained automatic movement control");
            finalPrepared=true;ScreenCapture.CaptureScreenshot(Path.Combine(Output,"05-ready-beside-starting-cart.png"));foreach(var step in Wait(.2f))yield return step;
        }

        private static IEnumerable<object> Push(int direction,int destination,string label,bool repeatKeys=false)
        {
            Require(!court.IsCartAssisting&&!court.State.CartInMotion,label+": previous push is still active");int from=court.State.CartStop;
            var motion=new MotionProbe();Vector3 actorStart=court.Player.FeetPosition;int frame=Time.frameCount;bool assisted=false,followed=false;
            foreach(var step in Press(direction>0?Key.E:Key.Q)){Measure(motion,label);assisted|=court.IsCartAssisting;followed|=court.Player.IsPushingCart;yield return step;}
            Require(court.IsCartAssisting||court.State.CartInMotion||court.State.CartStop==destination,label+": input did not start; "+Diagnostic());
            if(repeatKeys)
            {
                foreach(var step in Press(Key.E)){Measure(motion,label);assisted|=court.IsCartAssisting;followed|=court.Player.IsPushingCart;yield return step;}
                foreach(var step in Press(Key.Q)){Measure(motion,label);assisted|=court.IsCartAssisting;followed|=court.Player.IsPushingCart;yield return step;}
            }
            double deadline=EditorApplication.timeSinceStartup+24;
            while(court.IsCartAssisting||court.State.CartInMotion||court.State.CartStop!=destination)
            {
                Require(EditorApplication.timeSinceStartup<deadline,label+": timed out; "+Diagnostic());
                Require(court.State.CartStop==from||court.State.CartStop==destination,label+": repeated input changed the intended stop");
                if(court.State.CartInMotion)Require(court.State.PendingCartStop==destination,label+": repeated input changed the pending destination");
                assisted|=court.IsCartAssisting;followed|=court.Player.IsPushingCart;Measure(motion,label);yield return null;
            }
            Measure(motion,label);foreach(var step in Wait(.15f))yield return step;
            Require(assisted&&followed&&Time.frameCount-frame>5&&Vector3.Distance(actorStart,court.Player.FeetPosition)>1,label+": no real assisted approach/following was measured");
            Require(!court.Player.IsAutoPositioning&&!court.Player.IsPushingCart,label+": reaching the stop did not release actor control");
            Require(Vector3.Distance(court.Bindings.cart.position,court.Bindings.stops[destination].position)<.06f,label+": cart missed its authored stop");
            float separation=Vector3.ProjectOnPlane(court.Player.FeetPosition-court.Bindings.cart.position,Vector3.up).magnitude;
            Require(separation>1.7f&&separation<3.05f,label+": actor finished too far from the pushing end; distance="+separation.ToString("F3"));
            report.Add($"PASS {label}: cart {from}->{destination}; actor {actorStart-offset:F3}->{court.Player.FeetPosition-offset:F3}; {Time.frameCount-frame}frames; max actor/cart steps={motion.maxActorStep:F3}/{motion.maxCartStep:F3}m; final separation={separation:F3}m"+(repeatKeys?"; repeated E/Q did not re-enter":""));Flush();
        }
        private sealed class MotionProbe
        {
            public Vector3 lastActor,lastCart;public float lastTime,maxActorStep,maxCartStep;public int lastFrame=-1;
        }
        private static void Measure(MotionProbe p,string label)
        {
            if(p.lastFrame==Time.frameCount)return;Vector3 actor=court.Player.FeetPosition,cart=court.Bindings.cart.position;
            Require(float.IsFinite(actor.x)&&float.IsFinite(actor.y)&&float.IsFinite(actor.z)&&float.IsFinite(cart.x)&&float.IsFinite(cart.y)&&float.IsFinite(cart.z),label+": non-finite transform");
            if(p.lastFrame>=0)
            {
                float a=Vector3.Distance(actor,p.lastActor),c=Vector3.Distance(cart,p.lastCart),allowed=Mathf.Max(.45f,7*Mathf.Max(0,Time.unscaledTime-p.lastTime)+.15f);
                p.maxActorStep=Mathf.Max(p.maxActorStep,a);p.maxCartStep=Mathf.Max(p.maxCartStep,c);
                Require(a<allowed&&c<allowed,label+$": discontinuous movement actor/cart {a:F3}/{c:F3}m; "+Diagnostic());
            }
            bool supported=false;
            foreach(var hit in Physics.RaycastAll(actor+Vector3.up*.4f,Vector3.down,1.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                if(Owned(hit.transform,court.Player.transform)||Owned(hit.transform,court.Bindings.cart)||hit.normal.y<.65f)continue;
                if(Mathf.Abs(actor.y-hit.point.y)<.28f)supported=true;
            }
            Require(supported,label+": actor has no nearby real floor; "+Diagnostic());
            // A smaller interior capsule detects material clipping, while tolerating the CharacterController's skin at contacts.
            foreach(var obstacle in Physics.OverlapCapsule(actor+Vector3.up*.42f,actor+Vector3.up*1.38f,.19f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                Require(Owned(obstacle.transform,court.Player.transform),label+": actor interior penetrated "+obstacle.name+"; "+Diagnostic());
            if(!capturedHumanPush&&court.Player.IsPushingCart&&court.Player.PlanarVelocity.magnitude>.1f)
            {
                movingPushTime+=p.lastFrame>=0?Mathf.Max(0,Time.unscaledTime-p.lastTime):Time.unscaledDeltaTime;
                if(movingPushTime>.35f)CaptureMovingHumanoid();
            }
            p.lastActor=actor;p.lastCart=cart;p.lastFrame=Time.frameCount;p.lastTime=Time.unscaledTime;
        }
        private static void CaptureMovingHumanoid(string filename="02a-active-human-push.png",string label="ACTIVE HUMAN PUSH")
        {
            var visual=court.Player.GetComponentInChildren<StandardHumanoidVisual>();
            Require(visual!=null,"Actual pushing requires the installed humanoid visual");Animator animator=visual.Animator;
            Require(animator!=null&&animator.isActiveAndEnabled&&animator.runtimeAnimatorController!=null
                &&animator.avatar!=null&&animator.avatar.isValid&&animator.avatar.isHuman&&!animator.applyRootMotion,
                "The moving humanoid needs a valid active Humanoid Animator with root motion disabled");
            float minimumY=float.PositiveInfinity,maximumY=float.NegativeInfinity;
            foreach(var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                // Unity 6000.5: useScale=true compensates Transform scaling; TransformPoint then applies it once.
                // https://docs.unity3d.com/6000.5/Documentation/ScriptReference/SkinnedMeshRenderer.BakeMesh.html
                var baked=new Mesh();renderer.BakeMesh(baked,true);
                foreach(Vector3 vertex in baked.vertices){float y=renderer.transform.TransformPoint(vertex).y;minimumY=Mathf.Min(minimumY,y);maximumY=Mathf.Max(maximumY,y);}
                UnityEngine.Object.DestroyImmediate(baked);
            }
            Require(float.IsFinite(minimumY)&&maximumY>minimumY,"The pushing visual has no evaluated skinned mesh");
            Transform left=animator.GetBoneTransform(HumanBodyBones.LeftFoot),right=animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Require(left!=null&&right!=null,"The imported Humanoid has no mapped feet");
            var clips=new List<string>();foreach(var clip in animator.GetCurrentAnimatorClipInfo(0))clips.Add(clip.clip.name+"@"+clip.weight.ToString("F3"));
            report.Add($"{label}: frame={Time.frameCount}; IsPushingCart={court.Player.IsPushingCart}; planarSpeed={court.Player.PlanarVelocity.magnitude:F3}; Animator={animator.name}; avatar={animator.avatar.isValid}/{animator.avatar.isHuman}; rootMotion={animator.applyRootMotion}; Speed={animator.GetFloat("Speed"):F3}; IKweight={visual.PushPoseWeight:F3}; actorFeet={court.Player.FeetPosition-offset:F3}; bakedSoleOffset={minimumY-court.Player.FeetPosition.y:F3}; animatedMeshHeight={maximumY-minimumY:F3}; left/rightFootBoneY={left.position.y-court.Player.FeetPosition.y:F3}/{right.position.y-court.Player.FeetPosition.y:F3}; clips=[{string.Join(",",clips)}].");
            RecordHandGeometry(visual,animator);
            Require(maximumY-minimumY>=1.3f&&maximumY-minimumY<=2.1f,"Animated humanoid world height is outside 1.3–2.1m; evaluated height="+(maximumY-minimumY).ToString("F4"));
            Require(minimumY-court.Player.FeetPosition.y>=-.12f&&minimumY-court.Player.FeetPosition.y<=.35f,
                "Animated soles are sunk or floating relative to the controller feet; offset="+(minimumY-court.Player.FeetPosition.y).ToString("F4"));
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,filename));capturedHumanPush=true;Flush();
        }
        private static void RecordHandGeometry(StandardHumanoidVisual visual,Animator animator)
        {
            BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;Type type=typeof(StandardHumanoidVisual);
            Transform facing=(Transform)type.GetField("visualFacing",flags).GetValue(visual);
            float height=(float)type.GetField("handHeight",flags).GetValue(visual),reach=(float)type.GetField("handReach",flags).GetValue(visual),halfWidth=(float)type.GetField("handHalfWidth",flags).GetValue(visual);
            Vector3 center=court.Player.FeetPosition+Vector3.up*height+facing.forward*reach;
            for(int side=0;side<2;side++)
            {
                Transform upper=animator.GetBoneTransform(side==0?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                Transform lower=animator.GetBoneTransform(side==0?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                Transform hand=animator.GetBoneTransform(side==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                Require(upper!=null&&lower!=null&&hand!=null,"The Humanoid is missing an arm/hand mapping");
                Vector3 target=center+facing.right*(side==0?-halfWidth:halfWidth);
                float upperLength=Vector3.Distance(upper.position,lower.position),lowerLength=Vector3.Distance(lower.position,hand.position),shoulderToTarget=Vector3.Distance(upper.position,target);
                report.Add($"HAND {(side==0?"left":"right")}: actual={hand.position-offset:F3}; target={target-offset:F3}; wristTargetError={Vector3.Distance(hand.position,target):F3}m; upper/lowerArmLength={upperLength:F3}/{lowerLength:F3}m; shoulderJointToTarget={shoulderToTarget:F3}m; armTotalReach={upperLength+lowerLength:F3}m; configured height/reach/halfWidth={height:F3}/{reach:F3}/{halfWidth:F3}m.");
            }
        }

        [MenuItem("Borrowing Sun/Rain Cargo/Diagnose freight ramp corner geometry")]
        public static void DiagnoseFreightCorner()
        {
            if(sequence!=null)throw new InvalidOperationException("Wait until push checks stop before querying the fixed geometry samples.");
            RainCargoController owner=UnityEngine.Object.FindAnyObjectByType<RainCargoController>();
            if(owner==null)throw new InvalidOperationException("Rain cargo scene is required.");
            CourtPlayer player=owner.Player;Vector3 origin=owner.Bindings.offset;
            Type type=typeof(CourtPlayer);BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            FieldInfo bodyField=type.GetField("body",flags);object savedBody=bodyField.GetValue(player);
            MethodInfo segment=type.GetMethod("CanWalkAssistedSegment",flags),ground=type.GetMethod("TryAssistedGround",flags),capsule=type.GetMethod("AssistedCapsule",flags);
            var lines=new List<string>{"Freight ramp corner / fixed authored-coordinate physics queries / "+DateTime.UtcNow.ToString("O"),
                "Read-only geometry query: no player/cart transform changes and no puzzle-state changes. Editor-only body cache is restored afterward. This is not a replacement for a real-input traversal."};
            Vector3[] points={new Vector3(6.145f,12.036f,18),new Vector3(6.30f,12,18),new Vector3(6.145f,12,18.7f),new Vector3(7,12,18.7f)};
            int[,] pairs={{0,1},{0,2},{2,3}};
            try
            {
                if(savedBody==null)bodyField.SetValue(player,player.GetComponent<CharacterController>());
                Physics.SyncTransforms();
                for(int pair=0;pair<pairs.GetLength(0);pair++)
                {
                    Vector3 from=origin+points[pairs[pair,0]],to=origin+points[pairs[pair,1]];
                    object[] floorArgs={to,.3f,Vector3.zero};bool floor=(bool)ground.Invoke(player,floorArgs);
                    bool canWalk=(bool)segment.Invoke(player,new object[]{from,to});
                    lines.Add($"SEGMENT {from-origin:F3}->{to-origin:F3}: TryAssistedGround={floor}, ground={(Vector3)floorArgs[2]-origin:F3}, CanWalkAssistedSegment={canWalk}");
                    object[] shape={from,Vector3.zero,Vector3.zero,0f};capsule.Invoke(player,shape);Vector3 bottom=(Vector3)shape[1],top=(Vector3)shape[2];float radius=(float)shape[3];
                    Vector3 delta=to-from;
                    foreach(var hit in Physics.CapsuleCastAll(bottom,top,radius,delta.normalized,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    {
                        if(Owned(hit.transform,player.transform))continue;Bounds b=hit.collider.bounds;
                        lines.Add($"  CAST {hit.collider.name}: distance={hit.distance:F4}, normal={hit.normal:F3}, bounds min/max={b.min-origin:F4}/{b.max-origin:F4}");
                    }
                    object[] endShape={to,Vector3.zero,Vector3.zero,0f};capsule.Invoke(player,endShape);
                    foreach(var hit in Physics.OverlapCapsule((Vector3)endShape[1],(Vector3)endShape[2],(float)endShape[3],Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    {
                        if(Owned(hit.transform,player.transform))continue;Bounds b=hit.bounds;
                        lines.Add($"  END OVERLAP {hit.name}: bounds min/max={b.min-origin:F4}/{b.max-origin:F4}");
                    }
                }
            }
            finally{bodyField.SetValue(player,savedBody);Directory.CreateDirectory(Output);File.WriteAllLines(Path.Combine(Output,"corner-diagnostic.txt"),lines);}
            Debug.Log("RAIN_CARGO_CORNER_DIAGNOSTIC_WRITTEN");
        }
        private static bool Owned(Transform t,Transform root)=>t==root||t.IsChildOf(root);
        private static IEnumerable<object> Refused(Key key,string label)
        {
            Vector3 actor=court.Player.FeetPosition,cart=court.Bindings.cart.position;string state=Puzzle();
            foreach(var step in Press(key))yield return step;
            Require(!court.IsCartAssisting&&!court.State.CartInMotion&&!court.State.Transferring&&Puzzle()==state
                &&Vector3.Distance(actor,court.Player.FeetPosition)<.12f&&Vector3.Distance(cart,court.Bindings.cart.position)<.05f,label+": forbidden action changed state; "+Diagnostic());
            report.Add("PASS "+label);Flush();
        }
        private static RainCargoState LoadedAt(int stop)
        {
            var s=new RainCargoState();AdvanceCart(s);s.InsertCard(0);s.ToggleRain(0);s.Advance(10);s.RemoveCard(0);
            s.InsertCard(1);s.ToggleRain(1);s.Advance(10);s.RemoveCard(1);s.ToggleDrain(0);s.Advance(10);s.ToggleDrain(0);
            while(s.CartStop<stop)AdvanceCart(s);Require(s.TankFull&&s.IsALow,"Loaded state setup failed");return s;
        }
        private static void AdvanceCart(RainCargoState state){Require(state.BeginCartMove(1)&&state.CompleteCartMove(),"Explicit cart-state setup failed");}
        private static void Setup(RainCargoState state,Vector3 feet,string label)
        {
            court.ResetCourt();typeof(RainCargoController).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(court,state);court.Simulate(0);Physics.SyncTransforms();court.Player.Teleport(feet);
            report.Add("SETUP "+label+"; "+Puzzle()+"; actor="+(feet-offset).ToString("F3"));Flush();
        }
        private static string Puzzle(){var s=court.State;return $"A{s.WaterA:F3}/B{s.WaterB:F3}/tank{s.TankWater:F3}/card{s.CardStation}/cart{s.CartStop}/{s.PendingCartStop}/rain{s.RainActive}/drain{s.DrainAOpen},{s.DrainBOpen}";}
        private static string Diagnostic()=>Puzzle()+$"; actor={court.Player.FeetPosition-offset:F3}; cart={court.Bindings.cart.position-offset:F3}; assist={court.IsCartAssisting}; approach={court.Player.IsAutoPositioning}; push={court.Player.IsPushingCart}; focus={Application.isFocused}; current={Keyboard.current==keyboard}; prompt={court.Prompt()}; message={court.Message}";
        private static void Tick(){if(sequence==null)return;try{Require(EditorApplication.isPlaying&&!EditorApplication.isPaused,"Play Mode stopped/paused");if(sequence.MoveNext())return;Finish(true,null);}catch(Exception e){Finish(false,e);}}
        private static void Finish(bool passed,Exception error)
        {
            EditorApplication.update-=Tick;InputSystem.onBeforeUpdate-=BeforeInput;InputSystem.onAfterUpdate-=AfterInput;AssemblyReloadEvents.beforeAssemblyReload-=Abort;EditorApplication.playModeStateChanged-=PlayChanged;sequence=null;pending=observing=false;
            try
            {
                if(keyboard!=null&&keyboard.added){InputState.Change(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);}if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();
                if(court!=null){if(lifecycleDisabledController){court.enabled=true;lifecycleDisabledController=false;}if(!finalPrepared){court.ResetCourt();court.Player.Teleport(World(-16,15.06f,-1.35f));Physics.SyncTransforms();}report.Add("FINAL TEST BASELINE: "+Puzzle()+"; actor="+(court.Player.FeetPosition-offset).ToString("F3"));}
            }
            catch(Exception e){passed=false;report.Add("CLEANUP FAILURE "+e.Message);}
            string marker=visualRun?"RAIN_CARGO_PUSH_VISUAL":"RAIN_CARGO_PUSH";
            if(error!=null)report.Add("FAIL "+error.Message);report.Add(marker+(passed?"_PASSED":"_FAILED"));Flush();File.WriteAllLines(Path.Combine(Output,(visualRun?"visual-checks-":"checks-")+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".txt"),report);
            if(passed)Debug.Log(marker+"_PASSED");else Debug.LogError(marker+"_FAILED: "+error?.Message);
        }
        private static void BeforeInput(){if(!pending||keyboard==null||!keyboard.added||InputState.currentUpdateType!=InputUpdateType.Dynamic)return;InputSystem.QueueStateEvent(keyboard,requested);queued=serial;pending=false;}
        private static void AfterInput(){if(!observing||keyboard==null||InputState.currentUpdateType!=InputUpdateType.Dynamic)return;var key=keyboard[observedKey];held=key.isPressed;if(key.wasPressedThisFrame)downs++;if(key.wasReleasedThisFrame)ups++;}
        private static void SetInput(params Key[] keys){requested=new KeyboardState(keys);serial++;pending=true;}
        private static IEnumerable<object> Press(Key key)
        {
            observedKey=key;observing=true;downs=ups=0;held=false;SetInput(key);foreach(var step in WaitInput(serial,.12f))yield return step;
            Require(downs==1,"Input "+key+" did not produce one Dynamic press; "+Diagnostic());SetInput();foreach(var step in WaitInput(serial,.12f))yield return step;observing=false;
            Require(downs==1&&ups>=1&&!held,"Input "+key+" did not release cleanly in Dynamic; "+Diagnostic());
        }
        private static IEnumerable<object> WaitInput(int id,float seconds){int frame=Time.frameCount;double start=EditorApplication.timeSinceStartup;while(queued<id||Time.frameCount<frame+2||EditorApplication.timeSinceStartup<start+seconds){Require(EditorApplication.timeSinceStartup<start+5,"Input/frame timeout; "+Diagnostic());yield return null;}}
        private static IEnumerable<object> Wait(float seconds){double end=EditorApplication.timeSinceStartup+seconds;while(EditorApplication.timeSinceStartup<end)yield return null;}
        private static void Flush()=>File.WriteAllLines(Path.Combine(Output,visualRun?"visual-checks.txt":"checks.txt"),report);
        private static Vector3 World(float x,float y,float z)=>offset+new Vector3(x,y,z);
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        private static void Abort()=>Finish(false,new InvalidOperationException("Assembly reload interrupted push checks"));
        private static void PlayChanged(PlayModeStateChange s){if(sequence!=null&&s==PlayModeStateChange.ExitingPlayMode)Finish(false,new InvalidOperationException("Play Mode exited"));}
    }
}

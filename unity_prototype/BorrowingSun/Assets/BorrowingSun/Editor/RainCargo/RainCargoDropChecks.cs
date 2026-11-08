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
    /// <summary>Isolated authored-state setups followed by real keyboard/CharacterController drop probes.</summary>
    public static class RainCargoDropChecks
    {
        private static RainCargoController court;
        private static RainCargoCutaway cutaway;
        private static IEnumerator sequence;
        private static Keyboard keyboard, oldKeyboard;
        private static KeyboardState requested;
        private static bool pending, observingKey, lastHeld, fixtureActive, savedControllerEnabled, finalPrepared;
        private static bool originalCutawayEnabled, originalCourtEnabled;
        private static Key keyObserved;
        private static int serial, queuedSerial, downEdges, upEdges;
        private static Vector3 offset, savedCartPosition;
        private static Quaternion savedCartRotation;
        private static readonly List<string> report = new List<string>();
        private static readonly Dictionary<Renderer,bool> rendererBaseline = new Dictionary<Renderer,bool>();
        private static readonly List<Renderer> cutawayChanges = new List<Renderer>();
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../validation/rain-cargo-drop"));

        [MenuItem("Borrowing Sun/Rain Cargo/Run contextual drop checks")]
        public static void Run()
        {
            if(sequence!=null)throw new InvalidOperationException("Drop checks already running.");
            if(!EditorApplication.isPlaying||EditorApplication.isPaused||Time.timeScale<=0)throw new InvalidOperationException("Unpaused Play Mode is required.");
            court=UnityEngine.Object.FindAnyObjectByType<RainCargoController>();
            if(court==null)throw new InvalidOperationException("Rain cargo scene is required.");
            cutaway=UnityEngine.Object.FindAnyObjectByType<RainCargoCutaway>();
            if(cutaway==null)throw new InvalidOperationException("Rain cargo visibility component is required.");
            originalCourtEnabled=court.enabled;originalCutawayEnabled=cutaway.enabled;
            offset=court.Bindings.offset; report.Clear(); Directory.CreateDirectory(Output);
            report.Add("Rain cargo contextual drop / actual Dynamic input + CharacterController / "+DateTime.UtcNow.ToString("O"));
            report.Add("Each scenario's setup explicitly sets puzzle state and player position. Measured walks/drops use Update and keyboard input; no Teleport occurs inside them.");
            report.Add("The occupied-landing fixture briefly freezes only court simulation and moves the real cart collider; it is restored immediately afterward.");
            report.Add("On exit the test resets B0, card in hand and cart at stop0, then walks west to the east-bank lip near local(32.3,15,18). This is a test baseline, not restoration of the user's progress.");
            serial=queuedSerial=0;pending=observingKey=fixtureActive=finalPrepared=false;
            rendererBaseline.Clear();cutawayChanges.Clear();
            oldKeyboard=Keyboard.current;
            try
            {
                foreach(var w in Resources.FindObjectsOfTypeAll<EditorWindow>())if(w.GetType().Name=="GameView"){w.Focus();break;}
                keyboard=InputSystem.AddDevice<Keyboard>("RainCargoDropValidationKeyboard");keyboard.MakeCurrent();
                InputSystem.onBeforeUpdate+=BeforeInput;InputSystem.onAfterUpdate+=AfterInput;
                AssemblyReloadEvents.beforeAssemblyReload+=Abort;EditorApplication.playModeStateChanged+=OnPlayChanged;
                sequence=Check();EditorApplication.update+=Tick;
            }
            catch(Exception e){Finish(false,e);}
        }

        private static IEnumerator Check()
        {
            Setup(new RainCargoState(),World(32.8f,15.06f,18),"B0 east bank");
            foreach(var s in Wait(.2f))yield return s;
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))rendererBaseline[renderer]=renderer.enabled;
            Require(court.Player.PuzzleEdgeProtectionEnabled&&court.CameraRig.FollowViewEnabled&&!court.CameraRig.IsOverview&&!court.IsObserving,"Close view and ordinary walking protection are enabled");
            foreach(var s in HoldAtLip(Vector3.left,.7f,15,"Walking west alone remains on the east bank"))yield return s;
            Require(court.Player.TryGetDropLanding(out Vector3 lowLanding),"East-bank edge exposes an intentional lower landing");
            Require(Mathf.Abs(lowLanding.y-offset.y-12)<.12f&&lowLanding.x<offset.x+32,"Drop target is the B low deck, not empty space or an upper surface");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"01-before-drop.png"));foreach(var s in Wait(.25f))yield return s;
            string initial=Puzzle();Vector3 cameraBefore=court.CameraRig.ViewCamera.transform.position;
            foreach(var s in Drop("East bank to B low",12))yield return s;
            Require(Puzzle()==initial,"Low-deck drop preserves water, tank, card and cart state");
            foreach(var s in Wait(.35f))yield return s;
            Require(court.CameraRig.FollowViewEnabled&&!court.CameraRig.IsOverview&&!court.IsObserving&&Vector3.Distance(cameraBefore,court.CameraRig.ViewCamera.transform.position)>1,"Close gameplay camera follows the actual drop");
            foreach(var s in CheckVisibilityAndRestoration())yield return s;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"02-after-low-B.png"));foreach(var s in Wait(.2f))yield return s;
            foreach(var s in Walk(World(23.4f,12,18),5,"B low deck to fixed western cargo street"))yield return s;

            Setup(PartialB(1.7f),World(32.8f,15.06f,16),"B stopped at water1.7, cart5 empty after a completed transfer");
            foreach(var s in Wait(.2f))yield return s;
            foreach(var s in HoldAtLip(Vector3.left,.7f,15,"East bank remains protected above partial B"))yield return s;
            string partial=Puzzle();
            foreach(var s in Drop("East bank to stationary intermediate B",13.7f))yield return s;
            foreach(var s in Walk(World(24.35f,13.7f,16),5,"Walk beside the empty cart to B's west lip"))yield return s;
            foreach(var s in HoldAtLip(Vector3.left,.35f,13.7f,"Intermediate B west lip still requires intentional descent"))yield return s;
            foreach(var s in Drop("Intermediate B to the lower fixed cargo street",12))yield return s;
            Require(court.Player.FeetPosition.x<offset.x+24&&Puzzle()==partial,"Intermediate B has a downward exit without resetting its puzzle state");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"03-after-intermediate-return.png"));foreach(var s in Wait(.2f))yield return s;

            Setup(new RainCargoState(),World(36,15.06f,14.5f),"East terrace south edge has no lower landing within reach");
            foreach(var s in Wait(.2f))yield return s;
            foreach(var s in HoldAtLip(Vector3.back,.5f,15,"Empty-valley edge"))yield return s;
            foreach(var s in RefusedDrop("Empty valley is not a valid drop"))yield return s;

            Setup(new RainCargoState(),World(-14,17.06f,-4.2f),"West fixed ramp north rail; lower west court exists behind it");
            foreach(var s in Wait(.2f))yield return s;
            foreach(var s in HoldAtLip(Vector3.forward,.45f,17,"Approach the actual ramp-side rail"))yield return s;
            Require(Physics.Raycast(World(-14,16,-2.5f),Vector3.down,out var lower,2,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&Mathf.Abs(lower.point.y-offset.y-15)<.05f,"Rail probe has a real lower floor behind the obstruction");
            foreach(var s in RefusedDrop("A visible side rail blocks the outward drop path"))yield return s;

            Setup(new RainCargoState(),World(32.8f,15.06f,18),"Occupied landing fixture at the B east edge");
            foreach(var s in Wait(.2f))yield return s;
            foreach(var s in HoldAtLip(Vector3.left,.7f,15,"Approach occupancy-test drop edge"))yield return s;
            Require(court.Player.TryGetDropLanding(out Vector3 occupied),"Occupancy fixture starts from a valid landing");
            savedCartPosition=court.Bindings.cart.position;savedCartRotation=court.Bindings.cart.rotation;savedControllerEnabled=court.enabled;fixtureActive=true;
            court.enabled=false;court.Bindings.cart.position=occupied;Physics.SyncTransforms();
            foreach(var s in Wait(.15f))yield return s;
            foreach(var s in RefusedDrop("The actual cart collider cannot become an occupied landing or an unintended walkable cart roof"))yield return s;
            RestoreFixture();

            var empty=new RainCargoState();for(int i=0;i<5;i++)MoveState(empty);
            Setup(empty,World(28,12.06f,16),"Empty cart at stop5 on low B");
            foreach(var s in Wait(.2f))yield return s;
            Require(!court.State.CanMoveCart(1,out _),"A person reaching low B does not authorize cart5 to cross to the upper dock");
            foreach(var s in Press(Key.E))yield return s;
            Require(court.State.CartStop==5&&!court.State.CartInMotion&&!court.State.Transferring&&court.State.IsBLow&&court.State.TankWater==0,"Actual E refuses unloading/lifting an empty cart; cargo height rules remain active");
            report.Add("PASS Empty-cart E preserves the cargo height requirement");Flush();

            Setup(new RainCargoState(),World(32.8f,15.06f,18),"Final feedback-location test baseline");
            foreach(var s in Wait(.2f))yield return s;
            foreach(var s in HoldAtLip(Vector3.left,.35f,15,"Final real westward approach presents the contextual drop"))yield return s;
            Require(court.Player.TryGetDropLanding(out _),"Final feedback position must present a safe-drop candidate");
            AssertRenderersRestored("Returning to the upper east bank");AssertTerraceCollision();
            finalPrepared=true;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"04-ready-for-user.png"));foreach(var s in Wait(.2f))yield return s;
        }

        private static IEnumerable<object> CheckVisibilityAndRestoration()
        {
            AssertCharacterVisible("Low B landing in the close camera");AssertTerraceCollision();
            foreach(var entry in rendererBaseline)if(entry.Key!=null&&entry.Key.enabled!=entry.Value)cutawayChanges.Add(entry.Key);
            Require(cutawayChanges.Count>0,"The low-deck visibility case did not exercise reversible scene rendering");
            report.Add("PASS Low B keeps the actor visible while the upper terrace remains collidable; changed renderer count="+cutawayChanges.Count);Flush();

            foreach(var s in Press(Key.V))yield return s;
            Require(court.CameraRig.IsOverview,"Actual V must enter overview");AssertRenderersRestored("Overview");AssertTerraceCollision();
            foreach(var s in Press(Key.Escape))yield return s;
            foreach(var s in Wait(.2f))yield return s;
            Require(!court.CameraRig.IsOverview&&!court.IsObserving,"Actual Escape must return to the close camera");AssertCharacterVisible("Return from overview");

            cutaway.enabled=false;foreach(var s in Wait(.12f))yield return s;
            AssertRenderersRestored("Disabling the visibility component");AssertTerraceCollision();
            cutaway.enabled=originalCutawayEnabled;foreach(var s in Wait(.2f))yield return s;
            AssertCharacterVisible("Re-enabling the visibility component");
            court.enabled=false;foreach(var s in Wait(.12f))yield return s;
            AssertRenderersRestored("Disabling the gameplay controller");AssertTerraceCollision();
            court.enabled=originalCourtEnabled;foreach(var s in Wait(.2f))yield return s;
            AssertCharacterVisible("Re-enabling gameplay");
            report.Add("PASS Overview, component disable and controller disable restore the changed scene renderers; resuming close play exposes the actor again");Flush();
        }
        private static void AssertTerraceCollision()
        {
            Transform platform=court.Bindings.stops[6].parent.Find("East_receiving_terrace");
            Collider terrace=platform!=null?platform.GetComponent<Collider>():null;
            Require(terrace!=null&&terrace.enabled&&terrace.gameObject.activeInHierarchy
                &&terrace.Raycast(new Ray(World(38.5f,18,24),Vector3.down),out var hit,4)
                &&Mathf.Abs(hit.point.y-offset.y-15)<.03f,"The upper receiving terrace lost its solid Y15 support");
        }
        private static void AssertRenderersRestored(string label)
        {
            foreach(var renderer in cutawayChanges)Require(renderer!=null&&renderer.enabled==rendererBaseline[renderer],label+" left scene renderer changed: "+(renderer!=null?renderer.name:"destroyed"));
        }
        private static void AssertCharacterVisible(string label)
        {
            int clear=0;var blockers=new List<string>();Camera camera=court.CameraRig.ViewCamera;
            foreach(float height in new[]{.55f,1f,1.45f})
            {
                Vector3 point=court.Player.FeetPosition+Vector3.up*height,viewport=camera.WorldToViewportPoint(point);
                Require(viewport.z>0&&viewport.x>0&&viewport.x<1&&viewport.y>0&&viewport.y<1,label+": actor left the camera frame");
                // Orthographic rays are parallel; a perspective ray from camera.position would test the wrong visible surface.
                Ray ray=camera.ViewportPointToRay(viewport);float distance=Vector3.Dot(point-ray.origin,ray.direction);string obstruction=null;
                foreach(var hit in Physics.RaycastAll(ray,distance-.04f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    if(hit.transform==court.Player.transform||hit.transform.IsChildOf(court.Player.transform))continue;
                    Renderer renderer=hit.collider.GetComponent<Renderer>();
                    if(renderer==null)renderer=hit.collider.GetComponentInParent<Renderer>();
                    if(renderer==null||!renderer.enabled||renderer.forceRenderingOff||renderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)continue;
                    obstruction=renderer.name;break;
                }
                if(obstruction==null)clear++;else blockers.Add(obstruction);
            }
            Require(clear>=2,label+": fewer than two body-height projection rays are visible; blockers="+string.Join(",",blockers));
        }

        private static IEnumerable<object> Drop(string label,float expectedY)
        {
            Require(court.Player.TryGetDropLanding(out Vector3 target),label+": no eligible landing");
            var probe=new DropProbe{start=court.Player.FeetPosition,last=court.Player.FeetPosition};int startFrame=Time.frameCount;
            foreach(var s in Press(Key.Space)){TrackDrop(probe,label);yield return s;}
            double deadline=EditorApplication.timeSinceStartup+5;
            while(court.Player.IsDropping)
            {
                Require(EditorApplication.timeSinceStartup<deadline,label+": timed out");TrackDrop(probe,label);yield return null;
            }
            TrackDrop(probe,label);foreach(var s in Wait(.15f))yield return s;
            Vector3 end=court.Player.FeetPosition;
            Require(probe.sawDropping&&probe.sawIntermediate&&Time.frameCount-startFrame>5,label+": did not show a real intermediate descent");
            Require(Mathf.Abs(end.y-offset.y-expectedY)<.15f&&Vector3.ProjectOnPlane(end-target,Vector3.up).magnitude<.4f,label+": missed the intended lower surface");
            report.Add($"PASS {label}: {probe.start-offset:F3} -> {end-offset:F3}; {Time.frameCount-startFrame}frames, maxStep={probe.maxStep:F3}m.");Flush();
        }
        private sealed class DropProbe{public Vector3 start,last;public float maxStep;public bool sawDropping,sawIntermediate;}
        private static void TrackDrop(DropProbe p,string label)
        {
            Vector3 now=court.Player.FeetPosition;p.sawDropping|=court.Player.IsDropping;p.sawIntermediate|=now.y<p.start.y-.2f&&court.Player.IsDropping;
            p.maxStep=Mathf.Max(p.maxStep,Vector3.Distance(now,p.last));
            Require(p.maxStep<.8f&&now.y<=p.start.y+.08f,label+": teleport/recovery discontinuity or upward jump");p.last=now;
        }
        private static IEnumerable<object> RefusedDrop(string label)
        {
            Require(!court.Player.TryGetDropLanding(out _),label+": unexpectedly advertises a landing");
            Vector3 before=court.Player.FeetPosition;bool dropping=false;float max=0;
            foreach(var s in Press(Key.Space)){dropping|=court.Player.IsDropping;max=Mathf.Max(max,Vector3.Distance(before,court.Player.FeetPosition));yield return s;}
            Require(!dropping&&!court.Player.IsDropping&&max<.12f,label+": Space initiated unsafe displacement");report.Add("PASS "+label);Flush();
        }
        private static IEnumerable<object> HoldAtLip(Vector3 direction,float seconds,float height,string label)
        {
            Vector3 last=court.Player.FeetPosition;double end=EditorApplication.timeSinceStartup+seconds;int frame=Time.frameCount;
            SetDirection(direction);
            while(EditorApplication.timeSinceStartup<end)
            {
                Vector3 now=court.Player.FeetPosition;Require(Mathf.Abs(now.y-offset.y-height)<.18f&&Vector3.Distance(now,last)<.8f,label+": ordinary movement dropped or recovered");last=now;yield return null;
            }
            SetInput();foreach(var s in Wait(.12f))yield return s;
            Require(Time.frameCount-frame>5,label+": no actual game frames");report.Add($"PASS {label}: local={court.Player.FeetPosition-offset:F3}");Flush();
        }
        private static IEnumerable<object> Walk(Vector3 target,float seconds,string label)
        {
            Vector3 start=court.Player.FeetPosition,last=start;Vector3 line=Vector3.ProjectOnPlane(target-start,Vector3.up);float length=line.magnitude;Vector3 tangent=line.normalized;double end=EditorApplication.timeSinceStartup+seconds;
            while(Vector3.ProjectOnPlane(target-court.Player.FeetPosition,Vector3.up).magnitude>.16f)
            {
                Vector3 p=court.Player.FeetPosition;float along=Vector3.Dot(Vector3.ProjectOnPlane(p-start,Vector3.up),tangent);Vector3 cross=Vector3.ProjectOnPlane(p-start,Vector3.up)-tangent*along;
                Require(EditorApplication.timeSinceStartup<end,label+": timeout at "+(p-offset));Require(Mathf.Abs(p.y-target.y)<.2f&&Vector3.Distance(p,last)<.8f,label+": fell or recovered");
                SetDirection((Vector3.ProjectOnPlane(start+tangent*Mathf.Clamp(along+.45f,0,length)-p,Vector3.up)-cross*2).normalized);last=p;foreach(var s in Wait(.025f))yield return s;
            }
            SetInput();foreach(var s in Wait(.12f))yield return s;report.Add($"PASS {label}: {start-offset:F3} -> {court.Player.FeetPosition-offset:F3}");Flush();
        }
        private static void Setup(RainCargoState state,Vector3 feet,string label)
        {
            RestoreFixture();court.ResetCourt();typeof(RainCargoController).GetField("state",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(court,state);court.Simulate(0);Physics.SyncTransforms();court.Player.Teleport(feet);
            report.Add("SETUP "+label+"; "+Puzzle()+"; local="+(feet-offset).ToString("F3"));Flush();
        }
        private static RainCargoState PartialB(float height)
        {
            var s=new RainCargoState();MoveState(s);s.InsertCard(0);s.ToggleRain(0);s.Advance(10);s.RemoveCard(0);s.InsertCard(1);s.ToggleRain(1);s.Advance(10);s.RemoveCard(1);s.ToggleDrain(0);s.Advance(10);s.ToggleDrain(0);
            while(s.CartStop<5)MoveState(s);Require(s.TransferWater(),"Partial-B setup transfer failed");s.Advance(10);s.ToggleDrain(1);s.Advance((3-height)/RainCargoState.WaterSpeed);s.ToggleDrain(1);return s;
        }
        private static void MoveState(RainCargoState s){Require(s.BeginCartMove(1)&&s.CompleteCartMove(),"Isolated state setup rejected its cargo move");}
        private static string Puzzle(){var s=court.State;return $"A{s.WaterA:F4}/B{s.WaterB:F4}/tank{s.TankWater:F4}/card{s.CardStation}/cart{s.CartStop}/{s.PendingCartStop}/rain{s.RainActive}/drain{s.DrainAOpen},{s.DrainBOpen}/transfer{s.Transferring}";}
        private static void RestoreFixture(){if(!fixtureActive)return;court.Bindings.cart.SetPositionAndRotation(savedCartPosition,savedCartRotation);court.enabled=savedControllerEnabled;fixtureActive=false;Physics.SyncTransforms();}
        private static void Tick(){if(sequence==null)return;try{if(!EditorApplication.isPlaying||EditorApplication.isPaused)throw new InvalidOperationException("Play Mode stopped/paused");if(sequence.MoveNext())return;Finish(true,null);}catch(Exception e){Finish(false,e);}}
        private static void Finish(bool passed,Exception error)
        {
            EditorApplication.update-=Tick;InputSystem.onBeforeUpdate-=BeforeInput;InputSystem.onAfterUpdate-=AfterInput;AssemblyReloadEvents.beforeAssemblyReload-=Abort;EditorApplication.playModeStateChanged-=OnPlayChanged;sequence=null;pending=observingKey=false;
            try
            {
                if(keyboard!=null&&keyboard.added){InputState.Change(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);}if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();
                if(court!=null){RestoreFixture();court.enabled=originalCourtEnabled;if(cutaway!=null)cutaway.enabled=originalCutawayEnabled;if(!finalPrepared){court.ResetCourt();court.Player.Teleport(World(32.3f,15.06f,18));Physics.SyncTransforms();}report.Add("FINAL TEST BASELINE: "+Puzzle()+"; player="+(court.Player.FeetPosition-offset).ToString("F3")+"; actual westward approach="+finalPrepared);}
            }
            catch(Exception e){passed=false;report.Add("CLEANUP FAILURE "+e.Message);}
            if(error!=null)report.Add("FAIL "+error.Message);report.Add(passed?"RAIN_CARGO_DROP_PASSED":"RAIN_CARGO_DROP_FAILED");Flush();File.WriteAllLines(Path.Combine(Output,"checks-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".txt"),report);
            if(passed)Debug.Log("RAIN_CARGO_DROP_PASSED");else Debug.LogError("RAIN_CARGO_DROP_FAILED: "+error?.Message);
        }
        private static void BeforeInput(){if(!pending||keyboard==null||!keyboard.added||InputState.currentUpdateType!=InputUpdateType.Dynamic)return;InputSystem.QueueStateEvent(keyboard,requested);queuedSerial=serial;pending=false;}
        private static void AfterInput(){if(!observingKey||keyboard==null||InputState.currentUpdateType!=InputUpdateType.Dynamic)return;var k=keyboard[keyObserved];lastHeld=k.isPressed;if(k.wasPressedThisFrame)downEdges++;if(k.wasReleasedThisFrame)upEdges++;}
        private static void SetInput(params Key[] keys){requested=new KeyboardState(keys);serial++;pending=true;}
        private static IEnumerable<object> Press(Key key)
        {
            keyObserved=key;observingKey=true;downEdges=upEdges=0;lastHeld=false;SetInput(key);foreach(var s in WaitInput(serial))yield return s;Require(downEdges==1,"Input "+key+" did not have exactly one Dynamic press");SetInput();foreach(var s in WaitInput(serial))yield return s;observingKey=false;Require(downEdges==1&&upEdges>=1&&!lastHeld,"Input "+key+" did not release cleanly in Dynamic");
        }
        private static IEnumerable<object> WaitInput(int id){int f=Time.frameCount;double t=EditorApplication.timeSinceStartup;while(queuedSerial<id||Time.frameCount<f+2||EditorApplication.timeSinceStartup<t+.12){Require(EditorApplication.timeSinceStartup<t+5,"Input/game-frame timeout");yield return null;}}
        private static void SetDirection(Vector3 desired)
        {
            Transform c=court.CameraRig.ViewCamera.transform;Vector3 f=Vector3.ProjectOnPlane(c.forward,Vector3.up).normalized,r=Vector3.ProjectOnPlane(c.right,Vector3.up).normalized;float best=-2;int bx=0,by=0;
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++){if(x==0&&y==0)continue;float a=Vector3.Dot((r*x+f*y).normalized,desired);if(a<=best)continue;best=a;bx=x;by=y;}
            var keys=new List<Key>();if(bx!=0)keys.Add(bx>0?Key.D:Key.A);if(by!=0)keys.Add(by>0?Key.W:Key.S);SetInput(keys.ToArray());
        }
        private static IEnumerable<object> Wait(float s){double t=EditorApplication.timeSinceStartup+s;while(EditorApplication.timeSinceStartup<t)yield return null;}
        private static Vector3 World(float x,float y,float z)=>offset+new Vector3(x,y,z);
        private static void Require(bool yes,string why){if(!yes)throw new InvalidOperationException(why);}
        private static void Flush()=>File.WriteAllLines(Path.Combine(Output,"checks.txt"),report);
        private static void Abort()=>Finish(false,new InvalidOperationException("Assembly reload interrupted checks"));
        private static void OnPlayChanged(PlayModeStateChange s){if(sequence!=null&&s==PlayModeStateChange.ExitingPlayMode)Finish(false,new InvalidOperationException("Play Mode exited"));}
    }
}

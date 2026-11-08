using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    public static partial class SkywellChecks
    {
        public static void RunStory()=>StartCheck("SKYWELL_STORY",StorySequence());

        private static IEnumerator CompleteStoryDialogue(string label)
        {
            Require(court.Story&&court.Story.DialogueOpen,"Dialogue is open: "+label);
            for(int i=0;i<10&&court.Story.DialogueOpen;i++)yield return Press(Key.E);
            Require(!court.Story.DialogueOpen&&!court.Story.InputBlocked,"Real E acknowledgements complete dialogue: "+label);
        }
        private static Vector3 StoryApproach(int index)
        {
            Vector3 offset=index==0?Vector3.right*.9f:index==1?new Vector3(1.2f,0,-.5f):Vector3.left*1.1f;
            return court.Story.InteractionPoint(index).position+offset+Vector3.up*.03f;
        }
        private static IEnumerator StoryFixturePlace(int index)
        {
            Vector3 at=StoryApproach(index);court.Player.Teleport(at);yield return Wait(.5f);
            report.Add("STORY FIXTURE "+index+" feet="+court.Player.FeetPosition);SaveProgress();
        }

        // Called by the complete route after reaching the northern weather console.
        // Every crossing, conversation and return uses the production movement/interaction controls.
        private static IEnumerator StoryContinuousLoop()
        {
            var story=court.Story;var collector=court.Optics.Collector;
            Require(story&&!collector.IsRepaired&&!story.HasGem&&!story.GemGiven&&!court.Observation.HasViewed,"The first northern visit begins with a missing mirror heart and an unseen eastern view.");
            yield return Walk(StoryApproach(0),"Inspect the damaged northern collector from its maintenance spur");
            yield return Press(Key.E);Require(story.DialogueOpen&&story.Inspected&&!collector.IsRepaired,"E inspection reveals the broken mirror without repairing it.");
            yield return Wait(.4f);Capture(Path.Combine(Folder,"story-broken-mirror-dialogue.png"));
            yield return CompleteStoryDialogue("first mirror inspection");
            yield return Walk(court.Origin+new Vector3(-3,6,20),"Return along the maintenance spur");
            yield return Walk(court.Origin+new Vector3(-3,6,24.5f),"Walk clear of the calibration target toward the north villager");
            yield return Walk(StoryApproach(1),"Approach the northern mirror keeper");
            yield return Press(Key.E);Require(story.DialogueOpen&&!story.HasGem,"The north villager offers a clue, not the repair item.");
            yield return Wait(.4f);Capture(Path.Combine(Folder,"story-north-villager-dialogue.png"));
            yield return CompleteStoryDialogue("north villager clue");
            yield return Walk(court.Origin+new Vector3(-3,6,24.5f),"Leave the north villager through the clear court aisle");
            yield return Walk(court.Origin+new Vector3(-3,6,20),"Return to the bridge spur without crossing the calibration target");
            yield return ToTowerConsole();yield return TurnTowerTo(90);
            yield return ToEasternObservation();
            yield return Press(Key.E);Require(court.Observation.IsOpen&&court.Observation.HasViewed,"Actual E observation shows the shaded village and records that it was seen.");
            yield return Wait(.4f);Capture(Path.Combine(Folder,"story-shaded-village-observation.png"));
            yield return Press(Key.E);Require(!court.Observation.IsOpen&&!court.IsObserving,"Closing the landscape restores normal exploration.");
            yield return Walk(StoryApproach(2),"Approach the eastern lamp keeper after viewing the village");
            yield return Press(Key.E);Require(story.DialogueOpen&&!story.HasGem,"The eastern conversation begins before the gift is committed.");
            yield return Wait(.4f);Capture(Path.Combine(Folder,"story-east-villager-dialogue.png"));
            yield return CompleteStoryDialogue("eastern mirror-heart gift");
            Require(story.HasGem&&story.GemGiven&&!collector.IsRepaired,"Completing the eastern conversation grants the single mirror heart.");
            yield return FromEasternVillageToTower();yield return TurnTowerTo(0);yield return ToCollectorStation();
            yield return Walk(StoryApproach(0),"Return to the broken collector with the mirror heart");
            yield return Press(Key.E);Require(story.DialogueOpen&&story.HasGem&&!collector.IsRepaired,"The repair conversation retains the item until its final acknowledgement.");
            yield return CompleteStoryDialogue("install the mirror heart");
            Require(collector.IsRepaired&&!story.HasGem&&story.GemGiven&&!collector.Active,"Repair installs the heart once; it does not create sunlight or skip the Wind/Sunny puzzle.");
            yield return Wait(.4f);Capture(Path.Combine(Folder,"story-mirror-heart-installed.png"));
            yield return Walk(court.Origin+new Vector3(-3,6,20),"Leave the repaired collector along the maintenance spur");
            yield return Walk(court.Origin+new Vector3(-5,6,19.7f),"Return to the local weather console after repair");
        }
        private static IEnumerator ToEasternObservation()
        {
            yield return Walk(Local(-2.4f,1),"Tower weather aisle toward eastern village");
            yield return Walk(Local(-2.4f,3.9f),"Pass the cart-side clearance toward eastern bridge");
            yield return Walk(Local(0,4.8f),"Eastern tower doorway");
            yield return Walk(court.Origin+new Vector3(10,6,0),"Cross the connected eastern docking seam");
            yield return Walk(new Vector3(46,26.03f,108),"Walk the eastern freight bridge to the village");
            yield return Walk(new Vector3(48,26.03f,108),"Eastern observation landing");
            yield return Walk(court.Observation.transform.position+new Vector3(0,.03f,-.8f),"Approach the shaded-village observation marker");
        }
        private static IEnumerator FromEasternVillageToTower()
        {
            yield return Walk(new Vector3(48,26.03f,108),"Return from the eastern villager to the bridge landing");
            yield return Walk(court.Origin+new Vector3(10,6,0),"Walk back along the eastern freight bridge");
            yield return Walk(Local(0,4.8f),"Re-enter the tower from the eastern docking seam");
            yield return Walk(Local(-2.8f,3.9f),"Return along the tower cart aisle");
            yield return Walk(Local(-2.4f,-.8f),"Reach the tower weather console for the northern return");
        }

        private static IEnumerator StorySequence()
        {
            report.Add("EXPLICIT STORY FIXTURES: player placed beside the three authored story targets and observation marker. Actual E/Escape/R, WASD, camera and weather inputs drive every dialogue, cancellation, gift and repair. No item, observation or repair state is injected. Continuous bridge traversal is covered by the complete route separately.");
            court.ResetCourt();yield return Wait(.4f);var story=court.Story;var collector=court.Optics.Collector;
            Require(story&&story.InteractionPoint(0)&&story.InteractionPoint(1)&&story.InteractionPoint(2),"All authored story points are connected.");
            Require(!story.DialogueOpen&&!story.HasGem&&!story.GemGiven&&!story.Inspected&&!court.Observation.HasViewed&&!collector.IsRepaired,"Reset clears dialogue, observation, mirror-heart inventory and repair state.");

            yield return StoryFixturePlace(0);
            yield return EnsureCameraMode(true);yield return CameraMousePulse(new Vector2(65,-20));
            Quaternion gameplayRotation=court.CameraRig.ViewCamera.transform.rotation;bool shoulder=court.CameraRig.IsShoulderView;
            var selected=court.SelectedWeather;var tokens=court.Consoles.Select(s=>s.Token).ToArray();
            Vector3 actorBefore=court.Player.FeetPosition;
            yield return Press(Key.E);Require(story.DialogueOpen&&story.Inspected&&!collector.IsRepaired,"Inspecting the damaged collector opens its dialogue without silently repairing it.");
            yield return Wait(.4f);
            Require(court.Player.enabled&&court.IsObserving&&!court.CameraRig.enabled&&!string.IsNullOrEmpty(story.Speaker)&&!string.IsNullOrEmpty(story.Text),"Dialogue uses the normal player object, a modal input lock and the authored close camera.");
            Capture(Path.Combine(Folder,"story-check-inspection-closeup.png"));
            input=new KeyboardState(Key.W,Key.D,Key.Space,Key.Tab,Key.Digit2,Key.Q,Key.V,Key.F,Key.Z);yield return Wait(.5f);
            input=new KeyboardState();yield return Wait(.2f);
            Require(story.DialogueOpen&&Vector3.ProjectOnPlane(court.Player.FeetPosition-actorBefore,Vector3.up).magnitude<.02f&&!court.Player.IsDropping,"Movement and drop input remain blocked beneath the dialogue.");
            Require(!court.WeatherTargeting&&!court.WeatherSelectionHeld&&court.SelectedWeather==selected&&tokens.SequenceEqual(court.Consoles.Select(s=>s.Token)),"Dialogue input cannot select weather or change physical tokens.");
            Require(court.CameraRig.IsShoulderView==shoulder,"Dialogue input preserves the selected gameplay camera mode while its close camera is active.");
            input=new KeyboardState(Key.Escape,Key.Z);yield return Wait(.15f);input=new KeyboardState();yield return Wait(.3f);
            Require(!story.DialogueOpen&&!story.InputBlocked&&court.CameraRig.enabled&&!court.CameraRig.InputSuppressed&&!court.IsObserving,"Escape restores the normal camera driver and movement without leaving a modal lock.");
            Require(court.CameraRig.IsShoulderView==shoulder&&Quaternion.Angle(gameplayRotation,court.CameraRig.ViewCamera.transform.rotation)<.03f,"Escape plus Z on the closing frame restores the same gameplay mode, yaw and pitch without another toggle.");
            Vector3 moveStart=court.Player.FeetPosition;yield return MoveInDirection(Vector3.right,.2f);
            Require(Vector3.ProjectOnPlane(court.Player.FeetPosition-moveStart,Vector3.up).magnitude>.15f,"Normal movement resumes after closing the dialogue.");
            yield return StoryFixturePlace(0);yield return Press(Key.E);
            Require(story.DialogueOpen&&!court.CameraRig.enabled,"A live inspection dialogue owns the close camera before the component-disable lifecycle test.");
            story.enabled=false;yield return Wait(.3f);
            Require(!story.DialogueOpen&&!story.InputBlocked&&court.CameraRig.enabled&&!court.CameraRig.InputSuppressed&&!story.HasGem&&!story.GemGiven,"Disabling the live Story component closes its dialogue, restores the camera and grants no item.");
            story.enabled=true;yield return Wait(.3f);
            yield return Press(Key.E);Require(story.DialogueOpen,"Re-enabling Story restores the normal E interaction without stale modal state.");
            yield return CompleteStoryDialogue("inspection after component re-enable");

            yield return StoryFixturePlace(1);yield return Press(Key.E);yield return Wait(.4f);Capture(Path.Combine(Folder,"story-check-north-closeup.png"));yield return CompleteStoryDialogue("north clue fixture");
            Require(!story.HasGem&&!story.GemGiven&&!collector.IsRepaired,"The north hint does not grant or install a mirror heart.");
            yield return StoryFixturePlace(2);yield return Press(Key.E);yield return CompleteStoryDialogue("east villager before observation");
            Require(!story.HasGem&&!story.GemGiven,"Talking before seeing the shaded village does not grant the item.");
            court.Player.Teleport(court.Observation.transform.position+new Vector3(0,.03f,-.8f));yield return Wait(.4f);
            yield return Press(Key.E);Require(court.Observation.IsOpen&&court.Observation.HasViewed,"Opening the real observation image records the required view.");
            yield return Press(Key.E);Require(!court.Observation.IsOpen&&!court.IsObserving,"The image closes through its normal E interaction.");

            yield return StoryFixturePlace(2);yield return Press(Key.E);Require(story.DialogueOpen&&!story.HasGem,"The gift is pending while its conversation is open.");
            story.enabled=false;yield return Wait(.3f);
            Require(!story.DialogueOpen&&!story.HasGem&&!story.GemGiven&&court.CameraRig.enabled&&!court.CameraRig.InputSuppressed,"Live disable also cancels a pending gift without committing it or retaining the close camera.");
            story.enabled=true;yield return Wait(.3f);yield return Press(Key.E);
            Require(story.DialogueOpen&&!story.HasGem,"Re-enabled Story can offer the unclaimed gift again.");
            yield return Press(Key.Escape);Require(!story.HasGem&&!story.GemGiven&&!story.DialogueOpen,"Cancelling the gift conversation grants nothing.");
            yield return Press(Key.E);yield return Wait(.4f);Capture(Path.Combine(Folder,"story-check-east-closeup.png"));yield return CompleteStoryDialogue("complete eastern gift");
            Require(story.HasGem&&story.GemGiven&&!collector.IsRepaired,"Final acknowledgement grants the single mirror heart.");
            yield return Press(Key.E);yield return CompleteStoryDialogue("repeat east dialogue while carrying gem");
            Require(story.HasGem&&story.GemGiven&&!collector.IsRepaired,"Repeating the giver's dialogue leaves the existing single item unchanged.");

            yield return CancelledRepairRetainsGemAndCompletedRepairCannotAwardItAgain();
        }

        // Migrated from the EditMode IEnumerator: this case requires real game frames between
        // closing and reopening a conversation. Keep every inventory, visual and reset assertion.
        private static IEnumerator CancelledRepairRetainsGemAndCompletedRepairCannotAwardItAgain()
        {
            const string scenario="CancelledRepairRetainsGemAndCompletedRepairCannotAwardItAgain";
            report.Add("PLAY CASE BEGIN: "+scenario);SaveProgress();
            var story=court.Story;var collector=court.Optics.Collector;
            GameObject installedGem;
            using(var serialized=new UnityEditor.SerializedObject(collector))
                installedGem=serialized.FindProperty("installedGem").objectReferenceValue as GameObject;
            Require(installedGem!=null,scenario+": the actual serialized installed-gem visual is connected.");
            Require(story.HasGem&&story.GemGiven&&!collector.IsRepaired&&!installedGem.activeSelf,
                scenario+": completed gift starts with one carried gem and an empty mirror socket.");

            yield return StoryFixturePlace(0);yield return Press(Key.E);
            Require(story.DialogueOpen&&story.HasGem&&!collector.IsRepaired,
                scenario+": real E opens repair while retaining the carried item.");
            yield return Press(Key.E);
            Require(story.DialogueOpen&&story.HasGem&&story.GemGiven&&!collector.IsRepaired&&!installedGem.activeSelf,
                scenario+": acknowledging the first repair page still does not consume or install the gem.");
            yield return Press(Key.Escape);
            Require(!story.DialogueOpen&&!story.InputBlocked&&story.HasGem&&story.GemGiven&&!collector.IsRepaired&&!installedGem.activeSelf,
                scenario+": Escape cancels pending repair, keeps the gem, and clears the closing-frame gate in Play.");

            yield return Press(Key.E);
            Require(story.DialogueOpen,scenario+": a new E press reopens repair after cancellation.");
            yield return CompleteStoryDialogue("commit collector repair after cancellation");
            Require(collector.IsRepaired&&installedGem.activeSelf&&!story.HasGem&&story.GemGiven&&!collector.Active,
                scenario+": final acknowledgement consumes the item once, installs its visual, and still requires real Sunny light.");
            yield return Press(Key.E);yield return Wait(.4f);Capture(Path.Combine(Folder,"story-check-installed-closeup.png"));
            yield return CompleteStoryDialogue("inspect already repaired mirror");
            Require(collector.IsRepaired&&installedGem.activeSelf&&!story.HasGem&&story.GemGiven,
                scenario+": repeated inspection preserves the installation without returning or duplicating the item.");

            yield return StoryFixturePlace(2);yield return Press(Key.E);
            yield return CompleteStoryDialogue("repeat giver after repair");
            Require(collector.IsRepaired&&installedGem.activeSelf&&!story.HasGem&&story.GemGiven,
                scenario+": a completed repeat giver dialogue cannot grant a second gem.");
            yield return Press(Key.E);Require(story.DialogueOpen,scenario+": a new dialogue opens before reset.");
            yield return Press(Key.R);yield return Wait(.3f);
            Require(!story.DialogueOpen&&!story.InputBlocked&&!story.HasGem&&!story.GemGiven&&!story.Inspected&&
                !court.Observation.HasViewed&&!collector.IsRepaired&&!installedGem.activeSelf,
                scenario+": real R clears dialogue, observation, gift ownership, carried gem, repair and installed visual.");
            Require(court.CameraRig.enabled&&!court.CameraRig.InputSuppressed&&!court.IsObserving&&court.Player.enabled,
                scenario+": reset releases the camera and movement locks.");
            CheckReset();
            report.Add("PLAY CASE PASSED: "+scenario);SaveProgress();
        }
    }
}

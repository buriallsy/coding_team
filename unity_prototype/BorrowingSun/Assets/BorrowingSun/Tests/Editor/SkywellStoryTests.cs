using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class SkywellStoryTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private SkywellStory story;
        private SkywellObservation observation;
        private SkywellSunCollector collector;
        private CourtPlayer player;
        private CourtCamera cameraRig;
        private Camera viewCamera;
        private GameObject installedGem;

        private GameObject Make(string name)
        {
            var go = new GameObject(name); objects.Add(go); return go;
        }

        [SetUp]
        public void SetUp()
        {
            var owner = Make("story court").AddComponent<SkywellController>();
            viewCamera = Make("story camera").AddComponent<Camera>();
            cameraRig = viewCamera.gameObject.AddComponent<CourtCamera>();
            player = Make("story player").AddComponent<CourtPlayer>();
            cameraRig.Initialize(null, player, viewCamera);
            player.InitializeGameplay(owner, viewCamera, Vector3.zero);
            using (var serialized = new SerializedObject(owner))
            {
                serialized.FindProperty("player").objectReferenceValue = player;
                serialized.FindProperty("cameraRig").objectReferenceValue = cameraRig;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            observation = Make("story observation").AddComponent<SkywellObservation>();
            observation.Configure(Texture2D.whiteTexture, null);
            owner.ConfigureObservation(observation);
            collector = Make("story collector").AddComponent<SkywellSunCollector>();
            installedGem = Make("installed mirror heart");
            collector.ConfigureRepair(installedGem);
            var inspection = Make("inspection").transform;
            var north = Make("north keeper").transform; north.position = Vector3.right * 10;
            var east = Make("east keeper").transform; east.position = Vector3.right * 20;
            story = owner.gameObject.AddComponent<SkywellStory>();
            story.Configure(owner, observation, collector, inspection, north, east);
            owner.ConfigureStory(story);
        }

        [TearDown]
        public void Clean()
        {
            if (story) story.Close();
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        private void Approach(int index) => player.Teleport(story.InteractionPoint(index).position + Vector3.back);
        private void ViewVillage() { observation.Open(); observation.Close(); Assert.IsTrue(observation.HasViewed); }
        private void FinishDialogue()
        {
            for (int i = 0; i < 10 && story.DialogueOpen; i++) story.Advance();
            Assert.IsFalse(story.DialogueOpen, "The authored conversation must reach an explicit completion.");
        }

        [Test]
        public void EastKeeperCannotGiveGemBeforeObservation()
        {
            Approach(2); Assert.IsTrue(story.TryInteract(2)); FinishDialogue();
            Assert.IsFalse(observation.HasViewed);
            Assert.IsFalse(story.HasGem || story.GemGiven || collector.IsRepaired);
        }

        [Test]
        public void CancellingGiftDialogueDoesNotGrantItsPendingItem()
        {
            ViewVillage(); Approach(2); story.TryInteract(2); story.Advance();
            Assert.IsTrue(story.DialogueOpen);
            story.Close(); story.Advance();
            Assert.IsFalse(story.HasGem || story.GemGiven);
            Assert.IsTrue(story.InputBlocked, "The closing frame must not reopen or operate a nearby interaction.");
        }

        [Test]
        public void GiftCommitsOnlyWhenTheFinalSpokenPageIsAcknowledged()
        {
            ViewVillage(); Approach(2); story.TryInteract(2);
            Assert.IsFalse(story.HasGem || story.GemGiven);
            story.Advance(); Assert.IsFalse(story.HasGem || story.GemGiven);
            story.Advance(); Assert.IsFalse(story.HasGem || story.GemGiven);
            Assert.IsTrue(story.DialogueOpen);
            story.Advance();
            Assert.IsFalse(story.DialogueOpen);
            Assert.IsTrue(story.HasGem && story.GemGiven);
            story.Advance();
            Assert.IsTrue(story.HasGem && story.GemGiven, "Advancing a closed dialogue cannot run another completion.");
        }

        [Test]
        public void InspectionWithoutGemCannotRepairTheCollector()
        {
            Approach(0); Assert.IsTrue(story.TryInteract(0)); FinishDialogue();
            Assert.IsTrue(story.Inspected);
            Assert.IsFalse(collector.IsRepaired || installedGem.activeSelf || story.HasGem);
        }

        // Cross-frame cancellation, re-entry, single consumption and reset are exercised by
        // SkywellStoryChecks.CancelledRepairRetainsGemAndCompletedRepairCannotAwardItAgain
        // in the actual Play-mode sequence; Editor updates do not advance Time.frameCount.

        [Test]
        public void RepairedCollectorRejectsAnotherInstallation()
        {
            Assert.IsTrue(collector.TryInstallGem());
            Assert.IsTrue(collector.IsRepaired && installedGem.activeSelf);
            Assert.IsFalse(collector.TryInstallGem(), "A repaired collector rejects a second installation request.");
            Assert.IsTrue(collector.IsRepaired && installedGem.activeSelf, "A rejected request preserves the existing installed mirror heart.");
        }

        [Test]
        public void ClosingConversationCancelsGiftAndRestoresCameraDriverAndProjection()
        {
            ViewVillage(); Approach(2);
            bool originalEnabled = cameraRig.enabled, originalProjection = viewCamera.orthographic;
            float originalFov = viewCamera.fieldOfView, originalSize = viewCamera.orthographicSize;
            Vector3 position = viewCamera.transform.position;
            Quaternion rotation = viewCamera.transform.rotation;
            story.TryInteract(2); story.Advance();
            Assert.IsFalse(cameraRig.enabled); Assert.IsFalse(viewCamera.orthographic);
            Assert.IsTrue(player.enabled, "Conversation must preserve the player component that delivers E and R.");
            // Represent a camera that has reached its cinematic pose before public cleanup.
            viewCamera.transform.SetPositionAndRotation(Vector3.one * 7, Quaternion.Euler(10, 20, 0));
            viewCamera.fieldOfView = 42;
            story.Close();
            Assert.IsFalse(story.DialogueOpen || story.HasGem || story.GemGiven);
            Assert.That(cameraRig.enabled, Is.EqualTo(originalEnabled));
            Assert.That(viewCamera.orthographic, Is.EqualTo(originalProjection));
            Assert.That(viewCamera.fieldOfView, Is.EqualTo(originalFov));
            Assert.That(viewCamera.orthographicSize, Is.EqualTo(originalSize));
            Assert.That(Vector3.Distance(viewCamera.transform.position, position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(viewCamera.transform.rotation, rotation), Is.LessThan(.0001f));
        }

        [Test]
        public void ConversationCannotBeginOutsideItsPhysicalInteractionRange()
        {
            ViewVillage(); player.Teleport(story.InteractionPoint(2).position + Vector3.right * (story.InteractionRadius + .1f));
            Assert.IsFalse(story.TryInteract(2));
            Assert.IsFalse(story.DialogueOpen || story.HasGem || story.GemGiven);
            Assert.IsTrue(cameraRig.enabled);
        }

        [Test]
        public void RealSunnyAndAlignedMirrorStillNeedARepairedReceivingSurface()
        {
            var plate = Make("repair optical plate").transform; plate.position = new Vector3(3000, 10, 3000);
            var sun = Make("repair test sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(45, 0, 0);
            var station = Make("repair weather station").AddComponent<SkywellWeatherConsole>();
            var zone = Make("repair weather zone").AddComponent<SkywellWeatherZone>(); zone.Initialize(station, plate, 12);
            var calibration = Make("repair calibration").transform; calibration.position = plate.position + Vector3.forward * 8;
            var relay = Make("repair relay").transform; relay.position = plate.position + Vector3.right * 10;
            using (var serialized = new SerializedObject(collector))
            {
                serialized.FindProperty("collectorPlate").objectReferenceValue = plate;
                serialized.FindProperty("sunLight").objectReferenceValue = sun;
                serialized.FindProperty("weatherZone").objectReferenceValue = zone;
                var targets = serialized.FindProperty("focusTargets"); targets.arraySize = 2;
                targets.GetArrayElementAtIndex(0).objectReferenceValue = calibration;
                targets.GetArrayElementAtIndex(1).objectReferenceValue = relay;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            collector.ResetCollector(); station.Request(SkywellToken.Sun); station.Tick(2); Physics.SyncTransforms(); collector.Tick(2);
            Assert.IsTrue(collector.HasDirectSunlight && collector.IsLocked && zone.IsSunny);
            Assert.IsFalse(collector.Active, "Sunny and a valid physical aim cannot bypass the missing mirror heart.");
            Assert.IsTrue(collector.TryInstallGem()); collector.Tick(.1f);
            Assert.IsTrue(collector.Active);
            station.ReturnToken(); collector.Tick(.1f);
            Assert.IsTrue(collector.Active, "Returning Sunny retains clear weather after repair.");
            station.Request(SkywellToken.Rain); station.Tick(2); collector.Tick(.1f);
            Assert.IsFalse(collector.Active, "Repair does not bypass the local weather condition.");
            station.Request(SkywellToken.Sun); station.Tick(2); collector.ResetCollector(); collector.Tick(.1f);
            Assert.IsFalse(collector.IsRepaired || collector.Active || installedGem.activeSelf, "Replay restores the missing mirror heart even when the fixture's sky remains clear.");
        }
    }
}

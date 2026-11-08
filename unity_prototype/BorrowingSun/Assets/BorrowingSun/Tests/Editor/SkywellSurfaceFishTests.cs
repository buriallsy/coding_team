using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class SkywellSurfaceFishTests
    {
        private readonly List<GameObject> objects=new List<GameObject>();
        private GameObject Make(string name){var value=new GameObject(name);objects.Add(value);return value;}
        [TearDown] public void Cleanup(){foreach(var value in objects)if(value)Object.DestroyImmediate(value);objects.Clear();}

        private SkywellController Owner()
        {
            var owner=Make("surface court").AddComponent<SkywellController>();
            owner.transform.position=new Vector3(100,70,200);
            var player=Make("surface swimmer").AddComponent<CourtPlayer>();
            using(var serialized=new SerializedObject(owner))
            {
                serialized.FindProperty("origin").vector3Value=new Vector3(12,20,108);
                serialized.FindProperty("player").objectReferenceValue=player;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return owner;
        }
        private SkywellFish Fish(SkywellController owner)
        {
            var journey=owner.gameObject.AddComponent<SkywellJourney>();
            var fish=owner.gameObject.AddComponent<SkywellFish>();
            fish.Initialize(owner,journey,Make("fish visual").transform,null);
            return fish;
        }

        [Test] public void SurfaceConfigurationAndReplayUseTheConfiguredLayerWithoutMovingTheSceneRoot()
        {
            var owner=Owner();var fish=Fish(owner);Vector3 rootBefore=owner.transform.position;
            fish.ConfigureSurfaceWater(14.5f);
            Assert.IsTrue(fish.SurfaceWaterConfigured);
            Assert.That(fish.PatrolHeight,Is.EqualTo(14.15f).Within(.001f));
            Assert.That(Vector3.Distance(fish.Body.position,new Vector3(1,14.15f,73)),Is.LessThan(.001f));
            fish.LureAt(new Vector3(4,15.15f,82));fish.ResetFish();
            Assert.That(fish.Body.position.y,Is.EqualTo(fish.PatrolHeight).Within(.001f));
            Assert.That(fish.Phase,Is.EqualTo(SkywellFishPhase.Patrol));
            Assert.That(fish.LureCount,Is.Zero);
            Assert.That(owner.transform.position,Is.EqualTo(rootBefore));
            fish.ConfigureSurfaceWater(18.25f);fish.ResetFish();
            Assert.That(fish.Body.position.y,Is.EqualTo(17.9f).Within(.001f),"Reconfiguration must invalidate the old patrol-node height.");
        }

        [Test] public void SurfaceFishSightStillUsesActualBlockingCollision()
        {
            var owner=Owner();var fish=Fish(owner);fish.ConfigureSurfaceWater(14.5f);
            Vector3 start=fish.Body.position,end=start+Vector3.right*4;
            var obstruction=Make("real surface cover");obstruction.transform.position=start+Vector3.right*2;
            var collider=obstruction.AddComponent<BoxCollider>();collider.size=new Vector3(.5f,2,2);
            Physics.SyncTransforms();
            Assert.IsFalse(fish.Clear(start,end,.08f),"A solid pillar must block the real fish-to-swimmer cast at the new height.");
            collider.enabled=false;Physics.SyncTransforms();
            Assert.IsTrue(fish.Clear(start,end,.08f),"Removing only the physical obstruction restores the same ray.");
        }

        [Test] public void BellSurfaceResetUsesCourtCoordinatesEvenUnderADifferentVisualParent()
        {
            var owner=Owner();var bell=owner.gameObject.AddComponent<SkywellWindBell>();
            var parent=Make("bell visual parent").transform;parent.SetPositionAndRotation(new Vector3(4,50,-9),Quaternion.Euler(0,28,0));parent.localScale=Vector3.one*2;
            var vent=Make("actual bell sound source").transform;vent.SetParent(parent,false);
            bell.Initialize(owner,vent,null);bell.ConfigureSurfaceWater(14.5f);bell.ResetBell();
            Assert.IsTrue(bell.SurfaceWaterConfigured);
            Assert.That(bell.TravelHeight,Is.EqualTo(15.15f).Within(.001f));
            Assert.That(vent.position.x,Is.EqualTo(owner.Origin.x-18).Within(.001f));
            Assert.That(vent.position.y,Is.EqualTo(bell.TravelHeight).Within(.001f));
            Assert.That(vent.position.z,Is.EqualTo(owner.Origin.z-26).Within(.001f));
            bell.ConfigureSurfaceWater(17);bell.ResetBell();
            Assert.That(vent.position.y,Is.EqualTo(17.65f).Within(.001f));
        }

    }
}

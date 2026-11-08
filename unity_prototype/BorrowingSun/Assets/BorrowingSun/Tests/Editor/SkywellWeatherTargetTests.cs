using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class SkywellWeatherTargetTests
    {
        private readonly List<Object> created=new List<Object>();
        private static readonly Vector3 Origin=new Vector3(6000,1000,5000);
        private static readonly int BaseColor=Shader.PropertyToID("_BaseColor");

        private SkywellWeatherTarget Frame(out MeshRenderer renderer)
        {
            var go=new GameObject("weather target frame test");created.Add(go);go.transform.position=Origin;
            var mesh=new Mesh{name="open centre frame"};created.Add(mesh);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Quad(float x0,float y0,float x1,float y1)
            {
                int i=vertices.Count;
                vertices.Add(new Vector3(x0,y0,0));vertices.Add(new Vector3(x1,y0,0));
                vertices.Add(new Vector3(x1,y1,0));vertices.Add(new Vector3(x0,y1,0));
                triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
            }
            Quad(-2,-2,-1,2);Quad(1,-2,2,2);Quad(-1,1,1,2);Quad(-1,-2,1,-1);
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=go.AddComponent<MeshRenderer>();
            var shader=Shader.Find("Universal Render Pipeline/Lit");Assert.IsNotNull(shader);
            var material=new Material(shader);created.Add(material);material.SetColor(BaseColor,Color.gray);renderer.sharedMaterial=material;
            var target=go.AddComponent<SkywellWeatherTarget>();target.Configure(1,"Test mirror",new Renderer[]{renderer},new Collider[0]);
            Physics.SyncTransforms();return target;
        }
        [TearDown]
        public void Clean()
        {
            // Destroy owners before their mesh/material dependencies.
            foreach(var item in created)if(item is GameObject&&item)Object.DestroyImmediate(item);
            foreach(var item in created)if(item)Object.DestroyImmediate(item);
            created.Clear();Physics.SyncTransforms();
        }
        [Test]
        public void MeshOnlyTargetRequiresAnActualTriangleNotItsEmptyBounds()
        {
            var target=Frame(out _);
            Assert.IsTrue(target.TryHit(new Ray(Origin+new Vector3(1.5f,0,-5),Vector3.forward),out float front));
            Assert.That(front,Is.EqualTo(5).Within(.0001f));
            Assert.IsTrue(target.TryHit(new Ray(Origin+new Vector3(1.5f,0,5),Vector3.back),out float back));
            Assert.That(back,Is.EqualTo(5).Within(.0001f));
            Assert.IsFalse(target.TryHit(new Ray(Origin+Vector3.back*5,Vector3.forward),out _),"The frame's open centre must remain unclickable.");
            Assert.IsEmpty(target.GetComponentsInChildren<Collider>(),"Picking a visible mesh does not require adding an optical occluder.");
        }
        [Test]
        public void NearerSolidGeometryBlocksClickButATriggerDoesNot()
        {
            var target=Frame(out _);var ray=new Ray(Origin+new Vector3(1.5f,0,-5),Vector3.forward);
            var block=new GameObject("occlusion test");created.Add(block);block.transform.position=Origin+new Vector3(1.5f,0,-2);
            var collider=block.AddComponent<BoxCollider>();collider.size=Vector3.one*.5f;Physics.SyncTransforms();
            Assert.IsFalse(target.TryHit(ray,out _),"A target behind nearer solid geometry is not selectable through it.");
            collider.isTrigger=true;Physics.SyncTransforms();
            Assert.IsTrue(target.TryHit(ray,out float distance));Assert.That(distance,Is.EqualTo(5).Within(.0001f));
        }
        [Test]
        public void ExplicitColliderWorksWithoutRendererAndRespectsOcclusionAndEnabledState()
        {
            var target=Frame(out _);var collider=target.gameObject.AddComponent<BoxCollider>();
            // AddComponent fits the existing flat frame mesh, so author the intended solid fixture explicitly.
            collider.center=Vector3.zero;collider.size=Vector3.one;
            target.Configure(0,"Solid platform",new Renderer[0],new Collider[]{collider});
            var ray=new Ray(Origin+Vector3.back*5,Vector3.forward);Physics.SyncTransforms();
            Assert.IsTrue(target.TryHit(ray,out float distance));
            Assert.That(distance,Is.EqualTo(4.5f).Within(.0001f),"Hit the actual box front, not its transform origin.");
            var wall=new GameObject("solid in front of configured collider");created.Add(wall);
            wall.transform.position=Origin+Vector3.back*2;wall.AddComponent<BoxCollider>();Physics.SyncTransforms();
            Assert.IsFalse(target.TryHit(ray,out _));
            wall.transform.position=Origin+Vector3.forward*2;Physics.SyncTransforms();
            Assert.IsTrue(target.TryHit(ray,out _),"A wall beyond the target cannot hide its nearer surface.");
            collider.enabled=false;Physics.SyncTransforms();Assert.IsFalse(target.TryHit(ray,out _));
            collider.enabled=true;collider.isTrigger=true;Physics.SyncTransforms();Assert.IsFalse(target.TryHit(ray,out _));
        }
        [Test]
        public void ScaledAndRotatedMeshReportsWorldDistance()
        {
            var target=Frame(out _);target.transform.localScale=new Vector3(2,.75f,3);target.transform.rotation=Quaternion.Euler(15,30,4);Physics.SyncTransforms();
            Vector3 point=target.transform.TransformPoint(new Vector3(1.5f,0,0));Vector3 normal=target.transform.forward;
            Assert.IsTrue(target.TryHit(new Ray(point-normal*7,normal),out float distance));
            Assert.That(distance,Is.EqualTo(7).Within(.002f),"Nonuniform scale must not convert the hit distance into local units.");
        }
        [Test]
        public void RemoteEligibilityRequiresRangeViewportAndRealUnoccludedSurface()
        {
            var target=Frame(out _);
            var cameraObject=new GameObject("remote weather camera");created.Add(cameraObject);
            var camera=cameraObject.AddComponent<Camera>();camera.transform.position=Origin+Vector3.back*8;
            camera.transform.rotation=Quaternion.identity;camera.fieldOfView=60;camera.aspect=1.6f;
            Vector3 player=camera.transform.position;
            Assert.IsTrue(target.HasVisibleSurface(camera,player,12),"Visible frame rails remain selectable even though their centre is empty.");
            Assert.IsFalse(target.HasVisibleSurface(camera,player,4),"Seeing a target cannot override physical cast range.");
            camera.transform.rotation=Quaternion.Euler(0,180,0);
            Assert.IsFalse(target.HasVisibleSurface(camera,player,12),"Behind-camera targets are not remote candidates.");
            camera.transform.rotation=Quaternion.identity;
            var wall=new GameObject("opaque mountain");created.Add(wall);wall.transform.position=Origin+Vector3.back*4;
            wall.AddComponent<BoxCollider>().size=new Vector3(8,8,1);Physics.SyncTransforms();
            Assert.IsFalse(target.HasVisibleSurface(camera,player,12),"Remote weather cannot pass through an opaque mountain.");
        }
        [Test]
        public void DisabledRendererOrTargetCannotReceiveClicks()
        {
            var target=Frame(out var renderer);var ray=new Ray(Origin+new Vector3(1.5f,0,-5),Vector3.forward);
            Assert.IsTrue(target.TryHit(ray,out _));renderer.enabled=false;
            Assert.IsFalse(target.TryHit(ray,out _));renderer.enabled=true;target.enabled=false;
            Assert.IsFalse(target.TryHit(ray,out _));target.enabled=true;target.gameObject.SetActive(false);
            Assert.IsFalse(target.TryHit(ray,out _));
        }
        [Test]
        public void ReconfiguringClearsCachedOldPickSurfaces()
        {
            var target=Frame(out var renderer);var ray=new Ray(Origin+new Vector3(1.5f,0,-5),Vector3.forward);
            Assert.IsTrue(target.TryHit(ray,out _));target.Configure(1,"none",new Renderer[0],new Collider[0]);
            Assert.IsFalse(target.TryHit(ray,out _));target.Configure(1,"restored",new Renderer[]{renderer},new Collider[0]);
            Assert.IsTrue(target.TryHit(ray,out _));
        }
        [Test]
        public void HighlightDoesNotOverwriteLiveRendererFeedback()
        {
            var target=Frame(out var renderer);var block=new MaterialPropertyBlock();block.SetColor(BaseColor,Color.blue);renderer.SetPropertyBlock(block);
            target.SetHighlight(true,false);var highlighted=new MaterialPropertyBlock();renderer.GetPropertyBlock(highlighted,0);
            Assert.IsFalse(highlighted.isEmpty);Assert.AreNotEqual(Color.blue,highlighted.GetColor(BaseColor));
            // Weather/optics can update the renderer-level block while the per-slot highlight is shown.
            block.SetColor(BaseColor,Color.green);renderer.SetPropertyBlock(block);target.SetHighlight(false,false);
            var restored=new MaterialPropertyBlock();renderer.GetPropertyBlock(restored);Assert.AreEqual(Color.green,restored.GetColor(BaseColor));
            renderer.GetPropertyBlock(restored,0);Assert.IsTrue(restored.isEmpty,"Removing the temporary slot override reveals current gameplay feedback.");
        }
        [TestCase(false)]
        [TestCase(true)]
        public void HideOrReconfigureRestoresExistingMaterialSlotBlock(bool reconfigure)
        {
            var target=Frame(out var renderer);var original=new MaterialPropertyBlock();original.SetColor(BaseColor,Color.magenta);original.SetFloat("_Smoothness",.23f);renderer.SetPropertyBlock(original,0);
            target.SetHighlight(true,true);
            if(reconfigure)target.Configure(1,"same",new Renderer[]{renderer},new Collider[0]);else target.SetHighlight(false,false);
            var restored=new MaterialPropertyBlock();renderer.GetPropertyBlock(restored,0);
            Assert.AreEqual(Color.magenta,restored.GetColor(BaseColor));Assert.That(restored.GetFloat("_Smoothness"),Is.EqualTo(.23f));
        }
        [Test]
        public void MultipleMaterialSlotsKeepIndependentOriginalOverridesAndSharedMaterials()
        {
            var target=Frame(out var renderer);var first=renderer.sharedMaterial;
            var second=new Material(first);created.Add(second);second.SetColor(BaseColor,Color.cyan);
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var indices=mesh.triangles;
            mesh.subMeshCount=2;mesh.SetTriangles(new[]{indices[0],indices[1],indices[2]},0);
            var remainder=new int[indices.Length-3];System.Array.Copy(indices,3,remainder,0,remainder.Length);mesh.SetTriangles(remainder,1);
            renderer.sharedMaterials=new[]{first,second};
            var rendererBlock=new MaterialPropertyBlock();rendererBlock.SetColor(BaseColor,Color.blue);renderer.SetPropertyBlock(rendererBlock);
            var firstBlock=new MaterialPropertyBlock();firstBlock.SetColor(BaseColor,Color.magenta);firstBlock.SetFloat("_Smoothness",.23f);renderer.SetPropertyBlock(firstBlock,0);
            target.SetHighlight(true,false);target.SetHighlight(true,true);
            Assert.AreEqual(Color.gray,first.GetColor(BaseColor));Assert.AreEqual(Color.cyan,second.GetColor(BaseColor));
            var result=new MaterialPropertyBlock();renderer.GetPropertyBlock(result);
            Assert.AreEqual(Color.blue,result.GetColor(BaseColor),"Renderer-level gameplay feedback must remain untouched.");
            renderer.GetPropertyBlock(result,0);Assert.AreNotEqual(Color.magenta,result.GetColor(BaseColor));
            renderer.GetPropertyBlock(result,1);Assert.IsFalse(result.isEmpty);
            target.SetHighlight(false,false);
            renderer.GetPropertyBlock(result,0);Assert.AreEqual(Color.magenta,result.GetColor(BaseColor));Assert.That(result.GetFloat("_Smoothness"),Is.EqualTo(.23f));
            renderer.GetPropertyBlock(result,1);Assert.IsTrue(result.isEmpty,"A previously empty slot must be empty again, revealing renderer-level feedback.");
        }
    }
}

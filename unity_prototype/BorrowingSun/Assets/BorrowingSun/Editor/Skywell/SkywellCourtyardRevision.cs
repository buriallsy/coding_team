using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>Focused, one-time edit of the saved north court. Does not rebuild the level.</summary>
    public static class SkywellCourtyardRevision
    {
        internal static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/2026-10-18-open-court"));
        private const string Palette="Assets/BorrowingSun/Materials/SkywellDaylight/";
        private static Transform Root => UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="L5_Skywell").transform;
        private static Transform Find(string name) => Root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void SavedScene()
        {
            var scene=EditorSceneManager.GetActiveScene();
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&scene.path==SkywellBuilder.ScenePath&&!scene.isDirty,"Open the saved L5 scene outside Play Mode; preserve unsaved edits first.");
        }

        [MenuItem("Borrowing Sun/Skywell/Audit north courtyard")]
        public static void Audit()
        {
            SavedScene(); Directory.CreateDirectory(Evidence); Physics.SyncTransforms();
            bool revised=Root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Calibration_target_stand");
            var lines=new List<string>{DateTime.UtcNow.ToString("O"),"Saved L5 author scene; fixed review cameras, not a player comprehension test."};
            foreach(var t in Root.GetComponentsInChildren<Transform>(true))
                if(t.name.StartsWith("Carved_well")||t.name.StartsWith("Lightwell_rim")||new[]{"District_1_residences","District_rock_1","Far_north_ridge","North_sloping_mirror_roof","North_canopy_column","Calibration_target_stand"}.Contains(t.name)||t.parent==Find("North_fixed_service_gallery"))
                    lines.Add(t.name+" position="+t.position.ToString("F3")+" scale="+t.lossyScale.ToString("F3"));
            var lighting=Object.FindAnyObjectByType<SkywellSceneLighting>();
            lines.Add("Sun="+lighting.sunEulerAngles+" color="+lighting.sunColor+" ambient="+lighting.ambientColor+" * "+lighting.ambientIntensity+" background="+Camera.main.backgroundColor);
            CheckGeometry(lines);
            string stage=revised?"after":"before";
            File.WriteAllLines(Path.Combine(Evidence,stage+"-geometry.txt"),lines);
            SkywellBuilder.CaptureCourtyardViews(stage);
        }

        [MenuItem("Borrowing Sun/Skywell/Open north courtyard and neutral daylight")]
        public static void Apply()
        {
            SavedScene(); Directory.CreateDirectory(Evidence);
            Require(!Root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Calibration_target_stand"),"Courtyard revision already exists; do not repeat this one-time edit.");
            var village=Find("District_1_residences");
            Require(Vector3.Distance(village.position,new Vector3(0,26,156))<.01f,"North village has changed since review; inspect before moving it.");
            var well=Find("Terrain_sunlight_well");var gallery=Find("North_fixed_service_gallery");
            var lighting=Object.FindAnyObjectByType<SkywellSceneLighting>();
            var court=Object.FindAnyObjectByType<SkywellController>();
            Require(Vector3.Distance(lighting.sunEulerAngles,new Vector3(38,145,0))<.01f,"Keep the reviewed physical sun direction.");
            var created=new List<string>();int group=Undo.GetCurrentGroup();Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Open north village courtyard and neutral daylight");
            Undo.RegisterFullObjectHierarchyUndo(Root.gameObject,"North courtyard revision");
            try
            {
                // Local palette copies: the existing stone assets are shared with other levels.
                if(!AssetDatabase.IsValidFolder(Palette.TrimEnd('/')))AssetDatabase.CreateFolder("Assets/BorrowingSun/Materials","SkywellDaylight");
                var palette=new Dictionary<Material,Material>();
                AddMaterial(palette,created,"Assets/BorrowingSun/Materials/Chalk.mat","Skywell_Chalk",new Color(.82f,.83f,.81f));
                AddMaterial(palette,created,"Assets/BorrowingSun/Materials/Stone.mat","Skywell_Stone",new Color(.65f,.68f,.69f));
                AddMaterial(palette,created,"Assets/BorrowingSun/Art/Terrain/Materials/Terrace_PaleStone.mat","Skywell_PaleStone",new Color(.65f,.69f,.68f));
                foreach(var renderer in Root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials=renderer.sharedMaterials;bool changed=false;
                    for(int i=0;i<materials.Length;i++)if(materials[i]&&palette.TryGetValue(materials[i],out var replacement)){materials[i]=replacement;changed=true;}
                    if(changed){Undo.RecordObject(renderer,"L5 daylight materials");renderer.sharedMaterials=materials;}
                }
                village.position+=Vector3.back*18;Find("District_rock_1").position+=Vector3.back*18;
                Find("North_sloping_mirror_roof").position+=Vector3.back*18;
                foreach(var column in Root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="North_canopy_column"))column.position+=Vector3.back*18;
                Find("Far_north_ridge").position+=Vector3.back*12;
                SetBox(gallery.Find("Outer_mountain_court"),new Vector3(-3.5f,25.7f,136),new Vector3(21,.6f,18));
                SetBox(gallery.Find("Long_freight_bridge"),new Vector3(0,25.8f,122.5f),new Vector3(3.6f,.4f,9));
                SetBox(Find("Fixed_mirror_maintenance_spur"),new Vector3(-5.9f,25.8f,126.5f),new Vector3(8.2f,.4f,1));
                gallery.Find("Emergency_manual_winch").position=new Vector3(2.8f,26,133.5f);
                foreach(var t in gallery.Cast<Transform>().ToArray())
                {
                    if(t.name=="Bridge_cross_beam"&&t.position.z>127)Undo.DestroyObjectImmediate(t.gameObject);
                    else if(t.name=="Continuous_freight_rail")SetBox(t,new Vector3(t.position.x,26.035f,125.15f),new Vector3(.06f,.05f,19.7f));
                }
                // The aperture and rays stay put. The target gets a narrow stand instead of three enclosing walls.
                foreach(string name in new[]{"Carved_well_west_cheek","Carved_well_east_cheek","Lightwell_rim_west","Lightwell_rim_east","Lightwell_rim_back"})
                    Undo.DestroyObjectImmediate(well.Find(name).gameObject);
                var stand=well.Find("Carved_well_back_rock");stand.name="Calibration_target_stand";
                SetBox(stand,new Vector3(-8,29.15f,133),new Vector3(.22f,6.3f,.22f));
                stand.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/BorrowingSun/Materials/SkywellLiftMechanism.mat");
                lighting.sunColor=new Color(1,.97f,.92f);lighting.ambientColor=new Color(.40f,.47f,.56f);lighting.ApplySettings();
                Camera.main.backgroundColor=new Color(.30f,.36f,.42f);
                EditorUtility.SetDirty(lighting);EditorUtility.SetDirty(lighting.sunLight);EditorUtility.SetDirty(Camera.main);
                Physics.SyncTransforms();var report=new List<string>{DateTime.UtcNow.ToString("O")};CheckGeometry(report);
                Require(court.Observation&&Vector3.Distance(court.Observation.transform.position,new Vector3(48,26,110))<.01f,"East village observation stays on its authored platform.");
                foreach(var material in palette.Values)EditorUtility.SetDirty(material);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
                Require(EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()),"Could not save north courtyard revision.");
                Undo.CollapseUndoOperations(group);report.Add("COURTYARD_REVISION_SAVED");File.WriteAllLines(Path.Combine(Evidence,"apply.txt"),report);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                foreach(string path in created)AssetDatabase.DeleteAsset(path);
                throw;
            }
        }

        private static void AddMaterial(Dictionary<Material,Material> map,List<string> created,string source,string name,Color color)
        {
            var original=AssetDatabase.LoadAssetAtPath<Material>(source);Require(original,"Missing source material: "+source);
            string path=Palette+name+".mat";Require(!File.Exists(path),"Palette copy already exists: "+path);
            var material=new Material(original){name=name};material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            AssetDatabase.CreateAsset(material,path);created.Add(path);map.Add(original,material);
        }
        private static void SetBox(Transform t,Vector3 position,Vector3 scale)
        {Require(t,"Missing reviewed geometry.");t.position=position;t.localScale=scale;}
        private static void CheckGeometry(List<string> report)
        {
            var collector=Object.FindAnyObjectByType<SkywellSunCollector>();
            var sample=Find("Sun_visibility_sample").position;var sun=-collector.SunLight.transform.forward;
            var sunHits=Physics.RaycastAll(sample+sun*.08f,sun,250,~0,QueryTriggerInteraction.Ignore).Where(h=>!collector.IsCollectorCollider(h.collider)).ToArray();
            Require(sunHits.Length==0,"Actual sun blocked: "+string.Join(",",sunHits.Select(h=>h.collider.name)));
            report.Add("PASS: actual sun ray from authored sample remains clear.");
            ClearLeg(collector.OutputOrigin,collector.FocusTargets[0].position,collector,"Mechanical_calibration_target",report,"collector to calibration target");
            ClearLeg(collector.OutputOrigin,collector.FocusTargets[1].position,collector,null,report,"collector to high relay target");
            foreach(var p in new[]{new Vector3(0,26,126.8f),new Vector3(0,26,127.2f),new Vector3(-3,26,128),new Vector3(-8,26,129.2f),new Vector3(-5,26,129)})
            {
                Require(Physics.Raycast(p+Vector3.up*.1f,Vector3.down,out var hit,.3f,~0,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y-26)<.02f,"Missing continuous north floor at "+p);
                report.Add("PASS: walkable floor at "+p);
            }
        }
        private static void ClearLeg(Vector3 a,Vector3 b,SkywellSunCollector collector,string allowed,List<string> report,string label)
        {
            var delta=b-a;
            var hits=Physics.RaycastAll(a+delta.normalized*.025f,delta.normalized,delta.magnitude-.1f,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>!collector.IsCollectorCollider(h.collider)&&h.collider.name!=allowed).ToArray();
            Require(hits.Length==0,label+" blocked: "+string.Join(",",hits.Select(h=>h.collider.name)));
            report.Add("PASS: physical clearance, "+label);
        }
    }

    public static partial class SkywellBuilder
    {
        internal static void CaptureCourtyardViews(string stage)
        {
            string folder=SkywellCourtyardRevision.Evidence;
            CapturePortfolioCamera(Path.Combine(folder,stage+"-north-court.png"),new PortfolioView{position=new Vector3(15,43,112),target=new Vector3(-4,29,135),fieldOfView=53});
            CapturePortfolioCamera(Path.Combine(folder,stage+"-north-approach.png"),new PortfolioView{position=new Vector3(-12,35,116),target=new Vector3(-5,29,135),fieldOfView=62});
            var main=Camera.main;
            CapturePortfolioCamera(Path.Combine(folder,stage+"-entry.png"),new PortfolioView{position=main.transform.position,target=main.transform.position+main.transform.forward*20,fieldOfView=main.fieldOfView,orthographicSize=main.orthographic?main.orthographicSize:0});
        }
    }
}

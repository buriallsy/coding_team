using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowingSun.Editor
{
    /// <summary>Adds only the village viewpoint to the saved scene; never rebuilds its geometry.</summary>
    public static class SkywellObservationSetup
    {
        [MenuItem("Borrowing Sun/Skywell/Preview village observation (Play Mode)")]
        public static void Preview()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first; preview never changes the author scene.");
            var court=UnityEngine.Object.FindAnyObjectByType<SkywellController>();
            if(!court||!court.Observation)throw new InvalidOperationException("Open the L5 scene with its observation point.");
            court.ResetCourt();
            court.Player.Teleport(court.Observation.transform.position+new Vector3(0,.03f,-.8f));
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        }

        private const string ImagePath="Assets/BorrowingSun/Art/Observation/ShadedVillage.png";
        [MenuItem("Borrowing Sun/Skywell/Add village observation point")]
        public static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.isDirty||scene.path!=SkywellBuilder.ScenePath)
                throw new InvalidOperationException("Open the saved L5 scene outside Play Mode before adding its viewpoint.");
            var court=UnityEngine.Object.FindAnyObjectByType<SkywellController>();
            if(!court||court.Observation||UnityEngine.Object.FindAnyObjectByType<SkywellObservation>())
                throw new InvalidOperationException("Missing L5 controller, or viewpoint already installed. Do not repeat setup.");
            var receiver=court.Optics?court.Optics.SettlementReceiver:null;
            var image=AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
            var markerMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/BorrowingSun/Materials/Copper.mat");
            if(!receiver||!image||!markerMaterial)throw new InvalidOperationException("Missing village receiver, supplied illustration or existing marker material.");
            var importer=(TextureImporter)AssetImporter.GetAtPath(ImagePath);
            importer.textureType=TextureImporterType.Default;
            // Keep the illustration in sRGB and preserve its original pixel dimensions.
            importer.sRGBTexture=true;importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;
            importer.wrapMode=TextureWrapMode.Clamp;importer.alphaSource=TextureImporterAlphaSource.None;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            image=AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);

            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/2026-10-18-observation"));
            Directory.CreateDirectory(folder);
            string sceneFile=Path.GetFullPath(Path.Combine(Application.dataPath,"..",scene.path));
            File.Copy(sceneFile,Path.Combine(folder,"05_Skywell-before.unity"),false);
            File.Copy(sceneFile+".meta",Path.Combine(folder,"05_Skywell-before.unity.meta"),false);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add village observation");
            try
            {
                var go=new GameObject("Village_observation");
                Undo.RegisterCreatedObjectUndo(go,"Add village observation");
                go.transform.SetParent(receiver.transform.parent,false);
                go.transform.position=new Vector3(48,26,110);
                var view=Undo.AddComponent<SkywellObservation>(go);view.Configure(image,receiver);
                var marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Undo.RegisterCreatedObjectUndo(marker,"Add observation floor marker");
                marker.name="Observation_floor_marker";marker.transform.SetParent(go.transform,false);
                marker.transform.localPosition=new Vector3(0,.025f,0);
                marker.transform.localScale=new Vector3(.65f,.025f,.65f);
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.GetComponent<Renderer>().sharedMaterial=markerMaterial;
                Undo.RecordObject(court,"Bind observation point");court.ConfigureObservation(view);
                EditorUtility.SetDirty(court);EditorUtility.SetDirty(view);
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save village observation point.");
                AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(group);
                File.WriteAllText(Path.Combine(folder,"setup.txt"),DateTime.UtcNow.ToString("O")+
                    "\nSKYWELL_OBSERVATION_SETUP_PASSED\nPoint=(48,26,110); radius=2.2; no gameplay collider added.\n"+
                    "User image assigned directly; existing geometry, sun, receiver, weather and transport unchanged.\n");
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }
    }
}

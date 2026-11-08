using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowingSun.Editor
{
    /// <summary>Read-only serialized asset audit and the existing EditMode suite.</summary>
    public static class ProjectValidation
    {
        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/project"));
        private static TestRunnerApi runner;
        private sealed class TestResults : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(Folder);
                TestRunnerApi.SaveResultToFile(result,Path.Combine(Folder,"editmode-results.xml"));
                File.WriteAllText(Path.Combine(Folder,"editmode-summary.txt"),$"UTC: {DateTime.UtcNow:O}\nResult: {result.ResultState}\nPassed: {result.PassCount}\nFailed: {result.FailCount}\nSkipped: {result.SkipCount}\n");
            }
        }
        [MenuItem("Borrowing Sun/Validation/Run existing EditMode tests")]
        public static void RunTests()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(!runner){runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new TestResults());}
            runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,assemblyNames=new[]{"BorrowingSun.Tests"}}));
        }
        [MenuItem("Borrowing Sun/Validation/Check all authored scenes and prefabs")]
        public static void CheckReferences()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Folder);var log=new List<string>{DateTime.UtcNow.ToString("O"),"Serialized reference audit; scenes opened additively and closed without saving."};
            var active=SceneManager.GetActiveScene();int count=0;
            try
            {
                foreach(var guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/BorrowingSun"}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                    if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    try {foreach(var root in scene.GetRootGameObjects())count+=Inspect(root,path);log.Add("PASS: "+path);}
                    finally {if(opened)EditorSceneManager.CloseScene(scene,true);}
                }
                foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/BorrowingSun"}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
                    try {count+=Inspect(root,path);log.Add("PASS: "+path);}finally {PrefabUtility.UnloadPrefabContents(root);}
                }
                log.Add("Components checked: "+count);log.Add("PROJECT_REFERENCES_PASSED");Debug.Log("PROJECT_REFERENCES_PASSED");
            }
            catch(Exception e){log.Add("FAILED: "+e.Message);throw;}
            finally {if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);File.WriteAllLines(Path.Combine(Folder,"references.txt"),log);}
        }
        private static int Inspect(GameObject root,string asset)
        {
            int count=0;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new InvalidOperationException(asset+": missing script on "+t.name);
                foreach(var component in t.GetComponents<Component>())
                {
                    var data=new SerializedObject(component);var p=data.GetIterator();count++;
                    while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue==null&&!IsDefault(p.objectReferenceEntityIdValue))
                        throw new InvalidOperationException(asset+": missing reference "+t.name+"."+p.propertyPath);
                }
            }
            return count;
        }
        private static bool IsDefault<T>(T value)=>EqualityComparer<T>.Default.Equals(value,default);
    }
}

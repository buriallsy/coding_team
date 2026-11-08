using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowingSun.Editor
{
    public sealed class SkywellTuningWindow : EditorWindow
    {
        private Vector2 scroll;
        private SkywellFish fish;
        private static readonly string[] FishFields={"lureDuration","patrolSpeed","investigateSpeed","chaseSpeed","turnSpeed","detectionRange","fieldOfView","warningDuration","lostSightDuration","warningDecay","catchDistance"};
        [MenuItem("Borrowing Sun/Skywell/水下玩法调参")]
        public static void Open(){var w=GetWindow<SkywellTuningWindow>("水下玩法调参");w.minSize=new Vector2(430,470);w.Show();}
        private void OnInspectorUpdate(){if(EditorApplication.isPlaying)Repaint();}
        private void OnGUI()
        {
            if(!fish)fish=Object.FindAnyObjectByType<SkywellFish>();
            if(!fish){EditorGUILayout.HelpBox("请打开 05_Skywell 场景。",MessageType.Info);return;}
            EditorGUILayout.HelpBox(EditorApplication.isPlaying?"试玩中修改立即生效，停止 Play 后恢复。正式参数请在非 Play 模式修改并保存场景。":"修改保存在当前场景。吸引时长从敲钟起计，含游向钟的时间；重复敲钟重新计时。",MessageType.Info);
            if(!EditorApplication.isPlaying&&GUILayout.Button("保存当前场景参数"))EditorSceneManager.SaveScene(fish.gameObject.scene);
            if(GUILayout.Button("定位鱼组件 / 在 Scene 查看范围")){Selection.activeGameObject=fish.gameObject;EditorGUIUtility.PingObject(fish.gameObject);SceneView.RepaintAll();}
            DrawLive(fish);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            DrawFields(fish,FishFields);
            EditorGUILayout.Space(14);
            DrawFields(fish.GetComponent<SkywellJourney>(),new[]{"boatSpeed","climbSpeed"});
            EditorGUILayout.Space(14);
            DrawFields(fish.GetComponent<SkywellWindBell>(),new[]{"bellTravelDuration"});
            EditorGUILayout.EndScrollView();
        }
        private static void DrawFields(Object target,string[] names)
        {
            if(!target)return;
            using var so=new SerializedObject(target);so.Update();
            foreach(var name in names)EditorGUILayout.PropertyField(so.FindProperty(name),Label(target,name));
            if(so.ApplyModifiedProperties())SceneView.RepaintAll();
        }
        internal static GUIContent Label(Object target,string name)
        {
            var field=target.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            var label=field==null?null:System.Attribute.GetCustomAttribute(field,typeof(InspectorNameAttribute)) as InspectorNameAttribute;
            var tooltip=field==null?null:System.Attribute.GetCustomAttribute(field,typeof(TooltipAttribute)) as TooltipAttribute;
            return new GUIContent(label?.displayName??ObjectNames.NicifyVariableName(name),tooltip?.tooltip??"");
        }
        internal static void DrawLive(SkywellFish f)
        {
            if(!Application.isPlaying)return;
            EditorGUILayout.LabelField("当前状态",f.Phase+" · "+f.CurrentSpeed.ToString("0.00")+" m/s");
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false,22),f.LureRemaining/Mathf.Max(.1f,f.lureDuration),"吸引剩余 "+f.LureRemaining.ToString("0.0")+" / "+f.lureDuration.ToString("0.0")+" 秒");
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false,22),f.WarningProgress,"警觉进度 "+Mathf.RoundToInt(f.WarningProgress*100)+"%");
            EditorGUILayout.LabelField("看见玩家",f.PlayerVisible?"是":"否");
        }
    }

    [CustomEditor(typeof(SkywellFish))]
    public sealed class SkywellFishInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();var property=serializedObject.GetIterator();bool first=true;
            while(property.NextVisible(first)){first=false;using(new EditorGUI.DisabledScope(property.name=="m_Script"))EditorGUILayout.PropertyField(property,SkywellTuningWindow.Label(target,property.name),true);}
            serializedObject.ApplyModifiedProperties();SkywellTuningWindow.DrawLive((SkywellFish)target);if(Application.isPlaying)Repaint();if(GUILayout.Button("打开水下玩法调参面板"))SkywellTuningWindow.Open();
        }
        private void OnSceneGUI()
        {
            var f=(SkywellFish)target;if(!f.Body)return;var p=f.Body.position;
            Handles.color=new Color(1,.75f,.15f,.8f);
            var edge=Quaternion.AngleAxis(-f.fieldOfView*.5f,Vector3.up)*Vector3.ProjectOnPlane(f.Body.forward,Vector3.up).normalized;
            Handles.DrawWireArc(p,Vector3.up,edge,f.fieldOfView,f.detectionRange);
            Handles.DrawLine(p,p+edge*f.detectionRange);Handles.DrawLine(p,p+Quaternion.AngleAxis(f.fieldOfView,Vector3.up)*edge*f.detectionRange);
            Handles.color=Color.red;Handles.DrawWireDisc(p,Vector3.up,f.catchDistance);
            if(Application.isPlaying&&f.LureRemaining>0){Handles.color=Color.cyan;Handles.DrawDottedLine(p,f.InvestigationPoint,4);Handles.Label(f.InvestigationPoint,"吸引剩余 "+f.LureRemaining.ToString("0.0")+"s");}
        }
    }
}

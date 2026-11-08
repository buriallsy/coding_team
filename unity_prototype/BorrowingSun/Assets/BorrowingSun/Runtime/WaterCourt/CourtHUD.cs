using UnityEngine;

namespace BorrowingSun
{
    public sealed class CourtHUD : MonoBehaviour
    {
        [SerializeField] private CourtController court;
        [SerializeField] private CourtPlayer player;
        [SerializeField] private CourtCamera cameraRig;
        [SerializeField] private Font font;
        private GUIStyle title;
        private GUIStyle body;
        private GUIStyle hint;
        private GUIStyle button;
        private GUIStyle small;
        private Texture2D panel;

        public void Initialize(CourtController owner, CourtPlayer actor, CourtCamera cameraController, Font chineseFont = null)
        {
            court = owner;
            player = actor;
            cameraRig = cameraController;
            font = chineseFont;
        }

        private bool BuildStyles()
        {
            if (font == null)
                font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (font == null)
            {
                Debug.LogError("CourtHUD needs the bundled Noto Sans CJK font with Include Font Data enabled.", this);
                enabled = false;
                return false;
            }
            title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 27, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(0.92f, 0.97f, 0.92f);
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true };
            body.normal.textColor = new Color(0.87f, 0.93f, 0.93f);
            hint = new GUIStyle(body) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            hint.normal.textColor = new Color(1, 0.83f, 0.44f);
            small = new GUIStyle(body) { fontSize = 14 };
            small.normal.textColor = new Color(0.66f, 0.78f, 0.8f);
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16 };
            panel = new Texture2D(1, 1);
            panel.SetPixel(0, 0, new Color(0.035f, 0.07f, 0.1f, 0.90f));
            panel.Apply();
            return true;
        }

        private void OnGUI()
        {
            if (court == null) return;
            if (title == null && !BuildStyles()) return;
            float scale = Mathf.Clamp(Screen.height / 900f, 0.7f, 1.6f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            var state = court.State;

            GUI.DrawTexture(new Rect(22, 20, 410, 192), panel);
            GUI.Label(new Rect(40, 32, 370, 40), "借晴 · 水院", title);
            GUI.Label(new Rect(40, 78, 370, 45), court.GetObjective(), body);
            string water = state.IsHigh ? "高位" : state.IsLow ? "低位" : "中间";
            string weather = state.RainActive ? "下雨" : state.DrainOpen && !state.IsLow ? "排水" : "保留";
            GUI.Label(new Rect(40, 129, 370, 25), "水位 " + state.WaterHeight.ToString("0.0") + " m · " + water + " · " + weather, body);
            GUI.Label(new Rect(40, 158, 370, 30), "雨牌：" + (state.CardInserted ? "在控制座" : "在手中") + "    苗车：" + (state.CartStop + 1) + " / 8", small);

            float barWidth = Mathf.Min(850, width - 44);
            float barX = (width - barWidth) * 0.5f;
            GUI.DrawTexture(new Rect(barX, height - 124, barWidth, 103), panel);
            GUI.Label(new Rect(barX + 12, height - 119, barWidth - 24, 38), court.GetInteractionPrompt(), hint);
            GUI.Label(new Rect(barX + 18, height - 77, barWidth - 36, 27), "WASD / 方向键 移动    E 操作    Q 插拔雨牌 / 倒推    R 重置", small);
            GUI.Label(new Rect(barX + 18, height - 51, barWidth - 36, 26), "C 切换整体 / 亭内观察    Esc 回到整体视角    无跳跃 · 走失自动回岸", small);

            if (!string.IsNullOrEmpty(court.Message))
            {
                GUI.DrawTexture(new Rect(barX, height - 199, barWidth, 66), panel);
                GUI.Label(new Rect(barX + 15, height - 196, barWidth - 30, 60), court.Message, hint);
            }
            if (cameraRig != null && cameraRig.IsObserving)
            {
                GUI.DrawTexture(new Rect(width - 358, 22, 336, 78), panel);
                GUI.Label(new Rect(width - 340, 31, 304, 61), "亭内观察：移动暂停\nE / Q 仍需人物靠近机关 · C / Esc 返回", body);
            }
            if (state.Completed)
            {
                GUI.DrawTexture(new Rect(width - 338, 117, 316, 112), panel);
                GUI.Label(new Rect(width - 322, 128, 287, 81), "第一段货路已复通\n留下道路，带走雨牌。\nR 可重新推演其他操作顺序。", body);
            }
            GUI.matrix = previous;
        }

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }
    }
}

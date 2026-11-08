using UnityEngine;

namespace BorrowingSun
{
    public sealed class BorrowedRainHUD : MonoBehaviour
    {
        [SerializeField] private BorrowedRainController court;
        private GUIStyle title, body, small, prompt;
        private Texture2D panel;

        public void Initialize(BorrowedRainController owner) => court = owner;

        private bool BuildStyles()
        {
            Font font = court.Bindings.hudFont != null ? court.Bindings.hudFont : Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (font == null)
            {
                Debug.LogError("BorrowedRainHUD requires bundled Noto Sans CJK font data.", this);
                enabled = false;
                return false;
            }
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true };
            body.normal.textColor = new Color(0.88f, 0.95f, 0.93f);
            title = new GUIStyle(body) { fontSize = 26, fontStyle = FontStyle.Bold };
            small = new GUIStyle(body) { fontSize = 14 };
            small.normal.textColor = new Color(0.72f, 0.84f, 0.83f);
            prompt = new GUIStyle(body) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            prompt.normal.textColor = new Color(1, 0.85f, 0.48f);
            panel = new Texture2D(1, 1);
            panel.SetPixel(0, 0, new Color(0.025f, 0.065f, 0.08f, 0.91f));
            panel.Apply();
            return true;
        }

        private void OnGUI()
        {
            if (court == null) return;
            if (body == null && !BuildStyles()) return;
            float scale = Mathf.Clamp(Screen.height / 900f, 0.7f, 1.6f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            float panelWidth = Mathf.Min(425, width - 36);
            var state = court.State;
            GUI.DrawTexture(new Rect(18, 18, panelWidth, 198), panel);
            GUI.Label(new Rect(34, 26, panelWidth - 32, 40), "借晴 · 借雨双院", title);
            GUI.Label(new Rect(34, 72, panelWidth - 32, 44), court.GetObjective(), body);
            string waterA = "A 池 " + state.WaterA.ToString("0.0") + " / 3 m" + (state.DrainAOpen ? " · 排水" : "");
            string waterB = "B 池 " + state.WaterB.ToString("0.0") + " / 3 m" + (state.DrainBOpen ? " · 排水" : "");
            GUI.Label(new Rect(34, 121, panelWidth - 32, 25), waterA + "     " + waterB, small);
            string holder = state.CardStation < 0 ? "在手中" : state.CardStation == 0 ? "本地亭 S0" : "借景亭 S1";
            GUI.Label(new Rect(34, 148, panelWidth - 32, 24), "唯一雨牌：" + holder + (state.RainActive ? " · 开雨" : " · 停雨"), small);
            GUI.Label(new Rect(34, 176, panelWidth - 32, 24), "苗车：" + (state.CartStop + 1) + " / 6 停靠位", small);

            float barWidth = Mathf.Min(960, width - 36), barX = (width - barWidth) / 2;
            GUI.DrawTexture(new Rect(barX, height - 110, barWidth, 94), panel);
            GUI.Label(new Rect(barX + 14, height - 107, barWidth - 28, 38), court.GetInteractionPrompt(), prompt);
            GUI.Label(new Rect(barX + 18, height - 68, barWidth - 36, 25), "WASD / 方向键 移动    E 操作    Q 插拔雨牌 / 倒推    R 整段重置", small);
            GUI.Label(new Rect(barX + 18, height - 44, barWidth - 36, 25), "C 高槽侧面观察    Esc 整体视角    无跳跃 · 掉落保留进度回到岸边", small);
            if (!string.IsNullOrEmpty(court.Message))
            {
                GUI.DrawTexture(new Rect(barX, height - 181, barWidth, 61), panel);
                GUI.Label(new Rect(barX + 16, height - 178, barWidth - 32, 55), court.Message, prompt);
            }
            if (court.IsObserving)
            {
                GUI.DrawTexture(new Rect(width - 336, 20, 316, 88), panel);
                GUI.Label(new Rect(width - 321, 30, 286, 72), "观察高槽：人物移动暂停\nE / Q 仍需靠近设备\nC / Esc 回到整体", body);
            }
            GUI.matrix = previous;
        }

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }
    }
}

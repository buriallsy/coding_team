using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Compact play information for the carried-rain revision; every value comes from the active court.</summary>
    public sealed class RainCargoHUD : MonoBehaviour
    {
        [SerializeField] private RainCargoController court;
        private GUIStyle title, body, small, prompt, marker;
        private static readonly Color PanelColor = new Color(0.025f, 0.065f, 0.08f, 0.91f);
        private static readonly Color Gold = new Color(1f, 0.84f, 0.44f);

        public void Initialize(RainCargoController owner) => court = owner;

        private bool BuildStyles()
        {
            Font font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (font != null) font.RequestCharactersInTexture("借晴雨水箱牌", 18, FontStyle.Normal);
            if (font == null || !font.HasCharacter('雨') || !font.HasCharacter('晴'))
            {
                Debug.LogError("RainCargoHUD requires the bundled Noto Sans CJK font with Chinese glyphs and Include Font Data enabled.", this);
                enabled = false;
                return false;
            }
            // Explicit font/style ownership avoids inheriting an Editor overlay's skin or missing-glyph fallback.
            body = new GUIStyle { font = font, fontSize = 15, wordWrap = true, richText = false };
            body.normal.textColor = new Color(0.9f, 0.96f, 0.93f);
            title = new GUIStyle(body) { fontSize = 22, fontStyle = FontStyle.Bold };
            small = new GUIStyle(body) { fontSize = 13 };
            small.normal.textColor = new Color(0.75f, 0.86f, 0.84f);
            prompt = new GUIStyle(body) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            prompt.normal.textColor = Gold;
            marker = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            marker.normal.textColor = Gold;
            return true;
        }

        private void OnGUI()
        {
            if (court == null || court.Player == null || court.CameraRig == null) return;
            if (body == null && !BuildStyles()) return;
            float scale = Mathf.Clamp(Screen.height / 900f, 0.7f, 1.6f);
            float width = Screen.width / scale, height = Screen.height / scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color, previousContent = GUI.contentColor, previousBackground = GUI.backgroundColor;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                GUI.color = GUI.contentColor = GUI.backgroundColor = Color.white;
                Rect information = new Rect(16, 16, Mathf.Min(350, width - 32), 142);
                float barWidth = Mathf.Min(940, width - 32);
                bool narrow = barWidth < 690;
                Vector3 dropLanding = default;
                bool canDrop = !court.IsCartAssisting && !court.IsCartTargeted && court.Player.TryGetDropLanding(out dropLanding);
                string activePrompt = court.IsCartAssisting ? court.Prompt()
                    : court.Player.IsDropping ? "正在下台" : canDrop ? "空格 · 下到低台" : court.Prompt();
                float promptHeight = Mathf.Max(32, prompt.CalcHeight(new GUIContent(activePrompt), barWidth - 24));
                float controlHeight = promptHeight + (narrow ? 56 : 34);
                Rect controls = new Rect((width - barWidth) * 0.5f, height - controlHeight - 16, barWidth, controlHeight);
                DrawDestination(width, height, information, controls);
                DrawCartInteraction(width, height, information, controls);
                if(canDrop) DrawDropLanding(dropLanding, width, height, information, controls);
                DrawInformation(information);
                DrawControls(controls, narrow, activePrompt, promptHeight);
                DrawContext(controls);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                GUI.contentColor = previousContent;
                GUI.backgroundColor = previousBackground;
            }
        }

        private void DrawInformation(Rect area)
        {
            DrawPanel(area);
            float x = area.x + 14, width = area.width - 28;
            var state = court.State;
            GUI.Label(new Rect(x, area.y + 9, width, 29), court.Region, title);
            GUI.Label(new Rect(x, area.y + 42, width, 41), court.Objective, body);
            GUI.Label(new Rect(x, area.y + 88, width, 23),
                "水箱 " + state.TankWater.ToString("0.0") + " / 3     配重斗 " + state.WaterB.ToString("0.0") + " / 3", body);
            string holder = state.CardStation == RainCargoState.Hand ? "在手中"
                : state.CardStation == 0 ? "升台雨亭" : "屋檐集雨亭";
            GUI.Label(new Rect(x, area.y + 115, width, 22),
                "雨牌 · " + holder + (state.RainActive ? "    开雨" : "    停雨"), small);
        }

        private void DrawControls(Rect area, bool narrow, string activePrompt, float promptHeight)
        {
            DrawPanel(area);
            GUI.Label(new Rect(area.x + 12, area.y + 3, area.width - 24, promptHeight), activePrompt, prompt);
            string keys = narrow
                ? "WASD 移动 · E 操作 · Q 插拔 / 倒推 · 空格 下台\nV 总览 · C 观察 · Esc 近景 · R 重置"
                : "WASD / 方向键 移动    E 操作    Q 插拔 / 倒推    空格 下台    V 总览    C 观察    Esc 近景    R 重置";
            GUI.Label(new Rect(area.x + 18, area.y + promptHeight + 8, area.width - 36, narrow ? 42 : 23), keys, small);
        }

        private void DrawContext(Rect controls)
        {
            string context;
            if (court.IsObserving) context = "屋檐观察 · 人物移动暂停 · Esc 返回近景";
            else if (court.State.Completed) context = "运雨街完成 · 苗车与雨牌已到位";
            else
            {
                Vector3 distance = court.Bindings.cart.position - court.Player.FeetPosition;
                bool nearCart = Mathf.Abs(distance.y) < 2f && new Vector2(distance.x, distance.z).sqrMagnitude < 30.25f;
                context = nearCart ? court.CargoStatus : "";
                if (!string.IsNullOrEmpty(court.Message))
                    context = string.IsNullOrEmpty(context) ? court.Message : context + "\n" + court.Message;
                if (string.IsNullOrEmpty(context) && court.CameraRig.IsOverview) context = "街区总览 · V 或 Esc 返回近景";
            }
            if (string.IsNullOrEmpty(context)) return;
            float width = Mathf.Min(760, controls.width);
            float textHeight = body.CalcHeight(new GUIContent(context), width - 28);
            Rect area = new Rect(controls.center.x - width * 0.5f, controls.y - textHeight - 22, width, textHeight + 14);
            DrawPanel(area);
            GUI.Label(new Rect(area.x + 14, area.y + 5, width - 28, textHeight + 3), context, body);
        }

        private void DrawDestination(float width, float height, Rect information, Rect controls)
        {
            var state = court.State;
            if (state.Delivered || court.IsObserving || court.Bindings.stops == null) return;
            int index = state.CartInMotion ? state.PendingCartStop : state.CartStop + 1;
            if (index < 0 || index >= court.Bindings.stops.Length || court.Bindings.stops[index] == null) return;
            Camera camera = court.CameraRig.ViewCamera;
            if (camera == null) return;
            Vector3 viewport = camera.WorldToViewportPoint(court.Bindings.stops[index].position + Vector3.up * 0.65f);
            if (viewport.z <= 0 || viewport.x < 0.07f || viewport.x > 0.93f || viewport.y < 0.14f || viewport.y > 0.94f) return;
            Vector2 point = new Vector2(viewport.x * width, (1 - viewport.y) * height);
            Rect area = new Rect(point.x - 91, point.y - 45, 182, 36);
            if (area.Overlaps(information) || area.yMax > controls.y - 95) return;
            DrawPanel(area);
            string label = state.CartInMotion ? court.CargoStatus : "下一站 · " + court.NextStop;
            GUI.Label(new Rect(area.x + 7, area.y + 3, area.width - 14, area.height - 6), label, marker);
            Color saved = GUI.color;
            GUI.color = Gold;
            GUI.DrawTexture(new Rect(point.x - 1, area.yMax, 2, 6), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(point.x - 3, area.yMax + 6, 6, 3), Texture2D.whiteTexture);
            GUI.color = saved;
        }

        private void DrawDropLanding(Vector3 landing, float width, float height, Rect information, Rect controls)
        {
            Camera camera = court.CameraRig.ViewCamera;
            if(camera == null || court.IsObserving) return;
            Vector3 point = camera.WorldToViewportPoint(landing + Vector3.up * .06f);
            if(point.z <= 0 || point.x < .04f || point.x > .96f || point.y < .08f || point.y > .94f) return;
            float x = point.x * width, y = (1 - point.y) * height;
            Rect label = new Rect(x - 60, y + 13, 120, 27);
            if(label.Overlaps(information) || label.yMax >= controls.y - 8) return;
            Color saved = GUI.color;
            GUI.color = Gold;
            GUI.DrawTexture(new Rect(x - 11, y - 1, 22, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1, y - 8, 2, 16), Texture2D.whiteTexture);
            GUI.color = saved;
            DrawPanel(label);
            GUI.Label(new Rect(label.x + 4, label.y + 3, label.width - 8, 22), "空格 · 下台", marker);
        }

        private void DrawCartInteraction(float width, float height, Rect information, Rect controls)
        {
            if (!court.IsCartTargeted || court.IsCartAssisting || court.IsObserving || court.State.Completed) return;
            Camera camera = court.CameraRig.ViewCamera;
            if (camera == null) return;
            Vector3 point = camera.WorldToViewportPoint(court.Bindings.cart.position + Vector3.up * 2.8f);
            if (point.z <= 0 || point.x < .08f || point.x > .92f || point.y < .15f || point.y > .92f) return;
            Rect label = new Rect(point.x * width - 63, (1 - point.y) * height - 15, 126, 30);
            if (label.Overlaps(information) || label.yMax > controls.y - 60) return;
            DrawPanel(label);
            GUI.Label(new Rect(label.x + 5, label.y + 4, label.width - 10, 23), "E · 操作苗车", marker);
        }

        private static void DrawPanel(Rect area)
        {
            Color saved = GUI.color;
            GUI.color = PanelColor;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = saved;
        }
    }
}

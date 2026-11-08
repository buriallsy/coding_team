using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun
{
    /// <summary>L5's mirror-heart errand and modal conversations; physical optics remain owned by the collector.</summary>
    [DefaultExecutionOrder(-80)]
    public sealed class SkywellStory : MonoBehaviour
    {
        [Header("Story references / 剧情接线")]
        [SerializeField] private SkywellController court;
        [SerializeField] private SkywellObservation observation;
        [SerializeField] private SkywellSunCollector collector;
        [SerializeField] private Transform inspectionPoint, northNpc, eastNpc;
        [SerializeField, Range(.5f, 2.7f)] private float interactionRadius = 2.5f;

        [Header("Speakers / 对话名称")]
        [SerializeField] private string inspectionSpeaker = "采日镜";
        [SerializeField] private string northSpeaker = "守镜人";
        [SerializeField] private string eastSpeaker = "守灯人";

        [Header("Inspection / 镜心检查与修补")]
        [SerializeField, TextArea(2, 4)] private string[] brokenLines =
        {
            "年久失修，需要填补镜心。这里缺了一块菱形的集光石。"
        };
        [SerializeField, TextArea(2, 4)] private string[] repairLines =
        {
            "这块菱形集光石，与镜心的缺口恰好吻合。",
            "将集光石嵌入镜心，让这面老镜重新接住日光。"
        };
        [SerializeField, TextArea(2, 4)] private string[] repairedLines =
        {
            "镜心已经补好。风能转动镜面，晴天会让日光落进镜心。"
        };
        [SerializeField, TextArea(2, 4)] private string repairFailureLine =
            "集光石还没有嵌稳，先收好它，再检查一次镜心。";

        [Header("North village / 向阳山守镜人")]
        [SerializeField, TextArea(2, 4)] private string[] northHintLines =
        {
            "山阳也有照不到的檐角，山阴也有人守着灯。",
            "光明中有黑暗，黑暗中亦有光明。去对岸问问守灯人吧。"
        };
        [SerializeField, TextArea(2, 4)] private string[] northGemLines =
        {
            "原来那块镜心一直被妥善收着。把它放回采日镜吧，老镜还等着再照一次对岸。"
        };
        [SerializeField, TextArea(2, 4)] private string[] northRepairedLines =
        {
            "镜心已经补好。让风把镜面转向中央接力镜，再借一片晴天，就看见光要走的路了。"
        };
        [SerializeField, TextArea(2, 4)] private string[] northRewardLines =
        {
            "看，光已经过桥了。守灯的人终于等到了这一刻。"
        };

        [Header("East village / 山阴守灯人")]
        [SerializeField, TextArea(2, 4)] private string[] eastBeforeViewLines =
        {
            "先到接光盘边，看看我们的村子吧。"
        };
        [SerializeField, TextArea(2, 4)] private string[] eastGiftLines =
        {
            "祖父说，向阳山的老镜曾把日光送到这里。后来镜心破损，这一侧便只剩窗里的灯。",
            "这块集光石是祖父留下的，一直由我们守着。请把它带回对岸，补上采日镜缺失的镜心。",
            "让日光再过一次桥——拜托了。"
        };
        [SerializeField, TextArea(2, 4)] private string[] eastGemGivenLines =
        {
            "集光石已经交给你了。向阳山的采日镜，就托付给你了。"
        };
        [SerializeField, TextArea(2, 4)] private string[] eastRepairedLines =
        {
            "祖父留下的镜心，又回到老镜里了。我们会在这里等它接住日光。"
        };
        [SerializeField, TextArea(2, 4)] private string[] eastRewardLines =
        {
            "日光真的过桥了。谢谢你，把祖父的话变成了眼前的风景。"
        };

        [Header("Dialogue camera / 对话近景")]
        [SerializeField, Min(.05f)] private float cameraBlendSeconds = .25f;
        [SerializeField, Range(30, 65)] private float dialogueFieldOfView = 45;
        [SerializeField, Tooltip("World offset from the player/interaction midpoint; author each close view without moving the player.")]
        private Vector3 inspectionCameraOffset = new Vector3(3, 6, 5.5f);
        [SerializeField] private Vector3 northCameraOffset = new Vector3(2.8f, 1.1f, -3.4f);
        [SerializeField] private Vector3 eastCameraOffset = new Vector3(-2.8f, 1.1f, -3.4f);

        private enum Completion { None, GiveGem, RepairMirror }
        private Completion pendingCompletion;
        private string[] lines;
        private int lineIndex, closedFrame = -1;
        private string speaker;
        private CourtCamera capturedRig;
        private Camera capturedCamera;
        private bool cameraCaptured, rigWasEnabled, cameraWasEnabled, cameraWasOrthographic;
        private Vector3 originalCameraPosition, dialogueCameraPosition;
        private Quaternion originalCameraRotation, dialogueCameraRotation;
        private float originalFieldOfView, originalOrthographicSize, cameraBlendStarted;

        public bool DialogueOpen { get; private set; }
        public bool InputBlocked => DialogueOpen || closedFrame == Time.frameCount;
        public string Speaker => DialogueOpen ? speaker : string.Empty;
        public string Text => DialogueOpen && lines != null && lineIndex < lines.Length ? lines[lineIndex] : string.Empty;
        public bool HasGem { get; private set; }
        public bool GemGiven { get; private set; }
        public bool Inspected { get; private set; }
        public float InteractionRadius => interactionRadius;
        private bool VillageLit => court && court.Optics && court.Optics.SettlementReceiver && court.Optics.SettlementReceiver.RewardAvailable;

        public void Configure(SkywellController owner, SkywellObservation view, SkywellSunCollector mirror,
            Transform inspectPoint, Transform northNpcPoint, Transform eastNpcPoint)
        {
            Close();
            court = owner; observation = view; collector = mirror;
            inspectionPoint = inspectPoint; northNpc = northNpcPoint; eastNpc = eastNpcPoint;
        }

        public Transform InteractionPoint(int index) => index == 0 ? inspectionPoint : index == 1 ? northNpc : index == 2 ? eastNpc : null;

        public string PromptFor(int index)
        {
            if (!isActiveAndEnabled || !InteractionPoint(index)) return string.Empty;
            if (index == 0) return collector && collector.IsRepaired ? "E 查看采日镜" : HasGem ? "E 填补镜心" : "E 检查采日镜";
            return "E 与" + (index == 1 ? northSpeaker : eastSpeaker) + "交谈";
        }

        public bool TryInteract(int index)
        {
            if (InputBlocked) return true;
            var point = InteractionPoint(index);
            if (!isActiveAndEnabled || !court || !court.Player || !point || !point.gameObject.activeInHierarchy) return false;
            Vector3 delta = court.Player.FeetPosition - point.position;
            if (Mathf.Abs(delta.y) > 1.5f || Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude > interactionRadius * interactionRadius) return false;
            if (court.ObservationOpen || court.WeatherInputBlocked || court.WeatherSelectionHeld || court.Journey && court.Journey.ControlsMovement) return false;

            pendingCompletion = Completion.None;
            if (index == 0)
            {
                Inspected = true;
                if (collector && collector.IsRepaired) Begin(inspectionSpeaker, repairedLines, index);
                else if (HasGem) { pendingCompletion = Completion.RepairMirror; Begin(inspectionSpeaker, repairLines, index); }
                else Begin(inspectionSpeaker, brokenLines, index);
            }
            else if (index == 1)
                Begin(northSpeaker, VillageLit ? northRewardLines : collector && collector.IsRepaired ? northRepairedLines : HasGem ? northGemLines : northHintLines, index);
            else
            {
                if (VillageLit) Begin(eastSpeaker, eastRewardLines, index);
                else if (collector && collector.IsRepaired) Begin(eastSpeaker, eastRepairedLines, index);
                else if (GemGiven) Begin(eastSpeaker, eastGemGivenLines, index);
                else if (!observation || !observation.HasViewed) Begin(eastSpeaker, eastBeforeViewLines, index);
                else { pendingCompletion = Completion.GiveGem; Begin(eastSpeaker, eastGiftLines, index); }
            }
            return true;
        }

        private void Begin(string who, string[] pages, int interaction)
        {
            speaker = who; lines = pages != null && pages.Length > 0 ? pages : new[] { "……" };
            lineIndex = 0; DialogueOpen = true;
            CaptureCamera(interaction);
        }

        // E is routed here by the controller. Do not read E a second time in this component.
        public void Advance()
        {
            if (!DialogueOpen) return;
            if (lineIndex + 1 < lines.Length) { lineIndex++; return; }
            if (pendingCompletion == Completion.GiveGem && !GemGiven && observation && observation.HasViewed)
            {
                HasGem = GemGiven = true;
                court.ShowMessage("获得集光石 · 带回向阳山填补镜心。", 5);
            }
            else if (pendingCompletion == Completion.RepairMirror && HasGem)
            {
                if (!collector || !collector.TryInstallGem())
                {
                    pendingCompletion = Completion.None;
                    lines = new[] { repairFailureLine }; lineIndex = 0;
                    return;
                }
                HasGem = false;
                court.ShowMessage("镜心已补好 · 用风转动镜面，再借晴接光。", 5);
            }
            Close();
        }

        public void Close()
        {
            bool wasOpen = DialogueOpen || cameraCaptured;
            DialogueOpen = false; pendingCompletion = Completion.None; lines = null; lineIndex = 0;
            RestoreCamera();
            if (wasOpen) closedFrame = Time.frameCount;
        }

        public void ResetStory()
        {
            Close(); HasGem = GemGiven = Inspected = false;
        }

        private void Update()
        {
            if (DialogueOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        private void CaptureCamera(int interaction)
        {
            if (!court.CameraRig || !court.CameraRig.ViewCamera) return;
            capturedRig = court.CameraRig; capturedCamera = capturedRig.ViewCamera;
            rigWasEnabled = capturedRig.enabled; cameraWasEnabled = capturedCamera.enabled;
            cameraWasOrthographic = capturedCamera.orthographic;
            originalCameraPosition = capturedCamera.transform.position; originalCameraRotation = capturedCamera.transform.rotation;
            originalFieldOfView = capturedCamera.fieldOfView; originalOrthographicSize = capturedCamera.orthographicSize;
            var anchor = InteractionPoint(interaction);
            Vector3 focus = interaction == 0 && collector
                ? collector.OutputOrigin - Vector3.up * .6f
                : Vector3.Lerp(court.Player.FeetPosition, anchor.position, .65f) + Vector3.up * 1.25f;
            Vector3 offset = interaction == 0 ? inspectionCameraOffset : interaction == 1 ? northCameraOffset : eastCameraOffset;
            dialogueCameraPosition = focus + offset;
            dialogueCameraRotation = Quaternion.LookRotation(focus - dialogueCameraPosition, Vector3.up);
            cameraBlendStarted = Time.unscaledTime; cameraCaptured = true;
            // Disabling only this driver preserves its private orbit/zoom and overview state.
            capturedRig.enabled = false; capturedRig.InputSuppressed = true;
            capturedCamera.orthographic = false;
        }

        private void LateUpdate()
        {
            if (!DialogueOpen || !cameraCaptured || !capturedCamera) return;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - cameraBlendStarted) / Mathf.Max(.05f, cameraBlendSeconds)));
            capturedCamera.transform.SetPositionAndRotation(Vector3.Lerp(originalCameraPosition, dialogueCameraPosition, t), Quaternion.Slerp(originalCameraRotation, dialogueCameraRotation, t));
            capturedCamera.fieldOfView = Mathf.Lerp(originalFieldOfView, dialogueFieldOfView, t);
        }

        private void RestoreCamera()
        {
            if (!cameraCaptured) return;
            if (capturedCamera)
            {
                capturedCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
                capturedCamera.fieldOfView = originalFieldOfView; capturedCamera.orthographicSize = originalOrthographicSize;
                capturedCamera.orthographic = cameraWasOrthographic; capturedCamera.enabled = cameraWasEnabled;
            }
            if (capturedRig)
            {
                capturedRig.enabled = rigWasEnabled;
                // The controller recomputes its weather/observation locks next frame; consume the closing key now.
                capturedRig.InputSuppressed = true;
            }
            cameraCaptured = false; capturedRig = null; capturedCamera = null;
        }

        private void OnDisable() => Close();
        private void OnApplicationFocus(bool focused) { if (!focused) Close(); }
    }
}

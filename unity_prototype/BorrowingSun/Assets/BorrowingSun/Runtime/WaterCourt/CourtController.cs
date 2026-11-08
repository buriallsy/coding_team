using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Owns scene references, input intents and the animation of CourtState.</summary>
    public sealed class CourtController : MonoBehaviour, ICourtGameplay
    {
        [SerializeField] private Transform lowerDeck;
        [SerializeField] private Transform upperDeck;
        [SerializeField] private Transform cart;
        [SerializeField] private Transform waterSurface;
        [SerializeField] private Transform rainMachine;
        [SerializeField] private Transform drainValve;
        [SerializeField] private CourtPlayer player;
        [SerializeField] private CourtCamera cameraRig;
        [SerializeField] private ParticleSystem rainVfx;
        [SerializeField] private float cartSpeed = 2.4f;
        private CourtState state;
        private Vector3 cartMoveStart;
        private Vector3 cartMoveEnd;
        private Quaternion cartTurnStart;
        private Quaternion cartTurnEnd;
        private float cartMoveProgress;
        private float cartMoveDuration;
        private float messageUntil;
        private string message = "";
        private bool configured;

        public CourtState State => state ?? (state = new CourtState());
        public CourtPlayer Player => player;
        public CourtCamera CameraRig => cameraRig;
        public bool IsObserving => cameraRig != null && cameraRig.IsObserving;
        public string Message => Time.unscaledTime < messageUntil ? message : "";
        public Transform CartTransform => cart;
        public Transform RainMachine => rainMachine;
        public Transform DrainValve => drainValve;

        public void Initialize(Transform lower, Transform upper, Transform cartRoot, Transform water,
            Transform weatherStation, Transform valve, CourtPlayer actor, CourtCamera cameraController,
            ParticleSystem rain = null)
        {
            lowerDeck = lower;
            upperDeck = upper;
            cart = cartRoot;
            waterSurface = water;
            rainMachine = weatherStation;
            drainValve = valve;
            player = actor;
            cameraRig = cameraController;
            rainVfx = rain;
        }

        private void Start()
        {
            if (lowerDeck == null || upperDeck == null || cart == null || rainMachine == null ||
                drainValve == null || player == null || cameraRig == null || cameraRig.ViewCamera == null)
            {
                Debug.LogError("水院缺少场景接线，请运行白盒场景生成器。", this);
                enabled = false;
                return;
            }
            player.Initialize(this, cameraRig.ViewCamera, player.SpawnPosition);
            configured = true;
            ResetCourt();
        }

        private void Update()
        {
            if (configured) Simulate(Time.deltaTime);
        }

        /// <summary>Used both by Update and repeatable editor smoke checks.</summary>
        public void Simulate(float deltaTime)
        {
            float previousHeight = State.WaterHeight;
            bool carryPlayer = player != null && IsOnMovingDeck(player.FeetPosition, previousHeight);
            State.Advance(deltaTime);
            float rise = State.WaterHeight - previousHeight;
            if (carryPlayer && Mathf.Abs(rise) > 0.00001f) player.CarryByPlatform(rise);
            ApplyWaterVisuals();
            if (Mathf.Abs(rise) > 0.00001f)
            {
                Physics.SyncTransforms();
            }

            if (State.CartInMotion && cart != null)
            {
                cartMoveProgress = Mathf.Min(1, cartMoveProgress + Mathf.Max(0, deltaTime) / cartMoveDuration);
                // Turning in place first keeps the cart's swept bounds inside the 3 m platforms.
                float turn = Mathf.Clamp01(cartMoveProgress / 0.2f);
                float travel = Mathf.Clamp01((cartMoveProgress - 0.2f) / 0.8f);
                cart.rotation = Quaternion.Slerp(cartTurnStart, cartTurnEnd, turn);
                cart.position = Vector3.Lerp(cartMoveStart, cartMoveEnd, Mathf.SmoothStep(0, 1, travel));
                if (cartMoveProgress >= 1)
                {
                    State.CompleteCartMove();
                    cart.position = GetCartStopPosition(State.CartStop);
                    if (State.Delivered)
                        ShowMessage(State.Completed ? "苗车已送达，雨牌也已带回。水院货路恢复！" : "苗车已送达。沿检修坡回亭里取回雨牌，再踏上下一段货路。");
                }
            }
            else if (cart != null) cart.position = GetCartStopPosition(State.CartStop);

            if (rainVfx != null)
            {
                if (State.RainActive && !rainVfx.isPlaying) rainVfx.Play();
                if (!State.RainActive && rainVfx.isPlaying) rainVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void ResetCourt()
        {
            State.Reset();
            cartMoveProgress = 0;
            if (cart != null)
            {
                cart.position = GetCartStopPosition(0);
                cart.rotation = Quaternion.identity;
            }
            ApplyWaterVisuals();
            if (rainVfx != null) rainVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (player != null) player.ResetPlayer();
            if (cameraRig != null) cameraRig.ExitObservation();
            Physics.SyncTransforms();
            ShowMessage("把苗车送到右上方收货台。靠近苗车按 E，把它推上浮架。", 7f);
        }

        public Vector3 GetCartStopPosition(int index)
        {
            switch (index)
            {
                case 0: return new Vector3(0, 0, -7);
                case 1: return new Vector3(0, State.WaterHeight, 0);
                case 2: return new Vector3(0, 3, 5);
                case 3: return new Vector3(-5.5f, 3, 5);
                case 4: return new Vector3(-5.5f, 3, 0);
                case 5: return new Vector3(0, State.WaterHeight + 3, 0);
                case 6: return new Vector3(8, 3, 0);
                default: return new Vector3(8, 3, 5);
            }
        }

        public bool TryMoveCart(int direction, bool requireProximity = true)
        {
            if (cart == null) return false;
            if (requireProximity && !IsNear(cart, 3.1f))
            { ShowMessage("靠近苗车后再推车。"); return false; }
            if (!State.CanMoveCart(direction, out string reason))
            { ShowMessage(reason); return false; }
            cartMoveStart = cart.position;
            cartMoveEnd = GetCartStopPosition(State.CartStop + direction);
            cartTurnStart = cart.rotation;
            Vector3 travelDirection = cartMoveEnd - cartMoveStart;
            cartTurnEnd = travelDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(travelDirection.normalized, Vector3.up) : cart.rotation;
            cartMoveDuration = Mathf.Max(0.8f, travelDirection.magnitude / cartSpeed / 0.8f);
            cartMoveProgress = 0;
            return State.BeginCartMove(direction);
        }

        public void InteractPrimary()
        {
            Interaction target = GetInteraction();
            if (target == Interaction.Cart) { TryMoveCart(1); return; }
            if (target == Interaction.Weather)
            {
                if (!State.CardInserted) { ShowMessage("先按 Q 把雨牌插入控制座，然后按 E 开雨。"); return; }
                if (!State.ToggleRain()) { ShowMessage("苗车移动时不能改变水位。"); return; }
                ShowMessage(State.RainActive ? "雨机启动，排水阀闭合。浮架开始上升。" : "雨机停止，池内水位保留。");
                return;
            }
            if (target == Interaction.Drain)
            {
                if (!State.ToggleDrain()) { ShowMessage("苗车移动时不能改变水位。"); return; }
                ShowMessage(State.DrainOpen ? "排水阀打开，雨机停止。浮架开始下降。" : "排水阀关闭，当前水位保留。");
                return;
            }
            ShowMessage("靠近苗车、雨亭控制座或上岸货台的排水阀操作。");
        }

        public void InteractSecondary()
        {
            Interaction target = GetInteraction();
            if (target == Interaction.Weather)
            {
                bool wasInserted = State.CardInserted;
                bool success = wasInserted ? State.RemoveCard() : State.InsertCard();
                if (!success) { ShowMessage("请等苗车停稳后再操作雨牌。"); return; }
                ShowMessage(wasInserted ? "雨牌已回收；停止下雨，已有水位保留。" : "雨牌已插入；按 E 开雨。");
                return;
            }
            if (target == Interaction.Cart) { TryMoveCart(-1); return; }
            ShowMessage("Q：靠近雨亭时插入／回收雨牌；靠近苗车时倒推一个停靠点。");
        }

        public string GetInteractionPrompt()
        {
            switch (GetInteraction())
            {
                case Interaction.Weather:
                    return State.CardInserted ? (State.RainActive ? "E 停雨   ·   Q 回收雨牌" : "E 开雨   ·   Q 回收雨牌")
                        : "Q 插入雨牌，然后 E 开雨";
                case Interaction.Drain: return State.DrainOpen ? "E 关闭排水阀" : "E 打开排水阀";
                case Interaction.Cart: return State.CartInMotion ? "苗车移动中…" : "E 向前推一站   ·   Q 倒推一站";
                default: return "走近苗车、雨亭或排水阀，查看操作";
            }
        }

        public string GetObjective()
        {
            if (State.Completed) return "完成：苗车送达，雨牌回收。";
            if (State.Delivered) return "沿岸边检修坡回到雨亭，取回雨牌。";
            return "把苗车送往右上方收货台，并带回雨牌。";
        }

        public void ShowMessage(string text, float seconds = 4f)
        {
            message = text;
            messageUntil = Time.unscaledTime + seconds;
        }

        public bool IsOnMovingDeck(Vector3 feet, float height)
        {
            return DetermineSupport(feet, height) != MovingDeckSupport.None;
        }

        public static MovingDeckSupport DetermineSupport(Vector3 feet, float height)
        {
            bool lower = Mathf.Abs(feet.x) < 1.48f && Mathf.Abs(feet.z) < 2.98f &&
                feet.y >= height - 0.12f && feet.y <= height + 0.32f;
            bool upper = Mathf.Abs(feet.x) < 3.98f && Mathf.Abs(feet.z) < 1.48f &&
                feet.y >= height + 2.88f && feet.y <= height + 3.32f;
            return lower ? MovingDeckSupport.Lower : upper ? MovingDeckSupport.Upper : MovingDeckSupport.None;
        }

        public bool IsPermanentGround(Vector3 feet)
        {
            if (IsOnMovingDeck(feet, State.WaterHeight)) return false;
            // Interior pool positions must never become a recovery point.
            if (feet.x > -4.05f && feet.x < 4.05f && feet.z > -3.05f && feet.z < 3.05f) return false;
            return feet.y > -0.2f && feet.y < 3.5f;
        }

        public bool IsInsideHazard(Vector3 feet)
        {
            if (feet.y < -1 || Mathf.Abs(feet.x) > 22 || Mathf.Abs(feet.z) > 22) return true;
            if (IsOnMovingDeck(feet, State.WaterHeight)) return false;
            bool inPool = feet.x > -4 && feet.x < 4 && feet.z > -3 && feet.z < 3;
            return inPool && feet.y < State.WaterHeight - 0.12f;
        }

        private void ApplyWaterVisuals()
        {
            float height = State.WaterHeight;
            if (lowerDeck != null) lowerDeck.position = new Vector3(0, height, 0);
            if (upperDeck != null) upperDeck.position = new Vector3(0, height + 3, 0);
            if (waterSurface != null)
            {
                Vector3 position = waterSurface.position;
                position.y = height - 0.12f;
                waterSurface.position = position;
            }
        }

        private bool IsNear(Transform target, float radius)
        {
            if (target == null || player == null) return false;
            Vector3 delta = target.position - player.FeetPosition;
            return Mathf.Abs(delta.y) < 1.6f && new Vector2(delta.x, delta.z).sqrMagnitude < radius * radius;
        }

        private Interaction GetInteraction()
        {
            Interaction result = Interaction.None;
            float nearest = float.PositiveInfinity;
            Consider(rainMachine, 2.3f, Interaction.Weather, ref result, ref nearest);
            Consider(drainValve, 2.3f, Interaction.Drain, ref result, ref nearest);
            Consider(cart, 3.1f, Interaction.Cart, ref result, ref nearest);
            return result;
        }

        private void Consider(Transform target, float radius, Interaction kind, ref Interaction result, ref float nearest)
        {
            if (!IsNear(target, radius)) return;
            float distance = (target.position - player.FeetPosition).sqrMagnitude;
            if (distance >= nearest) return;
            nearest = distance;
            result = kind;
        }

        private enum Interaction { None, Weather, Drain, Cart }
    }

    public enum MovingDeckSupport { None, Lower, Upper }
}

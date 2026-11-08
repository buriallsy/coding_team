using System;
using UnityEngine;

namespace BorrowingSun
{
    [Serializable]
    public sealed class BorrowedRainBindings
    {
        public Vector3 worldOffset = new Vector3(-6, 0, 66);
        public Transform aBridge, bLift, cart, waterA, waterB;
        public Transform station0, station1, valveA, valveB;
        public Transform cardToken, successfulFlow, wasteFlow;
        public Transform[] safeRecoveryPoints;
        public CourtPlayer player;
        public CourtCamera cameraRig;
        public ParticleSystem localRain, remoteRain;
        public Font hudFont;
    }

    public enum BorrowedRainSupport { None, BridgeA, LiftB }

    /// <summary>Scene binding and presentation for the two-courtyard prototype.</summary>
    public sealed class BorrowedRainController : MonoBehaviour, ICourtGameplay
    {
        [SerializeField] private BorrowedRainBindings bindings = new BorrowedRainBindings();
        [SerializeField] private float cartSpeed = 2.4f;
        private BorrowedRainState state;
        private bool configured;
        private Vector3 cartStart, cartEnd;
        private Quaternion turnStart, turnEnd;
        private float moveProgress, moveDuration;
        private string message = "";
        private float messageUntil;
        private readonly RaycastHit[] sightHits = new RaycastHit[24];
        private Transform movingGutterOutlet, wasteRainBeam, receivedRainBeam;
        private Vector3 wasteCatchPoint, receivingSpoutPoint, receivingWaterPoint;
        private bool flowVisualsCached;

        public BorrowedRainBindings Bindings => bindings;
        public BorrowedRainState State => state ?? (state = new BorrowedRainState());
        public CourtPlayer Player => bindings.player;
        public CourtCamera CameraRig => bindings.cameraRig;
        public Transform CartTransform => bindings.cart;
        public bool IsObserving => CameraRig != null && CameraRig.IsObserving;
        public string Message => Time.unscaledTime < messageUntil ? message : "";

        public void Initialize(BorrowedRainBindings sceneBindings)
        {
            bindings = sceneBindings ?? throw new ArgumentNullException(nameof(sceneBindings));
            flowVisualsCached = false;
        }

        private void Start()
        {
            if (bindings.aBridge == null || bindings.bLift == null || bindings.cart == null ||
                bindings.station0 == null || bindings.station1 == null || bindings.valveA == null ||
                bindings.valveB == null || Player == null || CameraRig == null || CameraRig.ViewCamera == null)
            {
                Debug.LogError("借雨双院场景接线缺失，请重新检查白盒生成器。", this);
                enabled = false;
                return;
            }
            Player.InitializeGameplay(this, CameraRig.ViewCamera, Player.SpawnPosition);
            configured = true;
            ResetCourt();
        }

        private void Update()
        {
            if (configured) Simulate(Time.deltaTime);
        }

        public void Simulate(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) return;
            float oldA = State.WaterA, oldB = State.WaterB;
            var support = Player != null
                ? DetermineSupport(Player.FeetPosition, oldA, oldB, bindings.worldOffset) : BorrowedRainSupport.None;
            State.Advance(deltaTime);
            float deltaA = State.WaterA - oldA, deltaB = State.WaterB - oldB;
            if (Player != null && support != BorrowedRainSupport.None)
                Player.CarryByPlatform(support == BorrowedRainSupport.BridgeA ? deltaA : deltaB);
            ApplyWaterVisuals();
            if (Mathf.Abs(deltaA) + Mathf.Abs(deltaB) > 0.00001f) Physics.SyncTransforms();

            if (State.CartInMotion && CartTransform != null)
            {
                moveProgress = Mathf.Min(1, moveProgress + deltaTime / Mathf.Max(0.01f, moveDuration));
                float turn = Mathf.Clamp01(moveProgress / 0.2f);
                float travel = Mathf.Clamp01((moveProgress - 0.2f) / 0.8f);
                CartTransform.rotation = Quaternion.Slerp(turnStart, turnEnd, turn);
                CartTransform.position = Vector3.Lerp(cartStart, cartEnd, Mathf.SmoothStep(0, 1, travel));
                if (moveProgress >= 1)
                {
                    State.CompleteCartMove();
                    CartTransform.position = GetCartStopPosition(State.CartStop);
                    if (State.Delivered) ShowMessage(State.Completed ? "苗车与雨牌都已到位，双院货路复通。" : GetObjective());
                }
            }
            else if (CartTransform != null) CartTransform.position = GetCartStopPosition(State.CartStop);
            ApplyEffects();
        }

        public void ResetCourt()
        {
            State.Reset();
            moveProgress = 0;
            if (CartTransform != null)
            {
                CartTransform.position = GetCartStopPosition(0);
                CartTransform.rotation = Quaternion.Euler(0, 90, 0);
            }
            ApplyWaterVisuals();
            StopAndClear(bindings.localRain);
            StopAndClear(bindings.remoteRain);
            if (Player != null) Player.ResetPlayer();
            if (CameraRig != null) CameraRig.ExitObservation();
            ApplyEffects();
            Physics.SyncTransforms();
            ShowMessage("恢复整条货路，把苗车送到对岸，再带走雨牌。先沿固定检修廊看看水路。", 7);
        }

        public Vector3 GetCartStopPosition(int index)
        {
            Vector3 local;
            switch (index)
            {
                case 0: local = new Vector3(-8, 15, 0); break;
                case 1: local = new Vector3(-3, 15 + State.WaterA, 0); break;
                case 2: local = new Vector3(1, 15, 0); break;
                case 3: local = new Vector3(4, 12 + State.WaterB, 0); break;
                case 4: local = new Vector3(8, 15, 0); break;
                default: local = new Vector3(8, 15, 4); break;
            }
            return bindings.worldOffset + local;
        }

        public bool TryMoveCart(int direction, bool requireProximity = true)
        {
            if (CartTransform == null) return false;
            if (requireProximity && !IsNear(CartTransform, 3.1f)) { ShowMessage("走到苗车旁再推车。"); return false; }
            if (!State.CanMoveCart(direction, out string reason)) { ShowMessage(reason); return false; }
            cartStart = CartTransform.position;
            cartEnd = GetCartStopPosition(State.CartStop + direction);
            turnStart = CartTransform.rotation;
            Vector3 directionVector = cartEnd - cartStart;
            // Reverse rolls backwards with the same forward heading on straight decks. Only the
            // fixed, wide eastern court needs a quarter-turn at its corner; never spin 180 on A or B.
            Vector3 heading = direction < 0 ? -directionVector : directionVector;
            turnEnd = Quaternion.LookRotation(heading.normalized, Vector3.up);
            moveDuration = Mathf.Max(0.8f, directionVector.magnitude / cartSpeed / 0.8f);
            moveProgress = 0;
            return State.BeginCartMove(direction);
        }

        public void InteractPrimary()
        {
            if (NeedsSafeReturn()) { ReturnToFixedWalkway(); return; }
            Interaction target = GetInteraction();
            if (target == Interaction.Cart) { TryMoveCart(1); return; }
            if (target == Interaction.Station0 || target == Interaction.Station1)
            {
                int station = target == Interaction.Station0 ? 0 : 1;
                if (State.CardStation != station)
                { ShowMessage(State.CardStation == BorrowedRainState.Hand ? "按 Q 插入雨牌，再按 E 开雨。" : "唯一雨牌留在另一座亭中，先去取回。"); return; }
                if (!State.ToggleRain(station)) { ShowMessage("苗车跨接中，请等停稳后操作。"); return; }
                ShowMessage(!State.RainActive ? "停止下雨，两池保留各自的水位。" : station == 0
                    ? "本地雨落入 A 池。" : "借景窗开始出雨，留意出口后的实际水路。");
                return;
            }
            if (target == Interaction.ValveA || target == Interaction.ValveB)
            {
                int basin = target == Interaction.ValveA ? 0 : 1;
                if (!State.ToggleDrain(basin)) { ShowMessage("苗车跨接中，请等停稳后操作。"); return; }
                bool opened = basin == 0 ? State.DrainAOpen : State.DrainBOpen;
                ShowMessage((basin == 0 ? "A" : "B") + (opened ? " 池开始排水，另一池水位独立保留。" : " 池排水关闭，当前水位保留。"));
                return;
            }
            ShowMessage("靠近雨亭、排水阀或苗车操作。C 可查看高槽侧面。");
        }

        public void InteractSecondary()
        {
            Interaction target = GetInteraction();
            if (target == Interaction.Cart) { TryMoveCart(-1); return; }
            if (target == Interaction.Station0 || target == Interaction.Station1)
            {
                int station = target == Interaction.Station0 ? 0 : 1;
                bool taking = State.CardStation == station;
                if (!(taking ? State.RemoveCard(station) : State.InsertCard(station)))
                {
                    ShowMessage(State.CartInMotion ? "苗车跨接中，请等停稳后取牌。" : "雨牌还在另一座亭，不能复制或隔空取回。");
                    return;
                }
                ShowMessage(taking ? "雨牌已取回，雨停水留。" : "雨牌已插入，按 E 开雨。");
                ApplyEffects();
                return;
            }
            ShowMessage("Q：靠近雨亭插拔雨牌；靠近苗车倒推一站。");
        }

        public string GetInteractionPrompt()
        {
            SafeReturnKind returnKind = GetSafeReturnKind();
            if (returnKind == SafeReturnKind.IsolatedLanding) return "中接台两端有高差 · E 安全返回两岸（保留车与水位）";
            if (returnKind == SafeReturnKind.StoppedPlatform) return "停在中间水位 · E 安全返回检修台（保留车与水位）";
            switch (GetInteraction())
            {
                case Interaction.Station0: return StationPrompt(0, "本地雨亭 S0");
                case Interaction.Station1: return StationPrompt(1, "借景雨亭 S1");
                case Interaction.ValveA: return State.DrainAOpen ? "A 池排水阀 · E 关闭" : "A 池排水阀 · E 打开";
                case Interaction.ValveB: return State.DrainBOpen ? "B 池排水阀 · E 关闭" : "B 池排水阀 · E 打开";
                case Interaction.Cart: return State.CartInMotion ? "苗车正在过接缝…" : "苗车 · E 前进一站   Q 倒推一站";
                default: return "沿固定检修廊观察两池与雨槽，靠近设备查看操作";
            }
        }

        private string StationPrompt(int station, string title)
        {
            if (State.CardStation == station) return title + (State.RainActive ? " · E 停雨   Q 取牌" : " · E 开雨   Q 取牌");
            return title + (State.CardStation == BorrowedRainState.Hand ? " · Q 插入唯一雨牌" : " · 雨牌在另一亭中");
        }

        public string GetObjective()
        {
            if (State.Completed) return "完成：双院货路复通，苗车与雨牌已到位。";
            if (State.Delivered && !State.CargoRouteConnected) return "苗车已交付；恢复整条货路，并带走雨牌。";
            if (State.Delivered) return "货路已接通，沿固定检修廊取回雨牌。";
            return "恢复整条货路，把苗车送到东侧，并带走雨牌。";
        }

        public void ShowMessage(string text, float seconds = 4f)
        {
            message = text;
            messageUntil = Time.unscaledTime + seconds;
        }

        public static BorrowedRainSupport DetermineSupport(Vector3 worldFeet, float waterA, float waterB, Vector3 offset)
        {
            Vector3 feet = worldFeet - offset;
            if (feet.z < -1.49f || feet.z > 1.49f) return BorrowedRainSupport.None;
            if (feet.x > -5.99f && feet.x < -0.01f && AtHeight(feet.y, 15 + waterA)) return BorrowedRainSupport.BridgeA;
            if (feet.x > 2.01f && feet.x < 5.99f && AtHeight(feet.y, 12 + waterB)) return BorrowedRainSupport.LiftB;
            return BorrowedRainSupport.None;
        }

        private static bool AtHeight(float feet, float height) => feet >= height - 0.12f && feet <= height + 0.32f;

        public bool IsPermanentGround(Vector3 feet)
        {
            if (DetermineSupport(feet, State.WaterA, State.WaterB, bindings.worldOffset) != BorrowedRainSupport.None) return false;
            if (IsIsolatedCentralLanding(feet)) return false;
            Vector3 local = feet - bindings.worldOffset;
            if (InsideMovingFootprint(local)) return false;
            return local.y >= 11.8f && local.y <= 20 && Mathf.Abs(local.x) < 22 && Mathf.Abs(local.z) < 18;
        }

        public bool IsInsideHazard(Vector3 feet)
        {
            Vector3 local = feet - bindings.worldOffset;
            if (local.y < 10.5f || Mathf.Abs(local.x) > 28 || Mathf.Abs(local.z) > 24) return true;
            if (DetermineSupport(feet, State.WaterA, State.WaterB, bindings.worldOffset) != BorrowedRainSupport.None) return false;
            // There are no walkable floors below these moving decks; a fallen actor recovers without resetting the puzzle.
            return InsideMovingFootprint(local) && local.y < (local.x < 0 ? 15 + State.WaterA : 12 + State.WaterB) - 0.2f;
        }

        private static bool InsideMovingFootprint(Vector3 local) => Mathf.Abs(local.z) < 1.5f &&
            ((local.x > -6 && local.x < 0) || (local.x > 2 && local.x < 6));

        public bool IsIsolatedCentralLanding(Vector3 worldFeet)
        {
            Vector3 feet = worldFeet - bindings.worldOffset;
            bool onLanding = feet.x >= 0 && feet.x <= 2 && feet.z >= -1.5f && feet.z <= 6 && AtHeight(feet.y, 15);
            bool westConnected = State.IsALow && !State.AMoving;
            bool eastConnected = State.IsBHigh && !State.BMoving;
            return onLanding && !westConnected && !eastConnected;
        }

        private bool NeedsSafeReturn() => GetSafeReturnKind() != SafeReturnKind.None;

        private SafeReturnKind GetSafeReturnKind()
        {
            if (Player == null || State.CartInMotion) return SafeReturnKind.None;
            if (IsIsolatedCentralLanding(Player.FeetPosition)) return SafeReturnKind.IsolatedLanding;
            var support = DetermineSupport(Player.FeetPosition, State.WaterA, State.WaterB, bindings.worldOffset);
            bool stoppedMidway = support == BorrowedRainSupport.BridgeA ? !State.IsALow && !State.IsAHigh && !State.AMoving :
                support == BorrowedRainSupport.LiftB && !State.IsBLow && !State.IsBHigh && !State.BMoving;
            return stoppedMidway ? SafeReturnKind.StoppedPlatform : SafeReturnKind.None;
        }

        private void ReturnToFixedWalkway()
        {
            Transform nearest = null;
            float distance = float.PositiveInfinity;
            if (bindings.safeRecoveryPoints != null)
                foreach (Transform point in bindings.safeRecoveryPoints)
                {
                    if (point == null || !IsPermanentGround(point.position)) continue;
                    float candidate = (point.position - Player.FeetPosition).sqrMagnitude;
                    if (candidate >= distance) continue;
                    nearest = point;
                    distance = candidate;
                }
            if (nearest != null) Player.Teleport(nearest.position + Vector3.up * 0.06f);
            // A previous checkpoint may itself have become isolated after a water change.
            // The configured spawn is the guaranteed fixed western shore, not that stale checkpoint.
            else Player.Teleport(Player.SpawnPosition + Vector3.up * 0.06f);
            ShowMessage("白盒安全返回：已回到固定检修台。苗车、雨牌和两池水位均保留。");
        }

        private void ApplyWaterVisuals()
        {
            SetHeight(bindings.aBridge, bindings.worldOffset.y + 15 + State.WaterA);
            SetHeight(bindings.bLift, bindings.worldOffset.y + 12 + State.WaterB);
            SetHeight(bindings.waterA, bindings.worldOffset.y + 14.78f + State.WaterA);
            SetHeight(bindings.waterB, bindings.worldOffset.y + 11.78f + State.WaterB);
        }

        private static void SetHeight(Transform target, float height)
        {
            if (target == null) return;
            Vector3 position = target.position;
            position.y = height;
            target.position = position;
        }

        private void ApplyEffects()
        {
            UpdateFlowVisuals();
            SetRainLifetimeToSurface(bindings.remoteRain, movingGutterOutlet != null
                ? movingGutterOutlet.position.y : bindings.worldOffset.y + 15.45f + State.WaterA);
            SetRainLifetimeToSurface(bindings.localRain, bindings.station0 != null
                ? bindings.station0.position.y + 0.27f : bindings.worldOffset.y + 15.27f);
            SetRain(bindings.localRain, State.RainActive && State.CardStation == 0);
            SetRain(bindings.remoteRain, State.IsRemoteRain);
            SetActive(bindings.successfulFlow, State.RainToB);
            SetActive(bindings.wasteFlow, State.RainToWaste);
            if (bindings.cardToken != null)
            {
                Transform holder = State.CardStation == 0 ? bindings.station0 : State.CardStation == 1 ? bindings.station1 : Player != null ? Player.transform : null;
                if (holder != null) bindings.cardToken.position = holder.position + Vector3.up * (State.CardStation == BorrowedRainState.Hand ? 1.7f : 0.7f);
            }
        }

        private void UpdateFlowVisuals()
        {
            if (!flowVisualsCached)
            {
                // These are the existing authored beam children; resolve once, including inactive flow groups.
                movingGutterOutlet = bindings.aBridge != null ? bindings.aBridge.Find("Moving_gutter_outlet") : null;
                wasteRainBeam = bindings.wasteFlow != null ? bindings.wasteFlow.Find("Unconnected_rain") : null;
                receivedRainBeam = bindings.successfulFlow != null ? bindings.successfulFlow.Find("B_falling_water") : null;
                if (wasteRainBeam != null)
                    wasteCatchPoint = wasteRainBeam.TransformPoint(new Vector3(0, 0, 0.5f));
                if (receivedRainBeam != null)
                {
                    receivingSpoutPoint = receivedRainBeam.TransformPoint(new Vector3(0, 0, -0.5f));
                    receivingWaterPoint = receivedRainBeam.TransformPoint(new Vector3(0, 0, 0.5f));
                    // The original endpoint sat under the cargo deck. Rain enters the exposed water strip
                    // outside its 1.5 m half-width, while remaining inside the basin's 1.8 m half-width.
                    receivingWaterPoint.z = bindings.worldOffset.z + 1.68f;
                }
                flowVisualsCached = true;
            }
            if (wasteRainBeam != null && movingGutterOutlet != null)
                FitFlowBeam(wasteRainBeam, movingGutterOutlet.position, wasteCatchPoint);
            if (receivedRainBeam != null && bindings.waterB != null)
            {
                Vector3 endpoint = receivingWaterPoint;
                endpoint.y = bindings.waterB.position.y + Mathf.Abs(bindings.waterB.lossyScale.y) * 0.5f + 0.025f;
                FitFlowBeam(receivedRainBeam, receivingSpoutPoint, endpoint);
            }
        }

        private static void FitFlowBeam(Transform beam, Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start;
            if (direction.sqrMagnitude < 0.000001f) return;
            beam.position = (start + end) * 0.5f;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            beam.rotation = Quaternion.LookRotation(direction.normalized, up);
            Vector3 scale = beam.localScale;
            float parentStretch = beam.parent != null
                ? beam.parent.TransformVector(beam.localRotation * Vector3.forward).magnitude : 1;
            scale.z = direction.magnitude / Mathf.Max(0.00001f, parentStretch);
            beam.localScale = scale;
        }

        private static void SetActive(Transform target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active) target.gameObject.SetActive(active);
        }
        private static void SetRain(ParticleSystem rain, bool active)
        {
            if (rain == null) return;
            if (active && !rain.isPlaying) rain.Play();
            if (!active && rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        private static void SetRainLifetimeToSurface(ParticleSystem rain, float targetY)
        {
            if (rain == null) return;
            var main = rain.main;
            // The authored rain is a constant-speed, downward box emitter with no gravity.
            // Its lifetime ends at the visible catch surface instead of continuing through a raised trough.
            float downwardSpeed = Mathf.Abs(rain.transform.forward.y) * main.startSpeed.constant;
            if (downwardSpeed < 0.001f) return;
            main.startLifetime = Mathf.Max(0.01f, (rain.transform.position.y - targetY) / downwardSpeed);
        }
        private static void StopAndClear(ParticleSystem rain)
        {
            if (rain != null) rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private bool IsNear(Transform target, float radius)
        {
            if (target == null || Player == null) return false;
            Vector3 delta = target.position - Player.FeetPosition;
            if (Mathf.Abs(delta.y) >= 1.6f || new Vector2(delta.x, delta.z).sqrMagnitude >= radius * radius) return false;
            Vector3 from = Player.FeetPosition + Vector3.up;
            Vector3 to = target.position + Vector3.up * 0.5f;
            Vector3 ray = to - from;
            int count = Physics.RaycastNonAlloc(from, ray.normalized, sightHits, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = sightHits[i].transform;
                if (hit == Player.transform || hit.IsChildOf(Player.transform) || hit == target || hit.IsChildOf(target)) continue;
                return false;
            }
            return count < sightHits.Length;
        }

        private Interaction GetInteraction()
        {
            Interaction result = Interaction.None;
            float nearest = float.PositiveInfinity;
            Consider(bindings.station0, 2.3f, Interaction.Station0, ref result, ref nearest);
            Consider(bindings.station1, 2.3f, Interaction.Station1, ref result, ref nearest);
            Consider(bindings.valveA, 2.3f, Interaction.ValveA, ref result, ref nearest);
            Consider(bindings.valveB, 2.3f, Interaction.ValveB, ref result, ref nearest);
            Consider(CartTransform, 3.1f, Interaction.Cart, ref result, ref nearest);
            return result;
        }
        private void Consider(Transform target, float radius, Interaction kind, ref Interaction result, ref float nearest)
        {
            if (!IsNear(target, radius)) return;
            float distance = (target.position - Player.FeetPosition).sqrMagnitude;
            if (distance >= nearest) return;
            nearest = distance;
            result = kind;
        }
        private enum Interaction { None, Station0, Station1, ValveA, ValveB, Cart }
        private enum SafeReturnKind { None, StoppedPlatform, IsolatedLanding }
    }
}

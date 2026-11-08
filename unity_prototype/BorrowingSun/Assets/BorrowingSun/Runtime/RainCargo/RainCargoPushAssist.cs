using UnityEngine;

namespace BorrowingSun
{
    /// <summary>A short local walk around the cart, followed by one supported freight segment.</summary>
    internal sealed class RainCargoPushAssist
    {
        private enum Phase { Idle, Approach, Turning, Hands, Pushing, Suspended }
        private readonly RainCargoController court;
        private readonly BoxCollider body;
        private readonly CourtPlayer player;
        private readonly Vector3[] nodes = new Vector3[32], path = new Vector3[32];
        private readonly float[] distances = new float[32];
        private readonly float[] goalSides = new float[32];
        private readonly int[] previous = new int[32];
        private readonly bool[] visited = new bool[32], goals = new bool[32];
        private Phase phase;
        private Vector3 destination, travelDirection, lastPlayerPosition;
        private Quaternion finalRotation;
        private int pathLength, pathIndex;
        private float stalled, pushSideOffset;
        public bool Active => phase != Phase.Idle;
        public bool Waiting { get; private set; }
        public bool Positioning => phase == Phase.Approach || phase == Phase.Turning || phase == Phase.Hands || phase == Phase.Suspended;
        public Vector3 Direction => Active ? travelDirection : Vector3.zero;

        public RainCargoPushAssist(RainCargoController owner, BoxCollider cartBody)
        { court = owner; body = cartBody; player = owner.Player; }

        public bool TryBegin(int direction, out string reason)
        {
            reason = "";
            if (Active) { reason = "正在推车，请稍候。"; return false; }
            if (!court.State.CanMoveCart(direction, out reason)) return false;
            destination = court.Bindings.stops[court.State.CartStop + direction].position;
            travelDirection = Vector3.ProjectOnPlane(destination - body.transform.position, Vector3.up).normalized;
            finalRotation = Quaternion.LookRotation(travelDirection * (direction < 0 ? -1 : 1));
            pushSideOffset = 0;
            if (!PlanWalk(true)) { reason = "车旁的步行通路被栏杆或高差隔开，暂时无法绕到推车端。"; return false; }
            if (!court.State.BeginCartMove(direction)) { reason = "请等水位和货梯停稳。"; return false; }
            phase = Phase.Approach;
            Waiting = false;
            stalled = 0;
            lastPlayerPosition = player.FeetPosition;
            SetWalkTarget();
            return true;
        }

        public void Advance(float dt)
        {
            if (!Active || dt <= 0 || !player.isActiveAndEnabled) return;
            // Recover/Teleport and disabling the actor release its movement override. Reacquire
            // through a fresh supported route instead of pursuing a stale pre-relocation waypoint.
            if (phase != Phase.Suspended && !player.IsAutoPositioning && !player.IsPushingCart)
                phase = Phase.Suspended;
            Waiting = false;
            if (court.IsObserving)
            { player.SetAssistedTarget(player.FeetPosition, travelDirection, phase == Phase.Pushing); return; }
            if (phase == Phase.Suspended)
            {
                if (!PlanWalk(true)) { Waiting = true; return; }
                phase = Phase.Approach;
                stalled = 0;
            }
            if (phase == Phase.Approach || phase == Phase.Hands)
            {
                if (FollowWalk(dt))
                {
                    if (phase == Phase.Approach) phase = Phase.Turning;
                    else phase = Phase.Pushing;
                }
                return;
            }
            if (phase == Phase.Turning)
            {
                player.SetAssistedTarget(player.FeetPosition, travelDirection, false);
                Quaternion next = Quaternion.RotateTowards(body.transform.rotation, finalRotation, 180 * dt);
                if (court.PlayerBlocksCartSweep(body.transform.position, body.transform.rotation, body.transform.position, next, dt))
                { Waiting = true; return; }
                body.transform.rotation = next;
                Physics.SyncTransforms();
                if (Quaternion.Angle(next, finalRotation) < .05f)
                {
                    if (!PlanWalk(false)) { Waiting = true; return; }
                    phase = Phase.Hands;
                    stalled = 0;
                    SetWalkTarget();
                }
                return;
            }

            Vector3 side = Vector3.Cross(Vector3.up, travelDirection) * pushSideOffset;
            Vector3 currentRear = body.transform.position - travelDirection * 2.05f + side;
            if (!player.TryAssistedGround(currentRear, .9f, out currentRear))
            { RepositionAroundObstacle(); return; }
            if (FlatDistance(player.FeetPosition, currentRear) > .22f)
            { player.SetAssistedTarget(currentRear, body.transform.position-player.FeetPosition, true); return; }
            Vector3 nextPosition = Vector3.MoveTowards(body.transform.position, destination, 2.1f * dt);
            Vector3 nextRear = nextPosition - travelDirection * 2.05f + side;
            if (!player.TryAssistedGround(nextRear, .9f, out nextRear)
                || !player.CanWalkAssistedSegment(player.FeetPosition, nextRear)
                || court.PlayerBlocksCartSweep(body.transform.position, body.transform.rotation, nextPosition, finalRotation, dt))
            { RepositionAroundObstacle(); return; }
            body.transform.SetPositionAndRotation(nextPosition, finalRotation);
            Physics.SyncTransforms();
            player.SetAssistedTarget(nextRear, body.transform.position-player.FeetPosition, true);
            if ((nextPosition - destination).sqrMagnitude < .000001f && FlatDistance(player.FeetPosition, nextRear) < .09f)
            {
                court.State.CompleteCartMove();
                Cancel();
                court.ShowMessage("已到：" + court.CurrentStopName);
            }
        }

        private void RepositionAroundObstacle()
        {
            // A ramp-side rail can cross the straight trailing line. Stop the vehicle and walk
            // around its open end to a side handle; never relax collision or push through the rail.
            if (PlanWalk(false, true))
            {
                phase = Phase.Hands;
                stalled = 0;
                Waiting = false;
                SetWalkTarget();
                court.ShowMessage("自动绕开障碍，换到另一侧推行。", 3);
            }
            else
            {
                Waiting = true;
                player.SetAssistedTarget(player.FeetPosition, body.transform.position-player.FeetPosition, true);
            }
        }

        private bool FollowWalk(float dt)
        {
            while (pathIndex < pathLength && FlatDistance(player.FeetPosition, path[pathIndex]) < .09f) pathIndex++;
            if (pathIndex >= pathLength) return true;
            SetWalkTarget();
            if (FlatDistance(player.FeetPosition, lastPlayerPosition) < .001f) stalled += dt;
            else stalled = 0;
            lastPlayerPosition = player.FeetPosition;
            Waiting = stalled > .8f;
            return false;
        }

        private void SetWalkTarget()
        {
            if (pathIndex >= pathLength) return;
            Vector3 facing = path[pathIndex] - player.FeetPosition;
            player.SetAssistedTarget(path[pathIndex], facing, false);
        }

        private bool PlanWalk(bool turningClearance, bool alternateHandle = false)
        {
            int count = 1;
            nodes[0] = player.FeetPosition;
            goals[0] = false;
            Vector3 side = Vector3.Cross(Vector3.up, travelDirection);
            float back = turningClearance ? 2.4f : 2.05f;
            for (int i = 0; i < (turningClearance || alternateHandle ? 5 : 1); i++)
            {
                float lateral = turningClearance || alternateHandle
                    ? (i == 0 ? 0 : (i % 2 == 1 ? 1 : -1) * (i <= 2 ? .8f : 1.3f)) : pushSideOffset;
                if (alternateHandle && Mathf.Abs(lateral-pushSideOffset)<.05f) continue;
                Vector3 goal = body.transform.position - travelDirection * back + side * lateral;
                if (!player.TryAssistedGround(goal, 1.2f, out goal)) continue;
                nodes[count] = goal; goalSides[count] = lateral; goals[count++] = true;
            }
            if (count == 1) return false;
            int goalCount = count;
            Vector3 half = Vector3.Scale(body.size * .5f, body.transform.lossyScale);
            Vector3 center = body.transform.TransformPoint(body.center);
            for (int ring = 0; ring < 2; ring++)
                for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue;
                    float margin = ring == 0 ? .55f : 1.1f;
                    Vector3 n = center + body.transform.rotation * new Vector3(x * (half.x + margin), 0, z * (half.z + margin));
                    n.y = body.transform.position.y;
                    if (!player.TryAssistedGround(n, 1.2f, out n)) continue;
                    nodes[count] = n; goals[count++] = false;
                }
            for (int i = 0; i < count; i++) { distances[i] = float.PositiveInfinity; previous[i] = -1; visited[i] = false; }
            distances[0] = 0;
            int result = -1;
            for (int iteration = 0; iteration < count; iteration++)
            {
                int current = -1;
                for (int i = 0; i < count; i++) if (!visited[i] && (current < 0 || distances[i] < distances[current])) current = i;
                if (current < 0 || float.IsPositiveInfinity(distances[current])) break;
                if (current > 0 && current < goalCount) { result = current; break; }
                visited[current] = true;
                for (int next = 1; next < count; next++)
                {
                    if (visited[next]) continue;
                    float distance = distances[current] + Vector3.Distance(nodes[current], nodes[next]);
                    if (distance >= distances[next] || !player.CanWalkAssistedSegment(nodes[current], nodes[next])) continue;
                    distances[next] = distance; previous[next] = current;
                }
            }
            if (result < 0) return false;
            if (!turningClearance) pushSideOffset = goalSides[result];
            pathLength = 0;
            for (int at = result; at > 0; at = previous[at]) path[pathLength++] = nodes[at];
            for (int i = 0; i < pathLength / 2; i++)
            { Vector3 temp = path[i]; path[i] = path[pathLength - 1 - i]; path[pathLength - 1 - i] = temp; }
            pathIndex = 0;
            return true;
        }

        public void Suspend() { if (Active) phase = Phase.Suspended; player.ReleaseAssistedMovement(); }
        public void Cancel() { phase = Phase.Idle; Waiting = false; player.ReleaseAssistedMovement(); }
        private static float FlatDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
    }
}

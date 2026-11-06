using System;
using System.Numerics;

namespace BorrowingSun2D.Core;

internal enum LightHit2D { None, Obstacle, Collector, TowerMirror, Receiver }

internal readonly record struct OpticalSurface2D(Vector2 Center, Vector2 Normal, float HalfLength);
internal readonly record struct LightSegment2D(Vector2 From, Vector2 To, LightHit2D Hit);

internal readonly record struct LightPath2D(LightSegment2D? Incoming, LightSegment2D? FirstReflection,
    LightSegment2D? SecondReflection)
{
    internal bool TowerHit => FirstReflection?.Hit == LightHit2D.TowerMirror;
    internal bool ReceiverLit => SecondReflection?.Hit == LightHit2D.Receiver;
}

internal static class Optics2D
{
    private const float Epsilon = 0.0001f;

    internal static LightPath2D Trace(Vector2 source, Vector2 incomingDirection, OpticalSurface2D collector,
        OpticalSurface2D towerMirror, OpticalSurface2D receiver, Func<Vector2, Vector2, Vector2?> firstObstacle,
        float maxDistance = 1500f)
    {
        if (incomingDirection.LengthSquared() <= Epsilon * Epsilon || maxDistance <= 0f) return default;

        Vector2 incoming = Vector2.Normalize(incomingDirection);
        LightSegment2D sunlight = Cast(source, incoming, LightHit2D.None);
        if (sunlight.Hit != LightHit2D.Collector || !Faces(incoming, collector))
            return new LightPath2D(sunlight, null, null);

        Vector2 towardTower = Vector2.Reflect(incoming, Vector2.Normalize(collector.Normal));
        LightSegment2D first = Cast(sunlight.To, towardTower, LightHit2D.Collector);
        if (first.Hit != LightHit2D.TowerMirror || !Faces(towardTower, towerMirror))
            return new LightPath2D(sunlight, first, null);

        Vector2 towardReceiver = Vector2.Reflect(towardTower, Vector2.Normalize(towerMirror.Normal));
        LightSegment2D second = Cast(first.To, towardReceiver, LightHit2D.TowerMirror);
        if (second.Hit == LightHit2D.Receiver && !Faces(towardReceiver, receiver))
            second = second with { Hit = LightHit2D.Obstacle };
        return new LightPath2D(sunlight, first, second);

        LightSegment2D Cast(Vector2 origin, Vector2 direction, LightHit2D previous)
        {
            float distance = maxDistance;
            LightHit2D hit = LightHit2D.None;
            Check(collector, LightHit2D.Collector);
            Check(towerMirror, LightHit2D.TowerMirror);
            Check(receiver, LightHit2D.Receiver);

            // Ask the engine only about the space up to the nearest optical surface.
            Vector2 end = origin + direction * distance;
            Vector2? blocked = firstObstacle(origin, end);
            return blocked.HasValue ? new LightSegment2D(origin, blocked.Value, LightHit2D.Obstacle)
                : new LightSegment2D(origin, end, hit);

            void Check(OpticalSurface2D surface, LightHit2D candidate)
            {
                if (candidate == previous || surface.HalfLength <= 0f
                    || surface.Normal.LengthSquared() <= Epsilon * Epsilon) return;
                Vector2 normal = Vector2.Normalize(surface.Normal);
                float incidence = Vector2.Dot(direction, normal);
                if (MathF.Abs(incidence) <= Epsilon) return;

                float alongRay = Vector2.Dot(surface.Center - origin, normal) / incidence;
                if (alongRay <= Epsilon || alongRay >= distance) return;
                Vector2 offset = origin + direction * alongRay - surface.Center;
                if (offset.LengthSquared() > surface.HalfLength * surface.HalfLength) return;

                distance = alongRay;
                hit = candidate;
            }
        }
    }

    private static bool Faces(Vector2 direction, OpticalSurface2D surface) => Vector2.Dot(direction, surface.Normal) < 0f;
}

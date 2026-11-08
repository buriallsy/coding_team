using System;
using System.Numerics;

namespace BorrowingSun3D;

internal enum LightHit3D { None, Obstacle, Collector, TowerMirror, Receiver }

internal readonly record struct OpticalSurface3D(Vector3 Center, Vector3 Normal, float Radius);
internal readonly record struct LightSegment3D(Vector3 From, Vector3 To, LightHit3D Hit);

internal readonly record struct LightPath3D(LightSegment3D? Incoming, LightSegment3D? FirstReflection,
    LightSegment3D? SecondReflection)
{
    internal bool TowerHit => FirstReflection?.Hit == LightHit3D.TowerMirror;
    internal bool ReceiverLit => SecondReflection?.Hit == LightHit3D.Receiver;
}

internal static class Optics3D
{
    private const float Epsilon = 0.0001f;

    internal static LightPath3D Trace(Vector3 source, Vector3 direction, OpticalSurface3D collector,
        OpticalSurface3D relay, OpticalSurface3D receiver, Func<Vector3, Vector3, Vector3?> firstObstacle,
        float maxDistance)
    {
        if (!CanNormalize(direction) || !float.IsFinite(maxDistance) || maxDistance <= 0f) return default;

        Vector3 incoming = Vector3.Normalize(direction);
        LightSegment3D sunlight = Cast(source, incoming, LightHit3D.None);
        if (sunlight.Hit != LightHit3D.Collector || !Faces(incoming, collector))
            return new LightPath3D(sunlight, null, null);

        Vector3 towardRelay = Vector3.Reflect(incoming, Vector3.Normalize(collector.Normal));
        LightSegment3D first = Cast(sunlight.To, towardRelay, LightHit3D.Collector);
        if (first.Hit != LightHit3D.TowerMirror || !Faces(towardRelay, relay))
            return new LightPath3D(sunlight, first, null);

        Vector3 towardReceiver = Vector3.Reflect(towardRelay, Vector3.Normalize(relay.Normal));
        LightSegment3D second = Cast(first.To, towardReceiver, LightHit3D.TowerMirror);
        if (second.Hit == LightHit3D.Receiver && !Faces(towardReceiver, receiver))
            second = second with { Hit = LightHit3D.Obstacle };
        return new LightPath3D(sunlight, first, second);

        LightSegment3D Cast(Vector3 origin, Vector3 ray, LightHit3D previous)
        {
            float distance = maxDistance;
            LightHit3D hit = LightHit3D.None;
            Check(collector, LightHit3D.Collector);
            Check(relay, LightHit3D.TowerMirror);
            Check(receiver, LightHit3D.Receiver);

            // Native physics only needs the finite segment up to the nearest aperture.
            Vector3 end = origin + ray * distance;
            Vector3? obstruction = firstObstacle(origin, end);
            return obstruction.HasValue
                ? new LightSegment3D(origin, obstruction.Value, LightHit3D.Obstacle)
                : new LightSegment3D(origin, end, hit);

            void Check(OpticalSurface3D surface, LightHit3D candidate)
            {
                if (candidate == previous || !float.IsFinite(surface.Radius) || surface.Radius <= 0f
                    || !CanNormalize(surface.Normal)) return;

                Vector3 normal = Vector3.Normalize(surface.Normal);
                float incidence = Vector3.Dot(ray, normal);
                if (MathF.Abs(incidence) <= Epsilon) return;

                float alongRay = Vector3.Dot(surface.Center - origin, normal) / incidence;
                if (!(alongRay > Epsilon && alongRay < distance)) return;

                Vector3 radialOffset = origin + ray * alongRay - surface.Center;
                if (!(radialOffset.LengthSquared() <= surface.Radius * surface.Radius)) return;

                distance = alongRay;
                hit = candidate;
            }
        }
    }

    private static bool Faces(Vector3 direction, OpticalSurface3D surface) =>
        Vector3.Dot(direction, surface.Normal) < 0f;

    private static bool CanNormalize(Vector3 vector)
    {
        float magnitudeSquared = vector.LengthSquared();
        return float.IsFinite(magnitudeSquared) && magnitudeSquared > Epsilon * Epsilon;
    }
}

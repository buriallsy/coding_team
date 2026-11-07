using System;
using System.Collections.Generic;
using System.Numerics;

namespace BorrowingSun2D.Core;

public enum SurfaceKind { SunMirror, TowerMirror, Receiver, Blocker }
public readonly record struct OpticalSurface(Vector2 A, Vector2 B, Vector2 FrontNormal, SurfaceKind Kind);
public readonly record struct LightSegment(Vector2 From, Vector2 To);
public sealed record LightPath(IReadOnlyList<LightSegment> Segments, bool ReceiverHit);

// Gameplay-critical finite-segment ray solver. Intentionally included in the future CCR core list.
public sealed class Optics2D
{
    private const float Epsilon = 0.0001f;

    public LightPath Trace(Vector2 origin, Vector2 direction, IReadOnlyList<OpticalSurface> surfaces,
        bool sunEnabled, float maxDistance = 100f)
    {
        if (!Finite(origin) || !Finite(direction) || direction.LengthSquared() < Epsilon * Epsilon
            || !float.IsFinite(maxDistance) || maxDistance <= 0f) throw new ArgumentException("Invalid ray.");
        foreach (var surface in surfaces)
        {
            if (!Finite(surface.A) || !Finite(surface.B) || !Finite(surface.FrontNormal)
                || Vector2.DistanceSquared(surface.A, surface.B) < Epsilon * Epsilon
                || surface.FrontNormal.LengthSquared() < Epsilon * Epsilon)
                throw new ArgumentException("Invalid optical surface.");
            if (!Enum.IsDefined(surface.Kind)) throw new ArgumentException("Unknown surface kind.");
            var tangent = Vector2.Normalize(surface.B - surface.A);
            if (Math.Abs(Vector2.Dot(tangent, Vector2.Normalize(surface.FrontNormal))) > 0.001f)
                throw new ArgumentException("Normal must be perpendicular to the finite surface.");
        }
        var segments = new List<LightSegment>();
        LightPath Result(bool hit) => new(segments.AsReadOnly(), hit);
        if (!sunEnabled) return Result(false);
        direction = Vector2.Normalize(direction);
        float remaining = maxDistance;
        for (int stage = 0; stage < 3; stage++)
        {
            int nearest = -1;
            float distance = remaining;
            for (int i = 0; i < surfaces.Count; i++)
            {
                var surface = surfaces[i];
                Vector2 edge = surface.B - surface.A;
                float denominator = Cross(direction, edge);
                if (Math.Abs(denominator) < Epsilon) continue;
                Vector2 offset = surface.A - origin;
                float t = Cross(offset, edge) / denominator;
                float u = Cross(offset, direction) / denominator;
                if (t <= Epsilon || t > distance + Epsilon || u < -Epsilon || u > 1f + Epsilon) continue;
                // An occluder wins a coincident hit, independent of collection order.
                if (nearest >= 0 && Math.Abs(t - distance) <= Epsilon
                    && surfaces[nearest].Kind == SurfaceKind.Blocker) continue;
                if (nearest >= 0 && t >= distance && surface.Kind != SurfaceKind.Blocker) continue;
                distance = t; nearest = i;
            }
            Vector2 end = origin + direction * distance;
            segments.Add(new(origin, end));
            if (nearest < 0) return Result(false);
            var hit = surfaces[nearest];
            var expected = stage == 0 ? SurfaceKind.SunMirror : stage == 1 ? SurfaceKind.TowerMirror : SurfaceKind.Receiver;
            if (hit.Kind != expected) return Result(false);
            Vector2 normal = Vector2.Normalize(hit.FrontNormal);
            if (Vector2.Dot(direction, normal) >= -Epsilon) return Result(false);
            if (stage == 2) return Result(true);
            direction = Vector2.Reflect(direction, normal);
            remaining -= distance;
            if (remaining <= Epsilon) return Result(false);
            origin = end;
        }
        return Result(false);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
}

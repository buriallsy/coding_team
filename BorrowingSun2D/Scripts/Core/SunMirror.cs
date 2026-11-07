namespace BorrowingSun2D.Core;

// Local weather remains active after leaving the station. No Godot coordinates here.
public sealed class SunMirror
{
    public int AngleIndex { get; private set; }
    public float NormalDegrees => 90f + AngleIndex * 45f;
    public Weather ActiveWeather { get; private set; }
    public bool SunEnabled => ActiveWeather == Weather.Sun;

    public MechanismResult Cast(Weather weather)
    {
        if (weather != Weather.Wind && weather != Weather.Sun)
            return MechanismResult.Unsupported;
        ActiveWeather = weather;
        if (weather == Weather.Wind) AngleIndex = (AngleIndex + 1) % 3;
        return MechanismResult.Applied;
    }
    public void Recall() => ActiveWeather = Weather.None;
    public void Reset() { AngleIndex = 0; ActiveWeather = Weather.None; }
}

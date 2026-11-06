namespace BorrowingSun2D.Core;

internal class RouteProgress
{
    internal bool ReachedSunCourt { get; private set; }
    internal bool Completed { get; private set; }

    internal bool Visit(Port port)
    {
        switch (port)
        {
            case Port.SunCourt when !ReachedSunCourt:
                ReachedSunCourt = true;
                return true;
            case Port.East when ReachedSunCourt && !Completed && MeetsEastFinishCondition():
                Completed = true;
                return true;
            default:
                return false;
        }
    }

    // A level can add an east-shore condition while Visit owns the route transitions.
    protected virtual bool MeetsEastFinishCondition() => true;

    internal void Reset()
    {
        ReachedSunCourt = false;
        Completed = false;
    }
}

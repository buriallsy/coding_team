using UnityEngine;

namespace BorrowingSun
{
    /// <summary>The existing walking controller can operate either courtyard without owning its rules.</summary>
    public interface ICourtGameplay
    {
        bool IsObserving { get; }
        bool IsInsideHazard(Vector3 feet);
        bool IsPermanentGround(Vector3 feet);
        void ResetCourt();
        void InteractPrimary();
        void InteractSecondary();
        void ShowMessage(string text, float seconds = 4f);
    }
}

using UnityEngine;

namespace BorrowingSun
{
    /// <summary>The court dispatches interaction; the selected market owns its rules and presentation.</summary>
    public interface ISkywellMarketInteraction
    {
        bool InputBlocked { get; }
        bool AllowsSunlight { get; }
        bool AllowsWeather(int station, SkywellToken token, bool delivered);
        bool TryInteract(CourtPlayer actor);
        string Prompt(Vector3 feet);
        void Tick(float seconds);
        void AfterWorldTick(float seconds);
        void PrimaryCommand();
        void CloseShop();
        void ResetMarket();
    }
}

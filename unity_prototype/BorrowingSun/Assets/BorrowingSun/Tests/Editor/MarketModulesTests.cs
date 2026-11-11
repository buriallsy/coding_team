using NUnit.Framework;

namespace BorrowingSun.Tests
{
    public sealed class MarketModulesTests
    {
        [Test] public void StartAndRepeatedCommandsPreserveSession() => MarketBehaviorScenarios.StartAndRepeatedCommandsPreserveSession();
        [Test] public void OrdersAppearOnlyAfterMenuAndReserveWithoutOverbooking() => MarketBehaviorScenarios.OrdersAppearOnlyAfterMenuAndReserveWithoutOverbooking();
        [Test] public void TwoBurnersAdvanceTogetherAndOnlyPaidWindowRefills() => MarketBehaviorScenarios.TwoBurnersAdvanceTogetherAndOnlyPaidWindowRefills();
        [Test] public void ClosingReturnsCookingStockAndReleasesWaitingReservations() => MarketBehaviorScenarios.ClosingReturnsCookingStockAndReleasesWaitingReservations();
        [Test] public void ClosingReturnsReadyMealsWithoutPaying() => MarketBehaviorScenarios.ClosingReturnsReadyMealsWithoutPaying();
        [Test] public void BusinessTimeoutStopsAtBoundaryAndRefunds() => MarketBehaviorScenarios.BusinessTimeoutStopsAtBoundaryAndRefunds();
        [Test] public void DirectEveningUsesExplicitReturnAndFullBusinessTime() => MarketBehaviorScenarios.DirectEveningUsesExplicitReturnAndFullBusinessTime();
        [Test] public void NightForageUsesHalfDayAndHalfBusinessTime() => MarketBehaviorScenarios.NightForageUsesHalfDayAndHalfBusinessTime();
        [Test] public void EarlyNightReturnDoesNotRestoreFullBusinessTime() => MarketBehaviorScenarios.EarlyNightReturnDoesNotRestoreFullBusinessTime();
        [Test] public void EmptyInventoryCanRestAndContinueWithoutDeadline() => MarketBehaviorScenarios.EmptyInventoryCanRestAndContinueWithoutDeadline();
        [Test] public void EarningsBuyOnePermanentUpgradeWithoutReducingGrossRevenue() => MarketBehaviorScenarios.EarningsBuyOnePermanentUpgradeWithoutReducingGrossRevenue();
        [Test] public void ResetClearsMarketAndAllowsOneFreshStart() => MarketBehaviorScenarios.ResetClearsMarketAndAllowsOneFreshStart();
        [Test] public void InvalidInputsLeaveTimeStockAndOrdersUnchanged() => MarketBehaviorScenarios.InvalidInputsLeaveTimeStockAndOrdersUnchanged();
        [Test] public void InvalidMenusAreRejectedAtomically() => MarketBehaviorScenarios.InvalidMenusAreRejectedAtomically();
        [Test] public void MixedMenuOnlyCreatesOrdersThatStockCanFulfil() => MarketBehaviorScenarios.MixedMenuOnlyCreatesOrdersThatStockCanFulfil();
        [Test] public void EnvironmentChangesMatureFruitButNotHarvestedInventory() => MarketBehaviorScenarios.EnvironmentChangesMatureFruitButNotHarvestedInventory();
        [Test] public void SunnyFruitCanBeCookedAndPaidBeforeNextDay() => MarketBehaviorScenarios.SunnyFruitCanBeCookedAndPaidBeforeNextDay();
        [Test] public void ForageCooldownPreventsDuplicateHarvests() => MarketBehaviorScenarios.ForageCooldownPreventsDuplicateHarvests();
        [Test] public void HarvestRejectedOutsideExplorationDoesNotConsumePoint() => MarketBehaviorScenarios.HarvestRejectedOutsideExplorationDoesNotConsumePoint();
        [Test] public void InvalidConfigurationIsRejected() => MarketBehaviorScenarios.InvalidConfigurationIsRejected();
    }
}

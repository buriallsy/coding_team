using NUnit.Framework;

namespace BorrowingSun.Tests
{
    public sealed class BorrowedRainStateTests
    {
        private static void Move(BorrowedRainState state, int direction = 1)
        {
            Assert.That(state.BeginCartMove(direction), Is.True);
            Assert.That(state.CompleteCartMove(), Is.True);
        }

        private static void FillA(BorrowedRainState state)
        {
            Assert.That(state.InsertCard(0), Is.True);
            Assert.That(state.ToggleRain(0), Is.True);
            state.Advance(10);
            Assert.That(state.RemoveCard(0), Is.True);
            Assert.That(state.IsAHigh, Is.True);
        }

        private static void FillBoth(BorrowedRainState state)
        {
            FillA(state);
            Assert.That(state.InsertCard(1), Is.True);
            Assert.That(state.ToggleRain(1), Is.True);
            state.Advance(10);
            Assert.That(state.IsBHigh, Is.True);
        }

        [Test]
        public void LowGutterSendsRemoteRainToWasteWithoutFillingEitherBasin()
        {
            var state = new BorrowedRainState();
            state.InsertCard(1);
            state.ToggleRain(1);
            state.Advance(1000);
            Assert.That(state.RainToWaste, Is.True);
            Assert.That(state.RainToB, Is.False);
            Assert.That(state.WaterA, Is.Zero);
            Assert.That(state.WaterB, Is.Zero);
            Assert.That(state.CardStation, Is.EqualTo(1));
        }

        [Test]
        public void OnlyOneCardCanBeInsertedAndItCannotBeRecalledFromOtherStation()
        {
            var state = new BorrowedRainState();
            Assert.That(state.InsertCard(0), Is.True);
            Assert.That(state.InsertCard(1), Is.False);
            Assert.That(state.RemoveCard(1), Is.False);
            Assert.That(state.ToggleRain(1), Is.False);
            Assert.That(state.CardStation, Is.Zero);
            Assert.That(state.RemoveCard(0), Is.True);
            Assert.That(state.InsertCard(1), Is.True);
        }

        [Test]
        public void RainCardTransferPreservesAAndDoesNotItselfFillB()
        {
            var state = new BorrowedRainState();
            FillA(state);
            state.Advance(100);
            state.InsertCard(1);
            state.Advance(100);
            Assert.That(state.IsAHigh, Is.True);
            Assert.That(state.WaterB, Is.Zero);
            Assert.That(state.RainActive, Is.False);
            state.ToggleRain(1);
            state.Advance(1);
            Assert.That(state.WaterB, Is.GreaterThan(0));
            Assert.That(state.IsAHigh, Is.True);
        }

        [Test]
        public void StoppingRemoteRainPreservesPartialBWaterAndCanResume()
        {
            var state = new BorrowedRainState();
            FillA(state);
            state.InsertCard(1);
            state.ToggleRain(1);
            state.Advance(2);
            float stored = state.WaterB;
            state.RemoveCard(1);
            state.Advance(100);
            Assert.That(state.WaterB, Is.EqualTo(stored));
            Assert.That(state.WaterInMotion, Is.False);
            state.InsertCard(1);
            state.ToggleRain(1);
            state.Advance(10);
            Assert.That(state.IsBHigh, Is.True);
        }

        [Test]
        public void MatureRouteNeedsALowBHighAndReclaimedCard()
        {
            var state = new BorrowedRainState();
            Move(state);
            Move(state);
            Assert.That(state.CanMoveCart(1, out _), Is.False);
            FillBoth(state);
            // Vehicle can continue from the saved central fixed node once B reaches its berth.
            Move(state);
            Move(state);
            Move(state);
            Assert.That(state.Delivered, Is.True);
            Assert.That(state.Completed, Is.False);
            Assert.That(state.RemoveCard(1), Is.True);
            Assert.That(state.Completed, Is.False, "Parking ahead is valid, but delivery alone does not restore the disconnected western route.");
            state.ToggleDrain(0);
            state.Advance(10);
            Assert.That(state.Completed, Is.True);
            // For a vehicle starting from the west, A must also be returned to its low stop.
            state.Reset();
            FillBoth(state);
            Assert.That(state.CanMoveCart(1, out _), Is.False);
            state.ToggleDrain(0);
            state.Advance(10);
            Assert.That(state.IsALow, Is.True);
            Assert.That(state.IsBHigh, Is.True);
            for (int i = 0; i < 5; i++) Move(state);
            state.RemoveCard(1);
            Assert.That(state.Completed, Is.True);
        }

        [Test]
        public void LoweringAWhileRemoteRainRunsDisconnectsFeedButPreservesB()
        {
            var state = new BorrowedRainState();
            FillBoth(state);
            state.ToggleDrain(0);
            Assert.That(state.IsGutterAligned, Is.False, "Moving away from the sealed upper stop disconnects its duct.");
            Assert.That(state.RainToWaste, Is.True);
            state.Advance(10);
            Assert.That(state.WaterA, Is.Zero);
            Assert.That(state.IsBHigh, Is.True);
            Assert.That(state.IsRemoteRain, Is.True);
        }

        [Test]
        public void DrainingBDoesNotEraseAAndCanBeRepairedWithExistingRainCard()
        {
            var state = new BorrowedRainState();
            FillBoth(state);
            state.ToggleDrain(1);
            state.Advance(10);
            Assert.That(state.IsBLow, Is.True);
            Assert.That(state.IsAHigh, Is.True);
            Assert.That(state.RainActive, Is.False);
            state.ToggleRain(1);
            state.Advance(10);
            Assert.That(state.IsBHigh, Is.True);
            Assert.That(state.DrainBOpen, Is.False);
        }

        [Test]
        public void MidHeightCannotBeCrossedEvenWhenWaterHasStopped()
        {
            var state = new BorrowedRainState();
            Move(state);
            state.InsertCard(0);
            state.ToggleRain(0);
            Assert.That(state.CanMoveCart(1, out _), Is.False, "A begins moving immediately, even before the first tick.");
            state.Advance(1);
            state.RemoveCard(0);
            Assert.That(state.WaterInMotion, Is.False);
            Assert.That(state.CanMoveCart(-1, out _), Is.False);
            Assert.That(state.CanMoveCart(1, out _), Is.False);
            state.ToggleDrain(0);
            state.Advance(10);
            Move(state, -1);
            Assert.That(state.CartStop, Is.Zero);
        }

        [Test]
        public void CartParkedOnBCanDescendAndBeRestoredWithoutReset()
        {
            var state = new BorrowedRainState();
            Move(state);
            Move(state);
            FillBoth(state);
            Move(state);
            state.ToggleDrain(1);
            state.Advance(10);
            Assert.That(state.CartStop, Is.EqualTo(3));
            Assert.That(state.CanMoveCart(1, out _), Is.False);
            Assert.That(state.CanMoveCart(-1, out _), Is.False);
            state.ToggleRain(1);
            state.Advance(10);
            Move(state, -1);
            Assert.That(state.CartStop, Is.EqualTo(2));
        }

        [Test]
        public void CrossingLocksWaterCardAndSecondMoveUntilAnimationCompletes()
        {
            var state = new BorrowedRainState();
            state.InsertCard(0);
            state.BeginCartMove(1);
            Assert.That(state.ToggleRain(0), Is.False);
            Assert.That(state.ToggleDrain(0), Is.False);
            Assert.That(state.ToggleDrain(1), Is.False);
            Assert.That(state.RemoveCard(0), Is.False);
            Assert.That(state.BeginCartMove(1), Is.False);
            state.Advance(100);
            Assert.That(state.WaterA, Is.Zero);
            Assert.That(state.CartStop, Is.Zero);
            Assert.That(state.PendingCartStop, Is.EqualTo(1));
            state.CompleteCartMove();
            Assert.That(state.ToggleRain(0), Is.True);
        }

        [Test]
        public void ReversedFinalRouteRemainsConnectedAndResetCancelsTransit()
        {
            var state = new BorrowedRainState();
            FillBoth(state);
            state.RemoveCard(1);
            state.ToggleDrain(0);
            state.Advance(10);
            for (int i = 0; i < 5; i++) Move(state);
            for (int i = 0; i < 5; i++) Move(state, -1);
            Assert.That(state.CartStop, Is.Zero);
            state.BeginCartMove(1);
            state.Reset();
            Assert.That(state.CartInMotion, Is.False);
            Assert.That(state.WaterA, Is.Zero);
            Assert.That(state.WaterB, Is.Zero);
            Assert.That(state.CardStation, Is.EqualTo(BorrowedRainState.Hand));
            Assert.That(state.RainActive || state.DrainAOpen || state.DrainBOpen, Is.False);
        }

        [Test]
        public void InvalidInputDoesNotCorruptWaterOrCardOwnership()
        {
            var state = new BorrowedRainState();
            Assert.That(state.InsertCard(-1), Is.False);
            Assert.That(state.InsertCard(2), Is.False);
            Assert.That(state.ToggleDrain(2), Is.False);
            Assert.That(state.BeginCartMove(0), Is.False);
            state.InsertCard(0);
            state.ToggleRain(0);
            state.Advance(float.NaN);
            state.Advance(float.PositiveInfinity);
            state.Advance(-1);
            Assert.That(state.WaterA, Is.Zero);
            Assert.That(state.CardStation, Is.Zero);
        }
    }
}

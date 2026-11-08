using NUnit.Framework;

namespace BorrowingSun.Tests
{
    public sealed class RainCargoStateTests
    {
        private static void Move(RainCargoState state, int direction = 1)
        {
            Assert.That(state.BeginCartMove(direction), Is.True);
            Assert.That(state.CompleteCartMove(), Is.True);
        }

        private static void FillA(RainCargoState state)
        {
            Assert.That(state.InsertCard(0), Is.True);
            Assert.That(state.ToggleRain(0), Is.True);
            state.Advance(10);
            Assert.That(state.RemoveCard(0), Is.True);
        }

        private static void FillTankOnA(RainCargoState state)
        {
            if (state.CartStop == 0) Move(state);
            Assert.That(state.CartStop, Is.EqualTo(1));
            FillA(state);
            Assert.That(state.InsertCard(1), Is.True);
            Assert.That(state.ToggleRain(1), Is.True);
            state.Advance(10);
            Assert.That(state.RemoveCard(1), Is.True);
            Assert.That(state.TankFull, Is.True);
        }

        private static void BringFullTankToB(RainCargoState state)
        {
            FillTankOnA(state);
            Assert.That(state.ToggleDrain(0), Is.True);
            state.Advance(10);
            for (int stop = 2; stop <= 5; stop++) Move(state);
        }

        [Test]
        public void FullRouteCarriesStoredRainAwayFromItsSourceAndSpendsItOnTheLift()
        {
            var state = new RainCargoState();
            FillTankOnA(state);
            Assert.That(state.CanMoveCart(1, out _), Is.False, "The upper filling dock does not connect to the lower cargo exit.");
            state.ToggleDrain(0);
            state.Advance(10);
            Assert.That(state.IsALow, Is.True);
            Assert.That(state.TankWater, Is.EqualTo(3));
            Assert.That(state.WaterB, Is.Zero);
            for (int stop = 2; stop <= 5; stop++) Move(state);
            Assert.That(state.CanMoveCart(1, out _), Is.False);
            Assert.That(state.TransferWater(), Is.True);
            state.Advance(10);
            Assert.That(state.TankWater, Is.Zero);
            Assert.That(state.IsBHigh, Is.True);
            Move(state);
            Move(state);
            Assert.That(state.Completed, Is.True);
        }

        [Test]
        public void RainMissesTheTankWhenTheDockIsLowOrTheCartIsAbsent()
        {
            var state = new RainCargoState();
            Move(state);
            state.InsertCard(1);
            state.ToggleRain(1);
            state.Advance(1000);
            Assert.That(state.RainToWaste, Is.True);
            Assert.That(state.TankWater, Is.Zero);
            state.RemoveCard(1);
            Move(state, -1);
            FillA(state);
            state.InsertCard(1);
            state.ToggleRain(1);
            state.Advance(1000);
            Assert.That(state.RainToWaste, Is.True);
            Assert.That(state.TankWater, Is.Zero);
            Assert.That(state.WaterB, Is.Zero);
        }

        [Test]
        public void PartialTankSurvivesRemovingTheCardAndCanResumeFilling()
        {
            var state = new RainCargoState();
            Move(state);
            FillA(state);
            state.InsertCard(1);
            state.ToggleRain(1);
            state.Advance(1);
            float stored = state.TankWater;
            Assert.That(stored, Is.InRange(0.1f, 2.9f));
            state.RemoveCard(1);
            state.Advance(100);
            Assert.That(state.TankWater, Is.EqualTo(stored));
            Assert.That(state.WaterInMotion, Is.False);
            state.InsertCard(1);
            state.ToggleRain(1);
            state.Advance(10);
            Assert.That(state.TankFull, Is.True);
        }

        [Test]
        public void LoweringTheFillingDockCutsTheRainConnectionWithoutErasingStoredWater()
        {
            var state = new RainCargoState();
            Move(state);
            FillA(state);
            state.InsertCard(1);
            state.ToggleRain(1);
            state.Advance(1);
            float stored = state.TankWater;
            state.ToggleDrain(0);
            Assert.That(state.RainToWaste, Is.True, "A departing upper dock loses its rain feed before the next simulation tick.");
            state.Advance(100);
            Assert.That(state.IsALow, Is.True);
            Assert.That(state.TankWater, Is.EqualTo(stored));
            Assert.That(state.WaterB, Is.Zero);
            Move(state);
            Assert.That(state.TankWater, Is.EqualTo(stored));
        }

        [Test]
        public void TransferConservesWaterAtEveryStepAndLocksConflictingOperations()
        {
            var state = new RainCargoState();
            BringFullTankToB(state);
            Assert.That(state.TransferWater(), Is.True);
            Assert.That(state.Transferring, Is.True);
            Assert.That(state.BeginCartMove(1), Is.False);
            Assert.That(state.InsertCard(0), Is.False);
            Assert.That(state.ToggleDrain(0), Is.False);
            Assert.That(state.ToggleDrain(1), Is.False);
            Assert.That(state.TransferWater(), Is.False);
            for (int i = 0; i < 15; i++)
            {
                state.Advance(0.3f);
                Assert.That(state.TankWater + state.WaterB, Is.EqualTo(3f).Within(0.00001f));
                Assert.That(state.TankWater, Is.InRange(0f, 3f));
                Assert.That(state.WaterB, Is.InRange(0f, 3f));
                Assert.That(state.CartStop, Is.EqualTo(5));
            }
            Assert.That(state.Transferring, Is.False);
            Assert.That(state.TankWater, Is.Zero);
            Assert.That(state.IsBHigh, Is.True);
        }

        [Test]
        public void WrongDockAndIncompleteTankCannotStartTheLift()
        {
            var state = new RainCargoState();
            Assert.That(state.TransferWater(), Is.False);
            FillTankOnA(state);
            Assert.That(state.TransferWater(), Is.False, "Rain must be physically carried to B.");
            state.Reset();
            for (int stop = 1; stop <= 5; stop++) Move(state);
            Assert.That(state.TransferWater(), Is.False, "An empty cart can arrive, but cannot raise the lift.");
            Assert.That(state.Transferring, Is.False);
            Assert.That(state.WaterB, Is.Zero);
            state.Reset();
            Move(state);
            FillA(state);
            state.InsertCard(1);
            state.ToggleRain(1);
            state.Advance(1);
            state.RemoveCard(1);
            state.ToggleDrain(0);
            state.Advance(10);
            for (int stop = 2; stop <= 5; stop++) Move(state);
            Assert.That(state.TransferWater(), Is.False, "Arriving with a partly filled tank gives the same recoverable requirement.");
        }

        [Test]
        public void TransferFinishesAndConservesItsContentsAcrossDifferentTickSizes()
        {
            foreach (float tick in new[] { 0.001f, 1f / 60f, 0.17f, 10f })
            {
                var state = new RainCargoState();
                BringFullTankToB(state);
                state.TransferWater();
                for (int step = 0; step < 5000 && state.Transferring; step++)
                {
                    state.Advance(tick);
                    Assert.That(state.WaterB + state.TankWater, Is.EqualTo(3f).Within(0.00001f));
                }
                Assert.That(state.Transferring, Is.False, "Unloading must finish even with very short simulation ticks.");
                Assert.That(state.IsBHigh, Is.True);
            }
        }

        [Test]
        public void LiftOnlyConnectsToTheFloorAtItsCurrentHeight()
        {
            var state = new RainCargoState();
            BringFullTankToB(state);
            Assert.That(state.CanMoveCart(-1, out _), Is.True);
            Assert.That(state.CanMoveCart(1, out _), Is.False);
            state.TransferWater();
            state.Advance(1);
            Assert.That(state.CanMoveCart(-1, out _), Is.False);
            Assert.That(state.CanMoveCart(1, out _), Is.False);
            state.Advance(10);
            Assert.That(state.CanMoveCart(-1, out _), Is.False);
            Assert.That(state.CanMoveCart(1, out _), Is.True);
        }

        [Test]
        public void DrainedLiftCanBeRecoveredByReturningToReloadTheSameTank()
        {
            var state = new RainCargoState();
            BringFullTankToB(state);
            state.TransferWater();
            state.Advance(10);
            Move(state);
            Move(state, -1);
            state.ToggleDrain(1);
            state.Advance(10);
            Assert.That(state.IsBLow, Is.True);
            Assert.That(state.TankWater, Is.Zero, "The drain discards water; it does not magically refill the cart.");
            for (int stop = 4; stop >= 1; stop--) Move(state, -1);
            FillTankOnA(state);
            state.ToggleDrain(0);
            state.Advance(10);
            for (int stop = 2; stop <= 5; stop++) Move(state);
            Assert.That(state.TransferWater(), Is.True);
            Assert.That(state.DrainBOpen, Is.False, "Coupling the tank closes the drain left open on the return trip.");
            state.Advance(10);
            Move(state);
            Move(state);
            Assert.That(state.Completed, Is.True);
        }

        [Test]
        public void OnlyTheOneInsertedRainCardCanBeTakenOrOperated()
        {
            var state = new RainCargoState();
            Assert.That(state.InsertCard(0), Is.True);
            Assert.That(state.InsertCard(1), Is.False);
            Assert.That(state.RemoveCard(1), Is.False);
            Assert.That(state.ToggleRain(1), Is.False);
            Assert.That(state.CardStation, Is.Zero);
            Assert.That(state.RemoveCard(0), Is.True);
            Assert.That(state.InsertCard(1), Is.True);
        }

        [Test]
        public void DeliveryNeedsTheCardButDoesNotAddAnUnrelatedRestorationTask()
        {
            var state = new RainCargoState();
            BringFullTankToB(state);
            state.TransferWater();
            state.Advance(10);
            state.InsertCard(0);
            Move(state);
            Move(state);
            Assert.That(state.Delivered, Is.True);
            Assert.That(state.Completed, Is.False);
            state.ToggleRain(0);
            state.Advance(10);
            state.RemoveCard(0);
            Assert.That(state.IsAHigh, Is.True);
            Assert.That(state.Completed, Is.True, "A is behind us; the delivery goal does not secretly require resetting it.");
        }

        [Test]
        public void CartTransitLocksWaterAndCardChangesUntilItsArrival()
        {
            var state = new RainCargoState();
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
            Assert.That(state.CanMoveCart(1, out _), Is.False);
        }

        [Test]
        public void InvalidInputsAreRejectedAndResetCancelsAnActiveTransfer()
        {
            var state = new RainCargoState();
            Assert.That(state.InsertCard(2), Is.False);
            Assert.That(state.ToggleDrain(-1), Is.False);
            Assert.That(state.BeginCartMove(0), Is.False);
            state.InsertCard(0);
            state.ToggleRain(0);
            state.Advance(float.NaN);
            state.Advance(float.PositiveInfinity);
            state.Advance(-1);
            Assert.That(state.WaterA, Is.Zero);
            state.Reset();
            BringFullTankToB(state);
            state.TransferWater();
            state.Advance(1);
            state.Reset();
            Assert.That(state.Transferring || state.CartInMotion || state.RainActive || state.DrainAOpen || state.DrainBOpen, Is.False);
            Assert.That(state.WaterA + state.WaterB + state.TankWater, Is.Zero);
            Assert.That(state.CardStation, Is.EqualTo(RainCargoState.Hand));
            Assert.That(state.CartStop, Is.Zero);
        }
    }
}

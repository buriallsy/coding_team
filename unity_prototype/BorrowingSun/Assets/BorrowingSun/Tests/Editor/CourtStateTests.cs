using NUnit.Framework;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class CourtStateTests
    {
        [Test]
        public void SameWorldHeightHasDifferentSupportAtHighAndLowWater()
        {
            Vector3 cartDeckHeight = new Vector3(0, 3, 0);
            Assert.That(CourtController.DetermineSupport(cartDeckHeight, 3), Is.EqualTo(MovingDeckSupport.Lower));
            Assert.That(CourtController.DetermineSupport(cartDeckHeight, 0), Is.EqualTo(MovingDeckSupport.Upper));
            Assert.That(CourtController.DetermineSupport(new Vector3(0, 3, 5), 3), Is.EqualTo(MovingDeckSupport.None));
            Assert.That(CourtController.DetermineSupport(new Vector3(-5.5f, 3, 0), 0), Is.EqualTo(MovingDeckSupport.None));
        }

        private static void Move(CourtState state, int direction = 1)
        {
            Assert.That(state.BeginCartMove(direction), Is.True, "Expected a connected, stationary cart route.");
            Assert.That(state.CompleteCartMove(), Is.True);
        }

        private static void Fill(CourtState state)
        {
            if (!state.CardInserted) Assert.That(state.InsertCard(), Is.True);
            if (!state.RainActive) Assert.That(state.ToggleRain(), Is.True);
            state.Advance(10);
            Assert.That(state.IsHigh, Is.True);
        }

        [Test]
        public void DeliveryRequiresLiftParkLowerAndReclaim()
        {
            var state = new CourtState();
            Move(state);
            Assert.That(state.CanMoveCart(1, out _), Is.False, "Cannot jump from low lift to high landing.");
            Fill(state);
            Move(state); // Leave the lift on the permanent north landing.
            Move(state);
            Move(state); // West abutment.
            Assert.That(state.CanMoveCart(1, out _), Is.False, "Upper cross deck is still too high.");
            Assert.That(state.ToggleDrain(), Is.True);
            state.Advance(10);
            Assert.That(state.CartStop, Is.EqualTo(4), "The saved cart position survives draining.");
            Move(state);
            Move(state);
            Move(state);
            Assert.That(state.Delivered, Is.True);
            Assert.That(state.Completed, Is.False, "The weather card must travel with the player.");
            Assert.That(state.RemoveCard(), Is.True);
            Assert.That(state.Completed, Is.True);
        }

        [Test]
        public void RemovingRainCardStopsRainButPreservesStoredWater()
        {
            var state = new CourtState();
            state.InsertCard();
            state.ToggleRain();
            state.Advance(2);
            float storedWater = state.WaterHeight;
            state.RemoveCard();
            state.Advance(100);
            Assert.That(state.RainActive, Is.False);
            Assert.That(state.WaterHeight, Is.EqualTo(storedWater));
            Assert.That(state.WaterInMotion, Is.False);
        }

        [Test]
        public void RisingAndDescendingPreventCrossingSeams()
        {
            var state = new CourtState();
            Move(state);
            state.InsertCard();
            state.ToggleRain();
            state.Advance(1);
            Assert.That(state.CanMoveCart(1, out _), Is.False);
            state.Advance(10);
            Move(state);
            state.ToggleDrain();
            Assert.That(state.CanMoveCart(-1, out _), Is.False);
            state.Advance(1);
            Assert.That(state.CanMoveCart(1, out _), Is.False);
        }

        [Test]
        public void CartMotionInterlocksBothWaterControlsAndCard()
        {
            var state = new CourtState();
            state.InsertCard();
            Assert.That(state.BeginCartMove(1), Is.True);
            Assert.That(state.ToggleRain(), Is.False);
            Assert.That(state.ToggleDrain(), Is.False);
            Assert.That(state.RemoveCard(), Is.False);
            Assert.That(state.BeginCartMove(1), Is.False);
            state.CompleteCartMove();
            Assert.That(state.ToggleRain(), Is.True);
        }

        [Test]
        public void MistakenEarlyDrainCanBeRecoveredWithoutReset()
        {
            var state = new CourtState();
            Move(state);
            Fill(state);
            state.ToggleDrain();
            state.Advance(10);
            Assert.That(state.CartStop, Is.EqualTo(1));
            Fill(state);
            Move(state);
            Assert.That(state.CartStop, Is.EqualTo(2));
        }

        [Test]
        public void BridgeParkingAndReverseTravelRemainRecoverable()
        {
            var state = new CourtState();
            Move(state);
            Fill(state);
            Move(state);
            Move(state);
            Move(state);
            state.ToggleDrain();
            state.Advance(10);
            Move(state); // On upper cross deck at low tide.
            Fill(state); // An intentional wrong turn lifts the cart to 6 m.
            Assert.That(state.CanMoveCart(1, out _), Is.False);
            state.ToggleDrain();
            state.Advance(10);
            Move(state, -1);
            Assert.That(state.CartStop, Is.EqualTo(4));
            Move(state, -1);
            Move(state, -1);
            Fill(state);
            Move(state, -1);
            state.ToggleDrain();
            state.Advance(10);
            Move(state, -1);
            Assert.That(state.CartStop, Is.Zero);
        }

        [Test]
        public void ResetCancelsAnInFlightCartAndClearsAllControls()
        {
            var state = new CourtState();
            state.InsertCard();
            state.BeginCartMove(1);
            state.Reset();
            Assert.That(state.CartInMotion, Is.False);
            Assert.That(state.CardInserted, Is.False);
            Assert.That(state.RainActive, Is.False);
            Assert.That(state.DrainOpen, Is.False);
            Assert.That(state.WaterHeight, Is.Zero);
            Assert.That(state.CartStop, Is.Zero);
            Assert.That(state.BeginCartMove(-1), Is.False);
        }

        [Test]
        public void InvalidTicksAndNonAdjacentMovementDoNotCorruptState()
        {
            var state = new CourtState();
            state.InsertCard();
            state.ToggleRain();
            state.Advance(float.NaN);
            state.Advance(float.PositiveInfinity);
            state.Advance(-1);
            Assert.That(state.WaterHeight, Is.Zero);
            Assert.That(state.BeginCartMove(0), Is.False);
            Assert.That(state.BeginCartMove(2), Is.False);
        }
    }
}

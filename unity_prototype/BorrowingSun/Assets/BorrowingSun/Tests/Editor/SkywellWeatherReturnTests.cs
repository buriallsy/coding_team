using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class SkywellWeatherReturnTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        private SkywellWeatherConsole Station(out SkywellWeatherZone zone)
        {
            var go = new GameObject("weather return test");
            objects.Add(go);
            var station = go.AddComponent<SkywellWeatherConsole>();
            zone = go.AddComponent<SkywellWeatherZone>();
            zone.Initialize(station);
            return station;
        }

        [TearDown]
        public void Clean()
        {
            foreach (var go in objects) if (go) Object.DestroyImmediate(go);
            objects.Clear();
        }

        [Test]
        public void SunnyReturnsToInventoryWhileItsLocalWeatherRemains()
        {
            var station = Station(out var zone);
            station.Request(SkywellToken.Sun); station.Tick(2);
            Assert.That(station.ReturnToken(), Is.EqualTo(SkywellToken.Sun));
            Assert.That(station.Token, Is.EqualTo(SkywellToken.None));
            Assert.That(station.ActiveToken, Is.EqualTo(SkywellToken.None));
            Assert.IsTrue(zone.IsSunny);
            Assert.That(station.ReturnToken(), Is.EqualTo(SkywellToken.None));
            station.Tick(10);
            Assert.IsTrue(zone.IsSunny, "Returning an already empty slot must not clear Sunny.");
        }

        [TestCase(SkywellToken.Rain)]
        [TestCase(SkywellToken.Wind)]
        public void RainAndWindStopAsSoonAsTheirTokenReturns(SkywellToken token)
        {
            var station = Station(out var zone);
            station.Request(token); station.Tick(2);
            Assert.That(zone.ActiveWeather, Is.EqualTo(token));
            Assert.That(station.ReturnToken(), Is.EqualTo(token));
            Assert.That(station.Token, Is.EqualTo(SkywellToken.None));
            Assert.That(zone.ActiveWeather, Is.EqualTo(SkywellToken.None));
            station.Tick(10);
            Assert.That(zone.ActiveWeather, Is.EqualTo(SkywellToken.None));
        }

        [TestCase(SkywellToken.Rain)]
        [TestCase(SkywellToken.Wind)]
        public void ReplacingSunnyDoesNotRestoreItWhenNewTokenReturns(SkywellToken replacement)
        {
            var station = Station(out var zone);
            station.Request(SkywellToken.Sun); station.Tick(2); station.ReturnToken();
            station.Request(replacement); station.Tick(2);
            Assert.That(zone.ActiveWeather, Is.EqualTo(replacement));
            station.ReturnToken(); station.Tick(2);
            Assert.That(zone.ActiveWeather, Is.EqualTo(SkywellToken.None));
        }

        [TestCase(SkywellToken.Sun, SkywellToken.Sun)]
        [TestCase(SkywellToken.Rain, SkywellToken.None)]
        [TestCase(SkywellToken.Wind, SkywellToken.None)]
        public void DepartureDuringSwitchStillReturnsTokenAndHonoursWeatherRule(SkywellToken token, SkywellToken expected)
        {
            var station = Station(out var zone);
            station.Request(token); station.Tick(.2f);
            Assert.IsTrue(station.Busy);
            station.ReturnToken(); station.Tick(2);
            Assert.That(station.Token, Is.EqualTo(SkywellToken.None));
            Assert.That(zone.ActiveWeather, Is.EqualTo(expected));
        }

        [Test]
        public void ReinsertingSunnyDoesNotInterruptExistingSunshine()
        {
            var station = Station(out var zone);
            station.Request(SkywellToken.Sun); station.Tick(2); station.ReturnToken();
            station.Request(SkywellToken.Sun);
            Assert.That(station.Token, Is.EqualTo(SkywellToken.Sun));
            Assert.IsTrue(zone.IsSunny);
        }

        [Test]
        public void ResetClearsSunnyEvenAfterPhysicalTokenHasReturned()
        {
            var station = Station(out var zone);
            station.Request(SkywellToken.Sun); station.Tick(2); station.ReturnToken();
            station.ResetConsole();
            Assert.That(station.Token, Is.EqualTo(SkywellToken.None));
            Assert.That(zone.ActiveWeather, Is.EqualTo(SkywellToken.None));
            Assert.IsFalse(station.Busy);
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using RailGame.Core;

namespace RailGame.Tests
{
    public class ScoringTests
    {
        static Route R(string a, string b, int length) => new Route(a + b, a, b, length, null);

        [Test]
        public void LongestPath_StraightLine()
        {
            var routes = new List<Route> { R("A", "B", 1), R("B", "C", 2) };
            Assert.AreEqual(3, Scoring.LongestPath(routes));
        }

        [Test]
        public void LongestPath_Branch_TakesTwoLongestArms()
        {
            var routes = new List<Route> { R("A", "B", 1), R("B", "C", 2), R("B", "D", 4) };
            Assert.AreEqual(6, Scoring.LongestPath(routes)); // C-B-D
        }

        [Test]
        public void LongestPath_Cycle_CanRevisitCityButNotRoute()
        {
            var routes = new List<Route> { R("A", "B", 1), R("B", "C", 1), R("C", "A", 1), R("C", "D", 5) };
            Assert.AreEqual(8, Scoring.LongestPath(routes)); // D-C-A-B-C
        }

        [Test]
        public void LongestPath_Empty_IsZero()
        {
            Assert.AreEqual(0, Scoring.LongestPath(new List<Route>()));
        }

        [Test]
        public void Ticket_CompletedOnlyWhenConnected()
        {
            var routes = new List<Route> { R("A", "B", 1), R("B", "C", 2), R("D", "E", 3) };
            Assert.IsTrue(Scoring.IsTicketCompleted(new Ticket("t", "A", "C", 5), routes));
            Assert.IsFalse(Scoring.IsTicketCompleted(new Ticket("t", "A", "E", 5), routes));
        }
    }

    public class MapTests
    {
        [Test]
        public void SmallMap_IsValid()
        {
            var errors = TestMaps.Small().Validate(new GameConfig().MaxRouteLength);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void BrokenMap_ReportsErrors()
        {
            var map = new MapDefinition("Сломанная",
                new[] { new City("A", "A", 0, 0), new City("B", "B", 0, 0) },
                new[]
                {
                    new Route("r1", "A", "X", 2, null),                  // нет города X
                    new Route("r2", "A", "B", 2, null, "r3"),            // r3 не ссылается обратно
                    new Route("r3", "A", "B", 2, null),
                    new Route("r4", "A", "B", 12, CardColor.Red)         // слишком длинный
                },
                new[] { new Ticket("t1", "A", "A", 5) });                // города совпадают

            var errors = map.Validate(8);
            Assert.AreEqual(4, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void Dto_ParsesColors()
        {
            var dto = new MapDto
            {
                name = "Dto",
                cities = new[] { new CityDto { id = "A", name = "A" }, new CityDto { id = "B", name = "B" } },
                routes = new[]
                {
                    new RouteDto { id = "r1", from = "A", to = "B", length = 2, color = "gray" },
                    new RouteDto { id = "r2", from = "A", to = "B", length = 2, color = "Blue" }
                },
                tickets = new TicketDto[0]
            };

            var map = dto.ToDefinition();
            Assert.IsTrue(map.GetRoute("r1").IsGray);
            Assert.AreEqual(CardColor.Blue, map.GetRoute("r2").Color);
            Assert.IsNull(map.GetRoute("r2").ParallelRouteId);
        }

        [Test]
        public void Dto_UnknownColor_Throws()
        {
            var dto = new MapDto
            {
                name = "Dto",
                cities = new CityDto[0],
                routes = new[] { new RouteDto { id = "r1", from = "A", to = "B", length = 2, color = "pink" } },
                tickets = new TicketDto[0]
            };
            Assert.Throws<FormatException>(() => dto.ToDefinition());
        }
    }
}

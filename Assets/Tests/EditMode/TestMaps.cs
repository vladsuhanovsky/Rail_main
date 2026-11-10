using RailGame.Core;

namespace RailGame.Tests
{
    /// <summary>Маленькая карта для тестов: 6 городов, серые, цветные и двойной маршрут.</summary>
    internal static class TestMaps
    {
        public static MapDefinition Small()
        {
            var cities = new[]
            {
                new City("A", "Альфа", 0.1f, 0.1f),
                new City("B", "Бета", 0.3f, 0.1f),
                new City("C", "Гамма", 0.5f, 0.3f),
                new City("D", "Дельта", 0.7f, 0.3f),
                new City("E", "Эпсилон", 0.7f, 0.7f),
                new City("F", "Зета", 0.1f, 0.7f)
            };

            var routes = new[]
            {
                new Route("ab", "A", "B", 1, CardColor.Red),
                new Route("bc", "B", "C", 2, null),
                new Route("cd", "C", "D", 3, CardColor.Blue, "cd2"),
                new Route("cd2", "C", "D", 3, CardColor.Green, "cd"),
                new Route("de", "D", "E", 4, CardColor.Yellow),
                new Route("ef", "E", "F", 5, null),
                new Route("af", "A", "F", 6, CardColor.Black),
                new Route("be", "B", "E", 2, CardColor.White),
                new Route("ac", "A", "C", 3, CardColor.Orange),
                new Route("df", "D", "F", 2, CardColor.Purple)
            };

            var tickets = new[]
            {
                new Ticket("t1", "A", "D", 6),
                new Ticket("t2", "B", "F", 7),
                new Ticket("t3", "C", "E", 5),
                new Ticket("t4", "A", "E", 8),
                new Ticket("t5", "B", "D", 5),
                new Ticket("t6", "C", "F", 6)
            };

            return new MapDefinition("Тестовая", cities, routes, tickets);
        }
    }
}

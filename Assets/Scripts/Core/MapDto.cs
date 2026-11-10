using System;
using System.Collections.Generic;

namespace RailGame.Core
{
    // Классы для чтения карты из JSON. Поля публичные и в нижнем регистре,
    // чтобы их понимал JsonUtility из Unity (и любой другой сериализатор).

    [Serializable]
    public class MapDto
    {
        public string name;
        public CityDto[] cities;
        public RouteDto[] routes;
        public TicketDto[] tickets;

        public MapDefinition ToDefinition()
        {
            var cityList = new List<City>();
            if (cities != null)
                foreach (var c in cities) cityList.Add(new City(c.id, c.name, c.x, c.y));

            var routeList = new List<Route>();
            if (routes != null)
                foreach (var r in routes)
                    routeList.Add(new Route(r.id, r.from, r.to, r.length, ParseRouteColor(r.color, r.id),
                        string.IsNullOrEmpty(r.parallel) ? null : r.parallel));

            var ticketList = new List<Ticket>();
            if (tickets != null)
                foreach (var t in tickets) ticketList.Add(new Ticket(t.id, t.from, t.to, t.points));

            return new MapDefinition(name, cityList, routeList, ticketList);
        }

        static CardColor? ParseRouteColor(string value, string routeId)
        {
            if (string.IsNullOrEmpty(value)
                || value.Equals("gray", StringComparison.OrdinalIgnoreCase)
                || value.Equals("grey", StringComparison.OrdinalIgnoreCase))
                return null;

            if (Enum.TryParse(value, true, out CardColor color) && color != CardColor.Locomotive)
                return color;

            throw new FormatException($"Маршрут {routeId}: неизвестный цвет \"{value}\".");
        }
    }

    [Serializable]
    public class CityDto
    {
        public string id;
        public string name;
        public float x;
        public float y;
    }

    [Serializable]
    public class RouteDto
    {
        public string id;
        public string from;
        public string to;
        public int length;
        /// <summary>red, orange, yellow, green, blue, purple, black, white или gray.</summary>
        public string color;
        /// <summary>Id параллельного маршрута (двойной путь), можно не указывать.</summary>
        public string parallel;
    }

    [Serializable]
    public class TicketDto
    {
        public string id;
        public string from;
        public string to;
        public int points;
    }
}

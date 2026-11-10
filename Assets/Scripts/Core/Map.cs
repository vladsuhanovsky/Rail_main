using System;
using System.Collections.Generic;

namespace RailGame.Core
{
    /// <summary>Город — вершина графа. X/Y — нормализованные координаты на картинке карты (0..1).</summary>
    public sealed class City
    {
        public string Id { get; }
        public string Name { get; }
        public float X { get; }
        public float Y { get; }

        public City(string id, string name, float x, float y)
        {
            Id = id;
            Name = name;
            X = x;
            Y = y;
        }

        public override string ToString() => Name;
    }

    /// <summary>Маршрут — ребро графа между двумя городами.</summary>
    public sealed class Route
    {
        public string Id { get; }
        public string CityA { get; }
        public string CityB { get; }
        public int Length { get; }

        /// <summary>Цвет маршрута. null — серый маршрут, его можно оплатить картами любого одного цвета.</summary>
        public CardColor? Color { get; }

        /// <summary>Id параллельного маршрута между теми же городами (двойной путь) или null.</summary>
        public string ParallelRouteId { get; }

        public bool IsGray => Color == null;

        public Route(string id, string cityA, string cityB, int length, CardColor? color, string parallelRouteId = null)
        {
            Id = id;
            CityA = cityA;
            CityB = cityB;
            Length = length;
            Color = color;
            ParallelRouteId = parallelRouteId;
        }

        public bool Connects(string cityId) => CityA == cityId || CityB == cityId;

        public string OtherEnd(string cityId) => cityId == CityA ? CityB : CityA;

        public override string ToString()
        {
            string color = Color.HasValue ? Color.Value.ToString() : "Gray";
            return CityA + "-" + CityB + " (" + Length + ", " + color + ")";
        }
    }

    /// <summary>Карта задания: соединить два города своими маршрутами.</summary>
    public sealed class Ticket
    {
        public string Id { get; }
        public string CityA { get; }
        public string CityB { get; }
        public int Points { get; }

        public Ticket(string id, string cityA, string cityB, int points)
        {
            Id = id;
            CityA = cityA;
            CityB = cityB;
            Points = points;
        }

        public override string ToString() => CityA + "-" + CityB + " (" + Points + ")";
    }

    /// <summary>Неизменяемое описание карты: города, маршруты и колода заданий.</summary>
    public sealed class MapDefinition
    {
        readonly Dictionary<string, City> _citiesById = new Dictionary<string, City>();
        readonly Dictionary<string, Route> _routesById = new Dictionary<string, Route>();

        public string Name { get; }
        public IReadOnlyList<City> Cities { get; }
        public IReadOnlyList<Route> Routes { get; }
        public IReadOnlyList<Ticket> Tickets { get; }

        public MapDefinition(string name, IEnumerable<City> cities, IEnumerable<Route> routes, IEnumerable<Ticket> tickets)
        {
            Name = name;
            Cities = new List<City>(cities);
            Routes = new List<Route>(routes);
            Tickets = new List<Ticket>(tickets);

            // Дубликаты id не ломают конструктор: их найдёт Validate().
            foreach (var city in Cities) _citiesById[city.Id] = city;
            foreach (var route in Routes) _routesById[route.Id] = route;
        }

        public City GetCity(string id) => id != null && _citiesById.TryGetValue(id, out var c) ? c : null;

        public Route GetRoute(string id) => id != null && _routesById.TryGetValue(id, out var r) ? r : null;

        /// <summary>Проверяет карту и возвращает список ошибок (пустой — карта корректна).</summary>
        public List<string> Validate(int maxRouteLength)
        {
            var errors = new List<string>();

            var cityIds = new HashSet<string>();
            foreach (var city in Cities)
            {
                if (string.IsNullOrEmpty(city.Id)) errors.Add("Город без id.");
                else if (!cityIds.Add(city.Id)) errors.Add("Повторяется id города: " + city.Id);
            }

            var routeIds = new HashSet<string>();
            foreach (var route in Routes)
            {
                if (string.IsNullOrEmpty(route.Id)) { errors.Add("Маршрут без id."); continue; }
                if (!routeIds.Add(route.Id)) errors.Add("Повторяется id маршрута: " + route.Id);
                if (GetCity(route.CityA) == null) errors.Add($"Маршрут {route.Id}: нет города {route.CityA}.");
                if (GetCity(route.CityB) == null) errors.Add($"Маршрут {route.Id}: нет города {route.CityB}.");
                if (route.CityA == route.CityB) errors.Add($"Маршрут {route.Id} ведёт из города в него же.");
                if (route.Length < 1 || route.Length > maxRouteLength)
                    errors.Add($"Маршрут {route.Id}: длина {route.Length} вне диапазона 1..{maxRouteLength}.");

                if (route.ParallelRouteId != null)
                {
                    var twin = GetRoute(route.ParallelRouteId);
                    if (twin == null)
                        errors.Add($"Маршрут {route.Id}: параллельный маршрут {route.ParallelRouteId} не найден.");
                    else if (twin.ParallelRouteId != route.Id)
                        errors.Add($"Маршрут {route.Id}: у {twin.Id} должна быть обратная ссылка parallel = {route.Id}.");
                    else if (!(twin.Connects(route.CityA) && twin.Connects(route.CityB)))
                        errors.Add($"Маршруты {route.Id} и {twin.Id} параллельны, но соединяют разные города.");
                }
            }

            var ticketIds = new HashSet<string>();
            foreach (var ticket in Tickets)
            {
                if (string.IsNullOrEmpty(ticket.Id)) { errors.Add("Задание без id."); continue; }
                if (!ticketIds.Add(ticket.Id)) errors.Add("Повторяется id задания: " + ticket.Id);
                if (GetCity(ticket.CityA) == null) errors.Add($"Задание {ticket.Id}: нет города {ticket.CityA}.");
                if (GetCity(ticket.CityB) == null) errors.Add($"Задание {ticket.Id}: нет города {ticket.CityB}.");
                if (ticket.CityA == ticket.CityB) errors.Add($"Задание {ticket.Id}: города совпадают.");
                if (ticket.Points <= 0) errors.Add($"Задание {ticket.Id}: очков должно быть больше нуля.");
            }

            return errors;
        }
    }
}

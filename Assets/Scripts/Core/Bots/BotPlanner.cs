using System.Collections.Generic;
using System.Linq;

namespace RailGame.Core
{
    /// <summary>План бота: какие маршруты нужно занять, чтобы выполнить задания.</summary>
    public sealed class RoutePlan
    {
        /// <summary>Все маршруты сети: уже занятые игроком и запланированные.</summary>
        public readonly HashSet<string> NetworkRouteIds = new HashSet<string>();
        /// <summary>Свободные маршруты, которые ещё нужно занять.</summary>
        public readonly List<Route> RoutesToClaim = new List<Route>();
        public int TrainsNeeded;

        public readonly List<Ticket> Completed = new List<Ticket>();
        public readonly List<Ticket> Reachable = new List<Ticket>();
        /// <summary>Задания, которые уже не выполнить: соперники перекрыли все пути.</summary>
        public readonly List<Ticket> Unreachable = new List<Ticket>();
    }

    /// <summary>
    /// Поиск путей с точки зрения конкретного игрока: свои маршруты бесплатны,
    /// свободные стоят своей длины, чужие и закрытые непроходимы.
    /// </summary>
    public sealed class BotPlanner
    {
        readonly Game _game;
        readonly int _playerIndex;
        readonly Dictionary<string, List<Route>> _adjacency = new Dictionary<string, List<Route>>();

        public BotPlanner(Game game, int playerIndex)
        {
            _game = game;
            _playerIndex = playerIndex;
            foreach (var route in game.Map.Routes)
            {
                AddEdge(route.CityA, route);
                AddEdge(route.CityB, route);
            }
        }

        PlayerState Me => _game.Players[_playerIndex];

        /// <summary>Сколько вагонов стоит пройти по маршруту; null — пройти нельзя.</summary>
        int? Cost(Route route, HashSet<string> freeRoutes)
        {
            var owner = _game.GetRouteOwner(route.Id);
            if (owner.HasValue) return owner.Value == _playerIndex ? 0 : (int?)null;
            if (!_game.IsRouteOpenFor(_playerIndex, route.Id)) return null;
            if (freeRoutes != null && freeRoutes.Contains(route.Id)) return 0;
            return route.Length;
        }

        /// <summary>Кратчайший путь (алгоритм Дейкстры). null — пути нет.</summary>
        public List<Route> ShortestPath(string from, string to, HashSet<string> freeRoutes, out int cost)
        {
            var distance = new Dictionary<string, int> { [from] = 0 };
            var previous = new Dictionary<string, Route>();
            var done = new HashSet<string>();

            while (true)
            {
                string current = null;
                int best = int.MaxValue;
                foreach (var pair in distance)
                {
                    if (!done.Contains(pair.Key) && pair.Value < best)
                    {
                        best = pair.Value;
                        current = pair.Key;
                    }
                }

                if (current == null)
                {
                    cost = 0;
                    return null;
                }
                if (current == to) break;
                done.Add(current);

                if (!_adjacency.TryGetValue(current, out var edges)) continue;
                foreach (var route in edges)
                {
                    var edgeCost = Cost(route, freeRoutes);
                    if (!edgeCost.HasValue) continue;
                    var next = route.OtherEnd(current);
                    if (done.Contains(next)) continue;
                    int candidate = best + edgeCost.Value;
                    if (!distance.TryGetValue(next, out var old) || candidate < old)
                    {
                        distance[next] = candidate;
                        previous[next] = route;
                    }
                }
            }

            cost = distance[to];
            var path = new List<Route>();
            var node = to;
            while (node != from)
            {
                var route = previous[node];
                path.Add(route);
                node = route.OtherEnd(node);
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Строит общую сеть под набор заданий. Дорогие задания прокладываются первыми,
        /// следующие пользуются уже запланированными маршрутами бесплатно.
        /// </summary>
        public RoutePlan BuildPlan(IEnumerable<Ticket> tickets)
        {
            var plan = new RoutePlan();
            foreach (var route in Me.ClaimedRoutes) plan.NetworkRouteIds.Add(route.Id);

            foreach (var ticket in tickets.OrderByDescending(t => t.Points))
            {
                if (Scoring.IsTicketCompleted(ticket, Me.ClaimedRoutes))
                {
                    plan.Completed.Add(ticket);
                    continue;
                }

                var path = ShortestPath(ticket.CityA, ticket.CityB, plan.NetworkRouteIds, out _);
                if (path == null)
                {
                    plan.Unreachable.Add(ticket);
                    continue;
                }

                plan.Reachable.Add(ticket);
                foreach (var route in path)
                {
                    if (!plan.NetworkRouteIds.Add(route.Id)) continue;
                    plan.RoutesToClaim.Add(route);
                    plan.TrainsNeeded += route.Length;
                }
            }
            return plan;
        }

        void AddEdge(string city, Route route)
        {
            if (!_adjacency.TryGetValue(city, out var list))
            {
                list = new List<Route>();
                _adjacency[city] = list;
            }
            list.Add(route);
        }
    }
}

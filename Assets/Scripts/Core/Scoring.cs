using System;
using System.Collections.Generic;
using System.Linq;

namespace RailGame.Core
{
    public sealed class PlayerScore
    {
        public int PlayerIndex { get; internal set; }
        public string PlayerName { get; internal set; }
        public int RoutePoints { get; internal set; }
        /// <summary>Плюс за выполненные задания, минус за невыполненные.</summary>
        public int TicketPoints { get; internal set; }
        public int CompletedTickets { get; internal set; }
        public int FailedTickets { get; internal set; }
        public int LongestPath { get; internal set; }
        public int LongestPathBonus { get; internal set; }

        public int Total => RoutePoints + TicketPoints + LongestPathBonus;
    }

    public sealed class GameResult
    {
        /// <summary>Игроки по местам: очки, затем выполненные задания, затем бонус за длинный путь.</summary>
        public IReadOnlyList<PlayerScore> Ranking { get; }
        /// <summary>Победители (несколько при полной ничьей).</summary>
        public IReadOnlyList<PlayerScore> Winners { get; }

        internal GameResult(List<PlayerScore> scores)
        {
            var sorted = scores
                .OrderByDescending(s => s.Total)
                .ThenByDescending(s => s.CompletedTickets)
                .ThenByDescending(s => s.LongestPathBonus)
                .ToList();
            Ranking = sorted;

            var top = sorted[0];
            Winners = sorted
                .Where(s => s.Total == top.Total
                            && s.CompletedTickets == top.CompletedTickets
                            && s.LongestPathBonus == top.LongestPathBonus)
                .ToList();
        }
    }

    public static class Scoring
    {
        /// <summary>Соединяют ли маршруты игрока два города задания (система непересекающихся множеств).</summary>
        public static bool IsTicketCompleted(Ticket ticket, IEnumerable<Route> routes)
        {
            return BuildNetwork(routes).Connected(ticket.CityA, ticket.CityB);
        }

        /// <summary>
        /// Длина самого длинного непрерывного пути: каждый маршрут используется не больше одного раза,
        /// через город можно проходить несколько раз. Перебор в глубину — для 45 вагонов этого достаточно.
        /// </summary>
        public static int LongestPath(IReadOnlyList<Route> routes)
        {
            var adjacency = new Dictionary<string, List<int>>();
            for (int i = 0; i < routes.Count; i++)
            {
                AddEdge(adjacency, routes[i].CityA, i);
                AddEdge(adjacency, routes[i].CityB, i);
            }

            var used = new bool[routes.Count];

            int Dfs(string city)
            {
                int best = 0;
                foreach (int edge in adjacency[city])
                {
                    if (used[edge]) continue;
                    used[edge] = true;
                    var route = routes[edge];
                    best = Math.Max(best, route.Length + Dfs(route.OtherEnd(city)));
                    used[edge] = false;
                }
                return best;
            }

            int longest = 0;
            foreach (var city in adjacency.Keys)
                longest = Math.Max(longest, Dfs(city));
            return longest;
        }

        public static GameResult Calculate(IReadOnlyList<PlayerState> players, GameConfig config)
        {
            var scores = new List<PlayerScore>();
            foreach (var player in players)
            {
                var network = BuildNetwork(player.ClaimedRoutes);
                var score = new PlayerScore
                {
                    PlayerIndex = player.Index,
                    PlayerName = player.Name,
                    RoutePoints = player.RoutePoints,
                    LongestPath = LongestPath(player.ClaimedRoutes)
                };

                foreach (var ticket in player.Tickets)
                {
                    if (network.Connected(ticket.CityA, ticket.CityB))
                    {
                        score.TicketPoints += ticket.Points;
                        score.CompletedTickets++;
                    }
                    else
                    {
                        score.TicketPoints -= ticket.Points;
                        score.FailedTickets++;
                    }
                }
                scores.Add(score);
            }

            int bestPath = scores.Max(s => s.LongestPath);
            if (bestPath > 0)
                foreach (var score in scores)
                    if (score.LongestPath == bestPath) score.LongestPathBonus = config.LongestPathBonus;

            return new GameResult(scores);
        }

        static DisjointSet BuildNetwork(IEnumerable<Route> routes)
        {
            var set = new DisjointSet();
            foreach (var route in routes) set.Union(route.CityA, route.CityB);
            return set;
        }

        static void AddEdge(Dictionary<string, List<int>> adjacency, string city, int edge)
        {
            if (!adjacency.TryGetValue(city, out var list))
            {
                list = new List<int>();
                adjacency[city] = list;
            }
            list.Add(edge);
        }
    }

    internal sealed class DisjointSet
    {
        readonly Dictionary<string, string> _parent = new Dictionary<string, string>();

        public string Find(string x)
        {
            if (!_parent.TryGetValue(x, out var parent))
            {
                _parent[x] = x;
                return x;
            }
            if (parent == x) return x;

            var root = Find(parent);
            _parent[x] = root; // сжатие пути
            return root;
        }

        public void Union(string a, string b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB) _parent[rootA] = rootB;
        }

        public bool Connected(string a, string b) => Find(a) == Find(b);
    }
}

using System;
using System.Collections.Generic;

namespace RailGame.Core
{
    /// <summary>Состояние игрока. Снаружи только читается; меняет его только Game.</summary>
    public sealed class PlayerState
    {
        readonly Dictionary<CardColor, int> _hand = new Dictionary<CardColor, int>();
        readonly List<Route> _routes = new List<Route>();
        readonly List<Ticket> _tickets = new List<Ticket>();
        readonly List<Ticket> _pendingTickets = new List<Ticket>();

        public int Index { get; }
        public string Name { get; }
        public int TrainsLeft { get; internal set; }
        /// <summary>Очки за занятые маршруты (задания и бонус считаются в конце игры).</summary>
        public int RoutePoints { get; internal set; }

        public IReadOnlyList<Route> ClaimedRoutes => _routes;
        /// <summary>Задания, которые игрок оставил себе.</summary>
        public IReadOnlyList<Ticket> Tickets => _tickets;
        /// <summary>Задания, из которых игрок сейчас выбирает.</summary>
        public IReadOnlyList<Ticket> PendingTickets => _pendingTickets;

        public int HandSize
        {
            get
            {
                int sum = 0;
                foreach (var pair in _hand) sum += pair.Value;
                return sum;
            }
        }

        internal PlayerState(int index, string name, int trains)
        {
            Index = index;
            Name = name;
            TrainsLeft = trains;
        }

        public int CardCount(CardColor color) => _hand.TryGetValue(color, out var n) ? n : 0;

        internal void AddCard(CardColor color, int count = 1) => _hand[color] = CardCount(color) + count;

        internal void RemoveCards(CardColor color, int count)
        {
            int have = CardCount(color);
            if (count > have)
                throw new InvalidOperationException($"У игрока {Name} нет {count} карт цвета {color}.");
            _hand[color] = have - count;
        }

        internal void ClearHand() => _hand.Clear();

        internal void AddRoute(Route route) => _routes.Add(route);

        internal List<Ticket> TicketList => _tickets;
        internal List<Ticket> PendingList => _pendingTickets;

        public override string ToString() => Name;
    }
}

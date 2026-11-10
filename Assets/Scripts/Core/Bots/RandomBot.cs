using System;
using System.Collections.Generic;
using System.Linq;

namespace RailGame.Core
{
    /// <summary>
    /// Бот, который делает случайные разрешённые ходы. Соперник из него никакой,
    /// зато он быстро прогоняет тысячи партий и находит ошибки в правилах.
    /// Умного бота (кратчайшие пути под задания) делаем следующим шагом.
    /// </summary>
    public sealed class RandomBot : IBot
    {
        readonly Random _rng;

        public RandomBot(int seed)
        {
            _rng = new Random(seed);
        }

        /// <summary>Делает одно действие за того, чья сейчас очередь.</summary>
        public ActionResult Act(Game game)
        {
            switch (game.Phase)
            {
                case GamePhase.SetupTickets:
                {
                    var player = game.Players[game.ActingPlayerIndex];
                    var ids = player.PendingTickets.Select(t => t.Id).ToList();
                    return game.KeepInitialTickets(player.Index, ids);
                }
                case GamePhase.ChoosingTickets:
                    return game.KeepTickets(new[] { game.CurrentPlayer.PendingTickets[0].Id });
                case GamePhase.DrawingSecondCard:
                    return DrawRandomCard(game, isSecondCard: true);
                case GamePhase.AwaitingAction:
                    return TakeTurn(game);
                default:
                    return ActionResult.Fail("Игра окончена.");
            }
        }

        ActionResult TakeTurn(Game game)
        {
            var claims = new List<KeyValuePair<Route, ClaimOption>>();
            foreach (var route in game.Map.Routes)
                foreach (var option in game.GetClaimOptions(route.Id))
                    claims.Add(new KeyValuePair<Route, ClaimOption>(route, option));

            bool canDraw = game.CanDrawAnyCard();
            bool canTakeTickets = game.TicketDeckCount > 0;
            double roll = _rng.NextDouble();

            if (claims.Count > 0 && (roll < 0.5 || !canDraw)) return Claim(game, claims);
            if (canDraw && (roll < 0.95 || !canTakeTickets)) return DrawRandomCard(game, isSecondCard: false);
            if (canTakeTickets) return game.DrawTickets();
            if (claims.Count > 0) return Claim(game, claims);
            return game.Pass();
        }

        ActionResult Claim(Game game, List<KeyValuePair<Route, ClaimOption>> claims)
        {
            var pick = claims[_rng.Next(claims.Count)];
            return game.ClaimRoute(pick.Key.Id, pick.Value.Color, pick.Value.Locomotives);
        }

        ActionResult DrawRandomCard(Game game, bool isSecondCard)
        {
            var choices = new List<int>(); // -1 — колода, 0..N — открытая ячейка
            if (game.DeckCount + game.DiscardCount > 0) choices.Add(-1);
            for (int i = 0; i < game.FaceUpCards.Count; i++)
            {
                var card = game.FaceUpCards[i];
                if (!card.HasValue) continue;
                if (isSecondCard && card.Value == CardColor.Locomotive) continue;
                choices.Add(i);
            }

            if (choices.Count == 0) return ActionResult.Fail("Нечего взять.");
            int choice = choices[_rng.Next(choices.Count)];
            return choice < 0 ? game.DrawCardFromDeck() : game.DrawFaceUpCard(choice);
        }
    }
}

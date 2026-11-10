using System.Text;
using RailGame.Core;
using UnityEngine;

namespace RailGame.Presentation
{
    /// <summary>
    /// Точка входа: загружает карту, создаёт партию и (пока нет графики)
    /// прогоняет её ботами, выводя ход игры в консоль Unity.
    /// Повесьте на пустой GameObject в сцене и нажмите Play.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] string mapResourcePath = "Maps/demo_map";
        [SerializeField] string[] playerNames = { "Аня", "Борис", "Вера" };
        [SerializeField] int trainsPerPlayer = 20;
        [SerializeField] int seed = 12345;
        [SerializeField] bool autoplayWithBots = true;
        [Tooltip("Уровень ботов по порядку игроков; если элементов меньше, остальные — Smart.")]
        [SerializeField] BotLevel[] botLevels = { BotLevel.Smart, BotLevel.Random, BotLevel.Smart };
        [SerializeField] int maxSteps = 10000;

        public Game Game { get; private set; }

        void Start()
        {
            var map = MapLoader.LoadFromResources(mapResourcePath);
            Debug.Log($"Карта «{map.Name}»: городов {map.Cities.Count}, маршрутов {map.Routes.Count}, заданий {map.Tickets.Count}.");

            Game = new Game(map, playerNames, new GameConfig { TrainsPerPlayer = trainsPerPlayer }, seed);
            Game.Log += message => Debug.Log("[Игра] " + message);

            if (autoplayWithBots) RunBotGame();
        }

        void RunBotGame()
        {
            var bots = new IBot[Game.Players.Count];
            for (int i = 0; i < bots.Length; i++)
                bots[i] = Bots.Create(i < botLevels.Length ? botLevels[i] : BotLevel.Smart, seed + i);

            int steps = 0;
            while (Game.Phase != GamePhase.GameOver && steps++ < maxSteps)
            {
                var result = bots[Game.ActingPlayerIndex].Act(Game);
                if (!result.Success)
                {
                    Debug.LogError("Бот сделал недопустимый ход: " + result.Error);
                    return;
                }
            }
            PrintResult();
        }

        void PrintResult()
        {
            if (Game.Result == null)
            {
                Debug.LogWarning("Партия не завершилась за отведённое число шагов.");
                return;
            }

            var text = new StringBuilder("Итоги партии:\n");
            int place = 1;
            foreach (var s in Game.Result.Ranking)
            {
                string bonus = s.LongestPathBonus > 0 ? $", бонус за путь +{s.LongestPathBonus}" : "";
                int ticketsTotal = s.CompletedTickets + s.FailedTickets;
                text.AppendLine($"{place++}. {s.PlayerName}: {s.Total} очк. (маршруты {s.RoutePoints}, " +
                                $"задания {s.TicketPoints} — выполнено {s.CompletedTickets} из {ticketsTotal}, " +
                                $"самый длинный путь {s.LongestPath}{bonus})");
            }
            Debug.Log(text.ToString());
        }
    }
}

using System.Linq;
using NUnit.Framework;
using RailGame.Core;

namespace RailGame.Tests
{
    /// <summary>
    /// Прогоняет сотни случайных партий ботами. Ловит зависания, исключения
    /// и «утечку» карт (их число в игре всегда должно оставаться прежним).
    /// </summary>
    public class SimulationTests
    {
        [Test]
        public void RandomGames_AlwaysFinish([Values(2, 3, 4, 5)] int players)
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var config = new GameConfig { TrainsPerPlayer = 12 };
                var names = Enumerable.Range(1, players).Select(i => "Бот " + i).ToList();
                var game = new Game(TestMaps.Small(), names, config, seed);
                var bot = new RandomBot(seed);

                int steps = 0;
                while (game.Phase != GamePhase.GameOver)
                {
                    var result = bot.Act(game);
                    Assert.IsTrue(result.Success, $"seed {seed}, шаг {steps}: {result.Error}");
                    Assert.AreEqual(config.TotalCards, game.Debug_CountAllCards(), $"seed {seed}: карты потерялись");
                    Assert.Less(++steps, 5000, $"seed {seed}: партия зависла");
                }

                Assert.IsNotNull(game.Result);
                Assert.AreEqual(players, game.Result.Ranking.Count);
                Assert.IsNotEmpty(game.Result.Winners);
            }
        }
    }
}

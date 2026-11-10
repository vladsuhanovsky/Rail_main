namespace RailGame.Core
{
    /// <summary>Бот делает одно действие за игрока, чья сейчас очередь (Game.ActingPlayerIndex).</summary>
    public interface IBot
    {
        ActionResult Act(Game game);
    }

    public enum BotLevel
    {
        /// <summary>Строит пути под задания, копит нужные карты.</summary>
        Smart,
        /// <summary>Случайные разрешённые ходы — для тестов и разминки.</summary>
        Random
    }

    public static class Bots
    {
        public static IBot Create(BotLevel level, int seed) =>
            level == BotLevel.Random ? (IBot)new RandomBot(seed) : new SmartBot(seed);
    }
}

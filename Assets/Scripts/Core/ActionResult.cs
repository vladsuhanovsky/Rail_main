namespace RailGame.Core
{
    public enum GamePhase
    {
        /// <summary>Начало игры: каждый игрок выбирает стартовые задания.</summary>
        SetupTickets,
        /// <summary>Ход игрока: взять карты, занять маршрут или взять задания.</summary>
        AwaitingAction,
        /// <summary>Игрок взял первую карту и должен взять вторую.</summary>
        DrawingSecondCard,
        /// <summary>Игрок вытянул задания и выбирает, какие оставить.</summary>
        ChoosingTickets,
        GameOver
    }

    /// <summary>Результат действия: успех или понятная причина отказа (её можно показать в UI).</summary>
    public readonly struct ActionResult
    {
        public bool Success { get; }
        public string Error { get; }

        ActionResult(bool success, string error)
        {
            Success = success;
            Error = error;
        }

        public static ActionResult Ok() => new ActionResult(true, null);
        public static ActionResult Fail(string error) => new ActionResult(false, error);

        public override string ToString() => Success ? "OK" : "Ошибка: " + Error;
    }

    /// <summary>Способ оплатить маршрут: сколько карт какого цвета и сколько локомотивов.</summary>
    public readonly struct ClaimOption
    {
        /// <summary>Цвет оплаты. Locomotive — маршрут оплачивается только локомотивами.</summary>
        public CardColor Color { get; }
        public int Locomotives { get; }

        public ClaimOption(CardColor color, int locomotives)
        {
            Color = color;
            Locomotives = locomotives;
        }

        public override string ToString() => Color + " + " + Locomotives + " локомотив(ов)";
    }
}

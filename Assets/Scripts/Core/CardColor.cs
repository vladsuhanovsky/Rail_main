namespace RailGame.Core
{
    /// <summary>Цвет карты вагона. Locomotive — джокер, подходит к маршруту любого цвета.</summary>
    public enum CardColor
    {
        Red,
        Orange,
        Yellow,
        Green,
        Blue,
        Purple,
        Black,
        White,
        Locomotive
    }

    public static class CardColors
    {
        /// <summary>Обычные цвета (все, кроме локомотива).</summary>
        public static readonly CardColor[] Regular =
        {
            CardColor.Red, CardColor.Orange, CardColor.Yellow, CardColor.Green,
            CardColor.Blue, CardColor.Purple, CardColor.Black, CardColor.White
        };

        /// <summary>Название цвета для сообщений игроку.</summary>
        public static string RussianName(CardColor color)
        {
            switch (color)
            {
                case CardColor.Red: return "красный";
                case CardColor.Orange: return "оранжевый";
                case CardColor.Yellow: return "жёлтый";
                case CardColor.Green: return "зелёный";
                case CardColor.Blue: return "синий";
                case CardColor.Purple: return "фиолетовый";
                case CardColor.Black: return "чёрный";
                case CardColor.White: return "белый";
                default: return "локомотив";
            }
        }
    }
}

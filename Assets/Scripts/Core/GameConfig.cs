namespace RailGame.Core
{
    /// <summary>Все числа правил в одном месте — удобно балансировать и делать варианты игры.</summary>
    public sealed class GameConfig
    {
        public int MinPlayers = 2;
        public int MaxPlayers = 5;

        public int TrainsPerPlayer = 45;
        public int StartingHandSize = 4;

        public int CardsPerColor = 12;
        public int LocomotiveCards = 14;

        public int FaceUpSlots = 5;
        /// <summary>Если открытых локомотивов больше этого числа, открытый ряд сбрасывается и выкладывается заново.</summary>
        public int MaxFaceUpLocomotives = 2;

        public int InitialTicketsDealt = 3;
        public int InitialTicketsMinKeep = 2;
        public int TicketsDrawn = 3;
        public int TicketsMinKeep = 1;

        /// <summary>Когда у игрока после хода остаётся столько вагонов или меньше, начинается последний круг.</summary>
        public int FinalRoundTrainThreshold = 2;

        public int LongestPathBonus = 10;

        /// <summary>Второй путь двойного маршрута открыт только при таком числе игроков и больше.</summary>
        public int DoubleRoutesMinPlayers = 4;

        /// <summary>Очки за маршрут по его длине (индекс = длина).</summary>
        public int[] RoutePointsByLength = { 0, 1, 2, 4, 7, 10, 15, 18, 21 };

        public int MaxRouteLength => RoutePointsByLength.Length - 1;

        public int TotalCards => CardsPerColor * CardColors.Regular.Length + LocomotiveCards;

        public int PointsForRouteLength(int length)
        {
            if (length < 0 || length >= RoutePointsByLength.Length) return 0;
            return RoutePointsByLength[length];
        }
    }
}

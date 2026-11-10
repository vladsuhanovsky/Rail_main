using RailGame.Core;
using UnityEngine;

namespace RailGame.Presentation.Ui
{
    /// <summary>Все цвета интерфейса в одном месте.</summary>
    public static class Palette
    {
        public static readonly Color MapBackground = new Color(0.93f, 0.90f, 0.82f);
        public static readonly Color PanelBackground = new Color(0.17f, 0.19f, 0.23f);
        public static readonly Color PanelSection = new Color(0.22f, 0.25f, 0.30f);
        public static readonly Color PanelText = new Color(0.93f, 0.94f, 0.96f);
        public static readonly Color PanelTextDim = new Color(0.65f, 0.68f, 0.74f);
        public static readonly Color MapText = new Color(0.18f, 0.16f, 0.14f);
        public static readonly Color TrackOutline = new Color(0.25f, 0.23f, 0.20f, 0.9f);
        public static readonly Color Highlight = new Color(1f, 0.85f, 0.1f);
        public static readonly Color Error = new Color(1f, 0.45f, 0.4f);
        public static readonly Color Success = new Color(0.45f, 0.85f, 0.45f);
        public static readonly Color Overlay = new Color(0.05f, 0.06f, 0.08f, 0.92f);
        public static readonly Color GrayRoute = new Color(0.66f, 0.66f, 0.66f);
        public static readonly Color CityFill = new Color(0.98f, 0.97f, 0.94f);

        /// <summary>Цвета игроков — не совпадают с цветами карт, чтобы занятые маршруты было видно.</summary>
        public static readonly Color[] Players =
        {
            new Color(0.10f, 0.78f, 0.80f), // бирюзовый
            new Color(0.90f, 0.25f, 0.70f), // малиновый
            new Color(0.55f, 0.36f, 0.20f), // коричневый
            new Color(0.62f, 0.88f, 0.12f), // салатовый
            new Color(0.42f, 0.47f, 0.60f)  // графитовый
        };

        public static Color PlayerColor(int index) => Players[index % Players.Length];

        public static Color Card(CardColor color)
        {
            switch (color)
            {
                case CardColor.Red: return new Color(0.86f, 0.22f, 0.20f);
                case CardColor.Orange: return new Color(0.96f, 0.56f, 0.16f);
                case CardColor.Yellow: return new Color(0.96f, 0.86f, 0.22f);
                case CardColor.Green: return new Color(0.30f, 0.68f, 0.32f);
                case CardColor.Blue: return new Color(0.24f, 0.45f, 0.86f);
                case CardColor.Purple: return new Color(0.58f, 0.34f, 0.76f);
                case CardColor.Black: return new Color(0.14f, 0.14f, 0.16f);
                case CardColor.White: return new Color(0.96f, 0.96f, 0.96f);
                default: return new Color(0.80f, 0.72f, 0.95f); // локомотив
            }
        }

        public static Color RouteColor(Route route) => route.Color.HasValue ? Card(route.Color.Value) : GrayRoute;

        /// <summary>Чёрный или белый текст — что лучше читается на этом фоне.</summary>
        public static Color TextOn(Color background)
        {
            float luminance = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
            return luminance > 0.6f ? new Color(0.1f, 0.1f, 0.1f) : Color.white;
        }

        public static string CardTitle(CardColor color)
        {
            string name = CardColors.RussianName(color);
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}

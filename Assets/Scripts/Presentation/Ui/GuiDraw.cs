using UnityEngine;

namespace RailGame.Presentation.Ui
{
    /// <summary>Простые фигуры поверх IMGUI: прямоугольники, круги, повёрнутые плашки вагонов.</summary>
    public static class GuiDraw
    {
        public static void Rect(Rect rect, Color color, float radius = 0f)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        public static void Frame(Rect rect, Color color, float width, float radius = 0f)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, width, radius);
        }

        public static void Circle(Vector2 center, float radius, Color color)
        {
            Rect(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), color, radius);
        }

        public static void Ring(Vector2 center, float radius, float width, Color color)
        {
            Frame(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), color, width, radius);
        }

        /// <summary>Прямоугольник с центром в center, повёрнутый на angle градусов.</summary>
        public static void RotatedRect(Vector2 center, float length, float thickness, float angle, Color color, float radius = 2f)
        {
            var previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
            Rect(new Rect(-length / 2f, -thickness / 2f, length, thickness), color, radius);
            GUI.matrix = previous;
        }

        /// <summary>Невидимая кнопка поверх уже нарисованной области.</summary>
        public static bool HitArea(Rect rect) => GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }
}

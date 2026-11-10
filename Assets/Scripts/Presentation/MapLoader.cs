using System.IO;
using RailGame.Core;
using UnityEngine;

namespace RailGame.Presentation
{
    /// <summary>Загружает карту из JSON-файла в папке Assets/Resources.</summary>
    public static class MapLoader
    {
        /// <param name="resourcePath">Путь внутри Resources без расширения, например "Maps/demo_map".</param>
        public static MapDefinition LoadFromResources(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
                throw new FileNotFoundException($"Не найден файл карты Assets/Resources/{resourcePath}.json");
            return FromJson(asset.text);
        }

        public static MapDefinition FromJson(string json) => JsonUtility.FromJson<MapDto>(json).ToDefinition();
    }
}

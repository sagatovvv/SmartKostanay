using System;
using System.Collections.Generic;
using System.Globalization;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace SmartKostanay.Helpers
{
    public static class GeoConverter
    {
        public static List<(double lat, double lon)> ParseWktToLatLon(string wkt)
        {
            try
            {
                if (string.IsNullOrEmpty(wkt)) return null;

                // Очищаем строку: убираем MULTIPOLYGON, POLYGON и все лишние скобки в начале и конце
                string coordinatesPart = wkt
                    .Replace("MULTIPOLYGON", "")
                    .Replace("POLYGON", "")
                    .Replace("(", "")
                    .Replace(")", "")
                    .Trim();

                var points = new List<(double lat, double lon)>();
                var coordinatePairs = coordinatesPart.Split(',');

                foreach (var pair in coordinatePairs)
                {
                    var parts = pair.Trim().Split(' ');
                    if (parts.Length >= 2)
                    {
                        // В ЕГКН координаты обычно в метрах (EPSG:3857 или 32641), 
                        // поэтому тут важна конвертация в градусы.
                        // Пока просто парсим числа:
                        if (double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double x) &&
                            double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double y))
                        {
                            points.Add((y, x)); // Инвертируем, если нужно Lat/Lon
                        }
                    }
                }
                return points.Count > 0 ? points : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
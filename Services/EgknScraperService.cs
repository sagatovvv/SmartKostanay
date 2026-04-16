using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using SmartKostanay.Helpers;
using System.Globalization;

namespace SmartKostanay.Services
{
    public class EgknScraperService
    {
        private readonly HttpClient _httpClient;
        private const string MainUrl = "https://map.gov4c.kz/egkn/";
        private const string DistrictUrl = "https://map.gov4c.kz/egkn/rest/map/district?code=193";
        private const string SearchApiUrl = "https://map.gov4c.kz/egkn/rest/map/search";

        public EgknScraperService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.Add("Referer", MainUrl);
        }

        public async Task<List<(double lat, double lon)>> GetActualCoordinatesAsync(string cadNumber)
        {
            try
            {
                await _httpClient.GetAsync(MainUrl);
                await _httpClient.GetAsync(DistrictUrl);

                var url = $"{SearchApiUrl}?searchText={cadNumber.Trim()}&offset=0&limit=10&lang=ru&layers=";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode) return null;

                var jsonString = await response.Content.ReadAsStringAsync();
                var rootJson = JObject.Parse(jsonString);
                var landsArray = rootJson["lands"] as JArray;

                if (landsArray != null && landsArray.Count > 0)
                {
                    var geometryWkt = landsArray[0]?["geometry"]?.ToString();
                    if (!string.IsNullOrEmpty(geometryWkt))
                    {
                        // ПАРСИНГ И КОНВЕРТАЦИЯ
                        return ParseAndConvertWkt(geometryWkt);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL ERROR]: {ex.Message}");
                return null;
            }
        }

        private List<(double lat, double lon)> ParseAndConvertWkt(string wkt)
        {
            var points = new List<(double lat, double lon)>();

            // Очищаем строку от MULTIPOLYGON и скобок
            string cleanData = wkt.Replace("MULTIPOLYGON", "").Replace("POLYGON", "")
                                  .Replace("(", "").Replace(")", "").Trim();

            var pairs = cleanData.Split(',');

            foreach (var pair in pairs)
            {
                var coords = pair.Trim().Split(' ');
                if (coords.Length >= 2)
                {
                    if (double.TryParse(coords[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double x) &&
                        double.TryParse(coords[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double y))
                    {
                        // ТАК КАК В ЕГКН МЕТРЫ (EPSG:3857), ПЕРЕВОДИМ В ГРАДУСЫ (WGS84)
                        var lon = (x / 20037508.34) * 180;
                        var lat = (y / 20037508.34) * 180;
                        lat = 180 / Math.PI * (2 * Math.Atan(Math.Exp(lat * Math.PI / 180)) - Math.PI / 2);

                        points.Add((lat, lon));
                    }
                }
            }
            return points.Count > 0 ? points : null;
        }
    }
}
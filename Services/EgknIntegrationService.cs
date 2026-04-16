using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using SmartKostanay.Models;
using System.Linq;

namespace SmartKostanay.Services
{
    public class EgknIntegrationService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://map.gov4c.kz/egkn/rest/map";

        public EgknIntegrationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Имитируем браузер, как в твоем HEADERS
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://map.gov4c.kz/egkn/");
        }

        public async Task<List<(double lat, double lon)>> GetCoordinatesAsync(string cadastralNumber)
        {
            // Шаг 1: Поиск объекта
            var searchUrl = $"{BaseUrl}/search?searchText={cadastralNumber}&offset=0&limit=10&lang=ru&layers=";
            var searchResponse = await _httpClient.GetStringAsync(searchUrl);
            var searchJson = JObject.Parse(searchResponse);

            var item = searchJson["results"]?.FirstOrDefault() ?? searchJson["features"]?.FirstOrDefault();
            if (item == null) return null;

            string id = item["id"]?.ToString() ?? item["objectId"]?.ToString();
            string layer = item["layer"]?.ToString() ?? item["layerName"]?.ToString() ?? "parcel";

            // Шаг 2: Получение геометрии
            // Пробуем основной эндпоинт из твоего списка
            var geoUrl = $"{BaseUrl}/feature/{layer}/{id}";
            var geoResponse = await _httpClient.GetStringAsync(geoUrl);
            var geoJson = JObject.Parse(geoResponse);

            var geometry = geoJson["geometry"] ?? geoJson;
            var type = geometry["type"]?.ToString();

            // Извлекаем "сырые" координаты (UTM)
            var rawCoords = new List<double[]>();
            if (type == "Polygon")
            {
                rawCoords = geometry["coordinates"]?[0]?.ToObject<List<double[]>>();
            }
            else if (type == "MultiPolygon")
            {
                rawCoords = geometry["coordinates"]?[0]?[0]?.ToObject<List<double[]>>();
            }

            if (rawCoords == null || rawCoords.Count == 0) return null;

            // Шаг 3: Конвертация (UTM -> WGS84)
            return ConvertUtmToWgs84(rawCoords);
        }

        private List<(double lat, double lon)> ConvertUtmToWgs84(List<double[]> utmCoords)
        {
            double firstX = utmCoords[0][0];

            // Определяем зону (как в твоем Python: detect_utm_zone)
            int epsgCode = 32642; // По умолчанию Центр (Астана/Костанай)
            if (firstX < 500000) epsgCode = 32640;
            else if (firstX > 700000) epsgCode = 32643;

            // Настройка трансформации через ProjNet
            var ctFact = new CoordinateTransformationFactory();
            var csFact = new CoordinateSystemFactory();

            // UTM (напр. EPSG:32642)
            var utmCS = csFact.CreateFromWkt($"PROJCS[\"WGS 84 / UTM zone {epsgCode % 100}N\",GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"latitude_of_origin\",0],PARAMETER[\"central_meridian\",{(epsgCode % 100 * 6 - 183)}],PARAMETER[\"scale_factor\",0.9996],PARAMETER[\"false_easting\",500000],PARAMETER[\"false_northing\",0],UNIT[\"metre\",1]]");

            // WGS84 (EPSG:4326)
            var wgs84CS = GeographicCoordinateSystem.WGS84;

            var trans = ctFact.CreateFromCoordinateSystems(utmCS, wgs84CS);

            var result = new List<(double lat, double lon)>();
            foreach (var coord in utmCoords)
            {
                var transformed = trans.MathTransform.Transform(new double[] { coord[0], coord[1] });
                // ProjNet возвращает [Lon, Lat]
                result.Add((lat: Math.Round(transformed[1], 7), lon: Math.Round(transformed[0], 7)));
            }

            return result;
        }

        public async Task<string> GetWktPolygonAsync(string cadastralNumber)
        {
            // 1. Поиск (как мы делали раньше)
            var searchUrl = $"{BaseUrl}/search?searchText={cadastralNumber}&lang=ru";
            var response = await _httpClient.GetStringAsync(searchUrl);
            var json = JObject.Parse(response);

            // 2. Достаем значение прямо из того поля, что на скрине
            // Путь в JSON: lands -> [0] -> geometry
            var wktGeometry = json["lands"]?[0]?["geometry"]?.ToString();

            return wktGeometry; // Вернет "MULTIPOLYGON(((542738.75...)))"
        }
    }
}
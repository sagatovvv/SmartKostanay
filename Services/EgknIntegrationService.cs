using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DotSpatial.Projections;

namespace SmartKostanay.Services
{
    public class EgknIntegrationService
    {
        private readonly HttpClient _httpClient;

        public EgknIntegrationService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            var baseUri = new Uri("https://map.gov4c.kz");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://map.gov4c.kz/egkn/");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");

            var cookieHandler = new HttpClientHandler();
            cookieHandler.CookieContainer.Add(baseUri, new System.Net.Cookie("MAP_SESSION_ID", "8mwBLd9mXCg7TWIzVxR5tQm4bZTyMkC3blw_VWTD"));
            cookieHandler.CookieContainer.Add(baseUri, new System.Net.Cookie("JSESSIONID", "N23bdgcDklscFibwOdTO7ZeYZH_WrVxwRjkY3D_T"));
        }

        public async Task<EgknLandItem?> GetLandByCadastre(string kadNumber)
        {
            // Используем номер как есть (на сайте он часто идет без двоеточий в запросе)
            var url = $"https://map.gov4c.kz/egkn/rest/map/search?searchText={kadNumber}&offset=0&limit=10&lang=ru&layers=";

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // ВНИМАНИЕ: Копируем заголовки ПУЛЯ В ПУЛЮ со скрина
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br, zstd");
            request.Headers.TryAddWithoutValidation("Accept-Language", "ru,en-US;q=0.9,en;q=0.8,kk;q=0.7");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Cookie", "MAP_SESSION_ID=8mwBLd9mXCg7TWIzVxR5tQm4bZTyMkC3blw_VWTD; JSESSIONID=N23bdgcDklscFibwOdTO7ZeYZH_WrVxwRjkY3D_T");
            request.Headers.TryAddWithoutValidation("Host", "map.gov4c.kz");
            request.Headers.TryAddWithoutValidation("Referer", "https://map.gov4c.kz/egkn/");
            request.Headers.TryAddWithoutValidation("Sec-Ch-Ua", "\"Chromium\";v=\"146\", \"Not-A.Brand\";v=\"24\", \"Google Chrome\";v=\"146\"");
            request.Headers.TryAddWithoutValidation("Sec-Ch-Ua-Mobile", "?0");
            request.Headers.TryAddWithoutValidation("Sec-Ch-Ua-Platform", "\"Windows\"");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36");

            var response = await _httpClient.SendAsync(request);
            var rawJson = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[RESULT]: {rawJson}"); // Если тут будет {"lands":[]}, значит куки протухли

            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = System.Text.Json.JsonSerializer.Deserialize<EgknRootResponse>(rawJson, options);

            return result?.Lands?.FirstOrDefault();
        }

        public (double lat, double lon) GetCenterCoordinates(string wktGeometry)
        {
            // Извлекаем все числа из MULTIPOLYGON(((...)))
            var matches = Regex.Matches(wktGeometry, @"([\d\.]+)\s+([\d\.]+)");

            if (matches.Count == 0) throw new Exception("Не удалось распарсить геометрию участка");

            double sumX = 0, sumY = 0;
            foreach (Match m in matches)
            {
                sumX += double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                sumY += double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            }

            // Берем среднюю точку (центроид)
            return ConvertUtmToWgs84(sumX / matches.Count, sumY / matches.Count);
        }

        private (double lat, double lon) ConvertUtmToWgs84(double x, double y)
        {
            double[] xy = { x, y };
            double[] z = { 0 };

            // UTM Zone 42N (EPSG:32642) -> WGS84 (EPSG:4326)
            ProjectionInfo source = ProjectionInfo.FromEpsgCode(32642);
            ProjectionInfo target = ProjectionInfo.FromEpsgCode(4326);

            Reproject.ReprojectPoints(xy, z, source, target, 0, 1);

            return (lat: xy[1], lon: xy[0]);
        }
    }

    // DTO классы для десериализации ответа ЕГКН
    public class EgknRootResponse
    {
        [JsonPropertyName("lands")] public List<EgknLandItem> Lands { get; set; } = new();
    }

    public class EgknLandItem
    {
        [JsonPropertyName("geometry")] public string Geometry { get; set; } = "";
        [JsonPropertyName("properties")] public EgknProperties Properties { get; set; } = new();
    }

    public class EgknProperties
    {
        [JsonPropertyName("kad_nomer")] public string CadastralNumber { get; set; } = "";
        [JsonPropertyName("address_ru")] public string Address { get; set; } = "";
        [JsonPropertyName("area")] public double Area { get; set; }
        [JsonPropertyName("district_id")] public int DistrictId { get; set; }
    }
}
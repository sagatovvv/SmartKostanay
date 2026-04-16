using MongoDB.Driver.GeoJsonObjectModel;

namespace SmartKostanay.Models.DTOs
{
    public class LandPlotUpsertDto
    {
        public string CadastralNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public double Area { get; set; }
        public OwnerDto Owner { get; set; } = new();
        public DateTime? IssueDate { get; set; }
        public GeoPoint Location { get; set; } = new();

        // НОВОЕ: Поле для границ участка
        // Передаем как список точек [[lon, lat], [lon, lat], ...]
        public List<List<double>>? Boundary { get; set; }
    }
}
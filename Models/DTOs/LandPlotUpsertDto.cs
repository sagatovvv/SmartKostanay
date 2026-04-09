namespace SmartKostanay.Models.DTOs
{
    public class LandPlotUpsertDto
    {
        public string CadastralNumber { get; set; }
        public string Address { get; set; }
        public string District { get; set; }
        public double Area { get; set; }
        public OwnerDto Owner { get; set; }
        public DateTime IssueDate { get; set; }
        public GeoPoint Location { get; set; }
        // Boundary можно добавить позже, когда настроишь работу с полигонами
    }
}

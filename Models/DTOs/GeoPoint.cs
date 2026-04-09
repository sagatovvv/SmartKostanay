namespace SmartKostanay.Models.DTOs
{
    public class GeoPoint
    {
        public string Type { get; set; } = "Point";
        public List<double> Coordinates { get; set; } // [долгота, широта]
    }
}

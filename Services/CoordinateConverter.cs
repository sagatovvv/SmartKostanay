using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;


namespace SmartKostanay.Services
{
    public class CoordinateConverter
    {
        public (double lat, double lon) Convert(double x, double y, int wkid)
        {
            if (wkid == 4326)
            {
                // Уже WGS84
                return (y, x);
            }

            // Для UTM 42N
            var wgs84 = GeographicCoordinateSystem.WGS84;
            var utm42 = ProjectedCoordinateSystem.WGS84_UTM(42, true);

            var transform = new CoordinateTransformationFactory()
                .CreateFromCoordinateSystems(utm42, wgs84);

            var result = transform.MathTransform.Transform(new[] { x, y });
            return (lat: result[1], lon: result[0]);
        }
    }
}
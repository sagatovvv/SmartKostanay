using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace SmartKostanay.Models
{
    public class IzhsPhoto
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string LandPlotId { get; set; }

        public int StageNumber { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? Comment { get; set; }
        public string UploadedBy { get; set; } = "SYSTEM_MOBILE";

        public PhotoMetadata Metadata { get; set; } = new();
        public PhotoValidation Validation { get; set; } = new();

        public bool Verified { get; set; } = false;
        public DateTime DateOfCreation { get; set; } = DateTime.UtcNow;
    }

    public class PhotoMetadata
    {
        public DateTime? CapturedAt { get; set; }
        public GeoJsonPoint<GeoJson2DGeographicCoordinates>? GeoLocation { get; set; }
        public string? DeviceMake { get; set; }
        public string? DeviceModel { get; set; }
        public int? ImageWidth { get; set; }
        public int? ImageHeight { get; set; }
        public bool HasExif { get; set; }
    }

    public class PhotoValidation
    {
        public bool IsGeoPresent { get; set; }
        public bool IsWithinBoundary { get; set; }
        public bool IsDatePresent { get; set; }
        public int TimeDriftSeconds { get; set; }
        public bool IsTimeDriftAcceptable { get; set; }
        public bool IsValid { get; set; }
    }
}
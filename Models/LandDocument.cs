using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class LandDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string LandPlotId { get; set; } // Ссылка на участок

    public int StageNumber { get; set; } // 1, 2 или 3

    public string Type { get; set; } // APZ, SKETCH и т.д.

    public string FileName { get; set; }

    public string FileUrl { get; set; } // Путь к файлу

    public long FileSize { get; set; }

    public string? Comment { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UploadedBy { get; set; } // ID пользователя

    public string UploadSource { get; set; } // WEB или MOBILE

    public DateTime DateOfCreation { get; set; } = DateTime.UtcNow;
}
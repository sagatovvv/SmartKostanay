using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;
using System;
using System.Collections.Generic;

namespace SmartKostanay.Models
{
    public class IzhsLandPlot
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("CadastralNumber")]
        public string CadastralNumber { get; set; }

        [BsonElement("Address")]
        public string Address { get; set; }

        [BsonElement("District")]
        public string District { get; set; }

        [BsonElement("Area")]
        public double Area { get; set; }

        [BsonElement("Owner")]
        public OwnerDetails Owner { get; set; }

        [BsonElement("IssueDate")]
        public DateTime? IssueDate { get; set; }

        [BsonElement("Location")]
        public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; }

        [BsonElement("Boundary")]
        public GeoJsonPolygon<GeoJson2DGeographicCoordinates> Boundary { get; set; }

        [BsonElement("CurrentStage")]
        public int CurrentStage { get; set; }

        [BsonElement("OverallStatus")]
        public string OverallStatus { get; set; } // "ACTIVE" | "COMPLETED" | "OVERDUE" | "EXCLUDED"

        [BsonElement("Stages")]
        public List<StageDetails> Stages { get; set; }

        [BsonElement("ExcludedFromControl")]
        public bool ExcludedFromControl { get; set; }

        [BsonElement("ExclusionReason")]
        public string ExclusionReason { get; set; }

        [BsonElement("ExclusionDate")]
        public DateTime? ExclusionDate { get; set; }

        [BsonElement("DateOfCreation")]
        public DateTime DateOfCreation { get; set; }

        [BsonElement("ModifiedOn")]
        public DateTime ModifiedOn { get; set; }
    }

    public class OwnerDetails
    {
        [BsonElement("ID")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ID { get; set; }

        [BsonElement("FullName")]
        public string FullName { get; set; }

        [BsonElement("IdentityNumber")]
        public string IdentityNumber { get; set; }
    }

    public class StageDetails
    {
        [BsonElement("StageNumber")]
        public int StageNumber { get; set; }

        [BsonElement("Name")]
        public string Name { get; set; }

        [BsonElement("Status")]
        public string Status { get; set; } // "PENDING" | "IN_PROGRESS" | "COMPLETED" | "OVERDUE"

        [BsonElement("Deadline")]
        public DateTime? Deadline { get; set; }

        [BsonElement("CompletedAt")]
        public DateTime? CompletedAt { get; set; }

        [BsonElement("Documents")]
        [BsonRepresentation(BsonType.ObjectId)]
        public List<string> Documents { get; set; }

        [BsonElement("Photos")]
        [BsonRepresentation(BsonType.ObjectId)]
        public List<string> Photos { get; set; }
    }
}
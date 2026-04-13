using System.Text.Json.Serialization;


public class EgknRootResponse
{
    [JsonPropertyName("lands")]
    public List<EgknLandItem> Lands { get; set; }
}

public class EgknLandItem
{
    [JsonPropertyName("geometry")]
    public string Geometry { get; set; }

    [JsonPropertyName("properties")]
    public EgknLandProperties Properties { get; set; }
}

public class EgknLandProperties
{
    [JsonPropertyName("kad_nomer")]
    public string CadastralNumber { get; set; }

    [JsonPropertyName("address_ru")]
    public string Address { get; set; }

    [JsonPropertyName("area")]
    public double Area { get; set; }

    [JsonPropertyName("category_ru")]
    public string Category { get; set; }

    [JsonPropertyName("district_id")]
    public int DistrictId { get; set; }
}

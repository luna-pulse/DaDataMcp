using System.Text.Json.Serialization;

namespace DadataMcp.Server.Models.Api;

public sealed class GeolocateApiResponse
{
    [JsonPropertyName("suggestions")]
    public IReadOnlyList<SuggestionApiItem>? Suggestions { get; init; }
}

public sealed class IplocateApiResponse
{
    [JsonPropertyName("location")]
    public SuggestionApiItem? Location { get; init; }
}

public sealed class SuggestionApiItem
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("unrestricted_value")]
    public string? UnrestrictedValue { get; init; }

    [JsonPropertyName("data")]
    public SuggestionAddressData? Data { get; init; }
}

public sealed class SuggestionAddressData
{
    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("region_with_type")]
    public string? RegionWithType { get; init; }

    [JsonPropertyName("city_with_type")]
    public string? CityWithType { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("street_with_type")]
    public string? StreetWithType { get; init; }

    [JsonPropertyName("house")]
    public string? House { get; init; }

    [JsonPropertyName("geo_lat")]
    public string? GeoLat { get; init; }

    [JsonPropertyName("geo_lon")]
    public string? GeoLon { get; init; }

    [JsonPropertyName("fias_id")]
    public string? FiasId { get; init; }

    [JsonPropertyName("qc_geo")]
    public string? QcGeo { get; init; }
}

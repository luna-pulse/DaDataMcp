namespace DadataMcp.Server.Models;

public sealed class GetCountryResult
{
    public required IReadOnlyList<CountrySuggestionResult> Suggestions { get; init; }
    public string? Message { get; init; }
}

public sealed class CountrySuggestionResult
{
    public required string Value { get; init; }
    public string? Code { get; init; }
    public string? Alfa2 { get; init; }
    public string? Alfa3 { get; init; }
    public string? NameShort { get; init; }
    public string? Name { get; init; }
}

public sealed class GetAddressResult
{
    public required IReadOnlyList<AddressSuggestionResult> Suggestions { get; init; }
}

public sealed class FindAddressByIpResult
{
    public AddressSuggestionResult? Location { get; init; }
    public string? Message { get; init; }
}

public sealed class AddressSuggestionResult
{
    public required string Value { get; init; }
    public string? UnrestrictedValue { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? Region { get; init; }
    public string? City { get; init; }
    public string? Street { get; init; }
    public string? House { get; init; }
    public string? GeoLat { get; init; }
    public string? GeoLon { get; init; }
    public string? FiasId { get; init; }
}

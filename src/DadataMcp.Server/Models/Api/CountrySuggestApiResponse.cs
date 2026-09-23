using System.Text.Json;
using System.Text.Json.Serialization;

namespace DadataMcp.Server.Models.Api;

public sealed class CountrySuggestApiResponse
{
    [JsonPropertyName("suggestions")]
    public IReadOnlyList<CountrySuggestionApiItem>? Suggestions { get; init; }
}

public sealed class CountrySuggestionApiItem
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("data")]
    public CountrySuggestionData? Data { get; init; }
}

public sealed class CountrySuggestionData
{
    [JsonPropertyName("code")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Code { get; init; }

    [JsonPropertyName("alfa2")]
    public string? Alfa2 { get; init; }

    [JsonPropertyName("alfa3")]
    public string? Alfa3 { get; init; }

    [JsonPropertyName("name_short")]
    public string? NameShort { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var number)
                ? number.ToString()
                : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => reader.GetString()
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}

using System.Text.Json;

namespace DadataMcp.Server.Logging;

public static class ToolCallLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Write(string toolName, object parameters, string status, int? httpStatus = null)
    {
        var payload = JsonSerializer.Serialize(parameters, JsonOptions);
        var line = httpStatus is { } code
            ? $"[{toolName}] params={payload} status={status} http={code}"
            : $"[{toolName}] params={payload} status={status}";

        Console.Error.WriteLine(line);
    }
}

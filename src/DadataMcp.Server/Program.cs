using System.Text;
using DadataMcp.Server.Client;
using DadataMcp.Server.Configuration;
using DadataMcp.Server.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace DadataMcp.Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services.AddSingleton(DaDataOptions.FromProcess());
        builder.Services.AddSingleton<IDaDataClient, DaDataClient>();

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "dadata-mcp",
                    Version = "1.0.0",
                    Title = "DaData MCP"
                };
                options.ServerInstructions =
                    "Локальный MCP-сервер DaData. " +
                    "Используй get_country для страны по названию или ISO-коду, " +
                    "get_address для адреса по координатам, find_address_by_ip для города по IP. " +
                    "Ключи API читаются из файла .env и не запрашиваются в чате.";
            })
            .WithStdioServerTransport()
            .WithTools<DaDataTools>();

        await builder.Build().RunAsync();
    }
}

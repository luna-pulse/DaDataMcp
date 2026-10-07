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

        // Ключ DaData живёт только в памяти процесса: ни .env, ни переменных окружения.
        // Процесс стартует без ключа: форма открывается при первом вызове tool, а не при включении MCP.
        // Сроки сессии можно переопределить (DADATA_SESSION_TTL_MINUTES и др.), сам ключ там не хранится.
        builder.Services.AddSingleton(ApiKeySessionOptions.FromConfiguration(builder.Configuration));
        builder.Services.AddSingleton<ApiKeySession>();
        builder.Services.AddSingleton<ApiKeyGate>();
        builder.Services.AddSingleton<IDaDataClient, DaDataClient>();

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "dadata-mcp",
                    Version = "1.0.0",
                    Title = "DaData MCP",
                    Description =
                        "Локальный MCP-сервер подсказок DaData: страна по названию или ISO-коду, адрес по координатам и город по IP.",
                    Icons =
                    [
                        new Icon
                        {
                            Source = LogoDataUri(),
                            MimeType = "image/png",
                            Sizes = ["18x18"]
                        }
                    ]
                };
                options.ServerInstructions =
                    "Локальный MCP-сервер DaData. " +
                    "Используй get_country для страны по названию или ISO-коду, " +
                    "get_address для адреса по координатам, find_address_by_ip для города по IP. " +
                    "API-ключ DaData пользователь вводит в форме, которую сервер сам открывает в чате: " +
                    "не спрашивай ключ текстом, не передавай его в аргументы tools и не копируй в ответ.";
            })
            .WithStdioServerTransport()
            .WithTools<DaDataTools>();

        using var host = builder.Build();

        // Страховка для graceful shutdown. Основной механизм: процесс завершается по EOF stdin,
        // и ключ исчезает вместе с ним.
        var session = host.Services.GetRequiredService<ApiKeySession>();
        host.Services.GetRequiredService<IHostApplicationLifetime>()
            .ApplicationStopping.Register(session.Clear);

        await host.RunAsync();
    }

    private static string LogoDataUri()
    {
        var assembly = typeof(Program).Assembly;
        using var stream = assembly.GetManifestResourceStream("DadataMcp.Server.logo.png")
            ?? throw new InvalidOperationException("В сборке нет ресурса logo.png.");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return "data:image/png;base64," + Convert.ToBase64String(buffer.ToArray());
    }
}

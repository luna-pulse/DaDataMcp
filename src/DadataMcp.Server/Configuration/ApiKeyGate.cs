using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DadataMcp.Server.Configuration;

/// <summary>
/// Единственная точка получения API-ключа: если сессия пустая или протухла,
/// открывает форму в чате Cursor через elicitation (mode: form) и кладёт ответ в <see cref="ApiKeySession"/>.
/// Вызывается только из tools. Это не MCP tool: агент ключ не видит и в аргументы не передаёт.
/// </summary>
public sealed class ApiKeyGate
{
    private const string FieldName = "apiKey";

    private readonly ApiKeySession _session;
    private readonly TimeSpan _formTimeout;
    private readonly SemaphoreSlim _formLock = new(1, 1);

    public ApiKeyGate(ApiKeySession session, ApiKeySessionOptions options)
    {
        _session = session;
        _formTimeout = options.FormTimeout;
    }

    /// <summary>
    /// Возвращает <see langword="true"/>, если после вызова в сессии лежит живой ключ.
    /// Форма показывается только когда ключа нет; параллельные вызовы ждут одну и ту же форму.
    /// Ожидание формы (и очереди за чужой формой) ограничено таймаутом: по его истечении возвращается <see langword="false"/>.
    /// </summary>
    public async Task<bool> EnsureApiKeyAsync(McpServer server, CancellationToken cancellationToken)
    {
        if (_session.TryGetApiKey(out _))
        {
            return true;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_formTimeout);

        try
        {
            await _formLock.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (IsTimeout(cancellationToken, timeoutCts))
        {
            Console.Error.WriteLine("[api-key] таймаут ожидания очереди за формой ключа");
            return false;
        }

        try
        {
            // Пока ждали форму, её мог заполнить параллельный вызов.
            if (_session.TryGetApiKey(out _))
            {
                return true;
            }

            if (server.ClientCapabilities?.Elicitation is null)
            {
                Console.Error.WriteLine("[api-key] клиент не поддерживает elicitation, форму показать нельзя");
                return false;
            }

            Console.Error.WriteLine("[api-key] запрос формы");

            ElicitResult result;
            try
            {
                result = await server.ElicitAsync(
                    new ElicitRequestParams
                    {
                        Mode = "form",
                        Message = "Введите API-ключ DaData.",
                        RequestedSchema = new ElicitRequestParams.RequestSchema
                        {
                            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                            {
                                [FieldName] = new ElicitRequestParams.StringSchema
                                {
                                    Title = "API-ключ DaData",
                                    Description = "Токен из личного кабинета dadata.ru → API-ключи",
                                    MinLength = 10,
                                    MaxLength = 200
                                }
                            },
                            Required = [FieldName]
                        }
                    },
                    timeoutCts.Token);
            }
            catch (OperationCanceledException) when (IsTimeout(cancellationToken, timeoutCts))
            {
                Console.Error.WriteLine(
                    $"[api-key] форма не отвечает дольше {ApiKeySession.Format(_formTimeout)}, ключ не сохранён");
                return false;
            }

            Console.Error.WriteLine($"[api-key] ответ получен (action={result.Action})");

            if (!result.IsAccepted
                || result.Content is null
                || !result.Content.TryGetValue(FieldName, out var value)
                || value.ValueKind != JsonValueKind.String)
            {
                Console.Error.WriteLine($"[api-key] форма закрыта без ключа (action={result.Action})");
                return false;
            }

            var apiKey = value.GetString()?.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.Error.WriteLine("[api-key] форма отправлена с пустым ключом");
                return false;
            }

            _session.Set(apiKey);
            Console.Error.WriteLine($"[api-key] ключ принят, сессия на {ApiKeySession.Format(_session.TimeToLive)}");
            return true;
        }
        finally
        {
            _formLock.Release();
        }
    }

    private static bool IsTimeout(CancellationToken callToken, CancellationTokenSource timeoutCts) =>
        !callToken.IsCancellationRequested && timeoutCts.IsCancellationRequested;
}

namespace DadataMcp.Server.Configuration;

/// <summary>
/// Настройки сессии ключа. Сам ключ здесь не хранится: только сроки.
/// Переопределяются переменными окружения или параметрами запуска (например, <c>--DADATA_SESSION_TTL_MINUTES=2</c>).
/// </summary>
public sealed class ApiKeySessionOptions
{
    public const string TtlMinutesKey = "DADATA_SESSION_TTL_MINUTES";
    public const string RenewMinutesKey = "DADATA_SESSION_RENEW_MINUTES";
    public const string FormTimeoutSecondsKey = "DADATA_FORM_TIMEOUT_SECONDS";

    /// <summary>Сколько живёт ключ без продления.</summary>
    public TimeSpan TimeToLive { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Если до конца сессии осталось не больше этого срока, вызов tool сдвигает отсчёт заново.</summary>
    public TimeSpan RenewThreshold { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Сколько ждать ответа на форму ключа.</summary>
    public TimeSpan FormTimeout { get; init; } = TimeSpan.FromMinutes(3);

    public static ApiKeySessionOptions FromConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var defaults = new ApiKeySessionOptions();
        return new ApiKeySessionOptions
        {
            TimeToLive = ReadSpan(configuration, TtlMinutesKey, TimeSpan.FromMinutes, defaults.TimeToLive),
            RenewThreshold = ReadSpan(configuration, RenewMinutesKey, TimeSpan.FromMinutes, defaults.RenewThreshold),
            FormTimeout = ReadSpan(configuration, FormTimeoutSecondsKey, TimeSpan.FromSeconds, defaults.FormTimeout)
        };
    }

    private static TimeSpan ReadSpan(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string key,
        Func<double, TimeSpan> factory,
        TimeSpan fallback)
    {
        var raw = configuration[key];
        if (!string.IsNullOrWhiteSpace(raw)
            && double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            && value > 0)
        {
            return factory(value);
        }

        return fallback;
    }
}

/// <summary>
/// API-ключ DaData в памяти процесса. Ни на диск, ни в переменные окружения, ни в логи не попадает.
/// Сессия живёт до отключения MCP (конец процесса) или 24 часа (по умолчанию).
/// Вызов tool в последний час продлевает срок ещё на 24 часа с этого момента.
/// </summary>
public sealed class ApiKeySession
{
    private readonly object _sync = new();
    private readonly TimeSpan _timeToLive;
    private readonly TimeSpan _renewThreshold;
    private string? _apiKey;
    private DateTimeOffset _storedAt;

    public ApiKeySession(ApiKeySessionOptions options)
    {
        _timeToLive = options.TimeToLive;
        _renewThreshold = options.RenewThreshold;
    }

    public TimeSpan TimeToLive => _timeToLive;

    public bool TryGetApiKey(out string apiKey)
    {
        lock (_sync)
        {
            if (_apiKey is null)
            {
                apiKey = string.Empty;
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            var age = now - _storedAt;
            if (age >= _timeToLive)
            {
                _apiKey = null;
                apiKey = string.Empty;
                Console.Error.WriteLine("[api-key] сессия истекла, ключ сброшен");
                return false;
            }

            if (_timeToLive - age <= _renewThreshold)
            {
                _storedAt = now;
                Console.Error.WriteLine($"[api-key] продлен на {Format(_timeToLive)}");
            }

            apiKey = _apiKey;
            return true;
        }
    }

    public void Set(string apiKey)
    {
        lock (_sync)
        {
            _apiKey = apiKey;
            _storedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _apiKey = null;
        }
    }

    public static string Format(TimeSpan span) =>
        span.TotalHours >= 1 ? $"{span.TotalHours:0.##} ч" : span.TotalMinutes >= 1 ? $"{span.TotalMinutes:0.##} мин" : $"{span.TotalSeconds:0.##} с";
}

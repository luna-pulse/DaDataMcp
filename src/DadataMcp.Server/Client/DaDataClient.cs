using System.Text.Json;
using DadataMcp.Server.Configuration;
using DadataMcp.Server.Models.Api;
using Flurl.Http;

namespace DadataMcp.Server.Client;

public sealed class DaDataClient : IDaDataClient, IDisposable
{
    private const string SuggestionsBaseUrl = "https://suggestions.dadata.ru";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IFlurlClient _suggestions;
    private readonly ApiKeySession _session;

    public DaDataClient(ApiKeySession session)
    {
        _session = session;
        _suggestions = new FlurlClient(SuggestionsBaseUrl)
            .WithHeader("Accept", "application/json");
    }

    public Task<CountrySuggestApiResponse> SuggestCountryAsync(
        string query,
        int count,
        CancellationToken cancellationToken)
    {
        return PostAsync<CountrySuggestApiResponse>(
            "suggestions/api/4_1/rs/suggest/country",
            new { query, count },
            cancellationToken);
    }

    public Task<GeolocateApiResponse> GeolocateAddressAsync(
        double lat,
        double lon,
        int count,
        int radiusMeters,
        CancellationToken cancellationToken)
    {
        return PostAsync<GeolocateApiResponse>(
            "suggestions/api/4_1/rs/geolocate/address",
            new { lat, lon, count, radius_meters = radiusMeters },
            cancellationToken);
    }

    public Task<IplocateApiResponse> IplocateAddressAsync(
        string ip,
        CancellationToken cancellationToken)
    {
        return PostAsync<IplocateApiResponse>(
            "suggestions/api/4_1/rs/iplocate/address",
            new { ip },
            cancellationToken);
    }

    public void Dispose()
    {
        _suggestions.Dispose();
    }

    private async Task<T> PostAsync<T>(
        string path,
        object body,
        CancellationToken cancellationToken)
        where T : class, new()
    {
        var request = Request(path);

        try
        {
            using var response = await request.PostJsonAsync(body, cancellationToken: cancellationToken);
            var json = await response.GetStringAsync();

            if (string.IsNullOrWhiteSpace(json))
            {
                return new T();
            }

            return JsonSerializer.Deserialize<T>(json, ResponseJsonOptions) ?? new T();
        }
        catch (FlurlHttpException ex)
        {
            throw Wrap(ex);
        }
    }

    /// <summary>
    /// Ключ ставится на каждый запрос из текущей сессии: после повторного ввода уйдёт уже новый токен.
    /// </summary>
    private IFlurlRequest Request(string path)
    {
        if (!_session.TryGetApiKey(out var apiKey))
        {
            throw new DaDataClientException(401, "DaData: API-ключ не введён. Повторите вызов и заполните форму в чате.");
        }

        return _suggestions
            .Request(path)
            .WithHeader("Authorization", $"Token {apiKey}");
    }

    private DaDataClientException Wrap(FlurlHttpException ex)
    {
        var status = ex.StatusCode ?? 0;
        if (status == 401)
        {
            // Ключ отвергнут — сбрасываем сессию, следующий вызов снова откроет форму.
            _session.Clear();
        }

        var message = status switch
        {
            400 => "DaData: некорректный запрос.",
            401 => "DaData: неверный API-ключ. Сессия сброшена, при следующем вызове форма откроется снова.",
            403 => "DaData: почта не подтверждена или недостаточно средств на балансе.",
            405 => "DaData: метод отличается от POST.",
            429 => "DaData: слишком много запросов. Повторите позже.",
            >= 500 => "DaData: внутренняя ошибка сервиса.",
            _ => $"DaData: HTTP {status}."
        };

        return new DaDataClientException(status, message, ex);
    }
}

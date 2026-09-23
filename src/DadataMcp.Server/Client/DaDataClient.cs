using DadataMcp.Server.Configuration;
using DadataMcp.Server.Models.Api;
using Flurl.Http;

namespace DadataMcp.Server.Client;

public sealed class DaDataClient : IDaDataClient, IDisposable
{
    private const string SuggestionsBaseUrl = "https://suggestions.dadata.ru";

    private readonly IFlurlClient _suggestions;

    public DaDataClient(DaDataOptions options)
    {
        _suggestions = new FlurlClient(SuggestionsBaseUrl)
            .WithHeader("Authorization", $"Token {options.ApiKey}")
            .WithHeader("Accept", "application/json");
    }

    public async Task<CountrySuggestApiResponse> SuggestCountryAsync(
        string query,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _suggestions
                .Request("suggestions/api/4_1/rs/suggest/country")
                .PostJsonAsync(new { query, count }, cancellationToken: cancellationToken)
                .ReceiveJson<CountrySuggestApiResponse>();

            return result ?? new CountrySuggestApiResponse();
        }
        catch (FlurlHttpException ex)
        {
            throw await WrapAsync(ex);
        }
    }

    public async Task<GeolocateApiResponse> GeolocateAddressAsync(
        double lat,
        double lon,
        int count,
        int radiusMeters,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _suggestions
                .Request("suggestions/api/4_1/rs/geolocate/address")
                .PostJsonAsync(
                    new { lat, lon, count, radius_meters = radiusMeters },
                    cancellationToken: cancellationToken)
                .ReceiveJson<GeolocateApiResponse>();

            return result ?? new GeolocateApiResponse();
        }
        catch (FlurlHttpException ex)
        {
            throw await WrapAsync(ex);
        }
    }

    public async Task<IplocateApiResponse> IplocateAddressAsync(
        string ip,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _suggestions
                .Request("suggestions/api/4_1/rs/iplocate/address")
                .PostJsonAsync(new { ip }, cancellationToken: cancellationToken)
                .ReceiveJson<IplocateApiResponse>();

            return result ?? new IplocateApiResponse();
        }
        catch (FlurlHttpException ex)
        {
            throw await WrapAsync(ex);
        }
    }

    public void Dispose()
    {
        _suggestions.Dispose();
    }

    private static async Task<DaDataClientException> WrapAsync(FlurlHttpException ex)
    {
        var status = ex.StatusCode ?? 0;
        var message = status switch
        {
            400 => "DaData: некорректный запрос.",
            401 => "DaData: отсутствует или неверный API-ключ. Проверьте значение в карточке MCP в Cursor.",
            403 => "DaData: почта не подтверждена или недостаточно средств на балансе.",
            405 => "DaData: метод отличается от POST.",
            429 => "DaData: слишком много запросов. Повторите позже.",
            >= 500 => "DaData: внутренняя ошибка сервиса.",
            _ => $"DaData: HTTP {status}."
        };

        _ = await ex.GetResponseStringAsync();
        return new DaDataClientException(status, message, ex);
    }
}

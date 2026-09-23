using DadataMcp.Server.Models.Api;

namespace DadataMcp.Server.Client;

public interface IDaDataClient
{
    Task<CountrySuggestApiResponse> SuggestCountryAsync(
        string query,
        int count,
        CancellationToken cancellationToken);

    Task<GeolocateApiResponse> GeolocateAddressAsync(
        double lat,
        double lon,
        int count,
        int radiusMeters,
        CancellationToken cancellationToken);

    Task<IplocateApiResponse> IplocateAddressAsync(
        string ip,
        CancellationToken cancellationToken);
}

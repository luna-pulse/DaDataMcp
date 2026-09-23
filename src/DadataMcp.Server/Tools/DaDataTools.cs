using System.ComponentModel;
using System.Net;
using DadataMcp.Server.Client;
using DadataMcp.Server.Logging;
using DadataMcp.Server.Models;
using DadataMcp.Server.Models.Api;
using ModelContextProtocol.Server;

namespace DadataMcp.Server.Tools;

[McpServerToolType]
public sealed class DaDataTools
{
    private readonly IDaDataClient _client;

    public DaDataTools(IDaDataClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "get_country", Title = "Страна по названию или коду", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Ищет страны по справочнику ISO 3166 / ОКСМ: название, цифровой код, альфа-2, альфа-3. " +
        "Вызывай, когда пользователь спрашивает страну, ISO-код или код ОКСМ. " +
        "Не запрашивай API-ключ в чате.")]
    public async Task<GetCountryResult> GetCountry(
        [Description("Запрос одной строкой: «та», «Россия», «TH», «643». Поиск по названию и кодам.")]
        string query,
        [Description("Количество результатов, от 1 до 20. По умолчанию 10.")]
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        var parameters = new { query, count };
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ToolCallLog.Write("get_country", parameters, "error");
                throw new ArgumentException("Параметр query обязателен.");
            }

            if (query.Trim().Length > 300)
            {
                ToolCallLog.Write("get_country", parameters, "error");
                throw new ArgumentException("Параметр query не длиннее 300 символов.");
            }

            if (count is < 1 or > 20)
            {
                ToolCallLog.Write("get_country", parameters, "error");
                throw new ArgumentOutOfRangeException(nameof(count), "count должен быть от 1 до 20.");
            }

            var response = await _client.SuggestCountryAsync(query.Trim(), count, cancellationToken);
            var suggestions = (response.Suggestions ?? [])
                .Select(MapCountry)
                .ToList();

            ToolCallLog.Write("get_country", parameters, "success");
            return new GetCountryResult
            {
                Suggestions = suggestions,
                Message = suggestions.Count == 0 ? "Страна по запросу не найдена." : null
            };
        }
        catch (DaDataClientException ex)
        {
            ToolCallLog.Write("get_country", parameters, "error", ex.StatusCode);
            throw;
        }
        catch (Exception)
        {
            ToolCallLog.Write("get_country", parameters, "error");
            throw;
        }
    }

    [McpServerTool(Name = "get_address", Title = "Адрес по координатам", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Находит ближайшие адреса в России по географическим координатам (обратное геокодирование). " +
        "Вызывай, когда известны широта и долгота. Не запрашивай API-ключ в чате.")]
    public async Task<GetAddressResult> GetAddress(
        [Description("Географическая широта, например 55.878")]
        double lat,
        [Description("Географическая долгота, например 37.653")]
        double lon,
        [Description("Количество результатов, от 1 до 20. По умолчанию 10.")]
        int count = 10,
        [Description("Радиус поиска в метрах, от 1 до 1000. По умолчанию 100.")]
        int radiusMeters = 100,
        CancellationToken cancellationToken = default)
    {
        var parameters = new { lat, lon, count, radiusMeters };
        try
        {
            if (count is < 1 or > 20)
            {
                ToolCallLog.Write("get_address", parameters, "error");
                throw new ArgumentOutOfRangeException(nameof(count), "count должен быть от 1 до 20.");
            }

            if (radiusMeters is < 1 or > 1000)
            {
                ToolCallLog.Write("get_address", parameters, "error");
                throw new ArgumentOutOfRangeException(nameof(radiusMeters), "radiusMeters должен быть от 1 до 1000.");
            }

            var response = await _client.GeolocateAddressAsync(lat, lon, count, radiusMeters, cancellationToken);
            var suggestions = (response.Suggestions ?? [])
                .Select(MapSuggestion)
                .ToList();

            ToolCallLog.Write("get_address", parameters, "success");
            return new GetAddressResult { Suggestions = suggestions };
        }
        catch (DaDataClientException ex)
        {
            ToolCallLog.Write("get_address", parameters, "error", ex.StatusCode);
            throw;
        }
        catch (Exception)
        {
            ToolCallLog.Write("get_address", parameters, "error");
            throw;
        }
    }

    [McpServerTool(Name = "find_address_by_ip", Title = "Адрес по IP", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Определяет город и адрес по IPv4 или IPv6. Вызывай, когда пользователь даёт IP-адрес " +
        "или просит узнать город посетителя. Не запрашивай API-ключ в чате.")]
    public async Task<FindAddressByIpResult> FindAddressByIp(
        [Description("IPv4 или IPv6, например 46.226.227.20")]
        string ip,
        CancellationToken cancellationToken)
    {
        var parameters = new { ip };
        try
        {
            if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip.Trim(), out _))
            {
                ToolCallLog.Write("find_address_by_ip", parameters, "error");
                throw new ArgumentException("Параметр ip должен быть корректным IPv4 или IPv6 адресом.");
            }

            var response = await _client.IplocateAddressAsync(ip.Trim(), cancellationToken);
            if (response.Location is null)
            {
                ToolCallLog.Write("find_address_by_ip", parameters, "success");
                return new FindAddressByIpResult
                {
                    Location = null,
                    Message = "Город по указанному IP определить не удалось."
                };
            }

            ToolCallLog.Write("find_address_by_ip", parameters, "success");
            return new FindAddressByIpResult
            {
                Location = MapSuggestion(response.Location)
            };
        }
        catch (DaDataClientException ex)
        {
            ToolCallLog.Write("find_address_by_ip", parameters, "error", ex.StatusCode);
            throw;
        }
        catch (Exception)
        {
            ToolCallLog.Write("find_address_by_ip", parameters, "error");
            throw;
        }
    }

    private static CountrySuggestionResult MapCountry(CountrySuggestionApiItem item)
    {
        var data = item.Data;
        return new CountrySuggestionResult
        {
            Value = item.Value ?? string.Empty,
            Code = data?.Code,
            Alfa2 = data?.Alfa2,
            Alfa3 = data?.Alfa3,
            NameShort = data?.NameShort,
            Name = data?.Name
        };
    }

    private static AddressSuggestionResult MapSuggestion(SuggestionApiItem item)
    {
        var data = item.Data;
        return new AddressSuggestionResult
        {
            Value = item.Value ?? string.Empty,
            UnrestrictedValue = item.UnrestrictedValue,
            PostalCode = data?.PostalCode,
            Country = data?.Country,
            Region = data?.RegionWithType,
            City = data?.CityWithType ?? data?.City,
            Street = data?.StreetWithType,
            House = data?.House,
            GeoLat = data?.GeoLat,
            GeoLon = data?.GeoLon,
            FiasId = data?.FiasId
        };
    }
}

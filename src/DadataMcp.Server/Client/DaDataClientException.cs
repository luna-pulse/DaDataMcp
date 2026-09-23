namespace DadataMcp.Server.Client;

public sealed class DaDataClientException : Exception
{
    public int StatusCode { get; }

    public DaDataClientException(int statusCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}

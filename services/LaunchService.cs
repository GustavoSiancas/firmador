using System.Web;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public class LaunchService
{
    public LaunchParameters GetLaunchParameters(string[] args)
    {
#if DEBUG
        if (args.Length == 0)
            return GetDevelopmentParameters();
#endif

        if (args.Length != 1)
            throw new Exception("Esta aplicación solo puede iniciarse desde el sistema web.");

        Uri uri = new(args[0]);
        if (uri.Scheme != "firmaapp")
            throw new Exception("Protocolo no válido.");

        var query = HttpUtility.ParseQueryString(uri.Query);
        string token = query["token"]
            ?? throw new Exception("No se recibió el parámetro token.");

        if (string.IsNullOrWhiteSpace(token))
            throw new Exception("El parámetro token no es válido.");

        return new LaunchParameters
        {
            SessionEndpoint = GetEndpoint(query["sessionEndpoint"], "sessionEndpoint"),
            Token = token
        };
    }

#if DEBUG
    private static LaunchParameters GetDevelopmentParameters() => new()
    {
        SessionEndpoint = new Uri("http://127.0.0.1:8080/api/documents"),
        Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOjYwLCJ0eXBlIjoiZXh0ZXJuYWwiLCJpYXQiOjE3ODk0MDAxNTMsImV4cCI6MTc5MDAwNDk1M30.wjXoicMeJ5oc5VZnZuUxxDm6UbBEltXfjCD7uB6DKZs",
        IsDevelopment = true
    };
#endif

    private static Uri GetEndpoint(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new Exception($"No se recibió el parámetro {parameterName}.");

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
        {
            throw new Exception($"El parámetro {parameterName} no es una URL HTTP válida.");
        }

        return endpoint;
    }
}

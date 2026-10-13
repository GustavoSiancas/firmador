using System.Web;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public class LaunchService
{
    public LaunchParameters GetLaunchParameters(string[] args)
    {
        if (args.Length != 1)
            throw new LaunchParameterException("Esta aplicación solo puede iniciarse desde el sistema web.");

        if (!Uri.TryCreate(args[0], UriKind.Absolute, out Uri? uri) || uri.Scheme != "firmaapp")
            throw new LaunchParameterException("Protocolo no válido.");

        var query = HttpUtility.ParseQueryString(uri.Query);
        string token = query["token"]
            ?? throw new LaunchParameterException("No se recibió el parámetro token.");

        if (string.IsNullOrWhiteSpace(token))
            throw new LaunchParameterException("El parámetro token no es válido.");

        return new LaunchParameters
        {
            SessionEndpoint = GetEndpoint(query["sessionEndpoint"], "sessionEndpoint"),
            Token = token
        };
    }

    private static Uri GetEndpoint(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new LaunchParameterException($"No se recibió el parámetro {parameterName}.");

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
        {
            throw new LaunchParameterException($"El parámetro {parameterName} no es una URL HTTP válida.");
        }

        return endpoint;
    }
}

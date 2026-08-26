using System.Web;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public class LaunchService
{
    public LaunchParameters GetLaunchParameters(string[] args)
    {
        // La aplicación solo puede iniciarse mediante el protocolo firmaapp://
        if (args.Length != 1)
        {
            throw new Exception("Esta aplicación solo puede iniciarse desde el sistema web.");
        }

        Uri uri = new Uri(args[0]);

        if (uri.Scheme != "firmaapp")
        {
            throw new Exception("Protocolo no válido.");
        }

        var query = HttpUtility.ParseQueryString(uri.Query);

        Uri inputEndpoint = GetEndpoint(query["inputEndpoint"], "inputEndpoint");
        Uri outputEndpoint = GetEndpoint(query["outputEndpoint"], "outputEndpoint");

        string fileId = query["fileId"]
            ?? throw new Exception("No se recibió el parámetro fileId.");

        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new Exception("El parámetro fileId no es válido.");
        }

        string token = query["token"]
            ?? throw new Exception("No se recibió el parámetro token.");

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("El parámetro token no es válido.");
        }

        return new LaunchParameters
        {
            InputEndpoint = inputEndpoint,
            OutputEndpoint = outputEndpoint,
            FileId = fileId,
            Token = token
        };
    }

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

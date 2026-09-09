using System.Web;
using System.Globalization;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public class LaunchService
{
    public LaunchParameters GetLaunchParameters(string[] args)
    {
#if DEBUG
        // Permite iniciar directamente desde Visual Studio durante el desarrollo.
        // Los valores se mantienen fuera del código para no publicar tokens ni IDs reales.
        if (args.Length == 0)
        {
            return GetDevelopmentParameters();
        }
#endif

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
            Token = token,
            X = GetCoordinate(query["x"], "x", LaunchParameters.DefaultX),
            Y = GetCoordinate(query["y"], "y", LaunchParameters.DefaultY),
            Clean = GetClean(query["clean"])
        };
    }

#if DEBUG
    private static LaunchParameters GetDevelopmentParameters()
    {
        return new LaunchParameters
        {
            InputEndpoint = new Uri("https://backend.cal.org.pe/servicios-cal-dev/documents/get-document-artifact-original"),
            OutputEndpoint = new Uri("https://backend.cal.org.pe/servicios-cal-dev/documents/upload-document-artifact-version-signed"),
            FileId = "a76e6ea9-6322-48e0-8a82-befd24a87a7e",
            Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOjYwLCJ0eXBlIjoiZXh0ZXJuYWwiLCJpYXQiOjE3ODg1MzYzODEsImV4cCI6MTc4OTE0MTE4MX0.AKKz_IetGSOUBWJdRnzgIhDXOCJ3PHpy2h4xxZHY05c",
            Clean=true
        };
    }
#endif

    private static bool GetClean(string? value)
    {
        if (value is null)
            return false;

        if (!bool.TryParse(value, out bool clean))
            throw new ArgumentException("El parámetro clean debe ser true o false.");

        return clean;
    }

    private static float GetCoordinate(string? value, string parameterName, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float coordinate) ||
            !float.IsFinite(coordinate))
        {
            throw new Exception($"El parámetro {parameterName} debe ser una coordenada numérica válida.");
        }

        return coordinate;
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

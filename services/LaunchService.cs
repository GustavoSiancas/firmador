using System.Web;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public class LaunchService
{
    public LaunchParameters GetLaunchParameters(string[] args)
    {
#if DEBUG
        // Modo desarrollo
        if (args.Length == 0)
        {
            return new LaunchParameters
            {   
                Backend = "https://backend.cal.org.pe/servicios-cal-dev",
                DocumentArtifactId = "48a69b61-5879-40c8-81c2-5126dec00584",
                Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOjIxLCJ0eXBlIjoiZXh0ZXJuYWwiLCJpYXQiOjE3ODU3Njc4ODMsImV4cCI6MTc4NjM3MjY4M30.PTI8y8dUztrp8U1l0hQPMkTpV9NYkYSKQ3f9v4JtdzU"
            };
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

        string backend = query["backend"]
            ?? throw new Exception("No se recibió el parámetro backend.");

        string documentArtifactId = query["documentArtifactId"]
            ?? throw new Exception("No se recibió el parámetro documentArtifactId.");

        if (string.IsNullOrWhiteSpace(documentArtifactId))
        {
            throw new Exception("El parámetro documentArtifactId no es válido.");
        }

        string token = query["token"]
            ?? throw new Exception("No se recibió el parámetro token.");

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("El parámetro token no es válido.");
        }

        return new LaunchParameters
        {
            Backend = backend,
            DocumentArtifactId = documentArtifactId,
            Token = token
        };
    }
}

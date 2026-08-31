using System.Net.Http.Headers;
using System.Text.Json;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public class ApiService
{
    private readonly HttpClient _apiClient;
    private readonly Uri _inputEndpoint;
    private readonly Uri _outputEndpoint;

    private readonly string _fileId;

    public ApiService(
        Uri inputEndpoint,
        Uri outputEndpoint,
        string fileId,
        string token)
    {
        _apiClient = new HttpClient();
        _inputEndpoint = inputEndpoint;
        _outputEndpoint = outputEndpoint;
        _fileId = fileId;

        _apiClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    // El endpoint de entrada devuelve directamente el PDF en binario.
    public async Task<byte[]> GetDocumentPdfAsync()
    {
        Uri requestUri = AppendPathSegment(_inputEndpoint, _fileId);
        var response = await _apiClient.GetAsync(requestUri);

        if (!response.IsSuccessStatusCode)
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception(GetBackendErrorMessage(responseBody, response));
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<UpdateDocumentResponse> UpdateDocumentAsync(byte[] pdfBytes)
    {
        using var form = new MultipartFormDataContent();    

        // Archivo PDF
        using var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/pdf");

        form.Add(
            fileContent,
            "file",
            "documento-firmado.pdf"
        );

        // Campo fileId
        form.Add(
            new StringContent(_fileId),
            "fileId"
        );

        // OJO:
        // Ya no agregamos _fileId a la URL.
        // Se manda dentro del multipart/form-data.
        Uri requestUri = _outputEndpoint;

        var response = await _apiClient.PostAsync(requestUri, form);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                GetBackendErrorMessage(responseBody, response)
            );
        }

        return JsonSerializer.Deserialize<UpdateDocumentResponse>(
            responseBody,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
            ?? throw new Exception("No se recibió respuesta.");
    }

    private static string GetBackendErrorMessage(
        string responseBody,
        HttpResponseMessage response)
    {
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                // message puede ser string o array
                if (root.TryGetProperty("message", out var message))
                {
                    if (message.ValueKind == JsonValueKind.Array)
                    {
                        var errores = message.EnumerateArray()
                            .Select(x => x.GetString())
                            .Where(x => !string.IsNullOrWhiteSpace(x));

                        return string.Join(Environment.NewLine, errores!);
                    }

                    if (message.ValueKind == JsonValueKind.String)
                    {
                        return message.GetString()!;
                    }
                }

                if (root.TryGetProperty("error", out var error))
                    return error.GetString()!;

                if (root.TryGetProperty("detail", out var detail))
                    return detail.GetString()!;
            }
            catch
            {
                return responseBody;
            }

            return responseBody;
        }

        return $"Error {(int)response.StatusCode}: {response.ReasonPhrase}";
    }

    private static Uri AppendPathSegment(Uri endpoint, string value)
    {
        var builder = new UriBuilder(endpoint)
        {
            Path = $"{endpoint.AbsolutePath.TrimEnd('/')}/{Uri.EscapeDataString(value)}"
        };

        return builder.Uri;
    }
}

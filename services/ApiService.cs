using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FirmadorPades.Models;

namespace FirmadorPades.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;

    private readonly string _baseUrl;

    private readonly string _documentId;

    private readonly string _token;

    public ApiService(string baseUrl, string documentId, string token)
    {
        _httpClient = new HttpClient();

        _baseUrl = baseUrl.TrimEnd('/');

        _token = token;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", token);

        _documentId = documentId;
    }

    // obtengo un pdf en bytes
    public async Task<byte[]> GetDocumentPdfAsync()
    {

        var url = $"{_baseUrl}/documents/get-document-artifact-original/{_documentId}";

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception(GetBackendErrorMessage(responseBody, response));
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    // fix
    public async Task<UpdateDocumentResponse> UpdateDocumentAsync(
        byte[] pdfBytes,
        string documentId)
    {
        using var content = new MultipartFormDataContent();

        // documentArtifactId
        content.Add(
            new StringContent(documentId),
            "documentArtifactId");

        // archivo
        var pdfContent = new ByteArrayContent(pdfBytes);

        pdfContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/pdf");

        content.Add(
            pdfContent,
            "file",
            "signed.pdf");

        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/documents/upload-document-artifact-version-signed",
            content);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(GetBackendErrorMessage(responseBody, response));
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
}
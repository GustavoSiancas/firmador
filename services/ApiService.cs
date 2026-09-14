using System.Net.Http.Headers;
using System.Text.Json;
using FirmadorPades.Models;
using iText.Kernel.Pdf;

namespace FirmadorPades.Services;

public class ApiService
{
    private readonly HttpClient _apiClient;
    private readonly Uri _sessionEndpoint;
    private readonly string _token;
    private Uri? _lastRequestUri;

    public TemporaryResourcesResponse? TemporaryResources { get; private set; }
    public IReadOnlyList<TemporarySigningDocument> TemporaryDocuments { get; private set; } = [];
    public string DocumentFileName { get; private set; } = "documento.pdf";

    public ApiService(Uri sessionEndpoint, string token)
    {
        _sessionEndpoint = sessionEndpoint;
        _token = token;
        _apiClient = new HttpClient();
        _apiClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<TemporaryResourcesResponse> GetTemporaryResourcesAsync()
    {
        using var response = await GetAsync(_sessionEndpoint);
        string responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(GetBackendErrorMessage(responseBody, response));

        TemporaryResources = JsonSerializer.Deserialize<TemporaryResourcesResponse>(
            responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("No se recibió la configuración de recursos temporales.");

        return TemporaryResources;
    }

    public async Task<byte[]> GetDocumentPdfAsync(string documentId)
    {
        Uri requestUri = AppendPathSegment(GetTemporaryResources().InputEndpoint, documentId);
        using var response = await GetAsync(requestUri);

        if (!response.IsSuccessStatusCode)
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception(GetBackendErrorMessage(responseBody, response));
        }

        var disposition = response.Content.Headers.ContentDisposition;
        string? name = disposition?.FileNameStar ?? disposition?.FileName;
        DocumentFileName = string.IsNullOrWhiteSpace(name)
            ? "documento.pdf"
            : Path.GetFileName(name.Trim('"').Replace('\\', '/'));

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<IReadOnlyList<TemporarySigningDocument>> GetTemporaryDocumentsAsync()
    {
        TemporaryResourcesResponse resources = GetTemporaryResources();
        var documents = new List<TemporarySigningDocument>(resources.Documents.Count);
        var cleaningService = new PdfCleaningService();
        var previewService = new PdfSignaturePreviewService();

        foreach (TemporaryDocumentResource resource in resources.Documents)
        {
            byte[] pdfBytes = await GetDocumentPdfAsync(resource.Id);
            bool clean = resource.Clean == true;
            if (clean)
                pdfBytes = cleaningService.Clean(pdfBytes);

            SignatureLocation signatureLocation = GetSignatureLocation(pdfBytes, resource);
            documents.Add(new TemporarySigningDocument
            {
                Id = resource.Id,
                FileName = DocumentFileName,
                PdfBytes = pdfBytes,
                PreviewPdfBytes = previewService.Create(pdfBytes, signatureLocation),
                Clean = clean,
                SignatureLocation = signatureLocation
            });
        }

        TemporaryDocuments = documents;
        return TemporaryDocuments;
    }

    public async Task<UpdateDocumentResponse> UpdateDocumentAsync(
        string documentId,
        byte[] pdfBytes)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "documento-firmado.pdf");
        form.Add(new StringContent(documentId), "fileId");

        // El endpoint de salida recibe el id original y el PDF firmado por multipart POST.
        using var response = await _apiClient.PostAsync(GetTemporaryResources().OutputEndpoint, form);
        string responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(GetBackendErrorMessage(responseBody, response));

        return JsonSerializer.Deserialize<UpdateDocumentResponse>(
            responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("No se recibió respuesta al actualizar el documento.");
    }

    private TemporaryResourcesResponse GetTemporaryResources() =>
        TemporaryResources ?? throw new InvalidOperationException(
            "Primero debe obtener los recursos temporales de la sesión.");

    public string GetLastRequestCurl()
    {
        Uri requestUri = _lastRequestUri ?? _sessionEndpoint;
        return $"curl --request GET --url \"{requestUri.AbsoluteUri}\" " +
            $"--header \"Authorization: Bearer {_token}\" " +
            "--header \"Accept: application/json\"";
    }

    private async Task<HttpResponseMessage> GetAsync(Uri requestUri)
    {
        _lastRequestUri = requestUri;
        return await _apiClient.GetAsync(requestUri);
    }

    private static SignatureLocation GetSignatureLocation(
        byte[] pdfBytes,
        TemporaryDocumentResource resource)
    {
        const float signatureWidth = 170;
        const float signatureHeight = 60;
        const float signatureMargin = 20;

        using var stream = new MemoryStream(pdfBytes, writable: false);
        using var pdf = new PdfDocument(new PdfReader(stream));
        int pageCount = pdf.GetNumberOfPages();
        if (pageCount == 0)
            throw new Exception($"El documento {resource.Id} no contiene páginas.");

        int page = resource.Page.HasValue && resource.Page.Value >= 1 && resource.Page.Value <= pageCount
            ? resource.Page.Value
            : pageCount;
        var pageSize = pdf.GetPage(page).GetPageSize();
        float width = Math.Min(signatureWidth, pageSize.GetWidth());
        float height = Math.Min(signatureHeight, pageSize.GetHeight());

        float x = resource.X ?? pageSize.GetRight() - width - signatureMargin;
        float y = resource.Y ?? pageSize.GetBottom() + signatureMargin;

        return new SignatureLocation
        {
            Page = page,
            X = Math.Clamp(x, pageSize.GetLeft(), pageSize.GetRight() - width),
            Y = Math.Clamp(y, pageSize.GetBottom(), pageSize.GetTop() - height),
            Width = width,
            Height = height
        };
    }

    private static string GetBackendErrorMessage(string responseBody, HttpResponseMessage response)
    {
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;
                foreach (string propertyName in new[] { "message", "error", "detail" })
                {
                    if (root.TryGetProperty(propertyName, out var value) &&
                        value.ValueKind == JsonValueKind.String)
                        return value.GetString()!;
                }
            }
            catch (JsonException)
            {
                // El cuerpo no es JSON: se devolverá tal cual.
            }

            return responseBody;
        }

        return $"Error {(int)response.StatusCode}: {response.ReasonPhrase}";
    }

    private static Uri AppendPathSegment(Uri endpoint, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El id del documento no es válido.", nameof(value));

        var builder = new UriBuilder(endpoint)
        {
            Path = $"{endpoint.AbsolutePath.TrimEnd('/')}/{Uri.EscapeDataString(value)}"
        };

        return builder.Uri;
    }
}

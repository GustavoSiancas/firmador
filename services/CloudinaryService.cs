namespace FirmadorPades.Services;

public class CloudinaryService
{
    private readonly HttpClient _httpClient;

    public CloudinaryService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<byte[]> DownloadPdfAsync(string cloudinaryUrl)
    {
        try
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync(cloudinaryUrl);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception(
                $"Error al descargar el PDF.\n" +
                $"URL consultada: {cloudinaryUrl}\n" +
                $"Detalle: {ex.Message}",
                ex);
        }
    }
}
namespace FirmadorPades.Models;
public class LaunchParameters
{
    public Uri InputEndpoint { get; set; } = null!;

    public Uri OutputEndpoint { get; set; } = null!;

    public string FileId { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
}

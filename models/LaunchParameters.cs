namespace FirmadorPades.Models;
public class LaunchParameters
{
    public Uri SessionEndpoint { get; set; } = null!;

    public string Token { get; set; } = string.Empty;

    public bool IsDevelopment { get; set; }
}

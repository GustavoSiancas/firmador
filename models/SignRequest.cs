namespace FirmadorPades.Models;

public class SignRequest
{
    public string InputPdf { get; set; } = "";

    public string OutputPdf { get; set; } = "";

    public string Reason { get; set; } = "Firma Digital";

    public string Location { get; set; } = "Perú";

    public string Contact { get; set; } = "";

    public bool VisibleSignature { get; set; } = false;

    public int PageNumber { get; set; } = 1;
}
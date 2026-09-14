namespace FirmadorPades.Models;

public class TemporaryResourcesResponse
{
    public Uri InputEndpoint { get; set; } = null!;
    public Uri OutputEndpoint { get; set; } = null!;
    public List<TemporaryDocumentResource> Documents { get; set; } = [];
}

public class TemporaryDocumentResource
{
    public string Id { get; set; } = string.Empty;
    public float? X { get; set; }
    public float? Y { get; set; }
    public int? Page { get; set; }
    public bool? Clean { get; set; }
}

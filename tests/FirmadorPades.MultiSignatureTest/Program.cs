using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Models;
using FirmadorPades.Services;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

if (args.Length == 3 && args[0] == "--sign-once-real")
{
    byte[] inputPdf = File.ReadAllBytes(args[1]);
    var certificateService = new CertificateService();
    using X509Certificate2 signingCertificate = certificateService.GetSigningCertificate();
    var stampService = new StampService();
    var realSingleSigner = new PdfSignatureService();
    var realSingleValidator = new PdfValidationService();
    const string reason = "Documento firmado digitalmente";

    var signaturesBefore = realSingleValidator.GetSignatures(inputPdf);
    if (signaturesBefore.Any(signature => !signature.IsValid))
        throw new Exception("El PDF de entrada ya contiene una firma criptográficamente inválida.");

    byte[] signedPdf = realSingleSigner.Sign(
        inputPdf,
        reason,
        signingCertificate,
        stampService.CreateStamp(signingCertificate.Subject, reason),
        new SignatureLocation { Page = 1, X = 50, Y = 20, Width = 170, Height = 60 });

    var signaturesAfter = realSingleValidator.GetSignatures(signedPdf);
    if (signaturesAfter.Count != signaturesBefore.Count + 1 ||
        signaturesAfter.Any(signature => !signature.IsValid))
    {
        throw new Exception("El resultado no conservó íntegras todas las firmas.");
    }

    File.WriteAllBytes(args[2], signedPdf);
    Console.WriteLine(string.Join(
        Environment.NewLine,
        signaturesAfter.Select(signature =>
            $"{signature.SignatureName}: válida={signature.IsValid}, revisión={signature.Revision}/{signature.TotalRevisions}")));
    Console.WriteLine($"Resultado: {Path.GetFullPath(args[2])}");
    return;
}

if (args.Length == 3 && args[0] == "--double-sign-real")
{
    byte[] realCurrentPdf = File.ReadAllBytes(args[1]);
    var certificateService = new CertificateService();
    using X509Certificate2 realCertificate = certificateService.GetSigningCertificate();
    var stampService = new StampService();
    var pdfSigner = new PdfSignatureService();
    var realValidator = new PdfValidationService();
    var firstPlacement = new SignatureLocation { Page = 1, X = 50, Y = 90, Width = 170, Height = 60 };
    var secondPlacement = new SignatureLocation { Page = 1, X = 240, Y = 90, Width = 170, Height = 60 };
    const string reason = "Documento firmado digitalmente";

    realCurrentPdf = pdfSigner.Sign(
        realCurrentPdf, reason, realCertificate,
        stampService.CreateStamp(realCertificate.Subject, reason), firstPlacement);
    var firstValidation = realValidator.GetSignatures(realCurrentPdf);
    Console.WriteLine($"Después de Signature1: {string.Join(", ", firstValidation.Select(s => $"{s.SignatureName}={s.IsValid}"))}");
    if (firstValidation.Any(s => !s.IsValid)) throw new Exception("La primera firma no superó la validación local.");

    realCurrentPdf = pdfSigner.Sign(
        realCurrentPdf, reason, realCertificate,
        stampService.CreateStamp(realCertificate.Subject, reason), secondPlacement);
    var secondValidation = realValidator.GetSignatures(realCurrentPdf);
    Console.WriteLine($"Después de Signature2: {string.Join(", ", secondValidation.Select(s => $"{s.SignatureName}={s.IsValid}, rev={s.Revision}/{s.TotalRevisions}"))}");
    if (secondValidation.Count != 2 || secondValidation.Any(s => !s.IsValid))
        throw new Exception("La doble firma no superó la validación local.");

    File.WriteAllBytes(args[2], realCurrentPdf);
    Console.WriteLine($"Resultado: {args[2]} ({realCurrentPdf.Length} bytes)");
    return;
}

if (args.Length == 1)
{
    byte[] inspectedPdf = File.ReadAllBytes(args[0]);
    var inspected = new PdfValidationService().GetSignatures(inspectedPdf);
    using var inspectedDocument = new PdfDocument(new PdfReader(new MemoryStream(inspectedPdf)));
    var signatureUtil = new iText.Signatures.SignatureUtil(inspectedDocument);
    foreach (var signature in inspected)
    {
        var dictionary = signatureUtil.GetSignatureDictionary(signature.SignatureName);
        Console.WriteLine(
            $"{signature.SignatureName}: valid={signature.IsValid}, revision={signature.Revision}/{signature.TotalRevisions}, " +
            $"coversFinal={signature.CoversWholeDocument}, algorithm={signature.Algorithm}, " +
            $"byteRange={dictionary?.GetAsArray(PdfName.ByteRange)}");
        using Stream revisionStream = signatureUtil.ExtractRevision(signature.SignatureName);
        using var revisionMemory = new MemoryStream();
        revisionStream.CopyTo(revisionMemory);
        var revisionValidation = new PdfValidationService().GetSignatures(revisionMemory.ToArray());
        var matchingRevision = revisionValidation.First(item => item.SignatureName == signature.SignatureName);
        Console.WriteLine(
            $"  extractedLength={revisionMemory.Length}, validInOwnRevision={matchingRevision.IsValid}, " +
            $"coversOwnRevision={matchingRevision.CoversWholeDocument}");
    }
    return;
}

using RSA rsa = RSA.Create(2048);
var request = new CertificateRequest(
    "CN=PRUEBA FIR",
    rsa,
    HashAlgorithmName.SHA256,
    RSASignaturePadding.Pkcs1);
request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
using X509Certificate2 certificate = request.CreateSelfSigned(
    DateTimeOffset.UtcNow.AddDays(-1),
    DateTimeOffset.UtcNow.AddDays(1));

byte[] currentPdf = CreatePdf();
byte[] stamp = Convert.FromBase64String(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z8N8AAAAASUVORK5CYII=");
var placement = new SignatureLocation { Page = 1, X = 50, Y = 650, Width = 170, Height = 60 };
var signer = new PdfSignatureService();
var validator = new PdfValidationService();

for (int expected = 1; expected <= 3; expected++)
{
    placement.Y -= 70;
    currentPdf = signer.Sign(currentPdf, $"Prueba {expected}", certificate, stamp, placement);
    var signatures = validator.GetSignatures(currentPdf);

    if (signatures.Count != expected || signatures.Any(signature => !signature.IsValid))
        throw new Exception($"Falló la validación después de la firma {expected}.");

    string report = string.Join(
        ", ",
        signatures.Select(signature =>
            $"{signature.SignatureName} -> revisión {signature.Revision}/{signature.TotalRevisions}: válida={signature.IsValid}"));
    Console.WriteLine(report);
}

static byte[] CreatePdf()
{
    using var output = new MemoryStream();
    using var pdf = new PdfDocument(new PdfWriter(output));
    using var document = new Document(pdf);
    document.Add(new Paragraph("Prueba de firmas incrementales"));
    document.Close();
    return output.ToArray();
}

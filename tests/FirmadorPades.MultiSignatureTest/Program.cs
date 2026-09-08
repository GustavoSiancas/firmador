using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Models;
using FirmadorPades.Services;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

if (args.Length == 2 && args[0] == "--audit-cms")
{
    CmsAudit.Verify(File.ReadAllBytes(args[1]));
    return;
}

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
    CmsAudit.Verify(signedPdf);
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

const string launchUri = "firmaapp://sign?inputEndpoint=https%3A%2F%2Fexample.com%2Fin&outputEndpoint=https%3A%2F%2Fexample.com%2Fout&fileId=test&token=test";
foreach (var (query, x, y) in new[] { ("", 50f, 20f), ("&X=125.5&Y=220.25", 125.5f, 220.25f), ("&x=0", 0f, 20f), ("&y=90", 50f, 90f) })
{
    var launch = new LaunchService().GetLaunchParameters(new[] { launchUri + query });
    if (launch.X != x || launch.Y != y)
        throw new Exception("Coordenadas de inicio incorrectas.");
}
Console.WriteLine("Coordenadas de inicio y valores predeterminados correctos.");

// Comprueba el renderizado con System.Drawing incluido en Windows Forms.
byte[] resizedStamp = new StampService().CreateStamp("CN=PRUEBA FIR", "Prueba", 340, 120);
using (var stampStream = new MemoryStream(resizedStamp))
using (var stampImage = System.Drawing.Image.FromStream(stampStream))
{
    if (stampImage.Width != 340 || stampImage.Height != 120)
        throw new Exception("El sello no tiene las dimensiones solicitadas.");
}
Console.WriteLine("Sello generado y redimensionado correctamente: 340 x 120.");

using RSA rsa = RSA.Create(2048);
var request = new CertificateRequest(
    "CN=PRUEBA FIR",
    rsa,
    HashAlgorithmName.SHA256,
    RSASignaturePadding.Pkcs1);
request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
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
    byte[] previousRevision = currentPdf;
    currentPdf = signer.Sign(currentPdf, $"Prueba {expected}", certificate, stamp, placement);
    AssertPreservedRevision(previousRevision, currentPdf);
    CmsAudit.Verify(currentPdf, certificate);
    string auditOutput = Path.Combine(AppContext.BaseDirectory, "cms-audit");
    Directory.CreateDirectory(auditOutput);
    File.WriteAllBytes(Path.Combine(auditOutput, $"prueba-{expected}-firmas.pdf"), currentPdf);
    if (expected == 1)
    {
        byte[] tampered = (byte[])currentPdf.Clone();
        tampered[7] = tampered[7] == (byte)'7' ? (byte)'6' : (byte)'7';
        bool rejected = false;
        try { CmsAudit.Verify(tampered, certificate); }
        catch (Exception ex) when (ex.Message == "Auditoría CMS: messageDigest de ByteRange") { rejected = true; }
        if (!rejected) throw new Exception("La auditoría no rechazó un PDF alterado.");
        Console.WriteLine("Control negativo: modificación de bytes firmados detectada.");
    }
    using (var signedDocument = new PdfDocument(new PdfReader(new MemoryStream(currentPdf))))
    {
        var field = iText.Forms.PdfAcroForm.GetAcroForm(signedDocument, false).GetField($"Signature{expected}");
        var widget = field.GetWidgets()[0];
        var rectangle = widget.GetRectangle().ToRectangle();
        if (!widget.GetPage().GetPdfObject().Equals(signedDocument.GetLastPage().GetPdfObject()) ||
            rectangle.GetX() != placement.X || rectangle.GetY() != placement.Y)
            throw new Exception("El sello no está en las coordenadas de la última página.");
    }
    var signatures = validator.GetSignatures(currentPdf);

    if (signatures.Count != expected || signatures.Any(signature => !signature.IsValid))
        throw new Exception($"Falló la validación después de la firma {expected}.");

    string report = string.Join(
        ", ",
        signatures.Select(signature =>
            $"{signature.SignatureName} -> revisión {signature.Revision}/{signature.TotalRevisions}: válida={signature.IsValid}"));
    Console.WriteLine(report);
}

foreach (var (query, expectedClean) in new[] { ("", false), ("&clean=false", false), ("&clean=true", true) })
{
    if (new LaunchService().GetLaunchParameters(new[] { launchUri + query }).Clean != expectedClean)
        throw new Exception("Valor de clean incorrecto.");
}
bool invalidCleanRejected = false;
try { new LaunchService().GetLaunchParameters(new[] { launchUri + "&clean=invalid" }); }
catch (ArgumentException) { invalidCleanRejected = true; }
if (!invalidCleanRejected) throw new Exception("Se aceptó un clean inválido.");

byte[] originalBytes = currentPdf.ToArray();
byte[] cleanedPdf = new PdfCleaningService().Clean(currentPdf);
if (!currentPdf.SequenceEqual(originalBytes) || validator.GetSignatures(currentPdf).Count != 3)
    throw new Exception("La limpieza modificó la entrada.");
if (validator.GetSignatures(cleanedPdf).Count != 0)
    throw new Exception("Quedan firmas después de limpiar.");
using (var original = new PdfDocument(new PdfReader(new MemoryStream(currentPdf))))
using (var cleaned = new PdfDocument(new PdfReader(new MemoryStream(cleanedPdf))))
{
    if (original.GetNumberOfPages() != cleaned.GetNumberOfPages())
        throw new Exception("La limpieza cambió las páginas.");
    for (int page = 1; page <= original.GetNumberOfPages(); page++)
    {
        if (!original.GetPage(page).GetContentBytes().SequenceEqual(cleaned.GetPage(page).GetContentBytes()))
            throw new Exception("La limpieza cambió el contenido de una página.");
        if (cleaned.GetPage(page).GetAnnotations().Count != 0)
            throw new Exception("Quedan apariencias de firma.");
    }
}
byte[] signedAgain = signer.Sign(cleanedPdf, "Firma después de limpiar", certificate, stamp, placement);
var newSignatures = validator.GetSignatures(signedAgain);
if (newSignatures.Count != 1 || !newSignatures[0].IsValid)
    throw new Exception("No se pudo firmar el PDF limpio.");
if (validator.GetSignatures(new PdfCleaningService().Clean(CreatePdf())).Count != 0)
    throw new Exception("Falló la limpieza de un PDF sin firmas.");
Console.WriteLine("Limpieza correcta: entrada intacta, contenido conservado, sin firmas y nueva firma válida.");

string batchDirectory = Path.Combine(AppContext.BaseDirectory, "batch-test", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(batchDirectory);
var batchPaths = Enumerable.Range(1, 20).Select(index => Path.Combine(batchDirectory, $"documento-{index}.pdf")).ToArray();
foreach (string path in batchPaths) File.WriteAllBytes(path, currentPdf);
var batchService = new BatchSignatureService();
var batchResults = batchService.Sign(batchPaths, certificate, clean: true);
if (batchResults.Count != 20 || batchResults.Any(result => result.Error is not null || result.PdfBytes is null))
    throw new Exception("Falló la firma del lote de 20 PDFs.");
foreach (var result in batchResults)
{
    var signatures = validator.GetSignatures(result.PdfBytes!);
    if (signatures.Count != 1 || !signatures[0].IsValid)
        throw new Exception("El lote contiene una firma inválida.");
}
if (batchPaths.Any(path => !File.ReadAllBytes(path).SequenceEqual(currentPdf)))
    throw new Exception("La firma masiva modificó los archivos originales.");
string invalidPath = Path.Combine(batchDirectory, "invalido.pdf");
File.WriteAllText(invalidPath, "No es un PDF");
var mixedResults = batchService.Sign(new[] { invalidPath, batchPaths[0] }, certificate, clean: false);
if (mixedResults[0].Error is null || mixedResults[1].PdfBytes is null ||
    validator.GetSignatures(mixedResults[1].PdfBytes!).Count != 4)
    throw new Exception("El lote no continuó tras un error o no conservó las firmas existentes.");
bool oversizedRejected = false;
try { batchService.Sign(batchPaths.Append(batchPaths[0]).ToArray(), certificate, false); }
catch (ArgumentException) { oversizedRejected = true; }
if (!oversizedRejected) throw new Exception("Se aceptaron más de 20 PDFs.");
Console.WriteLine("Lote de 20 PDFs: firmas válidas, originales intactos, límite y errores parciales verificados.");

// Una firma CMS externa puede no declarar /Extensions. La contrafirma no debe
// introducir esa entrada en el catálogo de una revisión ya firmada.
byte[] externalSignedPdf;
using (var externalInput = new MemoryStream(CreatePdf()))
using (var externalOutput = new MemoryStream())
using (var externalReader = new PdfReader(externalInput))
{
    var externalSigner = new iText.Signatures.PdfSigner(externalReader, externalOutput,
        new StampingProperties().UseAppendMode());
    externalSigner.SetFieldName("ExternalSignature");
    externalSigner.SignDetached(
        new FirmadorPades.Helpers.X509Certificate2Signature(certificate, "SHA256"),
        FirmadorPades.Helpers.CertificateConverter.ToChain(certificate),
        null, null, null, 0, iText.Signatures.PdfSigner.CryptoStandard.CMS);
    externalSignedPdf = externalOutput.ToArray();
}
using (var externalDocument = new PdfDocument(new PdfReader(new MemoryStream(externalSignedPdf))))
{
    if (externalDocument.GetCatalog().GetPdfObject().ContainsKey(PdfName.Extensions))
        throw new Exception("El caso de prueba debe empezar sin extensiones.");
}
byte[] appendedPdf = signer.Sign(externalSignedPdf, "Firma adicional", certificate, stamp, placement);
AssertPreservedRevision(externalSignedPdf, appendedPdf);
if (validator.GetSignatures(appendedPdf).Count != 2 || validator.GetSignatures(appendedPdf).Any(s => !s.IsValid))
    throw new Exception("Falló la conservación de la firma CMS externa.");
Console.WriteLine("Firma adicional: catálogo sin extensiones nuevas, páginas intactas y ambas firmas válidas.");

static void AssertPreservedRevision(byte[] before, byte[] after)
{
    if (!after.AsSpan(0, before.Length).SequenceEqual(before))
        throw new Exception("Se reescribieron bytes de la revisión anterior.");
    using var previous = new PdfDocument(new PdfReader(new MemoryStream(before)));
    using var current = new PdfDocument(new PdfReader(new MemoryStream(after)));
    if (previous.GetNumberOfPages() != current.GetNumberOfPages())
        throw new Exception("Se cambió la cantidad de páginas.");
    for (int page = 1; page <= previous.GetNumberOfPages(); page++)
        if (!previous.GetPage(page).GetContentBytes().SequenceEqual(current.GetPage(page).GetContentBytes()))
            throw new Exception("Se modificó contenido de página al agregar una firma.");
    if (new iText.Signatures.SignatureUtil(previous).GetSignatureNames().Count == 0) return;
    var previousCatalog = previous.GetCatalog().GetPdfObject();
    var currentCatalog = current.GetCatalog().GetPdfObject();
    if (!previousCatalog.KeySet().OrderBy(k => k.ToString()).SequenceEqual(currentCatalog.KeySet().OrderBy(k => k.ToString())))
        throw new Exception("Se agregaron o quitaron entradas del catálogo después de una firma.");
    if (previousCatalog.GetAsDictionary(PdfName.Extensions)?.ToString() != currentCatalog.GetAsDictionary(PdfName.Extensions)?.ToString())
        throw new Exception("Se cambiaron las extensiones de un PDF firmado.");
}

static byte[] CreatePdf()
{
    using var output = new MemoryStream();
    using var pdf = new PdfDocument(new PdfWriter(output));
    using var document = new Document(pdf);
    document.Add(new Paragraph("Prueba de firmas incrementales"));
    document.Add(new AreaBreak());
    document.Add(new Paragraph("Última página para los sellos"));
    document.Close();
    return output.ToArray();
}

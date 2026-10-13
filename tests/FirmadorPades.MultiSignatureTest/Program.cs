using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Models;
using FirmadorPades.Services;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

if (args.Length == 2 && args[0] == "--render-local-ui")
{
    Exception? renderError = null;
    var thread = new Thread(() =>
    {
        try
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            Directory.CreateDirectory(args[1]);
            using var form = new FirmadorPades.Forms.LocalSigningForm(new CertificateService(), detectCertificates: false);
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            form.PerformLayout();
            using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(Path.Combine(args[1], "local-signing.png"));
            using var placementForm = new FirmadorPades.Forms.SignaturePlacementForm(new LocalSignaturePlacement());
            placementForm.Show();
            System.Windows.Forms.Application.DoEvents();
            placementForm.PerformLayout();
            using var grid = new System.Drawing.Bitmap(placementForm.Width, placementForm.Height);
            placementForm.DrawToBitmap(grid, new System.Drawing.Rectangle(0, 0, placementForm.Width, placementForm.Height));
            grid.Save(Path.Combine(args[1], "signature-placement.png"));
        }
        catch (Exception ex) { renderError = ex; }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (renderError is not null) throw renderError;
    return;
}

if (args.Length == 2 && args[0] == "--audit-cms")
{
    CmsAudit.Verify(File.ReadAllBytes(args[1]));
    return;
}

if (args.Length == 1 && args[0] == "--read-dnie")
{
    var certificates = DniePcscCertificateService.GetCertificates(out string? diagnostic);
    foreach (var dnieCertificate in certificates)
    {
        Console.WriteLine($"DNIe 2.0 PC/SC: {dnieCertificate.Subject} | vence={dnieCertificate.NotAfter:yyyy-MM-dd} | huella={dnieCertificate.Thumbprint}");
        dnieCertificate.Dispose();
    }
    if (certificates.Count == 0) throw new Exception(diagnostic ?? "No se encontró un DNIe 2.0 compatible.");
    return;
}

if (args.Length == 1 && args[0] == "--test-dnie")
{
    DniePcscTests.Run();
    return;
}

if (args.Length == 1 && args[0] == "--list-smart-card")
{
    var service = new CertificateService();
    var certificates = service.GetAllCertificates();
    foreach (var cardCertificate in certificates)
    {
        Console.WriteLine($"{cardCertificate.Subject} | PC/SC={DniePcscCertificateService.TryGetReader(cardCertificate, out _)} | privadaWindows={cardCertificate.HasPrivateKey}");
        cardCertificate.Dispose();
    }
    if (service.LastDetectionDiagnostic is not null) Console.WriteLine(service.LastDetectionDiagnostic);
    Console.WriteLine($"Presencia PC/SC: {CertificateService.GetReaderSnapshot()}");
    return;
}

if (args.Length == 3 && args[0] == "--sign-once-real")
{
    byte[] inputPdf = File.ReadAllBytes(args[1]);
    var certificateService = new CertificateService();
    using X509Certificate2 signingCertificate = certificateService.GetSigningCertificate();
    using var realSinglePin = ReadSigningPin();
    using var realSingleSession = SigningSession.Open(signingCertificate, realSinglePin);
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
        new SignatureLocation { Page = 1, X = 50, Y = 20, Width = 170, Height = 60 }, realSingleSession);

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
    using var realDoublePin = ReadSigningPin();
    using var realDoubleSession = SigningSession.Open(realCertificate, realDoublePin);
    var stampService = new StampService();
    var pdfSigner = new PdfSignatureService();
    var realValidator = new PdfValidationService();
    var firstPlacement = new SignatureLocation { Page = 1, X = 50, Y = 90, Width = 170, Height = 60 };
    var secondPlacement = new SignatureLocation { Page = 1, X = 240, Y = 90, Width = 170, Height = 60 };
    const string reason = "Documento firmado digitalmente";

    realCurrentPdf = pdfSigner.Sign(
        realCurrentPdf, reason, realCertificate,
        stampService.CreateStamp(realCertificate.Subject, reason), firstPlacement, realDoubleSession);
    var firstValidation = realValidator.GetSignatures(realCurrentPdf);
    Console.WriteLine($"Después de Signature1: {string.Join(", ", firstValidation.Select(s => $"{s.SignatureName}={s.IsValid}"))}");
    if (firstValidation.Any(s => !s.IsValid)) throw new Exception("La primera firma no superó la validación local.");

    realCurrentPdf = pdfSigner.Sign(
        realCurrentPdf, reason, realCertificate,
        stampService.CreateStamp(realCertificate.Subject, reason), secondPlacement, realDoubleSession);
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

const string launchUri = "firmaapp://sign?sessionEndpoint=https%3A%2F%2Fexample.com%2Fsession&token=test";
DniePcscTests.Run();
var launchParameters = new LaunchService().GetLaunchParameters(new[] { launchUri });
if (launchParameters.SessionEndpoint != new Uri("https://example.com/session") ||
    launchParameters.Token != "test")
{
    throw new Exception("Parámetros de inicio de la sesión incorrectos.");
}

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

// El modo independiente entrega ambos PDF, aunque compartan nombre, con firmas v?lidas.
string localTestDirectory = Path.Combine(Path.GetTempPath(), "FirmadorLocalTest-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(localTestDirectory);
try
{
    string source = Path.Combine(localTestDirectory, "documento.pdf");
    File.WriteAllBytes(source, currentPdf);
    var localDocuments = LocalBatchSignatureService.LoadDocuments(new[] { source, source });
    for (int row = 0; row < 4; row++)
    for (int col = 0; col < 3; col++)
    foreach (bool firstPage in new[] { true, false })
    {
        var positioned = LocalBatchSignatureService.Reposition(localDocuments, new LocalSignaturePlacement(col, row, firstPage));
        using var input = new MemoryStream(currentPdf);
        using var pdf = new PdfDocument(new PdfReader(input));
        var location = positioned[0].SignatureLocation;
        int expectedPage = firstPage ? 1 : pdf.GetNumberOfPages();
        var size = pdf.GetPage(expectedPage).GetPageSize();
        if (location.Page != expectedPage || location.X < size.GetLeft() || location.Y < size.GetBottom() ||
            location.X + location.Width > size.GetRight() || location.Y + location.Height > size.GetTop())
            throw new Exception("La matriz ubic? la firma fuera de la p?gina seleccionada.");
        if (!positioned[0].PdfBytes.SequenceEqual(currentPdf))
            throw new Exception("La vista previa alter? el PDF original.");
    }

    string archivePath = Path.Combine(localTestDirectory, "firmados.zip");
    using var localPin = new System.Security.SecureString();
    var localResults = await new LocalBatchSignatureService().SignToZipAsync(
        localDocuments, certificate, localPin, archivePath);
    if (localResults.Count != 2 || localResults.Any(result => result.Error is not null))
        throw new Exception("Fall? la firma del lote local.");
    using var archive = System.IO.Compression.ZipFile.OpenRead(archivePath);
    if (archive.Entries.Count != 2 || archive.Entries.Select(entry => entry.FullName).Distinct().Count() != 2)
        throw new Exception("El ZIP perdi? documentos con nombres repetidos.");
    foreach (var entry in archive.Entries)
    {
        using var content = entry.Open();
        using var bytes = new MemoryStream();
        content.CopyTo(bytes);
        var signatures = new PdfValidationService().GetSignatures(bytes.ToArray());
        if (signatures.Count != 1 || !signatures[0].IsValid)
            throw new Exception("El ZIP contiene un PDF sin una firma v?lida.");
    }
    string failedArchivePath = Path.Combine(localTestDirectory, "fallidos.zip");
    var failedResults = await new LocalBatchSignatureService().SignToZipAsync(
        new[] { new TemporarySigningDocument { FileName = "invalido.pdf", PdfBytes = new byte[] { 1, 2, 3 } } },
        certificate, localPin, failedArchivePath);
    using (var failedArchive = System.IO.Compression.ZipFile.OpenRead(failedArchivePath))
    {
        var errorEntry = failedArchive.GetEntry("errores.txt")
            ?? throw new Exception("El lote fallido no gener? el reporte dentro del ZIP.");
        using var errorReader = new StreamReader(errorEntry.Open());
        string errorReport = errorReader.ReadToEnd();
        if (failedResults.Single().Error is null || !errorReport.Contains("invalido.pdf") ||
            !errorReport.Contains(failedResults.Single().Error!))
            throw new Exception("El reporte no identifica el PDF fallido y su motivo.");
    }
    Console.WriteLine("OK: lote local a ZIP con nombres repetidos y firmas v?lidas.");
}
finally
{
    foreach (string file in Directory.GetFiles(localTestDirectory)) File.Delete(file);
    Directory.Delete(localTestDirectory);
}

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
        if (!widget.GetPage().GetPdfObject().Equals(signedDocument.GetPage(placement.Page).GetPdfObject()) ||
            rectangle.GetX() != placement.X || rectangle.GetY() != placement.Y)
            throw new Exception("El sello no está en las coordenadas de la página seleccionada.");
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

foreach (var pinError in new[] {
    (Net.Pkcs11Interop.Common.CKR.CKR_PIN_INCORRECT, "incorrecta"),
    (Net.Pkcs11Interop.Common.CKR.CKR_PIN_LOCKED, "bloqueada"),
    (Net.Pkcs11Interop.Common.CKR.CKR_PIN_EXPIRED, "vencido"),
    (Net.Pkcs11Interop.Common.CKR.CKR_CANCEL, "cancel") })
{
    var wrapped = new iText.Kernel.Exceptions.PdfException("Failed to sign",
        new CryptographicException("Provider error",
            new Net.Pkcs11Interop.Common.Pkcs11Exception("C_Login", pinError.Item1)));
    string message = SigningErrorMessages.GetMessage(wrapped);
    if (!message.Contains(pinError.Item2, StringComparison.OrdinalIgnoreCase) ||
        message.Contains("CKR") || message.Contains("Failed"))
        throw new Exception("No se tradujo la causa del error de PIN.");
}
var windowsPinError = new CryptographicException(unchecked((int)0x8010006B));
if (!SigningErrorMessages.GetMessage(new Exception("Failed to sign", windowsPinError)).Contains("incorrecta"))
    throw new Exception("No se tradujo el error de PIN de Windows.");
if (SigningErrorMessages.GetMessage(new InvalidDataException("PDF inv?lido")).Contains("PIN"))
    throw new Exception("Se confundi? un PDF inv?lido con un error de PIN.");

bool fallbackReported = false;
object? fallbackSession = Pkcs11Fallback.OpenOrUseWindows<object>(
    () => throw new Net.Pkcs11Interop.Common.Pkcs11Exception("C_OpenSession", Net.Pkcs11Interop.Common.CKR.CKR_TOKEN_NOT_RECOGNIZED),
    () => fallbackReported = true);
if (fallbackSession is not null || !fallbackReported)
    throw new Exception("No se activó el respaldo de Windows para un token no reconocido.");
var compatibleSession = new object();
if (!ReferenceEquals(Pkcs11Fallback.OpenOrUseWindows(() => compatibleSession), compatibleSession))
    throw new Exception("Se descartó una sesión PKCS#11 compatible.");
foreach (var error in new[] { Net.Pkcs11Interop.Common.CKR.CKR_PIN_INCORRECT,
    Net.Pkcs11Interop.Common.CKR.CKR_PIN_LOCKED, Net.Pkcs11Interop.Common.CKR.CKR_CANCEL })
{
    bool propagated = false;
    try
    {
        Pkcs11Fallback.OpenOrUseWindows<object>(() => throw new Net.Pkcs11Interop.Common.Pkcs11Exception("C_Login", error));
    }
    catch (Net.Pkcs11Interop.Common.Pkcs11Exception ex) when (ex.RV == error) { propagated = true; }
    if (!propagated) throw new Exception("Un error de autenticación activó el respaldo indebidamente.");
}
Console.WriteLine("Respaldo Windows: token no reconocido activa el cambio; errores de PIN y cancelación se conservan.");

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

static System.Security.SecureString ReadSigningPin()
{
    if (Console.IsInputRedirected) throw new InvalidOperationException("Ingrese el PIN en una consola interactiva, no por argumentos ni archivos.");
    Console.Write("PIN de firma (entrada oculta): ");
    var pin = new System.Security.SecureString();
    try
    {
        while (true)
        {
            var input = Console.ReadKey(intercept: true);
            if (input.Key == ConsoleKey.Escape) throw new OperationCanceledException("Firma cancelada.");
            if (input.Key == ConsoleKey.Enter) break;
            if (input.Key == ConsoleKey.Backspace && pin.Length > 0) pin.RemoveAt(pin.Length - 1);
            else if (!char.IsControl(input.KeyChar) && pin.Length < 64) pin.AppendChar(input.KeyChar);
        }
        Console.WriteLine();
        pin.MakeReadOnly();
        return pin;
    }
    catch { pin.Dispose(); throw; }
}

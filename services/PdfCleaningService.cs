using iText.Forms;
using iText.Kernel.Pdf;

namespace FirmadorPades.Services;

public class PdfCleaningService
{
    /// <summary>
    /// Devuelve una copia sin campos de firma ni sus apariencias visuales.
    /// Conserva el contenido de las páginas y no modifica los bytes de entrada.
    /// </summary>
    public byte[] Clean(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        using var input = new MemoryStream(pdfBytes, writable: false);
        using var output = new MemoryStream();
        using (var pdf = new PdfDocument(new PdfReader(input), new PdfWriter(output)))
        {
            // Reescribe la copia para descartar revisiones y objetos de firma antiguos.
            pdf.SetFlushUnusedObjects(false);
            var form = PdfAcroForm.GetAcroForm(pdf, false);
            if (form is not null)
            {
                var signatureFields = form.GetFormFields()
                    .Where(field => PdfName.Sig.Equals(field.Value.GetFormType()))
                    .Select(field => field.Key)
                    .ToArray();

                foreach (string name in signatureFields)
                    form.RemoveField(name);

                form.GetPdfObject().Remove(PdfName.SigFlags);
            }

            // Elimina restricciones de certificación y datos de validación de firmas.
            pdf.GetCatalog().GetPdfObject().Remove(PdfName.Perms);
            pdf.GetCatalog().GetPdfObject().Remove(PdfName.DSS);
        }

        return output.ToArray();
    }
}

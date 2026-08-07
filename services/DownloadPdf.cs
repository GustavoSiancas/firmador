using System.Windows.Forms;

namespace FirmadorPades.Services;

public class DownloadService
{
    public async Task DownloadPdfAsync(byte[] pdfBytes)
    {
        using SaveFileDialog dialog = new SaveFileDialog();

        dialog.Filter = "PDF (*.pdf)|*.pdf";
        dialog.FileName = $"Documento_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        dialog.Title = "Guardar PDF";

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        await File.WriteAllBytesAsync(
            dialog.FileName,
            pdfBytes);

        MessageBox.Show(
            "PDF descargado correctamente.",
            "Éxito",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
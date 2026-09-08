using System.IO.Compression;
using FirmadorPades.Services;

namespace FirmadorPades.Forms;

public sealed class BatchSignatureResultsForm : Form
{
    public BatchSignatureResultsForm(IReadOnlyList<BatchSignatureResult> results)
    {
        Text = "Resultados de firma masiva";
        ClientSize = new Size(820, 460);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        var successful = results.Where(result => result.PdfBytes is not null).ToArray();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = $"{successful.Length} firmados de {results.Count}. Guarde el ZIP antes de cerrar.", AutoSize = true }, 0, 0);
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
        list.Columns.Add("PDF", 260);
        list.Columns.Add("Resultado", 490);
        foreach (var result in results)
            list.Items.Add(new ListViewItem(new[] { result.FileName, result.Error ?? "Firmado correctamente" }));
        layout.Controls.Add(list, 0, 1);
        var save = new Button { Text = "Guardar PDFs firmados en ZIP", AutoSize = true, Enabled = successful.Length > 0 };
        save.Click += (_, _) =>
        {
            using var dialog = new SaveFileDialog { Filter = "Archivo ZIP (*.zip)|*.zip", FileName = "pdfs-firmados.zip", DefaultExt = "zip", AddExtension = true, OverwritePrompt = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                using var buffer = new MemoryStream();
                using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
                {
                    for (int i = 0; i < successful.Length; i++)
                    {
                        var result = successful[i];
                        var entry = archive.CreateEntry($"{i + 1:D2}_{Path.GetFileNameWithoutExtension(result.FileName)}_firmado.pdf");
                        using var stream = entry.Open();
                        stream.Write(result.PdfBytes!);
                    }
                }
                File.WriteAllBytes(dialog.FileName, buffer.ToArray());
                MessageBox.Show(this, $"Se guardaron {successful.Length} PDFs firmados.", "Descarga completada");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error al guardar"); }
        };
        layout.Controls.Add(save, 0, 2);
        Controls.Add(layout);
    }
}

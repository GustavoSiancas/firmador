using FirmadorPades.Services;

namespace FirmadorPades.Forms;

public sealed class BatchSignatureResultsForm : Form
{
    private static readonly Color BrandTeal = Color.FromArgb(20, 125, 106);
    private static readonly Color Surface = Color.FromArgb(244, 247, 251);

    public BatchSignatureResultsForm(IReadOnlyList<TemporarySignatureResult> results)
    {
        Text = "Resultados de firma masiva";
        ClientSize = new Size(820, 460);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        BackColor = Surface;
        var successful = results.Where(result => result.Error is null).ToArray();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = $"{successful.Length} firmados y actualizados en la nube; {results.Count - successful.Length} no actualizados.",
            AutoSize = true, ForeColor = Color.FromArgb(84, 97, 110),
            Padding = new Padding(4, 0, 0, 10)
        }, 0, 0);
        var list = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
            GridLines = false, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle
        };
        list.Columns.Add("Documento", 260);
        list.Columns.Add("Estado", 490);
        foreach (var result in results)
            list.Items.Add(new ListViewItem(new[]
            {
                result.FileName,
                result.Error ?? "Firmado y actualizado en la nube"
            }));
        layout.Controls.Add(list, 0, 1);
        Controls.Add(layout);
        var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = BrandTeal };
        header.Controls.Add(new Label
        {
            Text = "Firma completada", AutoSize = true, Location = new Point(22, 13),
            Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.White
        });
        header.Controls.Add(new Label
        {
            Text = "Resumen del procesamiento de documentos", AutoSize = true,
            Location = new Point(24, 45), ForeColor = Color.FromArgb(222, 244, 240)
        });
        Controls.Add(header);
    }
}

using FirmadorPades.Services;

namespace FirmadorPades.Forms;

public sealed class BatchSignatureResultsForm : Form
{
    private readonly System.Windows.Forms.Timer? _closeTimer;

    public BatchSignatureResultsForm(
        IReadOnlyList<BatchSignatureResult> results,
        bool closeAutomatically = false)
    {
        Text = "Resultados de firma masiva";
        ClientSize = new Size(820, 460);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        var successful = results.Where(result => result.PdfBytes is not null).ToArray();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = closeAutomatically
                ? $"{successful.Length} firmados y actualizados en la nube; {results.Count - successful.Length} no actualizados. Esta ventana se cerrará en 3 segundos."
                : $"{successful.Length} firmados de {results.Count}. Guarde el ZIP antes de cerrar.",
            AutoSize = true
        }, 0, 0);
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
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

        if (closeAutomatically)
        {
            _closeTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _closeTimer.Tick += (_, _) => Close();
            Shown += (_, _) => _closeTimer.Start();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _closeTimer?.Dispose();
        base.Dispose(disposing);
    }
}

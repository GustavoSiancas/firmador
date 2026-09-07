namespace FirmadorPades.Forms;

/// <summary>Confirma el resultado y cierra el diálogo tras dos segundos.</summary>
public sealed class SignatureSuccessForm : Form
{
    private readonly System.Windows.Forms.Timer _closeTimer = new() { Interval = 2000 };
    private readonly byte[] _signedPdf;

    public SignatureSuccessForm(byte[] signedPdf)
    {
        _signedPdf = signedPdf;
        Text = "Firma completada";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "logo.ico"));
        ClientSize = new Size(460, 190);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(244, 247, 251);

        Controls.Add(new Label
        {
            Text = "Documento firmado correctamente",
            Location = new Point(24, 28),
            Size = new Size(412, 40),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(20, 125, 106)
        });
        Controls.Add(new Label
        {
            Text = "La aplicación se cerrará en 2 segundos.",
            Location = new Point(24, 76),
            Size = new Size(412, 24),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10)
        });
        var closeButton = new Button
        {
            Text = "Cerrar",
            Location = new Point(246, 122),
            Size = new Size(150, 44),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            BackColor = Color.FromArgb(20, 125, 106),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK
        };
        closeButton.FlatAppearance.BorderSize = 0;
        Controls.Add(closeButton);
        var downloadButton = new Button
        {
            Text = "Descargar",
            Location = new Point(64, 122),
            Size = new Size(166, 44),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(20, 125, 106),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        downloadButton.Click += DownloadButton_Click;
        Controls.Add(downloadButton);
        AcceptButton = closeButton;
        CancelButton = closeButton;
        _closeTimer.Tick += (_, _) => Close();
        Shown += (_, _) => _closeTimer.Start();
    }

    private void DownloadButton_Click(object? sender, EventArgs e)
    {
        _closeTimer.Stop();
        try
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Guardar documento firmado",
                Filter = "Documento PDF (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                AddExtension = true,
                FileName = "documento_firmado.pdf",
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                File.WriteAllBytes(dialog.FileName, _signedPdf);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo guardar el documento: {ex.Message}",
                "Error al descargar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed && !Disposing)
                _closeTimer.Start();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _closeTimer.Dispose();
        base.Dispose(disposing);
    }
}

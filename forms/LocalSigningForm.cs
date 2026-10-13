using System.Security.Cryptography.X509Certificates;
using FirmadorPades.Models;
using FirmadorPades.Services;

namespace FirmadorPades.Forms;

internal sealed class LocalSigningForm : Form
{
    private static readonly Color Teal = Color.FromArgb(20, 125, 106);
    private static readonly Color Ink = Color.FromArgb(32, 43, 54);
    private static readonly Color Muted = Color.FromArgb(84, 97, 110);
    private readonly CertificateService _service;
    private IReadOnlyList<TemporarySigningDocument> _documents = [];
    private LocalSignaturePlacement _placement = new();
    private readonly ListBox _files = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false, DisplayMember = nameof(TemporarySigningDocument.FileName), ItemHeight = 28 };
    private readonly ComboBox _certificates = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, IntegralHeight = false, DropDownHeight = 220 };
    private readonly Label _fileCount = new() { Text = "Ningún PDF seleccionado", AutoSize = true, ForeColor = Muted };
    private readonly Label _certificateHint = new() { Dock = DockStyle.Fill, ForeColor = Muted, Text = "Buscando certificados…" };
    private readonly Label _placementLabel = new() { Dock = DockStyle.Fill, ForeColor = Muted };
    private readonly Label _status = new() { Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft, Text = "Agregue sus PDF para comenzar." };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Bottom, Height = 5, Visible = false };
    private readonly Button _add = ButtonFor("Agregar PDF");
    private readonly Button _remove = ButtonFor("Quitar");
    private readonly Button _preview = ButtonFor("Vista previa");
    private readonly Button _refresh = ButtonFor("Actualizar certificados");
    private readonly Button _position = ButtonFor("Ubicación de firma");
    private readonly Button _sign = ButtonFor("Firmar y guardar ZIP", true);
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5000 };
    private bool _busy;
    private bool _checking;
    private string? _snapshot;

    internal LocalSigningForm(CertificateService service, bool detectCertificates = true)
    {
        _service = service;
        Text = "Firmador CAL · Firma de PDF";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "logo.ico"));
        Font = new Font("Segoe UI", 10);
        ForeColor = Ink;
        BackColor = Color.FromArgb(244, 247, 251);
        ClientSize = new Size(940, 670);
        MinimumSize = new Size(880, 660);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        var header = new Panel { Dock = DockStyle.Fill, BackColor = Teal, Padding = new Padding(32, 22, 32, 18) };
        header.Controls.Add(new Label { Dock = DockStyle.Top, Height = 28, Text = "Seleccione sus PDF, elija dónde firmar y descargue un ZIP.", ForeColor = Color.FromArgb(222, 244, 240) });
        header.Controls.Add(new Label { Dock = DockStyle.Top, Height = 40, Text = "Firma de documentos", Font = new Font("Segoe UI", 21, FontStyle.Bold), ForeColor = Color.White });
        root.Controls.Add(header, 0, 0);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 24, 28, 24), ColumnCount = 2, RowCount = 1 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));

        var documentCard = Card("01   Documentos PDF");
        documentCard.Margin = new Padding(0, 0, 20, 0);
        var documentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        documentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        documentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        documentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        documentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        documentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        _add.Dock = DockStyle.Fill;
        documentLayout.Controls.Add(_add, 0, 0);
        _fileCount.Margin = new Padding(0, 8, 0, 0);
        documentLayout.Controls.Add(_fileCount, 0, 1);
        documentLayout.Controls.Add(_files, 0, 2);
        var fileActions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
        _preview.Width = 145;
        _remove.Width = 100;
        fileActions.Controls.AddRange([_preview, _remove]);
        documentLayout.Controls.Add(fileActions, 0, 3);
        documentLayout.Controls.Add(new Label { Text = "Los archivos originales se conservan.", Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft }, 0, 4);
        documentCard.Controls.Add(documentLayout);
        documentLayout.BringToFront();
        content.Controls.Add(documentCard, 0, 0);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        var certCard = Card("02   Certificado digital");
        certCard.Margin = new Padding(0, 0, 0, 16);
        var certLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        certLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        certLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        certLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        certLayout.Controls.Add(_certificates, 0, 0);
        certLayout.Controls.Add(_certificateHint, 0, 1);
        _refresh.Width = 215;
        certLayout.Controls.Add(_refresh, 0, 2);
        certCard.Controls.Add(certLayout);
        certLayout.BringToFront();
        right.Controls.Add(certCard, 0, 0);
        var positionCard = Card("03   Ubicación de firma");
        var positionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        positionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        positionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _position.Width = 240;
        _position.Height = 44;
        _position.Image = CreateGridIcon();
        _position.ImageAlign = ContentAlignment.MiddleLeft;
        _position.TextImageRelation = TextImageRelation.ImageBeforeText;
        _position.Padding = new Padding(12, 0, 8, 0);
        positionLayout.Controls.Add(_position, 0, 0);
        _placementLabel.Text = _placement.ToString() + "\nMatriz de 3 columnas × 4 filas";
        positionLayout.Controls.Add(_placementLabel, 0, 1);
        positionCard.Controls.Add(positionLayout);
        positionLayout.BringToFront();
        right.Controls.Add(positionCard, 0, 1);
        content.Controls.Add(right, 1, 0);
        root.Controls.Add(content, 0, 1);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(32, 22, 32, 22), ColumnCount = 2, RowCount = 1 };
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        var statusPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 24, 0) };
        statusPanel.Controls.Add(_status);
        statusPanel.Controls.Add(_progress);
        footer.Controls.Add(statusPanel, 0, 0);
        _sign.Dock = DockStyle.Fill;
        footer.Controls.Add(_sign, 1, 0);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);

        _add.Click += AddDocuments;
        _remove.Click += (_, _) =>
        {
            if (_files.SelectedIndex < 0) return;
            _documents = _documents.Where((_, index) => index != _files.SelectedIndex).ToArray();
            RefreshFiles();
        };
        _preview.Click += (_, _) => { using var preview = new DocumentListForm(_documents, this); preview.ShowDialog(this); };
        _position.Click += ChangePlacement;
        _refresh.Click += async (_, _) => await LoadCertificatesAsync();
        _certificates.SelectedIndexChanged += (_, _) => { UpdateCertificateHint(); UpdateControls(); };
        _files.SelectedIndexChanged += (_, _) => UpdateControls();
        _sign.Click += SignDocuments;
        Shown += async (_, _) => { if (!detectCertificates) return; await LoadCertificatesAsync(); if (!IsDisposed) _timer.Start(); };
        _timer.Tick += async (_, _) =>
        {
            if (_busy || _checking) return;
            _checking = true;
            try
            {
                string snapshot = await Task.Run(CertificateService.GetReaderSnapshot);
                if (!IsDisposed && !_busy && snapshot != _snapshot) await LoadCertificatesAsync();
            }
            catch { /* La actualización manual muestra los errores de detección. */ }
            finally { _checking = false; }
        };
        FormClosing += (_, e) => { if (_busy) e.Cancel = true; };
        UpdateControls();
    }

    private static Panel Card(string title)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20), Margin = Padding.Empty };
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Ink });
        return panel;
    }

    private static Button ButtonFor(string text, bool primary = false)
    {
        var button = new Button { Text = text, Height = 40, Width = 180, FlatStyle = FlatStyle.Flat, BackColor = primary ? Teal : Color.White, ForeColor = primary ? Color.White : Teal, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 10, 0), Font = new Font("Segoe UI", 10, primary ? FontStyle.Bold : FontStyle.Regular) };
        button.FlatAppearance.BorderColor = primary ? Teal : Color.FromArgb(200, 218, 214);
        return button;
    }

    private static Bitmap CreateGridIcon()
    {
        var bitmap = new Bitmap(26, 30);
        using var graphics = Graphics.FromImage(bitmap);
        using var pen = new Pen(Teal, 1.5f);
        for (int row = 0; row < 4; row++)
        for (int col = 0; col < 3; col++) graphics.DrawRectangle(pen, 1 + col * 8, 1 + row * 7, 5, 4);
        return bitmap;
    }

    private void UpdateControls()
    {
        _add.Enabled = _refresh.Enabled = _position.Enabled = _certificates.Enabled = !_busy;
        _remove.Enabled = !_busy && _files.SelectedIndex >= 0;
        _preview.Enabled = !_busy && _documents.Count > 0;
        _sign.Enabled = !_busy && _documents.Count > 0 && _certificates.SelectedItem is CertificateChoice;
        UseWaitCursor = _busy;
    }

    private void RefreshFiles()
    {
        _files.Items.Clear();
        foreach (var document in _documents) _files.Items.Add(document);
        if (_files.Items.Count > 0) _files.SelectedIndex = 0;
        _fileCount.Text = $"{_documents.Count} PDF en el lote";
        _status.Text = _documents.Count > 0 ? "Revise la ubicación y firme cuando esté listo." : "Agregue sus PDF para comenzar.";
        UpdateControls();
    }

    private async void AddDocuments(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "Documentos PDF (*.pdf)|*.pdf", Multiselect = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _busy = true;
        UpdateControls();
        _status.Text = "Preparando documentos y vista previa…";
        try
        {
            var added = await Task.Run(() => LocalBatchSignatureService.LoadDocuments(dialog.FileNames, _placement));
            _documents = _documents.Concat(added).ToArray();
            RefreshFiles();
        }
        catch (Exception ex) { ShowError(ex); }
        finally { _busy = false; UpdateControls(); }
    }

    private async void ChangePlacement(object? sender, EventArgs e)
    {
        using var dialog = new SignaturePlacementForm(_placement);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _busy = true;
        UpdateControls();
        try
        {
            var placement = dialog.Placement;
            var documents = await Task.Run(() => LocalBatchSignatureService.Reposition(_documents, placement));
            _placement = placement;
            _documents = documents;
            _placementLabel.Text = _placement.ToString() + "\nMatriz de 3 columnas × 4 filas";
            RefreshFiles();
        }
        catch (Exception ex) { ShowError(ex); }
        finally { _busy = false; UpdateControls(); }
    }

    private async Task LoadCertificatesAsync()
    {
        if (_busy) return;
        _busy = true;
        UpdateControls();
        _certificateHint.Text = "Buscando certificados…";
        try
        {
            var found = await Task.Run(() => (_service.GetAllCertificates(), CertificateService.GetReaderSnapshot()));
            if (IsDisposed) { foreach (var certificate in found.Item1) certificate.Dispose(); return; }
            var selected = (_certificates.SelectedItem as CertificateChoice)?.Certificate.Thumbprint;
            var old = _certificates.Items.Cast<CertificateChoice>().ToArray();
            _certificates.Items.Clear();
            foreach (var choice in old) choice.Certificate.Dispose();
            foreach (var certificate in found.Item1)
                _certificates.Items.Add(new CertificateChoice(certificate, $"{_service.GetHolderName(certificate)} — {_service.GetCertificatePurpose(certificate)}"));
            _snapshot = found.Item2;
            int index = _certificates.Items.Cast<CertificateChoice>().ToList().FindIndex(choice => choice.Certificate.Thumbprint == selected);
            _certificates.SelectedIndex = index >= 0 ? index : (_certificates.Items.Count > 0 ? 0 : -1);
            UpdateCertificateHint();
        }
        catch (Exception ex) { _certificateHint.Text = ex.Message; }
        finally { _busy = false; if (!IsDisposed) UpdateControls(); }
    }

    private void UpdateCertificateHint()
    {
        _certificateHint.Text = _certificates.SelectedItem is CertificateChoice choice
            ? $"{_service.GetHolderName(choice.Certificate)}\n{_service.GetCertificatePurpose(choice.Certificate)} · vence {choice.Certificate.NotAfter:dd/MM/yyyy}"
            : _service.LastDetectionDiagnostic ?? "Inserte el DNIe y actualice los certificados.";
    }

    private async void SignDocuments(object? sender, EventArgs e)
    {
        if (_busy || _certificates.SelectedItem is not CertificateChoice choice || _documents.Count == 0) return;
        using var save = new SaveFileDialog { Filter = "Archivo ZIP (*.zip)|*.zip", DefaultExt = "zip", AddExtension = true, FileName = "documentos_firmados.zip" };
        if (save.ShowDialog(this) != DialogResult.OK) return;
        _busy = true;
        _timer.Stop();
        UpdateControls();
        try
        {
            using var pinForm = new BatchPinForm(_documents.Count);
            if (pinForm.ShowDialog(this) != DialogResult.OK) { _status.Text = "Firma cancelada. Sus PDF siguen listos."; return; }
            using var pin = pinForm.CopyPin();
            _progress.Maximum = _documents.Count;
            _progress.Value = 0;
            _progress.Visible = true;
            var progress = new Progress<int>(count => { _progress.Value = Math.Min(count, _progress.Maximum); _status.Text = $"Procesados {count} de {_documents.Count} PDF…"; });
            var status = new Progress<string>(message => _status.Text = message);
            var results = await Task.Run(() => new LocalBatchSignatureService().SignToZipAsync(_documents, choice.Certificate, pin, save.FileName, progress, status));
            using var report = new BatchSignatureResultsForm(results, save.FileName);
            report.ShowDialog(this);
            _status.Text = $"ZIP guardado · {results.Count(result => result.Error is null)} firmados · {results.Count(result => result.Error is not null)} con error";
        }
        catch (Exception ex) { ShowError(ex); }
        finally { _busy = false; _progress.Visible = false; _timer.Start(); UpdateControls(); }
    }

    private void ShowError(Exception ex)
    {
        _status.Text = "No se pudo completar la operación.";
        MessageBox.Show(this, SigningErrorMessages.GetMessage(ex), "Firmador CAL", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _position.Image?.Dispose();
            foreach (var choice in _certificates.Items.Cast<CertificateChoice>()) choice.Certificate.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed record CertificateChoice(X509Certificate2 Certificate, string Label)
    {
        public override string ToString() => Label;
    }
}

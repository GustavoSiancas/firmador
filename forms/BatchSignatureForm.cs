using System.Security.Cryptography.X509Certificates;
using System.Security;
using FirmadorPades.Services;

namespace FirmadorPades.Forms;

public sealed class BatchSignatureForm : Form
{
    private readonly ListBox _files = new() { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, HorizontalScrollbar = true };
    private readonly ComboBox _certificates = new() { Width = 510, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _clean = new() { Text = "Quitar firmas anteriores (clean)", AutoSize = true };
    private readonly Label _status = new() { Text = "0 / 20 PDFs", AutoSize = true };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill };
    private readonly Button _sign = new() { Text = "Firmar todos", AutoSize = true };
    private readonly FlowLayoutPanel _actions = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly FlowLayoutPanel _options = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly CertificateService _certificateService = new();
    private readonly List<X509Certificate2> _ownedCertificates = new();
    private bool _busy;

    public BatchSignatureForm()
    {
        Text = "Firmador CAL - Prueba de firma masiva (IDEMIA PKCS#11)";
        ClientSize = new Size(850, 520);
        MinimumSize = new Size(750, 450);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 6 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Seleccione hasta 20 PDFs y un certificado para firmar el lote.", AutoSize = true }, 0, 0);
        var add = new Button { Text = "Agregar PDFs", AutoSize = true };
        var remove = new Button { Text = "Quitar seleccionados", AutoSize = true };
        add.Click += (_, _) => AddFiles();
        remove.Click += (_, _) =>
        {
            foreach (var item in _files.SelectedItems.Cast<string>().ToArray()) _files.Items.Remove(item);
            UpdateState();
        };
        _actions.Controls.AddRange(new Control[] { add, remove, _status });
        layout.Controls.Add(_actions, 0, 1);
        layout.Controls.Add(_files, 0, 2);
        var refresh = new Button { Text = "Actualizar certificados", AutoSize = true };
        refresh.Click += (_, _) => LoadCertificates();
        _options.Controls.AddRange(new Control[] { _certificates, refresh, _clean });
        layout.Controls.Add(_options, 0, 3);
        layout.Controls.Add(_progress, 0, 4);
        layout.Controls.Add(_sign, 0, 5);
        Controls.Add(layout);
        _certificates.SelectedIndexChanged += (_, _) => UpdateState();
        _sign.Click += Sign_Click;
        Shown += (_, _) => LoadCertificates();
        FormClosing += (_, e) => { if (_busy) e.Cancel = true; };
        UpdateState();
    }

    private void AddFiles()
    {
        using var dialog = new OpenFileDialog { Filter = "Documentos PDF (*.pdf)|*.pdf", Multiselect = true, Title = "Seleccionar PDFs" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var additions = dialog.FileNames.Except(_files.Items.Cast<string>(), StringComparer.OrdinalIgnoreCase).ToArray();
        if (_files.Items.Count + additions.Length > BatchSignatureService.MaxDocuments)
        {
            MessageBox.Show(this, "El lote admite como máximo 20 PDFs. Seleccione menos archivos.");
            return;
        }
        _files.Items.AddRange(additions);
        UpdateState();
    }

    private void LoadCertificates()
    {
        _certificates.Items.Clear();
        foreach (var certificate in _ownedCertificates) certificate.Dispose();
        _ownedCertificates.Clear();
        try
        {
            _ownedCertificates.AddRange(_certificateService.GetAllCertificates());
            foreach (var certificate in _ownedCertificates)
                _certificates.Items.Add($"{_certificateService.GetHolderName(certificate)} — {certificate.Thumbprint[^8..]}");
            if (_certificates.Items.Count > 0) _certificates.SelectedIndex = 0;
            else MessageBox.Show(this, "No se encontraron certificados de firma. Conecte su dispositivo y pulse Actualizar certificados.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error al leer certificados"); }
        UpdateState();
    }

    private void UpdateState()
    {
        _status.Text = $"{_files.Items.Count} / 20 PDFs";
        _sign.Enabled = !_busy && _files.Items.Count > 0 && _certificates.SelectedIndex >= 0;
    }

    private async void Sign_Click(object? sender, EventArgs e)
    {
        if (_busy || _certificates.SelectedIndex < 0 || _files.Items.Count == 0) return;
        var paths = _files.Items.Cast<string>().ToArray();
        var certificate = _ownedCertificates[_certificates.SelectedIndex];
        bool clean = _clean.Checked;
        SecureString pin;
        using (var pinForm = new BatchPinForm(paths.Length))
        {
            if (pinForm.ShowDialog(this) != DialogResult.OK) return;
            pin = pinForm.CopyPin();
        }
        _busy = true;
        _actions.Enabled = _options.Enabled = _files.Enabled = false;
        _progress.Maximum = paths.Length;
        _progress.Value = 0;
        UpdateState();
        var progress = new Progress<int>(count =>
        {
            _progress.Value = count;
            _status.Text = $"Procesados {count} / {paths.Length}";
        });
        try
        {
            IReadOnlyList<BatchSignatureResult> results;
            using (pin)
                results = await Task.Run(() => new BatchSignatureService().Sign(paths, certificate, clean, progress, pin, usePkcs11: true));
            using var resultForm = new BatchSignatureResultsForm(results);
            resultForm.ShowDialog(this);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error al firmar el lote"); }
        finally
        {
            _busy = false;
            _actions.Enabled = _options.Enabled = _files.Enabled = true;
            UpdateState();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) foreach (var certificate in _ownedCertificates) certificate.Dispose();
        base.Dispose(disposing);
    }
}

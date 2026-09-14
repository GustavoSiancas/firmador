namespace FirmadorPades.Forms;

internal sealed class ErrorDetailsForm : Form
{
    private readonly TextBox _details;

    public ErrorDetailsForm(string message, Exception exception)
    {
        Text = "FirmaCAL";
        ClientSize = new Size(520, 165);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "logo.ico"));

        Controls.Add(new Label
        {
            Text = message,
            Location = new Point(28, 25),
            Size = new Size(460, 58),
            Font = new Font("Segoe UI", 11),
            TextAlign = ContentAlignment.MiddleLeft
        });

        var detailsButton = new Button
        {
            Text = "Detalles",
            Location = new Point(28, 105),
            Size = new Size(110, 34)
        };
        detailsButton.Click += (_, _) => ShowDetails();
        Controls.Add(detailsButton);

        var closeButton = new Button
        {
            Text = "Cerrar",
            Location = new Point(378, 105),
            Size = new Size(110, 34),
            DialogResult = DialogResult.OK
        };
        Controls.Add(closeButton);
        AcceptButton = closeButton;
        CancelButton = closeButton;

        _details = new TextBox
        {
            Text = exception.ToString(),
            Location = new Point(28, 155),
            Size = new Size(460, 210),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Visible = false,
            Font = new Font("Consolas", 9)
        };
        Controls.Add(_details);
    }

    private void ShowDetails()
    {
        if (_details.Visible)
            return;

        _details.Visible = true;
        ClientSize = new Size(ClientSize.Width, 390);
    }
}

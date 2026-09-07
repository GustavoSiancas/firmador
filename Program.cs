using System.Windows.Forms;
using FirmadorPades.Forms;
using FirmadorPades.Models;
using FirmadorPades.Services;

namespace FirmadorPades;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        try
        {
            var launchService = new LaunchService();

            LaunchParameters parameters =
                launchService.GetLaunchParameters(args);

            var certificateService = new CertificateService();

            var apiService = new ApiService(
                parameters.InputEndpoint,
                parameters.OutputEndpoint,
                parameters.FileId,
                parameters.Token);

            var stampService =
                new StampService();

            var pdfSignatureService =
                new PdfSignatureService();

            var orchestratorService =
                new OrchestratorService(
                    stampService,
                    pdfSignatureService,
                    apiService);


            byte[] pdfBytes = apiService.GetDocumentPdfAsync()
                .GetAwaiter()
                .GetResult();

            Application.Run(
                new CertificateForm(
                    certificateService,
                    orchestratorService,
                    pdfBytes,
                    new SignatureLocation
                    {
                        X = parameters.X,
                        Y = parameters.Y,
                        Width = 170,
                        Height = 60
                    }));
        }
#if DEBUG
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
#else
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Inicio no permitido",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
#endif
    }
}

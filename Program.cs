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
                parameters.Backend,
                parameters.DocumentArtifactId,
                parameters.Token);

            var cloudinaryService =
                new CloudinaryService();

            var stampService =
                new StampService();

            var downloadService =
                new DownloadService();

            var pdfPreparationService =
                new PdfPreparationService();

            var pdfSignatureService =
                new PdfSignatureService();

            var orchestratorService =
                new OrchestratorService(
                    stampService,
                    pdfPreparationService,
                    pdfSignatureService,
                    apiService);


            byte[] pdfBytes = apiService.GetDocumentPdfAsync()
                .GetAwaiter()
                .GetResult();

            Application.Run(
                new MainForm(
                    parameters.DocumentArtifactId,
                    apiService,
                    pdfBytes,
                    certificateService,
                    orchestratorService,
                    stampService,
                    downloadService));
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

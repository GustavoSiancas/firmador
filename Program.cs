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
            if (args.Length == 1 && args[0] == "--batch-test")
            {
                Application.Run(new BatchSignatureForm());
                return;
            }

            var launchService = new LaunchService();

            LaunchParameters parameters =
                launchService.GetLaunchParameters(args);

            var apiService = new ApiService(
                parameters.SessionEndpoint,
                parameters.Token);

            IReadOnlyList<TemporarySigningDocument> documents;
            try
            {
                _ = apiService.GetTemporaryResourcesAsync()
                    .GetAwaiter()
                    .GetResult();

                documents = apiService.GetTemporaryDocumentsAsync()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex) when (parameters.IsDevelopment)
            {
                throw new Exception(
                    $"{ex.Message}{Environment.NewLine}{Environment.NewLine}" +
                    $"Solicitud enviada:{Environment.NewLine}{apiService.GetLastRequestCurl()}",
                    ex);
            }

            Application.Run(new CertificateForm(new CertificateService(), documents));
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

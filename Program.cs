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

            Application.Run(new CertificateForm(new CertificateService(), apiService, documents));
        }
        catch (LaunchParameterException ex)
        {
            ShowError("Por favor contacte con soporte y actualice a la última versión de FirmaCAL.", ex);
        }
        catch (Exception ex)
        {
            ShowError("Error en el sistema. Contacte a sistemas.", ex);
        }
    }

    private static void ShowError(string message, Exception exception)
    {
        using var error = new ErrorDetailsForm(message, exception);
        error.ShowDialog();
    }
}

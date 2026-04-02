namespace umg_basic_rover_application.Contracts;

public interface IWhatsAppService
{
    Task<bool> SendTextAsync(string telefono, string mensaje);
    Task<bool> SendPdfAsync(string telefono, byte[] pdfBytes, string fileName, string caption);
    Task<(bool ready, string state, bool qrAvailable)> GetStatusAsync();
    Task<string?> GetQrBase64Async();
}
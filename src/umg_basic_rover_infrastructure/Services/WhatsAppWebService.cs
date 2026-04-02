using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using umg_basic_rover_application.Contracts;

namespace umg_basic_rover_infrastructure.Services;

public class WhatsAppWebService : IWhatsAppService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppWebService> _logger;

    public WhatsAppWebService(
        HttpClient http,
        IConfiguration config,
        ILogger<WhatsAppWebService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    private bool IsEnabled()
    {
        return _config["WhatsAppWeb:Enabled"]?.ToLower() == "true";
    }

    private void AddApiKey()
    {
        var apiKey = _config["WhatsAppWeb:ApiKey"];

        _http.DefaultRequestHeaders.Remove("x-api-key");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        }
    }

    public async Task<bool> SendTextAsync(string telefono, string mensaje)
    {
        try
        {
            if (!IsEnabled())
            {
                _logger.LogWarning("[WA-WEB] WhatsAppWeb deshabilitado.");
                return false;
            }

            AddApiKey();

            var payload = new
            {
                to = telefono,
                message = mensaje
            };

            var json = JsonSerializer.Serialize(payload);

            var response = await _http.PostAsync(
                "/messages/text",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WA-WEB] Error enviando mensaje de texto.");
            return false;
        }
    }

    public async Task<bool> SendPdfAsync(string telefono, byte[] pdfBytes, string fileName, string caption)
    {
        try
        {
            if (!IsEnabled())
            {
                _logger.LogWarning("[WA-WEB] WhatsAppWeb deshabilitado.");
                return false;
            }

            AddApiKey();

            var payload = new
            {
                to = telefono,
                caption = caption,
                filename = fileName,
                mimeType = "application/pdf",
                base64Data = Convert.ToBase64String(pdfBytes)
            };

            var json = JsonSerializer.Serialize(payload);

            var response = await _http.PostAsync(
                "/messages/media",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WA-WEB] Error enviando PDF por WhatsApp.");
            return false;
        }
    }

    public async Task<(bool ready, string state, bool qrAvailable)> GetStatusAsync()
    {
        try
        {
            AddApiKey();

            var response = await _http.GetAsync("/session/status");
            if (!response.IsSuccessStatusCode)
                return (false, "error", false);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;

            return (
                root.GetProperty("ready").GetBoolean(),
                root.GetProperty("state").GetString() ?? "unknown",
                root.GetProperty("qr_available").GetBoolean()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WA-WEB] Error consultando estado.");
            return (false, "error", false);
        }
    }

    public async Task<string?> GetQrBase64Async()
    {
        try
        {
            AddApiKey();

            var response = await _http.GetAsync("/session/qr");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.GetProperty("qr_base64").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WA-WEB] Error obteniendo QR.");
            return null;
        }
    }
}
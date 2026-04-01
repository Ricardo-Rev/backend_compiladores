using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace umg_basic_rover_infrastructure.Services;

public class FaceSegmentationService
{
    private readonly ILogger<FaceSegmentationService> _logger;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    private const string DETECT_URL  = "https://api-us.faceplusplus.com/facepp/v3/detect";
    private const string COMPARE_URL = "https://api-us.faceplusplus.com/facepp/v3/compare";

    public FaceSegmentationService(ILogger<FaceSegmentationService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _http   = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    private string ApiKey    => _config["FacePlusPlus:ApiKey"]    ?? throw new InvalidOperationException("FacePlusPlus:ApiKey no configurado.");
    private string ApiSecret => _config["FacePlusPlus:ApiSecret"] ?? throw new InvalidOperationException("FacePlusPlus:ApiSecret no configurado.");

    public (bool Success, string? Base64Result, string? Message) SegmentFace(string imageBase64)
    {
        try
        {
            var cleanBase64 = imageBase64.Contains(',')
                ? imageBase64.Split(',')[1]
                : imageBase64;

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("api_key",      ApiKey),
                new KeyValuePair<string, string>("api_secret",   ApiSecret),
                new KeyValuePair<string, string>("image_base64", cleanBase64),
            });

            var response = _http.PostAsync(DETECT_URL, form).Result;
            var json     = response.Content.ReadAsStringAsync().Result;

            _logger.LogInformation("[FACE-SEG] Face++ detect response: {j}", json.Length > 200 ? json[..200] : json);

            using var doc = JsonDocument.Parse(json);
            var faces     = doc.RootElement.GetProperty("faces");

            if (faces.GetArrayLength() == 0)
                return (false, null, "No se detectó ninguna cara en la imagen.");

            var face_rect = faces[0].GetProperty("face_rectangle");
            var top    = face_rect.GetProperty("top").GetInt32();
            var left   = face_rect.GetProperty("left").GetInt32();
            var width  = face_rect.GetProperty("width").GetInt32();
            var height = face_rect.GetProperty("height").GetInt32();

            var imageBytes   = Convert.FromBase64String(cleanBase64);
            var resultBase64 = RecortarImagen(imageBytes, left, top, width, height);

            _logger.LogInformation("[FACE-SEG] ✅ Cara detectada y recortada. Face++ OK.");
            return (true, resultBase64, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FACE-SEG] ❌ Error al segmentar cara.");
            return (false, null, $"Error al procesar la imagen: {ex.Message}");
        }
    }

    public (bool Match, string? Message) CompararRostros(string base64A, string base64B)
    {
        try
        {
            var cleanA = base64A.Contains(',') ? base64A.Split(',')[1] : base64A;
            var cleanB = base64B.Contains(',') ? base64B.Split(',')[1] : base64B;

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("api_key",        ApiKey),
                new KeyValuePair<string, string>("api_secret",     ApiSecret),
                new KeyValuePair<string, string>("image_base64_1", cleanA),
                new KeyValuePair<string, string>("image_base64_2", cleanB),
            });

            var response = _http.PostAsync(COMPARE_URL, form).Result;
            var json     = response.Content.ReadAsStringAsync().Result;

            _logger.LogInformation("[FACE-SEG] Face++ compare response: {j}", json.Length > 200 ? json[..200] : json);

            using var doc  = JsonDocument.Parse(json);
            var confidence = doc.RootElement.GetProperty("confidence").GetDouble();
            var thresholds = doc.RootElement.GetProperty("thresholds");
            var umbral     = thresholds.GetProperty("1e-3").GetDouble();

            _logger.LogInformation("[FACE-SEG] Confianza: {c:F2} | Umbral: {u:F2}", confidence, umbral);

            var coincide = confidence >= umbral;
            return (coincide, coincide ? null : $"Rostros no coinciden (confianza: {confidence:F1}%)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FACE-SEG] Error al comparar rostros.");
            return (false, $"Error al comparar: {ex.Message}");
        }
    }

    private static string RecortarImagen(byte[] imageBytes, int left, int top, int width, int height)
    {
        using var ms     = new MemoryStream(imageBytes);
        using var bitmap = SkiaSharp.SKBitmap.Decode(ms);

        var pad_x = (int)(width  * 0.5);
        var pad_y = (int)(height * 0.8);

        var x = Math.Max(0, left - pad_x);
        var y = Math.Max(0, top  - pad_y);
        var w = Math.Min(bitmap.Width  - x, width  + pad_x * 2);
        var h = Math.Min(bitmap.Height - y, height + pad_y * 2);

        using var canvas_bmp = new SkiaSharp.SKBitmap(w, h);
        using var canvas     = new SkiaSharp.SKCanvas(canvas_bmp);

        canvas.Clear(SkiaSharp.SKColors.White);
        canvas.DrawBitmap(bitmap,
            new SkiaSharp.SKRect(x, y, x + w, y + h),
            new SkiaSharp.SKRect(0, 0, w, h));

        using var out_ms = new MemoryStream();
        canvas_bmp.Encode(out_ms, SkiaSharp.SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(out_ms.ToArray());
    }
}
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace umg_basic_rover_infrastructure.Services;

public class FaceSegmentationService
{
    private readonly ILogger<FaceSegmentationService> _logger;
    private const string CASCADE_NAME = "haarcascade_frontalface_default.xml";

    public FaceSegmentationService(ILogger<FaceSegmentationService> logger)
    {
        _logger = logger;
    }

    public (bool Success, string? Base64Result, string? Message) SegmentFace(string imageBase64)
    {
        try
        {
            var cleanBase64 = imageBase64.Contains(',')
                ? imageBase64.Split(',')[1]
                : imageBase64;

            var imageBytes = Convert.FromBase64String(cleanBase64);

            using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
            if (mat.Empty())
                return (false, null, "No se pudo decodificar la imagen.");

            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);

            var cascadePath = ObtenerRutaCascade();
            if (cascadePath is null)
                return (false, null, "No se encontró el archivo Haar Cascade.");

            using var cascade = new CascadeClassifier(cascadePath);

            var caras = cascade.DetectMultiScale(
                image: gray,
                scaleFactor: 1.1,
                minNeighbors: 5,
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new OpenCvSharp.Size(80, 80)
            );

            if (caras.Length == 0)
                return (false, null, "No se detectó ninguna cara en la imagen.");

            var cara = caras.OrderByDescending(r => r.Width * r.Height).First();

            var padding_x = (int)(cara.Width  * 0.5);
            var padding_y = (int)(cara.Height * 0.8);

            var x = Math.Max(0, cara.X - padding_x);
            var y = Math.Max(0, cara.Y - padding_y);
            var w = Math.Min(mat.Width  - x, cara.Width  + padding_x * 2);
            var h = Math.Min(mat.Height - y, cara.Height + padding_y * 2);

            using var recorte = new Mat(mat, new OpenCvSharp.Rect(x, y, w, h));

            // Fondo blanco con OpenCV puro — sin ImageSharp
            using var canvas = new Mat(recorte.Size(), MatType.CV_8UC3, new Scalar(255, 255, 255));
            recorte.CopyTo(canvas);

            var resultBytes  = canvas.ToBytes(".png");
            var resultBase64 = Convert.ToBase64String(resultBytes);

            _logger.LogInformation("[FACE-SEG] ✅ Cara detectada. Original: {w}x{h}", mat.Width, mat.Height);
            return (true, resultBase64, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FACE-SEG] ❌ Error al segmentar cara.");
            return (false, null, $"Error al procesar la imagen: {ex.Message}");
        }
    }

    private string? ObtenerRutaCascade()
    {
        var posibles = new[]
        {
            Path.Combine(AppContext.BaseDirectory, CASCADE_NAME),
            Path.Combine(AppContext.BaseDirectory, "haarcascades", CASCADE_NAME),
            Path.Combine(Directory.GetCurrentDirectory(), CASCADE_NAME),
        };

        foreach (var ruta in posibles)
        {
            if (File.Exists(ruta))
            {
                _logger.LogDebug("[FACE-SEG] Cascade en: {ruta}", ruta);
                return ruta;
            }
        }

        _logger.LogError("[FACE-SEG] ❌ No se encontró {cascade}.", CASCADE_NAME);
        return null;
    }
}
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QRCoder;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.IO.Image;
using umg_basic_rover_application.Contracts;
using umg_basic_rover_application.DTOs;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.persistence.context;


namespace umg_basic_rover_infrastructure.Services;

public class CredentialService : ICredentialService
{
    private readonly rover_db_context _db;
    private readonly IConfiguration _config;
    private readonly ILogger<CredentialService> _logger;
    private readonly IWhatsAppService _whatsAppService;

    public CredentialService(rover_db_context db, IConfiguration config, ILogger<CredentialService> logger,IWhatsAppService whatsAppService)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _whatsAppService = whatsAppService;
    }

    public async Task<CredentialResponse> GenerarYEnviarAsync(int usuario_id)
    {
        var usuario = await _db.usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.id == usuario_id)
            ?? throw new InvalidOperationException($"Usuario {usuario_id} no encontrado.");

        _logger.LogInformation("[CREDENTIAL] Generando credencial para usuario: {u}", usuario.usuario);

        var foto_facial = await _db.autenticacion_facial
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.usuario_id == usuario_id && f.activo);

        var imagen_referencia = foto_facial?.imagen_referencia;

        var qr_data = CifrarIdUsuario(usuario_id);
        var qr_bytes = GenerarQrBytes(qr_data);

        var pdf_bytes = GenerarPdf(usuario, qr_bytes, imagen_referencia);
        var pdf_b64 = Convert.ToBase64String(pdf_bytes);

        var firma = await FirmarPdfConApiAsync(pdf_bytes);

        var credencial = new credencial_pdf_entity
        {
            usuario_id = usuario_id,
            archivo_base64 = pdf_b64,
            firma_electronica = firma,
            canal_envio = "ambos",
            estado_envio = "pendiente",
            fecha_generacion = DateTime.Now
        };

        _db.credenciales_pdf.Add(credencial);
        await _db.SaveChangesAsync();

        await GuardarQrAsync(usuario_id, qr_data);
        await GuardarMetodosNotificacionAsync(usuario_id, usuario.email, usuario.telefono);

        bool email_ok = false;
        bool whatsapp_ok = false;

        email_ok = await EnviarEmailAsync(usuario, pdf_bytes, firma, credencial.id);
        whatsapp_ok = await EnviarWhatsAppAsync(usuario, pdf_bytes, credencial.id);

        credencial.estado_envio = (email_ok || whatsapp_ok) ? "enviado" : "error";
        credencial.fecha_envio = DateTime.Now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[CREDENTIAL] ✅ Credencial generada. Email:{e} WA:{w}", email_ok, whatsapp_ok);

        return new CredentialResponse
        {
            credencial_id = credencial.id,
            estado_envio = credencial.estado_envio,
            archivo_base64 = pdf_b64,
            email_enviado = email_ok,
            whatsapp_enviado = whatsapp_ok,
            fecha_generacion = credencial.fecha_generacion
        };
    }

    public async Task<CredentialResponse> ReenviarAsync(int usuario_id)
    {
        var ultima = await _db.credenciales_pdf
            .Where(c => c.usuario_id == usuario_id)
            .OrderByDescending(c => c.fecha_generacion)
            .FirstOrDefaultAsync();

        if (ultima == null)
            return await GenerarYEnviarAsync(usuario_id);

        var usuario = await _db.usuarios.AsNoTracking().FirstAsync(u => u.id == usuario_id);
        var pdf_bytes = Convert.FromBase64String(ultima.archivo_base64 ?? string.Empty);

        bool email_ok = await EnviarEmailAsync(usuario, pdf_bytes, ultima.firma_electronica ?? "", ultima.id);
        bool whatsapp_ok = await EnviarWhatsAppAsync(usuario, pdf_bytes, ultima.id);

        ultima.estado_envio = (email_ok || whatsapp_ok) ? "enviado" : "error";
        ultima.fecha_envio = DateTime.Now;
        await _db.SaveChangesAsync();

        return new CredentialResponse
        {
            credencial_id = ultima.id,
            estado_envio = ultima.estado_envio,
            email_enviado = email_ok,
            whatsapp_enviado = whatsapp_ok,
            fecha_generacion = ultima.fecha_generacion
        };
    }

    // ─────────────────────────────────────────────────────────
    // GENERACIÓN DEL PDF
    // Usa la imagen plantilla como fondo y solo superpone:
    // 1) Foto tomada con cámara
    // 2) Avatar
    // 3) QR
    // 4) Datos del usuario
    // ─────────────────────────────────────────────────────────
    private byte[] GenerarPdf(user_entity usuario, byte[] qr_bytes, string? foto_facial_base64 = null)
    {
        using var ms = new MemoryStream();
        using var writer = new PdfWriter(ms);

        // ── Tamaño real del fondo: 1684×2528 px → proporción 2:3
        //    Si W=595 pts → H = 595 × (2528/1684) = 893.2 pts
        const float W = 595f;
        const float H = 893f;

        using var pdf = new PdfDocument(writer);
        using var doc = new Document(pdf, new PageSize(W, H));
        doc.SetMargins(0, 0, 0, 0);

        var fontBold   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var fontMono   = PdfFontFactory.CreateFont(StandardFonts.COURIER_BOLD);

        // Paleta de colores
        var blanco       = new DeviceRgb(245, 245, 245);   // texto principal
        var azulCian     = new DeviceRgb(100, 210, 255);   // acentos/labels
        var grisClaro    = new DeviceRgb(200, 200, 200);   // hash firma
        var grisFirma    = new DeviceRgb(160, 160, 160);   // línea algoritmo

        // 1) FONDO — ocupa toda la página
        CargarFondo(doc, W, H);

        // ── Zona superior: foto facial (izq) + nombre/usuario (centro) + QR (der)
        // La plantilla tiene esa zona aprox en Y=680..820 de 893

        // 2) FOTO FACIAL — recuadro izquierdo superior
        //    Zona plantilla: X≈48, Y_top≈820 → en iText Y desde abajo = H - Y_top - h
        //    Aproximación visual: x=44, y=688, w=118, h=130
        DibujarImagenBase64(doc, foto_facial_base64, 44f, 690f, 118f, 128f);

        // 3) QR — esquina derecha superior
        doc.Add(new Image(ImageDataFactory.Create(qr_bytes))
            .SetFixedPosition(432f, 692f)
            .SetWidth(108f)
            .SetHeight(108f));

        // 4) NOMBRE COMPLETO — fila 1 zona central
        doc.Add(new Paragraph(usuario.nombre_completo ?? usuario.usuario)
            .SetFont(fontBold)
            .SetFontSize(12.5f)
            .SetFontColor(blanco)
            .SetFixedPosition(178f, 756f, 245f));

        // 5) USUARIO (zona central superior) — fila 2
        doc.Add(new Paragraph(usuario.usuario ?? "")
            .SetFont(fontBold)
            .SetFontSize(11.5f)
            .SetFontColor(blanco)
            .SetFixedPosition(178f, 716f, 245f));

        // ── Zona media: campos de datos (usuario, correo, whatsapp, vigencia)
        //    Cada campo tiene un label gris arriba y el valor blanco abajo
        //    Coordenadas Y calculadas sobre H=893

        // 6) USUARIO (campo grande) — aprox Y=608
        doc.Add(new Paragraph(usuario.usuario ?? "")
            .SetFont(fontBold)
            .SetFontSize(12f)
            .SetFontColor(blanco)
            .SetFixedPosition(82f, 610f, 330f));

        // 7) CORREO ELECTRÓNICO — aprox Y=545
        doc.Add(new Paragraph(usuario.email ?? "")
            .SetFont(fontBold)
            .SetFontSize(10.5f)
            .SetFontColor(blanco)
            .SetFixedPosition(82f, 550f, 290f));

        // 8) WHATSAPP — aprox Y=478
        doc.Add(new Paragraph(usuario.telefono ?? "")
            .SetFont(fontBold)
            .SetFontSize(11f)
            .SetFontColor(blanco)
            .SetFixedPosition(82f, 482f, 290f));

        // 9) EMISIÓN - VIGENCIA — aprox Y=408
        var vigencia = $"{usuario.fecha_creacion:dd/MM/yyyy} - {usuario.fecha_creacion.AddYears(1):dd/MM/yyyy}";
        doc.Add(new Paragraph(vigencia)
            .SetFont(fontBold)
            .SetFontSize(11f)
            .SetFontColor(blanco)
            .SetFixedPosition(82f, 412f, 290f));

        // 10) AVATAR — recuadro derecho medio
        //     Zona plantilla derecha: x≈388, y≈390..520 → en iText y≈382
        DibujarAvatar(doc, usuario.avatar_base64, usuario.usuario, 387f, 392f, 118f, 130f, fontBold, blanco);

        // ── Zona inferior: firma electrónica
        // 11) HASH SHA-256 — línea 1
        var hashFirma = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes($"{usuario.usuario}{usuario.email}{usuario.fecha_creacion:yyyyMMddHHmmss}")));

        doc.Add(new Paragraph(hashFirma)
            .SetFont(fontMono)
            .SetFontSize(6f)
            .SetFontColor(grisClaro)
            .SetFixedPosition(82f, 318f, 430f));

        // 12) Algoritmo — línea 2
        doc.Add(new Paragraph("SHA-256 · AES-256 · UMG Basic Rover 2.0-2026")
            .SetFont(fontNormal)
            .SetFontSize(7.5f)
            .SetFontColor(grisFirma)
            .SetFixedPosition(82f, 302f, 320f));

        doc.Close();
        return ms.ToArray();
    }

    // ─────────────────────────────────────────────────────────
    // HELPERS PDF
    // ─────────────────────────────────────────────────────────
    private void CargarFondo(Document doc, float width, float height)
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("fondo_credencial.jpeg", StringComparison.OrdinalIgnoreCase));

            if (resource == null)
            {
                _logger.LogWarning("[CREDENTIAL] Recurso fondo_credencial.jpeg no encontrado en el assembly.");
                return;
            }

            using var resStream = assembly.GetManifestResourceStream(resource)!;
            using var buffer = new MemoryStream();
            resStream.CopyTo(buffer);

            doc.Add(new Image(ImageDataFactory.Create(buffer.ToArray()))
                .SetFixedPosition(0f, 0f)
                .SetWidth(width)
                .SetHeight(height));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[CREDENTIAL] Fondo no cargado: {msg}", ex.Message);
        }
    }

    private void DibujarImagenBase64(Document doc, string? base64, float x, float y, float w, float h)
    {
        try
        {
            var bytes = DecodificarBase64Image(base64);
            if (bytes == null || bytes.Length == 0)
                return;

            doc.Add(new Image(ImageDataFactory.Create(bytes))
                .SetFixedPosition(x, y)
                .SetWidth(w)
                .SetHeight(h));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[CREDENTIAL] No se pudo dibujar imagen base64: {msg}", ex.Message);
        }
    }

    private void DibujarAvatar(
        Document doc,
        string? avatarBase64,
        string? usuario,
        float x,
        float y,
        float w,
        float h,
        PdfFont fontBold,
        DeviceRgb colorTexto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(avatarBase64))
            {
                DibujarIniciales(doc, usuario, x, y, w, h, fontBold, colorTexto);
                return;
            }

            var raw = avatarBase64.Contains(",")
                ? avatarBase64.Split(',')[1]
                : avatarBase64;

            var imgBytes = Convert.FromBase64String(raw);
            byte[] finalBytes;

            if (avatarBase64.Contains("image/svg", StringComparison.OrdinalIgnoreCase) ||
                avatarBase64.TrimStart().StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
                (imgBytes.Length > 4 && imgBytes[0] == '<'))
            {
                using var svgStream = new MemoryStream(imgBytes);
                var svg = new Svg.Skia.SKSvg();
                svg.Load(svgStream);

                using var bmp = new SkiaSharp.SKBitmap(240, 240);
                using var canvas = new SkiaSharp.SKCanvas(bmp);
                canvas.Clear(SkiaSharp.SKColors.Transparent);

                if (svg.Picture == null)
                {
                    DibujarIniciales(doc, usuario, x, y, w, h, fontBold, colorTexto);
                    return;
                }

                var sx = 240f / svg.Picture.CullRect.Width;
                var sy = 240f / svg.Picture.CullRect.Height;
                var matrix = SkiaSharp.SKMatrix.CreateScale(sx, sy);
                canvas.DrawPicture(svg.Picture, ref matrix);

                using var pngStream = new MemoryStream();
                bmp.Encode(pngStream, SkiaSharp.SKEncodedImageFormat.Png, 100);
                finalBytes = pngStream.ToArray();
            }
            else
            {
                finalBytes = imgBytes;
            }

            doc.Add(new Image(ImageDataFactory.Create(finalBytes))
                .SetFixedPosition(x, y)
                .SetWidth(w)
                .SetHeight(h));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[CREDENTIAL] No se pudo dibujar avatar: {msg}", ex.Message);
            DibujarIniciales(doc, usuario, x, y, w, h, fontBold, colorTexto);
        }
    }

    private static byte[]? DecodificarBase64Image(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;

        var raw = base64.Contains(",")
            ? base64.Split(',')[1]
            : base64;

        return Convert.FromBase64String(raw);
    }

    private static void DibujarIniciales(
        Document doc,
        string? usuario,
        float x,
        float y,
        float w,
        float h,
        PdfFont fontBold,
        DeviceRgb colorTexto)
    {
        var texto = string.IsNullOrWhiteSpace(usuario)
            ? "US"
            : usuario[..Math.Min(2, usuario.Length)].ToUpper();

        doc.Add(new Paragraph(texto)
            .SetFont(fontBold)
            .SetFontSize(26f)
            .SetFontColor(colorTexto)
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFixedPosition(x, y + (h / 2f) - 12f, w));
    }

    // Se deja por compatibilidad, aunque ya no se usa en la plantilla nueva
    private static void DibujarCampo(
        Document doc,
        string label,
        string valor,
        float x,
        float y,
        float w,
        PdfFont font_bold,
        PdfFont font_normal,
        DeviceRgb color_label,
        DeviceRgb color_valor,
        float label_size,
        float valor_size)
    {
        doc.Add(new Paragraph(label)
            .SetFont(font_bold)
            .SetFontSize(label_size)
            .SetFontColor(color_label)
            .SetFixedPosition(x, y + 14f, w));

        doc.Add(new Paragraph(valor)
            .SetFont(font_normal)
            .SetFontSize(valor_size)
            .SetFontColor(color_valor)
            .SetFixedPosition(x, y, w));
    }

    private static void AgregarFila(Table t, string label, string valor,
        PdfFont font_bold, PdfFont font_normal, DeviceRgb color_header)
    {
        t.AddCell(new Cell()
            .SetBackgroundColor(color_header)
            .SetPadding(8)
            .Add(new Paragraph(label).SetFont(font_bold).SetFontSize(10).SetFontColor(ColorConstants.WHITE)));
        t.AddCell(new Cell()
            .SetPadding(8)
            .Add(new Paragraph(valor).SetFont(font_normal).SetFontSize(10)));
    }

    // ─────────────────────────────────────────────────────────
    // QR
    // ─────────────────────────────────────────────────────────
    private static byte[] GenerarQrBytes(string data)
    {
        using var qr_generator = new QRCodeGenerator();
        var qr_data = qr_generator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        var qr_code = new PngByteQRCode(qr_data);
        return qr_code.GetGraphic(10);
    }

    private static string CifrarIdUsuario(int usuario_id)
    {
        var data = Encoding.UTF8.GetBytes($"UMG_ROVER_{usuario_id}_{DateTime.Now:yyyyMMdd}");
        var hash = SHA256.HashData(data);
        return $"ROVER-{usuario_id}-{Convert.ToHexString(hash)[..16]}";
    }

    private static string ComputarFirma(byte[] contenido, int usuario_id)
    {
        var salt = Encoding.UTF8.GetBytes($"UMG_FIRMA_{usuario_id}");
        var payload = contenido.Concat(salt).ToArray();
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    // ─────────────────────────────────────────────────────────
    // PERSISTENCIA AUX
    // ─────────────────────────────────────────────────────────
    private async Task GuardarQrAsync(int usuario_id, string qr_data)
    {
        var existente = await _db.codigos_qr
            .FirstOrDefaultAsync(q => q.usuario_id == usuario_id && q.activo);

        if (existente != null)
        {
            existente.activo = false;
        }

        _db.codigos_qr.Add(new codigo_qr_entity
        {
            usuario_id = usuario_id,
            codigo_qr = qr_data,
            qr_hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(qr_data))),
            activo = true,
            fecha_creacion = DateTime.Now
        });

        await _db.SaveChangesAsync();
    }

    private async Task GuardarMetodosNotificacionAsync(int usuario_id, string email, string telefono)
    {
        var existentes = await _db.metodos_notificacion
            .Where(m => m.usuario_id == usuario_id)
            .ToListAsync();

        if (!existentes.Any(m => m.tipo_notificacion == "email"))
        {
            _db.metodos_notificacion.Add(new metodo_notificacion_entity
            {
                usuario_id = usuario_id,
                tipo_notificacion = "email",
                destino = email
            });
        }

        if (!existentes.Any(m => m.tipo_notificacion == "whatsapp"))
        {
            _db.metodos_notificacion.Add(new metodo_notificacion_entity
            {
                usuario_id = usuario_id,
                tipo_notificacion = "whatsapp",
                destino = telefono
            });
        }

        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────
    // EMAIL (SENDGRID)
    // ─────────────────────────────────────────────────────────
    private async Task<bool> EnviarEmailAsync(user_entity usuario, byte[] pdf_bytes, string firma, int credencial_id)
    {
        try
        {
            // ── Validar config sin lanzar excepcion ───────────────
            var api_key = _config["SendGrid:ApiKey"];
            if (string.IsNullOrWhiteSpace(api_key))
            {
                _logger.LogError("[CREDENTIAL] ❌ SendGrid:ApiKey no configurado en variables de entorno.");
                await RegistrarNotificacionAsync(usuario.id, "credencial_pdf", "email",
                    $"Credencial PDF — {usuario.usuario}", "error_config", credencial_id);
                return false;
            }

            var from_email = _config["SendGrid:From"];
            if (string.IsNullOrWhiteSpace(from_email))
            {
                _logger.LogError("[CREDENTIAL] ❌ SendGrid:From no configurado en variables de entorno.");
                await RegistrarNotificacionAsync(usuario.id, "credencial_pdf", "email",
                    $"Credencial PDF — {usuario.usuario}", "error_config", credencial_id);
                return false;
            }

            var from_name = _config["SendGrid:FromName"] ?? "UMG Basic Rover 2.0";

            // ── firma nunca null ──────────────────────────────────
            var firma_safe    = firma ?? string.Empty;
            var firma_display = firma_safe.Length > 32
                ? firma_safe[..32] + "..."
                : (firma_safe.Length > 0 ? firma_safe : "RSA-2048-SHA256-PKCS1");

            var client  = new SendGrid.SendGridClient(api_key);
            var from    = new SendGrid.Helpers.Mail.EmailAddress(from_email, from_name);
            var to      = new SendGrid.Helpers.Mail.EmailAddress(usuario.email, usuario.nombre_completo ?? usuario.usuario);
            var subject = $"Tu Credencial de Acceso - UMG Basic Rover 2.0 | {usuario.usuario}";

            var html_body = $@"
<!DOCTYPE html>
<html>
<head><meta charset='UTF-8'></head>
<body style='font-family: Arial, sans-serif; background:#f5f5f5; margin:0; padding:20px;'>
<div style='max-width:600px; margin:auto; background:white; border-radius:8px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.1);'>
    <div style='background:#003087; padding:24px; text-align:center;'>
        <h1 style='color:white; margin:0; font-size:22px;'>UMG Basic Rover 2.0</h1>
        <p style='color:#FFD700; margin:6px 0 0;'>Universidad Mariano Galvez de Guatemala</p>
    </div>
    <div style='padding:28px;'>
        <h2 style='color:#003087;'>Bienvenido, <strong>{usuario.usuario}</strong>!</h2>
        <p style='color:#333; font-size:15px;'>Tu registro en la plataforma <strong>UMG Basic Rover 2.0</strong> fue exitoso.</p>
        <p style='color:#333; font-size:15px;'>Adjunto encontraras tu <strong>credencial de acceso en formato PDF</strong>, firmada electronicamente con tu informacion y codigo QR de acceso.</p>
        <div style='background:#f0f4ff; border-left:4px solid #003087; padding:14px; margin:20px 0; border-radius:4px;'>
            <p style='margin:0; font-size:13px; color:#555;'><strong>Firma Electronica:</strong><br/>
            <code style='font-size:11px; word-break:break-all;'>{firma_display}</code></p>
        </div>
        <p style='color:#555; font-size:13px;'>Usa tu nickname <strong>{usuario.usuario}</strong> y contrasena para ingresar a la plataforma.</p>
    </div>
    <div style='background:#003087; padding:16px; text-align:center;'>
        <p style='color:#aaa; font-size:12px; margin:0;'>UMG Ingenieria en Sistemas - Compiladores 2026</p>
    </div>
</div>
</body>
</html>";

            var msg     = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(from, to, subject, "", html_body);
            var pdf_b64 = Convert.ToBase64String(pdf_bytes);
            msg.AddAttachment($"credencial_{usuario.usuario}.pdf", pdf_b64, "application/pdf");

            _logger.LogInformation("[CREDENTIAL] Enviando email a: {email} via SendGrid...", usuario.email);
            var response    = await client.SendEmailAsync(msg);
            var status_code = (int)response.StatusCode;
            var enviado     = status_code >= 200 && status_code < 300;

            if (enviado)
            {
                _logger.LogInformation("[CREDENTIAL] ✅ Email enviado OK a: {email} | HTTP {s}", usuario.email, status_code);
            }
            else
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogError("[CREDENTIAL] ❌ SendGrid rechazo. HTTP {s} | Body: {b}", status_code, body);
            }

            await RegistrarNotificacionAsync(usuario.id, "credencial_pdf", "email",
                $"Credencial PDF — {usuario.usuario}", enviado ? "enviado" : "error", credencial_id);

            return enviado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREDENTIAL] ❌ Excepcion al enviar email a: {email}", usuario.email);
            await RegistrarNotificacionAsync(usuario.id, "credencial_pdf", "email",
                $"Credencial PDF — {usuario.usuario}", "error", credencial_id);
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────
    // WHATSAPP
    // ─────────────────────────────────────────────────────────
    private async Task<bool> EnviarWhatsAppAsync(user_entity usuario, byte[] pdf_bytes, int credencial_id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(usuario.telefono))
            {
                _logger.LogWarning("[CREDENTIAL] Usuario sin telefono. Saltando WhatsApp.");
                return false;
            }

            var mensaje = $"""
    🏁 UMG Basic Rover 2.0

    Hola {usuario.usuario}, tu registro fue exitoso.

    Adjunto encontrarás tu credencial PDF de acceso.
    También fue enviada a tu correo: {usuario.email}

    ✅ Universidad Mariano Gálvez — Ingeniería en Sistemas 2026
    """;

            var enviado = await _whatsAppService.SendPdfAsync(
                usuario.telefono,
                pdf_bytes,
                $"credencial_{usuario.usuario}.pdf",
                mensaje
            );

            await RegistrarNotificacionAsync(
                usuario.id,
                "credencial_pdf",
                "whatsapp",
                "Credencial generada — bienvenida",
                enviado ? "enviado" : "error",
                credencial_id
            );

            _logger.LogInformation("[CREDENTIAL] WhatsApp enviado={ok} a: {tel}", enviado, usuario.telefono);
            return enviado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREDENTIAL] Error WhatsApp a: {tel}", usuario.telefono);

            await RegistrarNotificacionAsync(
                usuario.id,
                "credencial_pdf",
                "whatsapp",
                "Credencial generada",
                "error",
                credencial_id
            );

            return false;
        }
    }
    // ─────────────────────────────────────────────────────────
    // HISTORIAL NOTIFICACIONES
    // ─────────────────────────────────────────────────────────
    private async Task RegistrarNotificacionAsync(int usuario_id, string tipo, string canal,
        string asunto, string estado, int? referencia_id = null)
    {
        _db.historial_notificaciones.Add(new historial_notificacion_entity
        {
            usuario_id = usuario_id,
            tipo = tipo,
            canal = canal,
            asunto = asunto,
            estado = estado,
            referencia_id = referencia_id,
            fecha_envio = DateTime.Now
        });

        await _db.SaveChangesAsync();
    }

    private async Task<string> FirmarPdfConApiAsync(byte[] pdf_bytes)
    {
        try
        {
            var base_url = _config["FirmaElectronica:BaseUrl"]
                ?? throw new InvalidOperationException("FirmaElectronica:BaseUrl no configurado.");
            var api_key = _config["FirmaElectronica:ApiKey"]
                ?? throw new InvalidOperationException("FirmaElectronica:ApiKey no configurado.");

            using var http = new HttpClient();
            using var content = new MultipartFormDataContent();

            http.DefaultRequestHeaders.Add("X-Api-Key", api_key);

            var pdf_content = new ByteArrayContent(pdf_bytes);
            pdf_content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            content.Add(pdf_content, "pdf", "credencial.pdf");

            var response = await http.PostAsync($"{base_url}/sign", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[CREDENTIAL] API firma respondió {s} — PDF no registrado.", response.StatusCode);
                return string.Empty;
            }

            _logger.LogInformation("[CREDENTIAL] ✅ PDF firmado y registrado en API de firma.");
            return "RSA-2048-SHA256-PKCS1";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREDENTIAL] ❌ Error al llamar API firma.");
            return string.Empty;
        }
    }

    public async Task<VerificarCredencialResponse> VerificarCredencialAsync(byte[] pdf_bytes)
    {
        try
        {
            var base_url = _config["FirmaElectronica:BaseUrl"]
                ?? throw new InvalidOperationException("FirmaElectronica:BaseUrl no configurado.");

            using var http = new HttpClient();
            using var content = new MultipartFormDataContent();

            var pdf_content = new ByteArrayContent(pdf_bytes);
            pdf_content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            content.Add(pdf_content, "pdf", "credencial.pdf");

            var response = await http.PostAsync($"{base_url}/verify", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[CREDENTIAL] API firma respondió {s} al verificar.", response.StatusCode);
                return new VerificarCredencialResponse
                {
                    valido = false,
                    mensaje = "❌ No se pudo contactar el servicio de firma.",
                    algoritmo = "RSA-2048 SHA-256 PKCS1"
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            var valido = root.GetProperty("valido").GetBoolean();
            var mensaje = root.GetProperty("mensaje").GetString() ?? string.Empty;
            var algoritmo = root.GetProperty("algoritmo").GetString() ?? string.Empty;

            DateTime? fecha_firma = null;
            if (root.TryGetProperty("fecha_firma", out var fecha_el) &&
                fecha_el.ValueKind != JsonValueKind.Null)
            {
                fecha_firma = fecha_el.GetDateTime();
            }

            _logger.LogInformation("[CREDENTIAL] Verificación: {v} — {m}", valido, mensaje);

            return new VerificarCredencialResponse
            {
                valido = valido,
                mensaje = mensaje,
                algoritmo = algoritmo,
                fecha_firma = fecha_firma
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREDENTIAL] ❌ Error al verificar credencial.");
            return new VerificarCredencialResponse
            {
                valido = false,
                mensaje = "❌ Error interno al verificar el documento.",
                algoritmo = "RSA-2048 SHA-256 PKCS1"
            };
        }
    }
}
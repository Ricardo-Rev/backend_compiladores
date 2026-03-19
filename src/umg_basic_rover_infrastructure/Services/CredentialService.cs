using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QRCoder;
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

// ============================================================
//  CredentialService
//  Genera la credencial PDF firmada y la envía por
//  Email (SMTP) y WhatsApp (Twilio).
//
//  NuGet requeridos (agregar al .csproj de infrastructure):
//    <PackageReference Include="itext7" Version="8.*" />
//    <PackageReference Include="QRCoder" Version="1.*" />
//    <PackageReference Include="Twilio" Version="7.*" />
//
//  appsettings.json requerido:
//    "Smtp": { "Host", "Port", "User", "Password", "From", "FromName" }
//    "Twilio": { "AccountSid", "AuthToken", "WhatsAppFrom" }
// ============================================================

public class CredentialService : ICredentialService
{
    private readonly rover_db_context  _db;
    private readonly IConfiguration    _config;
    private readonly ILogger<CredentialService> _logger;

    public CredentialService(rover_db_context db, IConfiguration config, ILogger<CredentialService> logger)
    {
        _db     = db;
        _config = config;
        _logger = logger;
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

        // 1. Generar QR con el id cifrado
        var qr_data   = CifrarIdUsuario(usuario_id);
        var qr_bytes  = GenerarQrBytes(qr_data);

        // 2. Generar PDF
        var pdf_bytes = GenerarPdf(usuario, qr_bytes, imagen_referencia);
        var pdf_b64   = Convert.ToBase64String(pdf_bytes);

        // 3. Firma electrónica (hash SHA-256 del PDF = firma básica avanzada)
        var firma = ComputarFirma(pdf_bytes, usuario_id);

        // 4. Persistir credencial
        var credencial = new credencial_pdf_entity
        {
            usuario_id      = usuario_id,
            archivo_base64  = pdf_b64,
            firma_electronica = firma,
            canal_envio     = "ambos",
            estado_envio    = "pendiente",
            fecha_generacion = DateTime.Now
        };
        _db.credenciales_pdf.Add(credencial);
        await _db.SaveChangesAsync();

        // 5. Guardar QR en BD
        await GuardarQrAsync(usuario_id, qr_data);

        // 6. Registrar métodos de notificación
        await GuardarMetodosNotificacionAsync(usuario_id, usuario.email, usuario.telefono);

        // 7. Envíos
        bool email_ok = false, whatsapp_ok = false;

        email_ok     = await EnviarEmailAsync(usuario, pdf_bytes, firma, credencial.id);
        whatsapp_ok  = await EnviarWhatsAppAsync(usuario, pdf_bytes, credencial.id);

        // 8. Actualizar estado
        credencial.estado_envio = (email_ok || whatsapp_ok) ? "enviado" : "error";
        credencial.fecha_envio  = DateTime.Now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[CREDENTIAL] ✅ Credencial generada. Email:{e} WA:{w}", email_ok, whatsapp_ok);

        return new CredentialResponse
        {
            credencial_id    = credencial.id,
            estado_envio     = credencial.estado_envio,
            archivo_base64   = pdf_b64,
            email_enviado    = email_ok,
            whatsapp_enviado = whatsapp_ok,
            fecha_generacion = credencial.fecha_generacion
        };
    }

    public async Task<CredentialResponse> ReenviarAsync(int usuario_id)
    {
        // Busca la última credencial generada y la reenvía
        var ultima = await _db.credenciales_pdf
            .Where(c => c.usuario_id == usuario_id)
            .OrderByDescending(c => c.fecha_generacion)
            .FirstOrDefaultAsync();

        if (ultima == null)
            return await GenerarYEnviarAsync(usuario_id);

        var usuario   = await _db.usuarios.AsNoTracking().FirstAsync(u => u.id == usuario_id);
        var pdf_bytes = Convert.FromBase64String(ultima.archivo_base64 ?? string.Empty);

        bool email_ok    = await EnviarEmailAsync(usuario, pdf_bytes, ultima.firma_electronica ?? "", ultima.id);
        bool whatsapp_ok = await EnviarWhatsAppAsync(usuario, pdf_bytes, ultima.id);

        ultima.estado_envio = (email_ok || whatsapp_ok) ? "enviado" : "error";
        ultima.fecha_envio  = DateTime.Now;
        await _db.SaveChangesAsync();

        return new CredentialResponse
        {
            credencial_id    = ultima.id,
            estado_envio     = ultima.estado_envio,
            email_enviado    = email_ok,
            whatsapp_enviado = whatsapp_ok,
            fecha_generacion = ultima.fecha_generacion
        };
    }

    // ── GENERACIÓN DEL PDF ───────────────────────────────────

    private byte[] GenerarPdf(user_entity usuario, byte[] qr_bytes, string? foto_facial_base64 = null)
    {
        using var ms     = new MemoryStream();
        using var writer = new PdfWriter(ms);
        using var pdf    = new PdfDocument(writer);
        using var doc    = new Document(pdf);

        doc.SetMargins(20, 25, 20, 25);

        // ── PALETA B — TECNOLÓGICO ───────────────────────────────
        var color_negro    = new DeviceRgb(17,  24,  39);   // #111827
        var color_azul_osc = new DeviceRgb(30,  58,  95);   // #1E3A5F
        var color_cyan     = new DeviceRgb(6,  182, 212);   // #06B6D4
        var color_purpura  = new DeviceRgb(109, 40, 217);   // #6D28D9
        var color_purp_bg  = new DeviceRgb(245, 243, 255);  // #F5F3FF
        var color_gris_clr = new DeviceRgb(249, 250, 251);  // #F9FAFB
        var color_texto    = new DeviceRgb(31,  41,  55);   // #1F2937
        var color_blanco   = ColorConstants.WHITE;

        var font_bold   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var font_normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var font_mono   = PdfFontFactory.CreateFont(StandardFonts.COURIER);

        // ── ENCABEZADO ──────────────────────────────────────────
        var header_table = new Table(new float[] { 1, 3 }).UseAllAvailableWidth();
        header_table.SetBackgroundColor(color_negro);

        var logo_cell = new Cell()
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetPadding(12)
            .Add(new Paragraph("UMG")
                .SetFont(font_bold).SetFontSize(26)
                .SetFontColor(color_cyan)
                .SetTextAlignment(TextAlignment.CENTER));
        header_table.AddCell(logo_cell);

        var title_cell = new Cell()
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetPadding(12)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
            .Add(new Paragraph("UNIVERSIDAD MARIANO GALVEZ DE GUATEMALA")
                .SetFont(font_bold).SetFontSize(11).SetFontColor(color_blanco))
            .Add(new Paragraph("Ingenieria en Sistemas")
                .SetFont(font_normal).SetFontSize(10).SetFontColor(color_blanco))
            .Add(new Paragraph("UMG Basic Rover 2.0 - 2026")
                .SetFont(font_bold).SetFontSize(9)
                .SetFontColor(color_cyan));
        header_table.AddCell(title_cell);
        doc.Add(header_table);

        // ── TÍTULO CREDENCIAL ───────────────────────────────────
        var titulo_table = new Table(1).UseAllAvailableWidth();
        titulo_table.SetMarginBottom(10);
        var titulo_cell = new Cell()
            .SetBackgroundColor(color_azul_osc)
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetPadding(7)
            .Add(new Paragraph("CREDENCIAL DE ACCESO  |  ASPIRANTE CONDUCTOR")
                .SetFont(font_bold).SetFontSize(12)
                .SetFontColor(color_cyan)
                .SetTextAlignment(TextAlignment.CENTER));
        titulo_table.AddCell(titulo_cell);
        doc.Add(titulo_table);

        // ── DATOS PRINCIPALES ───────────────────────────────────
        var data_table = new Table(new float[] { 1, 2 }).UseAllAvailableWidth();
        data_table.SetMarginBottom(10);

        AgregarFila(data_table, "Nickname",       usuario.usuario,         font_bold, font_normal, color_azul_osc);
        AgregarFila(data_table, "Nombre",         usuario.nombre_completo, font_bold, font_normal, color_azul_osc);
        AgregarFila(data_table, "Correo",         usuario.email,           font_bold, font_normal, color_azul_osc);
        AgregarFila(data_table, "Telefono",       usuario.telefono,        font_bold, font_normal, color_azul_osc);
        AgregarFila(data_table, "Rol",            "Aspirante Conductor",   font_bold, font_normal, color_azul_osc);
        AgregarFila(data_table, "Registro",       usuario.fecha_creacion.ToString("dd/MM/yyyy HH:mm"), font_bold, font_normal, color_azul_osc);
        doc.Add(data_table);

        // ── QR + AVATAR + FOTO ──────────────────────────────────
        var qr_section = new Table(new float[] { 1, 1, 1 }).UseAllAvailableWidth();
        qr_section.SetMarginBottom(10);

        // Columna 1 — QR
        var qr_image = ImageDataFactory.Create(qr_bytes);
        var qr_cell  = new Cell()
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetBackgroundColor(color_gris_clr)
            .SetPadding(8)
            .SetTextAlignment(TextAlignment.CENTER)
            .Add(new Paragraph("Codigo QR de Acceso")
                .SetFont(font_bold).SetFontSize(9).SetFontColor(color_cyan))
            .Add(new Image(qr_image).SetWidth(90).SetHeight(90));
        qr_section.AddCell(qr_cell);

        // Columna 2 — Avatar
        var avatar_cell = new Cell()
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetBackgroundColor(color_gris_clr)
            .SetPadding(8)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
            .SetTextAlignment(TextAlignment.CENTER);

        if (!string.IsNullOrEmpty(usuario.avatar_base64))
        {
            try
            {
                var svg_base64 = usuario.avatar_base64.Contains(",")
                    ? usuario.avatar_base64.Split(',')[1]
                    : usuario.avatar_base64;
                var svg_bytes = Convert.FromBase64String(svg_base64);
                using var svg_stream = new MemoryStream(svg_bytes);
                var svg = new Svg.Skia.SKSvg();
                svg.Load(svg_stream);
                using var bitmap = new SkiaSharp.SKBitmap(200, 200);
                using var canvas_sk = new SkiaSharp.SKCanvas(bitmap);
                canvas_sk.Clear(SkiaSharp.SKColors.White);
                var scale_x = 200f / svg.Picture!.CullRect.Width;
                var scale_y = 200f / svg.Picture.CullRect.Height;
                var matrix  = SkiaSharp.SKMatrix.CreateScale(scale_x, scale_y);
                canvas_sk.DrawPicture(svg.Picture, ref matrix);
                using var png_stream = new MemoryStream();
                bitmap.Encode(png_stream, SkiaSharp.SKEncodedImageFormat.Png, 100);
                var avatar_img = ImageDataFactory.Create(png_stream.ToArray());
                avatar_cell
                    .Add(new Paragraph("Avatar")
                        .SetFont(font_bold).SetFontSize(9).SetFontColor(color_cyan))
                    .Add(new Image(avatar_img).SetWidth(70).SetHeight(70));
            }
            catch
            {
                avatar_cell.Add(new Paragraph("Avatar\nno disponible")
                    .SetFont(font_normal).SetFontSize(8));
            }
        }
        else
        {
            avatar_cell.Add(new Paragraph($"[ {usuario.usuario} ]")
                .SetFont(font_bold).SetFontSize(13)
                .SetFontColor(color_cyan)
                .SetBackgroundColor(color_azul_osc)
                .SetPadding(15));
        }
        qr_section.AddCell(avatar_cell);

        // Columna 3 — Foto facial
        var foto_cell = new Cell()
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetBackgroundColor(color_gris_clr)
            .SetPadding(8)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
            .SetTextAlignment(TextAlignment.CENTER);

        if (!string.IsNullOrEmpty(foto_facial_base64))
        {
            try
            {
                var foto_bytes = Convert.FromBase64String(
                    foto_facial_base64.Contains(",")
                        ? foto_facial_base64.Split(',')[1]
                        : foto_facial_base64);
                var foto_img = ImageDataFactory.Create(foto_bytes);
                foto_cell
                    .Add(new Paragraph("Foto del Conductor")
                        .SetFont(font_bold).SetFontSize(9).SetFontColor(color_cyan))
                    .Add(new Image(foto_img).SetWidth(90).SetHeight(90)
                        .SetBorderRadius(new iText.Layout.Properties.BorderRadius(6)));
            }
            catch
            {
                foto_cell.Add(new Paragraph("Foto\nno disponible")
                    .SetFont(font_normal).SetFontSize(8));
            }
        }
        else
        {
            foto_cell.Add(new Paragraph("Sin foto\nregistrada")
                .SetFont(font_normal).SetFontSize(8)
                .SetFontColor(new DeviceRgb(150, 150, 150)));
        }
        qr_section.AddCell(foto_cell);
        doc.Add(qr_section);

        // ── FIRMA ELECTRÓNICA ───────────────────────────────────
        var firma_panel = new Table(1).UseAllAvailableWidth();
        firma_panel.SetMarginBottom(10);
        var firma_hash = ComputarFirma(qr_bytes, usuario.id);

        var firma_cell = new Cell()
            .SetBackgroundColor(color_purp_bg)
            .SetPadding(10)
            .SetBorder(new iText.Layout.Borders.SolidBorder(color_purpura, 1.5f))
            .Add(new Paragraph("FIRMA ELECTRONICA AVANZADA")
                .SetFont(font_bold).SetFontSize(9).SetFontColor(color_purpura))
            .Add(new Paragraph(firma_hash)
                .SetFont(font_mono).SetFontSize(7f).SetFontColor(color_texto))
            .Add(new Paragraph($"Emitida: {DateTime.Now:dd/MM/yyyy HH:mm:ss} UTC  |  Algoritmo: SHA-256  |  Proyecto: UMG Basic Rover 2.0-2026")
                .SetFont(font_normal).SetFontSize(7.5f).SetFontColor(color_texto));
        firma_panel.AddCell(firma_cell);
        doc.Add(firma_panel);

        // ── PIE ─────────────────────────────────────────────────
        var pie_table = new Table(1).UseAllAvailableWidth();
        var pie_cell = new Cell()
            .SetBackgroundColor(color_negro)
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetPadding(6)
            .Add(new Paragraph("Documento oficial generado electronicamente. La firma avanzada garantiza su autenticidad. | UMG Ingenieria en Sistemas 2026")
                .SetFont(font_normal).SetFontSize(7f)
                .SetFontColor(new DeviceRgb(156, 163, 175))
                .SetTextAlignment(TextAlignment.CENTER));
        pie_table.AddCell(pie_cell);
        doc.Add(pie_table);

        doc.Close();
        return ms.ToArray();
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

    // ── QR ───────────────────────────────────────────────────

    private static byte[] GenerarQrBytes(string data)
    {
        using var qr_generator = new QRCodeGenerator();
        var qr_data            = qr_generator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        var qr_code            = new PngByteQRCode(qr_data);
        return qr_code.GetGraphic(10);
    }

    private static string CifrarIdUsuario(int usuario_id)
    {
        var data  = Encoding.UTF8.GetBytes($"UMG_ROVER_{usuario_id}_{DateTime.Now:yyyyMMdd}");
        var hash  = SHA256.HashData(data);
        return $"ROVER-{usuario_id}-{Convert.ToHexString(hash)[..16]}";
    }

    private static string ComputarFirma(byte[] contenido, int usuario_id)
    {
        var salt    = Encoding.UTF8.GetBytes($"UMG_FIRMA_{usuario_id}");
        var payload = contenido.Concat(salt).ToArray();
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    // ── PERSISTENCIA AUX ─────────────────────────────────────

    private async Task GuardarQrAsync(int usuario_id, string qr_data)
    {
        var existente = await _db.codigos_qr
            .FirstOrDefaultAsync(q => q.usuario_id == usuario_id && q.activo);
        if (existente != null) { existente.activo = false; }

        _db.codigos_qr.Add(new codigo_qr_entity
        {
            usuario_id     = usuario_id,
            codigo_qr      = qr_data,
            qr_hash        = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(qr_data))),
            activo         = true,
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
                { usuario_id = usuario_id, tipo_notificacion = "email", destino = email });
        }

        if (!existentes.Any(m => m.tipo_notificacion == "whatsapp"))
        {
            _db.metodos_notificacion.Add(new metodo_notificacion_entity
                { usuario_id = usuario_id, tipo_notificacion = "whatsapp", destino = telefono });
        }

        await _db.SaveChangesAsync();
    }

    // ── EMAIL (SMTP) ─────────────────────────────────────────
    private async Task<bool> EnviarEmailAsync(user_entity usuario, byte[] pdf_bytes, string firma, int credencial_id)
    {
        try
        {
            var api_key   = _config["SendGrid:ApiKey"]   ?? throw new InvalidOperationException("SendGrid:ApiKey no configurado.");
            var from_email = _config["SendGrid:From"]    ?? throw new InvalidOperationException("SendGrid:From no configurado.");
            var from_name  = _config["SendGrid:FromName"] ?? "UMG Basic Rover 2.0";

            var client  = new SendGrid.SendGridClient(api_key);
            var from    = new SendGrid.Helpers.Mail.EmailAddress(from_email, from_name);
            var to      = new SendGrid.Helpers.Mail.EmailAddress(usuario.email, usuario.nombre_completo);
            var subject = $"🏁 Tu Credencial de Acceso — UMG Basic Rover 2.0 | {usuario.usuario}";

            var html_body = $@"
    <!DOCTYPE html>
    <html>
    <head><meta charset='UTF-8'></head>
    <body style='font-family: Arial, sans-serif; background:#f5f5f5; margin:0; padding:20px;'>
    <div style='max-width:600px; margin:auto; background:white; border-radius:8px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.1);'>
        <div style='background:#003087; padding:24px; text-align:center;'>
        <h1 style='color:white; margin:0; font-size:22px;'>🏁 UMG Basic Rover 2.0</h1>
        <p style='color:#FFD700; margin:6px 0 0;'>Universidad Mariano Gálvez de Guatemala</p>
        </div>
        <div style='padding:28px;'>
        <h2 style='color:#003087;'>¡Bienvenido, <strong>{usuario.usuario}</strong>!</h2>
        <p style='color:#333; font-size:15px;'>Tu registro en la plataforma <strong>UMG Basic Rover 2.0</strong> fue exitoso.</p>
        <p style='color:#333; font-size:15px;'>Adjunto encontrarás tu <strong>credencial de acceso en formato PDF</strong>, firmada electrónicamente con tu información y código QR de acceso.</p>
        <div style='background:#f0f4ff; border-left:4px solid #003087; padding:14px; margin:20px 0; border-radius:4px;'>
            <p style='margin:0; font-size:13px; color:#555;'><strong>Firma Electrónica:</strong><br/>
            <code style='font-size:11px; word-break:break-all;'>{firma[..32]}...</code></p>
        </div>
        <p style='color:#555; font-size:13px;'>Usa tu nickname <strong>{usuario.usuario}</strong> y contraseña para ingresar a la plataforma.</p>
        </div>
        <div style='background:#003087; padding:16px; text-align:center;'>
        <p style='color:#aaa; font-size:12px; margin:0;'>UMG Ingeniería en Sistemas — Compiladores 2026</p>
        </div>
    </div>
    </body>
    </html>";

            var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(from, to, subject, "", html_body);

            // Adjuntar PDF
            var pdf_b64 = Convert.ToBase64String(pdf_bytes);
            msg.AddAttachment($"credencial_{usuario.usuario}.pdf", pdf_b64, "application/pdf");

            var response = await client.SendEmailAsync(msg);
            var enviado  = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300;

            await RegistrarNotificacionAsync(usuario.id, "credencial_pdf", "email",
                $"Credencial PDF — {usuario.usuario}", enviado ? "enviado" : "error", credencial_id);

            _logger.LogInformation("[CREDENTIAL] ✅ Email SendGrid enviado a: {email} | Status: {s}", usuario.email, response.StatusCode);
            return enviado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREDENTIAL] ❌ Error al enviar email a: {email}", usuario.email);
            await RegistrarNotificacionAsync(usuario.id, "credencial_pdf", "email",
                $"Credencial PDF — {usuario.usuario}", "error", credencial_id);
            return false;
        }
    }
    // ── WHATSAPP (TWILIO) ────────────────────────────────────

    private async Task<bool> EnviarWhatsAppAsync(user_entity usuario, byte[] pdf_bytes, int credencial_id)
    {
        try
        {
            var account_sid = _config["Twilio:AccountSid"];
            var auth_token  = _config["Twilio:AuthToken"];
            var from_wa     = _config["Twilio:WhatsAppFrom"] ?? "whatsapp:+14155238886";

            if (string.IsNullOrEmpty(account_sid) || string.IsNullOrEmpty(auth_token))
            {
                _logger.LogWarning("[CREDENTIAL] Twilio no configurado. Saltando WhatsApp.");
                return false;
            }

            Twilio.TwilioClient.Init(account_sid, auth_token);

            var telefono_wa = usuario.telefono.StartsWith("+") ? usuario.telefono : $"+{usuario.telefono}";

            var message = await Twilio.Rest.Api.V2010.Account.MessageResource.CreateAsync(
                body: $"🏁 *UMG Basic Rover 2.0*\n\n¡Hola *{usuario.usuario}*! Tu registro fue exitoso.\n\nTu credencial de acceso ha sido generada. La recibirás también en tu correo *{usuario.email}* con el PDF adjunto.\n\n✅ _Universidad Mariano Gálvez — Ingeniería en Sistemas 2026_",
                from: new Twilio.Types.PhoneNumber(from_wa),
                to:   new Twilio.Types.PhoneNumber($"whatsapp:{telefono_wa}")
            );

            var enviado = message.Status != Twilio.Rest.Api.V2010.Account.MessageResource.StatusEnum.Failed;

            await RegistrarNotificacionAsync(usuario.id, "credencial_pdf", "whatsapp",
                "Credencial generada — bienvenida", enviado ? "enviado" : "error", credencial_id);

            _logger.LogInformation("[CREDENTIAL] WhatsApp {status} a: {tel}", message.Status, telefono_wa);
            return enviado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREDENTIAL] ❌ Error WhatsApp a: {tel}", usuario.telefono);
            await RegistrarNotificacionAsync(usuario.id, "credencial_pdf", "whatsapp",
                "Credencial generada", "error", credencial_id);
            return false;
        }
    }

    // ── HISTORIAL NOTIFICACIONES ─────────────────────────────

    private async Task RegistrarNotificacionAsync(int usuario_id, string tipo, string canal,
        string asunto, string estado, int? referencia_id = null)
    {
        _db.historial_notificaciones.Add(new historial_notificacion_entity
        {
            usuario_id    = usuario_id,
            tipo          = tipo,
            canal         = canal,
            asunto        = asunto,
            estado        = estado,
            referencia_id = referencia_id,
            fecha_envio   = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }
}

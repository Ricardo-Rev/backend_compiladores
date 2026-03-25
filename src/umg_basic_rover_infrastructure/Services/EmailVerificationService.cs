using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using umg_basic_rover_domain.entities;
using umg_basic_rover_infrastructure.persistence.context;

namespace umg_basic_rover_infrastructure.Services;

public class EmailVerificationService
{
    private readonly rover_db_context _db;
    private readonly IConfiguration   _config;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(rover_db_context db, IConfiguration config,
        ILogger<EmailVerificationService> logger)
    {
        _db     = db;
        _config = config;
        _logger = logger;
    }

    // Genera token, lo guarda en BD y envía el email
    public async Task EnviarVerificacionAsync(int usuario_id, string email, string nombre)
    {
        // 1. Invalidar tokens anteriores
        var anteriores = await _db.tokens_email_verificacion
            .Where(t => t.usuario_id == usuario_id && !t.usado)
            .ToListAsync();
        foreach (var t in anteriores) t.usado = true;
        await _db.SaveChangesAsync();

        // 2. Generar nuevo token
        var token = Guid.NewGuid().ToString("N"); // 32 chars sin guiones
        _db.tokens_email_verificacion.Add(new token_email_verificacion_entity
        {
            usuario_id     = usuario_id,
            token          = token,
            expira_en      = DateTime.Now.AddHours(24),
            usado          = false,
            fecha_creacion = DateTime.Now
        });
        await _db.SaveChangesAsync();

        // 3. Enviar email
        var base_url   = _config["App:BaseUrl"] ?? "http://localhost:5173";
        var verify_url = $"{base_url}/verify-email?token={token}";

        var api_key    = _config["SendGrid:ApiKey"]    ?? throw new InvalidOperationException("SendGrid:ApiKey no configurado.");
        var from_email = _config["SendGrid:From"]      ?? throw new InvalidOperationException("SendGrid:From no configurado.");
        var from_name  = _config["SendGrid:FromName"]  ?? "UMG Basic Rover 2.0";

        var client  = new SendGrid.SendGridClient(api_key);
        var from    = new SendGrid.Helpers.Mail.EmailAddress(from_email, from_name);
        var to      = new SendGrid.Helpers.Mail.EmailAddress(email, nombre);
        var subject = "Confirma tu correo — UMG Basic Rover 2.0";

        var html_body = $@"
<!DOCTYPE html>
<html>
<head><meta charset='UTF-8'></head>
<body style='font-family:Arial,sans-serif;background:#0D1B2A;margin:0;padding:20px;'>
<div style='max-width:600px;margin:auto;background:#111827;border-radius:10px;overflow:hidden;'>
    <div style='background:#111827;padding:24px;text-align:center;border-bottom:2px solid #06B6D4;'>
        <h1 style='color:#06B6D4;margin:0;font-size:22px;'>UMG Basic Rover 2.0</h1>
        <p style='color:#9CA3AF;margin:6px 0 0;font-size:13px;'>Universidad Mariano Galvez de Guatemala</p>
    </div>
    <div style='padding:32px;'>
        <h2 style='color:white;'>Hola, <strong style='color:#06B6D4;'>{nombre}</strong></h2>
        <p style='color:#D1D5DB;font-size:15px;'>Gracias por registrarte. Para activar tu cuenta haz click en el botón:</p>
        <div style='text-align:center;margin:30px 0;'>
            <a href='{verify_url}' style='background:#06B6D4;color:#111827;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:bold;font-size:16px;'>
                Verificar mi correo
            </a>
        </div>
        <p style='color:#9CA3AF;font-size:12px;'>Este link expira en 24 horas. Si no creaste una cuenta ignora este mensaje.</p>
        <p style='color:#6B7280;font-size:11px;word-break:break-all;'>Si el botón no funciona copia este link: {verify_url}</p>
    </div>
    <div style='background:#1E3A5F;padding:14px;text-align:center;'>
        <p style='color:#6B7280;font-size:11px;margin:0;'>UMG Ingenieria en Sistemas — Compiladores 2026</p>
    </div>
</div>
</body>
</html>";

        var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(from, to, subject, "", html_body);
        var response = await client.SendEmailAsync(msg);
        _logger.LogInformation("[EMAIL-VERIFY] Email enviado a {email} | Status: {s}", email, response.StatusCode);
    }

    // Valida el token y marca el correo como confirmado
    public async Task<(bool ok, string mensaje)> VerificarTokenAsync(string token)
    {
        var registro = await _db.tokens_email_verificacion
            .Include(t => t.usuario)
            .FirstOrDefaultAsync(t => t.token == token && !t.usado);

        if (registro is null)
            return (false, "El link de verificación no es válido o ya fue usado.");

        if (registro.expira_en < DateTime.Now)
            return (false, "El link de verificación ha expirado. Solicita uno nuevo.");

        // Marcar como usado y confirmar email
        registro.usado = true;

        var usuario = await _db.usuarios.FirstAsync(u => u.id == registro.usuario_id);
        usuario.email_confirmado = true;

        await _db.SaveChangesAsync();

        _logger.LogInformation("[EMAIL-VERIFY] ✅ Email confirmado. Usuario ID: {id}", usuario.id);
        return (true, "Correo verificado exitosamente.");
    }
}
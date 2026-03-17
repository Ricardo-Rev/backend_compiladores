    using System.Text.Json;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using umg_basic_rover_application.Contracts;

    namespace umg_basic_rover_infrastructure.Services;

    // ============================================================
    //  IMPLEMENTACIÓN: RecaptchaService
    //  Valida tokens de Google reCAPTCHA v2 consultando la API oficial.
    //
    //  FLUJO COMPLETO:
    //  1. El frontend muestra el widget de reCAPTCHA v2
    //  2. El usuario marca "No soy un robot"
    //  3. Google genera un token (expira en 2 minutos)
    //  4. El frontend envía el token en el body del request
    //  5. ESTE SERVICIO envía el token a la API de Google:
    //     POST https://www.google.com/recaptcha/api/siteverify
    //  6. Google responde con { "success": true/false }
    //  7. Si success = false → rechazar el request con 400
    //
    //  CONFIGURACIÓN NECESARIA (appsettings.json):
    //  "Recaptcha": {
    //    "SecretKey": "TU_SECRET_KEY_AQUI"  ← Se obtiene de Google Console
    //  }
    //
    //  CÓMO OBTENER LAS KEYS:
    //  1. Ir a: https://www.google.com/recaptcha/admin
    //  2. Crear un sitio nuevo → Elegir "reCAPTCHA v2"
    //  3. Agregar tu dominio (localhost para desarrollo)
    //  4. Obtendrás: Site Key (para el frontend) y Secret Key (para el backend)
    // ============================================================

    public class RecaptchaService : IRecaptchaService
    {
        private readonly IHttpClientFactory _http_factory;
        private readonly IConfiguration _config;
        private readonly ILogger<RecaptchaService> _logger;

        // URL oficial de verificación de Google
        private const string VERIFY_URL = "https://www.google.com/recaptcha/api/siteverify";

        public RecaptchaService(
            IHttpClientFactory http_factory,
            IConfiguration config,
            ILogger<RecaptchaService> logger)
        {
            _http_factory = http_factory;
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Valida el token con la API de Google reCAPTCHA.
        /// Retorna true si el token es válido, false si no.
        /// </summary>
        public async Task<bool> ValidateAsync(string token)
        {
            // Token vacío = inválido directamente
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("[RECAPTCHA] Token vacío recibido.");
                return false;
            }

            // ── BYPASS PARA DESARROLLO ──────────────────────────────
            // Si está configurado un token de bypass en appsettings,
            // se acepta directamente sin llamar a Google.
            var bypass_token = _config["Recaptcha:BypassToken"];
            if (!string.IsNullOrWhiteSpace(bypass_token) && token == bypass_token)
            {
                _logger.LogWarning("[RECAPTCHA] ⚠️ Bypass de desarrollo activado. NO usar en producción.");
                return true;
            }

            // Obtener la Secret Key del appsettings
            var secret_key = _config["Recaptcha:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret_key))
            {
                _logger.LogError("[RECAPTCHA] ❌ Recaptcha:SecretKey no está configurada en appsettings.");
                return false;
            }

            try
            {
                var client = _http_factory.CreateClient("recaptcha");

                // Construir el body del request hacia Google
                // Google espera: application/x-www-form-urlencoded
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret", secret_key),
                    new KeyValuePair<string, string>("response", token)
                });

                // Llamar a la API de Google
                var response = await client.PostAsync(VERIFY_URL, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[RECAPTCHA] ⚠️ Google respondió con status {Status}", response.StatusCode);
                    return false;
                }

                // Parsear la respuesta JSON de Google
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var success = doc.RootElement
                    .GetProperty("success")
                    .GetBoolean();

                if (success)
                {
                    _logger.LogInformation("[RECAPTCHA] ✅ Token válido.");
                }
                else
                {
                    // Obtener los códigos de error de Google (si los hay)
                    var error_codes = doc.RootElement.TryGetProperty("error-codes", out var errors)
                        ? string.Join(", ", errors.EnumerateArray().Select(e => e.GetString()))
                        : "sin detalles";

                    _logger.LogWarning("[RECAPTCHA] ❌ Token inválido. Errores: {Errors}", error_codes);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RECAPTCHA] ❌ Error al contactar la API de Google.");
                return false;
            }
        }
    }

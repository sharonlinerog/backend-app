namespace CreditosApi.Services;

/// <summary>
/// Configuración para firmar y validar los tokens JWT, leída desde
/// appsettings.json / variables de entorno.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Clave secreta usada para firmar los tokens (HMAC-SHA256).
    /// Debe tener al menos 32 caracteres (256 bits); nunca debe subirse
    /// a un repositorio con un valor real.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 60;
}

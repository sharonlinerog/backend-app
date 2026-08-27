namespace CreditosApi.Services;

/// <summary>
/// Configuración del servidor SMTP, leída desde appsettings.json / variables de entorno.
/// Nunca se deben poner usuario/contraseña reales directamente en este proyecto:
/// van en appsettings.Development.json (ignorado por git) o en variables de entorno.
/// </summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "App Creditos";
    public bool EnableSsl { get; set; } = true;
    public string DestinatarioCreditos { get; set; } = "fyasocialcapital@gmail.com";
}

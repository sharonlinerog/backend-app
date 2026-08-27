using System.Globalization;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CreditosApi.Services;

/// <summary>
/// Este es el "worker" que corre en segundo plano durante toda la vida de la
/// aplicación (BackgroundService lo registra ASP.NET Core como un hosted service).
///
/// Su trabajo es simple y en bucle infinito:
///   1. Esperar a que llegue un mensaje a la cola (IEmailQueue).
///   2. Armar el correo y enviarlo por SMTP.
///   3. Si falla, lo registra en el log pero NO tumba la aplicación ni afecta
///      al usuario que registró el crédito (esa petición HTTP ya respondió hace rato).
///
/// Con esto se cumple el requisito de "envío independiente / asíncrono / en segundo
/// plano" sin depender de un servicio externo de colas.
/// </summary>
public class EmailSenderBackgroundService : BackgroundService
{
    private readonly IEmailQueue _queue;
    // IOptionsMonitor (en vez de IOptions) para que, si alguien cambia el SMTP en
    // appsettings.json mientras la app está corriendo, el siguiente correo ya use
    // la configuración nueva sin tener que reiniciar el proceso.
    private readonly IOptionsMonitor<SmtpOptions> _smtpOptionsMonitor;
    private readonly ILogger<EmailSenderBackgroundService> _logger;

    public EmailSenderBackgroundService(
        IEmailQueue queue,
        IOptionsMonitor<SmtpOptions> smtpOptionsMonitor,
        ILogger<EmailSenderBackgroundService> logger)
    {
        _queue = queue;
        _smtpOptionsMonitor = smtpOptionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailSenderBackgroundService iniciado, esperando créditos para notificar por correo.");

        // ReadAllAsync se queda "dormido" sin consumir CPU hasta que llega un mensaje nuevo a la cola.
        await foreach (var mensaje in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await EnviarCorreoAsync(mensaje, stoppingToken);
                _logger.LogInformation(
                    "Correo de notificación enviado para el crédito de {Cliente} por {Comercial}",
                    mensaje.NombreCliente, mensaje.Comercial);
            }
            catch (Exception ex)
            {
                // Importante: si el correo falla (SMTP caído, credenciales mal, etc.)
                // el crédito YA quedó guardado en la base de datos. No perdemos el registro,
                // solo queda constancia en el log de que la notificación falló.
                _logger.LogError(ex,
                    "No se pudo enviar el correo de notificación para el crédito de {Cliente}",
                    mensaje.NombreCliente);
            }
        }
    }

    private async Task EnviarCorreoAsync(EmailMessage mensaje, CancellationToken cancellationToken)
    {
        // Se lee CurrentValue en cada envío (no en el constructor) para tomar
        // siempre la configuración SMTP más reciente.
        var smtpOptions = _smtpOptionsMonitor.CurrentValue;

        using var smtpClient = new SmtpClient(smtpOptions.Host, smtpOptions.Port)
        {
            EnableSsl = smtpOptions.EnableSsl,
            Credentials = new NetworkCredential(smtpOptions.User, smtpOptions.Password)
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(smtpOptions.FromAddress, smtpOptions.FromName),
            Subject = $"Nuevo crédito registrado - {mensaje.NombreCliente}",
            Body = ConstruirCuerpoCorreo(mensaje),
            IsBodyHtml = true
        };
        mailMessage.To.Add(smtpOptions.DestinatarioCreditos);

        try
        {
            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
        }
        catch (SmtpException ex) when (ex.Message.Contains("Authentication", StringComparison.OrdinalIgnoreCase))
        {
            // Pista específica para el caso más común: credenciales de ejemplo,
            // contraseña normal de Gmail (en vez de una "contraseña de aplicación"),
            // o la clave copiada con espacios.
            throw new InvalidOperationException(
                "El servidor SMTP rechazó la autenticación. Verifica: 1) que Smtp:User y Smtp:Password en " +
                "appsettings.json ya NO sean los valores de ejemplo, 2) que reiniciaste 'dotnet run' después " +
                "de editar el archivo, 3) que si usas Gmail, Smtp:Password sea una 'contraseña de aplicación' " +
                "de 16 caracteres SIN espacios (no la contraseña normal de la cuenta).", ex);
        }
    }

    // Cultura de Colombia para formatear el valor como pesos (COP) sin depender
    // de la cultura del servidor (en Linux suele ser Invariant/en-US).
    private static readonly CultureInfo CulturaCo = CultureInfo.GetCultureInfo("es-CO");

    private static string ConstruirCuerpoCorreo(EmailMessage mensaje)
    {
        // La fecha se guarda en UTC; se convierte a la hora de Colombia para el correo.
        var fechaLocal = ConvertirAHoraColombia(mensaje.FechaRegistro);
        var valorFormateado = mensaje.ValorCredito.ToString("C0", CulturaCo);

        return $"""
            <h2>Nuevo crédito registrado</h2>
            <p><strong>Nombre del cliente:</strong> {WebUtility.HtmlEncode(mensaje.NombreCliente)}</p>
            <p><strong>Valor del crédito:</strong> {valorFormateado}</p>
            <p><strong>Comercial:</strong> {WebUtility.HtmlEncode(mensaje.Comercial)}</p>
            <p><strong>Fecha de registro:</strong> {fechaLocal:dd/MM/yyyy HH:mm} (hora Colombia)</p>
            """;
    }

    private static DateTime ConvertirAHoraColombia(DateTime fechaUtc)
    {
        try
        {
            // "America/Bogota" en Linux/macOS; "SA Pacific Standard Time" en Windows.
            var zonaId = OperatingSystem.IsWindows() ? "SA Pacific Standard Time" : "America/Bogota";
            var zona = TimeZoneInfo.FindSystemTimeZoneById(zonaId);
            return TimeZoneInfo.ConvertTimeFromUtc(fechaUtc, zona);
        }
        catch (TimeZoneNotFoundException)
        {
            // Si el sistema no tiene la zona horaria instalada, se cae a UTC-5 fijo (Colombia no usa horario de verano).
            return fechaUtc.AddHours(-5);
        }
    }
}

namespace CreditosApi.Services;

/// <summary>
/// Cola de correos pendientes por enviar. El Controller solo "deja" el mensaje
/// aquí (QueueEmail) y sigue de inmediato con su respuesta HTTP; nunca espera
/// a que el correo salga. El envío real lo hace EmailSenderBackgroundService,
/// que va sacando mensajes de esta misma cola en un hilo aparte.
/// </summary>
public interface IEmailQueue
{
    void QueueEmail(EmailMessage message);

    IAsyncEnumerable<EmailMessage> DequeueAllAsync(CancellationToken cancellationToken);
}

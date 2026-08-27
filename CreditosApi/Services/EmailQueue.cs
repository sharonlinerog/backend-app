using System.Threading.Channels;

namespace CreditosApi.Services;

/// <summary>
/// Implementación de la cola usando System.Threading.Channels, que es la forma
/// recomendada por Microsoft de tener un "productor/consumidor" en memoria dentro
/// de una app ASP.NET Core, sin depender de un servicio externo (RabbitMQ, etc.).
///
/// Se registra como Singleton en Program.cs: existe UNA sola cola durante toda
/// la vida de la aplicación, compartida entre todas las peticiones.
/// </summary>
public class EmailQueue : IEmailQueue
{
    // Channel sin límite de tamaño: si en algún pico se registran muchos créditos
    // seguidos, los mensajes se acumulan aquí en vez de perderse o bloquear al usuario.
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();

    public void QueueEmail(EmailMessage message)
    {
        // TryWrite es no bloqueante: el hilo que registra el crédito no se detiene ni un milisegundo esperando el correo.
        _channel.Writer.TryWrite(message);
    }

    public IAsyncEnumerable<EmailMessage> DequeueAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

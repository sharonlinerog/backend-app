using System.Net;
using System.Text.Json;

namespace CreditosApi.Middleware;

/// <summary>
/// Middleware = una pieza que se ejecuta "envolviendo" cada petición HTTP.
/// Este en particular atrapa cualquier excepción no controlada que ocurra
/// más adelante en el pipeline (Controllers, Services, EF Core, etc.) y,
/// en vez de dejar que .NET devuelva un error genérico con detalles internos,
/// responde siempre con un JSON limpio y consistente.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado procesando {Method} {Path}", context.Request.Method, context.Request.Path);

            // Si la respuesta ya empezó a enviarse (headers ya emitidos) no se puede
            // reescribir: se relanza para no enmascarar la excepción original.
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var payload = JsonSerializer.Serialize(new
            {
                error = "Ocurrió un error inesperado procesando la solicitud.",
                traceId = context.TraceIdentifier
            });

            await context.Response.WriteAsync(payload);
        }
    }
}

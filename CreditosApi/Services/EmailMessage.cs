namespace CreditosApi.Services;

/// <summary>
/// Estructura simple con la información que necesitamos para armar
/// el correo de notificación de un nuevo crédito.
/// </summary>
public record EmailMessage(
    string NombreCliente,
    decimal ValorCredito,
    string Comercial,
    DateTime FechaRegistro
);

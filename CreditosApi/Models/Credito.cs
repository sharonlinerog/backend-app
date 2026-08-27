namespace CreditosApi.Models;

/// <summary>
/// Representa un crédito registrado en el sistema.
/// Esta es la clase que EF Core mapea 1 a 1 contra la tabla "creditos" en PostgreSQL.
/// </summary>
public class Credito
{
    public Guid Id { get; set; }

    public string NombreCliente { get; set; } = string.Empty;

    public string Cedula { get; set; } = string.Empty;

    public decimal ValorCredito { get; set; }

    public decimal TasaInteres { get; set; }

    public int PlazoMeses { get; set; }

    public string Comercial { get; set; } = string.Empty;

    public DateTime FechaRegistro { get; set; }
}

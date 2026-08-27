namespace CreditosApi.Dtos;

/// <summary>
/// Forma en la que devolvemos un crédito al frontend.
/// Se usa un DTO de salida (en vez de devolver la entidad Credito directamente)
/// para tener control total de qué campos viajan por la API.
/// </summary>
public class CreditoResponseDto
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

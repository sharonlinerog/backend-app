using System.ComponentModel.DataAnnotations;

namespace CreditosApi.Dtos;

/// <summary>
/// Datos que el frontend envía para registrar un nuevo crédito.
/// Las validaciones de aquí son la "segunda barrera": el frontend valida primero,
/// pero el backend NUNCA confía en eso y vuelve a validar todo antes de tocar la base de datos.
/// </summary>
public class CreditoCreateDto
{
    [Required(ErrorMessage = "El nombre del cliente es obligatorio")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 150 caracteres")]
    public string NombreCliente { get; set; } = string.Empty;

    [Required(ErrorMessage = "La cédula o ID es obligatoria")]
    [StringLength(30, MinimumLength = 5, ErrorMessage = "La cédula/ID debe tener entre 5 y 30 caracteres")]
    [RegularExpression(@"^[A-Za-z0-9\-]+$", ErrorMessage = "La cédula/ID solo puede contener letras, números y guiones")]
    public string Cedula { get; set; } = string.Empty;

    // Se usa la sobrecarga [Range(typeof(decimal), ...)] con los límites como texto:
    // la sobrecarga [Range(int, int)] convertiría el decimal a int antes de comparar
    // (redondeando), y dejaría pasar valores de borde como una tasa de 100.49.
    [Required(ErrorMessage = "El valor del crédito es obligatorio")]
    [Range(typeof(decimal), "1", "1000000000", ErrorMessage = "El valor del crédito debe estar entre 1 y 1.000.000.000")]
    public decimal ValorCredito { get; set; }

    [Required(ErrorMessage = "La tasa de interés es obligatoria")]
    [Range(typeof(decimal), "0", "100", ErrorMessage = "La tasa de interés debe estar entre 0 y 100")]
    public decimal TasaInteres { get; set; }

    [Required(ErrorMessage = "El plazo en meses es obligatorio")]
    [Range(1, 600, ErrorMessage = "El plazo debe estar entre 1 y 600 meses")]
    public int PlazoMeses { get; set; }

    [Required(ErrorMessage = "El comercial que registra el crédito es obligatorio")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "El nombre del comercial debe tener entre 3 y 150 caracteres")]
    public string Comercial { get; set; } = string.Empty;
}

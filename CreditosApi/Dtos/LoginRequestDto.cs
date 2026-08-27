using System.ComponentModel.DataAnnotations;

namespace CreditosApi.Dtos;

public class LoginRequestDto
{
    [Required(ErrorMessage = "El usuario es obligatorio")]
    public string Usuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    public string Password { get; set; } = string.Empty;
}

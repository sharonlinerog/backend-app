namespace CreditosApi.Services;

/// <summary>
/// Credenciales del usuario compartido del equipo comercial (opción "simple"
/// de autenticación: un solo usuario/contraseña, sin tabla de usuarios en la
/// base de datos). Si más adelante el equipo crece y se necesita distinguir
/// usuarios individuales, esto se reemplaza por una tabla de usuarios sin
/// tener que cambiar el resto del flujo (el frontend seguiría pidiendo un
/// token y mandándolo en cada petición igual que ahora).
///
/// PasswordHash NO es la contraseña: es su hash calculado con BCrypt (ver
/// "dotnet run -- hash-password" en Program.cs). Así, aunque alguien vea
/// appsettings.json, no obtiene la contraseña real, solo un hash que no se
/// puede revertir.
/// </summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    public string Usuario { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

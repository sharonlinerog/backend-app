using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using CreditosApi.Dtos;
using CreditosApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CreditosApi.Controllers;

/// <summary>
/// Único endpoint público sin autenticación de toda la API: aquí es donde el
/// frontend cambia usuario/contraseña por un JWT. Con ese token, el frontend
/// llama al resto de endpoints (que sí exigen [Authorize], ver CreditosController).
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IOptionsMonitor<AuthOptions> _authOptionsMonitor;
    private readonly IOptionsMonitor<JwtOptions> _jwtOptionsMonitor;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IOptionsMonitor<AuthOptions> authOptionsMonitor,
        IOptionsMonitor<JwtOptions> jwtOptionsMonitor,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _authOptionsMonitor = authOptionsMonitor;
        _jwtOptionsMonitor = jwtOptionsMonitor;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Valida usuario/contraseña contra las credenciales configuradas en
    /// Auth:Usuario / Auth:PasswordHash y, si coinciden, devuelve un JWT.
    /// La contraseña nunca se guarda en texto plano: PasswordHash es un hash
    /// BCrypt, y aquí se verifica con BCrypt.Verify (que internamente vuelve a
    /// calcular el hash de lo recibido y lo compara de forma segura, sin
    /// filtrar información por timing). Tiene su propio límite de intentos
    /// (más estricto que el resto de la API) para dificultar fuerza bruta.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponseDto> Login([FromBody] LoginRequestDto dto)
    {
        var authOptions = _authOptionsMonitor.CurrentValue;

        if (!UsuarioValido(dto.Usuario, authOptions.Usuario) ||
            !PasswordValida(dto.Password, authOptions.PasswordHash))
        {
            _logger.LogWarning("Intento de login fallido para el usuario '{Usuario}'", dto.Usuario);
            return Unauthorized(new { error = "Usuario o contraseña incorrectos." });
        }

        var jwtOptions = _jwtOptionsMonitor.CurrentValue;
        var token = _tokenService.GenerarToken(dto.Usuario);

        return Ok(new LoginResponseDto
        {
            Token = token,
            ExpiraEn = DateTime.UtcNow.AddMinutes(jwtOptions.ExpirationMinutes)
        });
    }

    /// <summary>
    /// Compara dos strings en tiempo constante (no corta la comparación en el
    /// primer carácter distinto), para no filtrar por timing cuánto del
    /// usuario acertó un atacante.
    /// </summary>
    private static bool UsuarioValido(string valorRecibido, string valorEsperado)
    {
        var bytesRecibidos = Encoding.UTF8.GetBytes(valorRecibido ?? string.Empty);
        var bytesEsperados = Encoding.UTF8.GetBytes(valorEsperado ?? string.Empty);

        if (bytesRecibidos.Length != bytesEsperados.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(bytesRecibidos, bytesEsperados);
    }

    /// <summary>
    /// Verifica la contraseña recibida contra el hash BCrypt guardado en
    /// Auth:PasswordHash. Si ese valor todavía tiene el placeholder de
    /// ejemplo o cualquier texto que no sea un hash BCrypt válido,
    /// BCrypt.Verify lanza SaltParseException: se atrapa y se trata como
    /// login inválido, en vez de devolver un error 500 al cliente.
    /// </summary>
    private static bool PasswordValida(string passwordRecibida, string passwordHashEsperado)
    {
        if (string.IsNullOrEmpty(passwordRecibida) || string.IsNullOrEmpty(passwordHashEsperado))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(passwordRecibida, passwordHashEsperado);
        }
        catch (SaltParseException)
        {
            return false;
        }
    }
}

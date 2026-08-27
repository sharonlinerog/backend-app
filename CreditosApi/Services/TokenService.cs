using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CreditosApi.Services;

/// <summary>
/// Arma el JWT que se le entrega al frontend después de un login correcto.
/// El token lleva el nombre de usuario como claim, una fecha de expiración,
/// y va firmado con la clave secreta de Jwt:SecretKey (HMAC-SHA256): eso es
/// lo que permite que, más adelante, el middleware de autenticación pueda
/// verificar que un token no fue alterado y que en efecto lo emitió esta API.
/// </summary>
public class TokenService : ITokenService
{
    // IOptionsMonitor para que, igual que con el SMTP, un cambio en appsettings.json
    // (por ejemplo rotar la clave secreta) se tome en cuenta sin reiniciar la app.
    private readonly IOptionsMonitor<JwtOptions> _jwtOptionsMonitor;

    public TokenService(IOptionsMonitor<JwtOptions> jwtOptionsMonitor)
    {
        _jwtOptionsMonitor = jwtOptionsMonitor;
    }

    public string GenerarToken(string usuario)
    {
        var jwtOptions = _jwtOptionsMonitor.CurrentValue;

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, usuario),
            new Claim(JwtRegisteredClaimNames.Sub, usuario),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwtOptions.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

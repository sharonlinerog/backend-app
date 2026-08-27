namespace CreditosApi.Services;

public interface ITokenService
{
    /// <summary>Genera un JWT firmado para el usuario indicado.</summary>
    string GenerarToken(string usuario);
}

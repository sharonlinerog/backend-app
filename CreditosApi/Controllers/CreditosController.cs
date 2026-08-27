using CreditosApi.Data;
using CreditosApi.Dtos;
using CreditosApi.Models;
using CreditosApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CreditosApi.Controllers;

/// <summary>
/// Expone los dos endpoints que pide el requerimiento:
///   POST /api/creditos  -> registrar un nuevo crédito
///   GET  /api/creditos  -> consultar créditos, con filtros y orden
///
/// [Authorize] a nivel de clase: TODOS los endpoints de este controller
/// exigen un JWT válido en el header "Authorization: Bearer {token}". Ese
/// token se obtiene primero en POST /api/auth/login (ver AuthController).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CreditosController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<CreditosController> _logger;

    public CreditosController(AppDbContext dbContext, IEmailQueue emailQueue, ILogger<CreditosController> logger)
    {
        _dbContext = dbContext;
        _emailQueue = emailQueue;
        _logger = logger;
    }

    /// <summary>
    /// Registra un nuevo crédito.
    /// El [ApiController] ya valida automáticamente el DTO contra las reglas
    /// de CreditoCreateDto y responde 400 con el detalle si algo no cumple,
    /// así que si llegamos al cuerpo del método es porque los datos son válidos.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("registro-creditos")]
    [ProducesResponseType(typeof(CreditoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreditoResponseDto>> RegistrarCredito([FromBody] CreditoCreateDto dto, CancellationToken cancellationToken)
    {
        var credito = new Credito
        {
            Id = Guid.NewGuid(),
            NombreCliente = dto.NombreCliente.Trim(),
            Cedula = dto.Cedula.Trim(),
            ValorCredito = dto.ValorCredito,
            TasaInteres = dto.TasaInteres,
            PlazoMeses = dto.PlazoMeses,
            Comercial = dto.Comercial.Trim(),
            FechaRegistro = DateTime.UtcNow
        };

        _dbContext.Creditos.Add(credito);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // El correo se encola aquí y el método sigue de inmediato: NO se usa "await"
        // sobre el envío del correo. Quien envía el correo de verdad es
        // EmailSenderBackgroundService, en otro hilo, así que el usuario del
        // formulario recibe su confirmación al instante sin esperar al SMTP.
        _emailQueue.QueueEmail(new EmailMessage(
            credito.NombreCliente,
            credito.ValorCredito,
            credito.Comercial,
            credito.FechaRegistro));

        _logger.LogInformation("Crédito {Id} registrado para {Cliente}", credito.Id, credito.NombreCliente);

        var response = MapToResponseDto(credito);
        return CreatedAtAction(nameof(ObtenerCreditoPorId), new { id = credito.Id }, response);
    }

    /// <summary>
    /// Consulta créditos con filtros opcionales y orden.
    /// Ejemplo: GET /api/creditos?nombreCliente=Juan&amp;sortBy=valor&amp;sortDir=desc
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CreditoResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CreditoResponseDto>>> ObtenerCreditos(
        [FromQuery] string? nombreCliente,
        [FromQuery] string? cedula,
        [FromQuery] string? comercial,
        [FromQuery] string? sortBy = "fecha",
        [FromQuery] string? sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Creditos.AsQueryable();

        // EF Core convierte cada Where en SQL parametrizado (equivalente a usar
        // "?" o "$1" en una consulta preparada), así que esto es seguro frente
        // a inyección SQL aunque el texto venga directo del usuario.
        if (!string.IsNullOrWhiteSpace(nombreCliente))
            query = query.Where(c => EF.Functions.ILike(c.NombreCliente, $"%{nombreCliente}%"));

        if (!string.IsNullOrWhiteSpace(cedula))
            query = query.Where(c => EF.Functions.ILike(c.Cedula, $"%{cedula}%"));

        if (!string.IsNullOrWhiteSpace(comercial))
            query = query.Where(c => EF.Functions.ILike(c.Comercial, $"%{comercial}%"));

        var descendente = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        query = sortBy?.ToLowerInvariant() switch
        {
            "valor" => descendente ? query.OrderByDescending(c => c.ValorCredito) : query.OrderBy(c => c.ValorCredito),
            _ => descendente ? query.OrderByDescending(c => c.FechaRegistro) : query.OrderBy(c => c.FechaRegistro),
        };

        var creditos = await query.ToListAsync(cancellationToken);
        return Ok(creditos.Select(MapToResponseDto));
    }

    /// <summary>
    /// Consulta un único crédito por id. Se usa internamente para armar la
    /// respuesta 201 Created de RegistrarCredito (header "Location").
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CreditoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreditoResponseDto>> ObtenerCreditoPorId(Guid id, CancellationToken cancellationToken)
    {
        var credito = await _dbContext.Creditos.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (credito is null)
            return NotFound();

        return Ok(MapToResponseDto(credito));
    }

    private static CreditoResponseDto MapToResponseDto(Credito credito) => new()
    {
        Id = credito.Id,
        NombreCliente = credito.NombreCliente,
        Cedula = credito.Cedula,
        ValorCredito = credito.ValorCredito,
        TasaInteres = credito.TasaInteres,
        PlazoMeses = credito.PlazoMeses,
        Comercial = credito.Comercial,
        FechaRegistro = credito.FechaRegistro
    };
}

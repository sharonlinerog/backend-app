using System.Text;
using System.Threading.RateLimiting;
using CreditosApi.Data;
using CreditosApi.Middleware;
using CreditosApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// ---------- Utilidad de línea de comandos: generar el hash de una contraseña ----------
// No levanta la API ni necesita base de datos ni SMTP configurados: solo sirve
// para calcular el valor que va en Auth:PasswordHash dentro de appsettings.json.
// Uso: dotnet run -- hash-password
if (args.Length > 0 && args[0] == "hash-password")
{
    Console.Write("Contraseña a hashear: ");
    var password = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(password))
    {
        Console.WriteLine("La contraseña no puede estar vacía.");
        return;
    }

    var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

    Console.WriteLine();
    Console.WriteLine("Copia este valor en Auth:PasswordHash dentro de appsettings.json:");
    Console.WriteLine(hash);
    return;
}

var builder = WebApplication.CreateBuilder(args);

// appsettings.Local.json es para tus credenciales reales en TU máquina (base de
// datos, SMTP, Jwt:SecretKey, Auth:PasswordHash). Está en .gitignore a propósito:
// nunca se sube al repositorio. Si existe, sus valores pisan a los de
// appsettings.json (que solo trae placeholders de ejemplo). "optional: true"
// significa que si el archivo no existe (por ejemplo, en el servidor de
// producción, donde los secretos se pasan por variables de entorno) la app
// arranca igual, sin error.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ---------- 1. Configuración (lee appsettings.json + variables de entorno) ----------
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

// ---------- 2. Base de datos: PostgreSQL vía Entity Framework Core ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection' en appsettings.json");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ---------- 3. Envío de correo asíncrono (cola en memoria + worker en segundo plano) ----------
builder.Services.AddSingleton<IEmailQueue, EmailQueue>();
builder.Services.AddHostedService<EmailSenderBackgroundService>();

// ---------- 3b. Autenticación con JWT ----------
// El login (POST /api/auth/login) valida usuario/contraseña y devuelve un JWT.
// A partir de ahí, todo endpoint marcado con [Authorize] (como CreditosController)
// exige ese token en el header "Authorization: Bearer {token}".
builder.Services.AddSingleton<ITokenService, TokenService>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtSecretKey = jwtSection["SecretKey"]
    ?? throw new InvalidOperationException("Falta la clave 'Jwt:SecretKey' en appsettings.json");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("Falta la clave 'Jwt:Issuer' en appsettings.json"),
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"] ?? throw new InvalidOperationException("Falta la clave 'Jwt:Audience' en appsettings.json"),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1) // margen pequeño por diferencias de reloj entre cliente y servidor
        };
    });

builder.Services.AddAuthorization();

// ---------- 4. Controllers + validación automática de modelos ----------
builder.Services.AddControllers();

// ---------- 5. CORS: permite que el frontend (React, en otro origen) llame a esta API ----------
const string CorsPolicyName = "FrontendPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:5173" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ---------- 6. Rate limiting: evita abuso/flood sobre el registro de créditos ----------
// La política se particiona por IP del cliente: cada IP tiene su propio cupo de
// 10 registros por minuto. (Ojo: si la API queda detrás de un proxy/reverse-proxy,
// RemoteIpAddress será la del proxy; en ese caso habría que habilitar
// app.UseForwardedHeaders(...) antes de UseRateLimiter para obtener la IP real.)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("registro-creditos", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,               // máximo 10 registros...
                Window = TimeSpan.FromMinutes(1), // ...por minuto, por cada IP.
                QueueLimit = 0
            }));

    // Límite propio y más estricto para el login: dificulta que alguien intente
    // adivinar la contraseña por fuerza bruta (probar muchas combinaciones seguidas).
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,                  // máximo 5 intentos...
                Window = TimeSpan.FromMinutes(1),  // ...por minuto, por cada IP.
                QueueLimit = 0
            }));
});

// ---------- 7. OpenAPI / Swagger (documentación interactiva de la API) ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Creditos API",
        Version = "v1",
        Description = "API para registrar y consultar créditos, con notificación automática por correo."
    });

    // Agrega el botón "Authorize" en Swagger UI: permite pegar el JWT obtenido
    // en POST /api/auth/login y probar desde ahí los endpoints protegidos.
    var jwtScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Pega aquí el token devuelto por POST /api/auth/login (sin la palabra 'Bearer')."
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ---------- Middlewares (el orden importa: cada uno envuelve al siguiente) ----------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Solo se fuerza HTTPS fuera de desarrollo. En local el frontend llama a
    // http://localhost:5080, así que redirigir a HTTPS aquí rompería esas
    // llamadas; en producción sí debe ir detrás de HTTPS con certificado válido.
    app.UseHttpsRedirection();
}

app.UseCors(CorsPolicyName);
// UseAuthentication (¿quién eres, según el JWT?) siempre va antes de
// UseAuthorization (¿te dejo pasar?), y ambos antes de que la petición
// llegue a los controllers.
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

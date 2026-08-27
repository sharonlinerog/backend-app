# AGENTS.md — Guía para agentes de IA (Claude, Copilot, Cursor, etc.)

Este archivo sigue la convención [AGENTS.md](https://agents.md) para que cualquier asistente de IA que trabaje sobre este repositorio tenga contexto inmediato, sin tener que adivinar la arquitectura.

## Qué es este proyecto

Backend de una app de registro y consulta de créditos. Al crear un crédito (`POST /api/creditos`) se guarda en PostgreSQL y se encola el envío de un correo de notificación a `fyasocialcapital@gmail.com`, que se envía **en segundo plano**, de forma asíncrona, sin bloquear la respuesta HTTP. Todos los endpoints de créditos requieren autenticación (JWT); el token se obtiene en `POST /api/auth/login`.

Stack: **.NET 8 / ASP.NET Core Web API / Entity Framework Core / Npgsql (PostgreSQL) / JWT (Microsoft.AspNetCore.Authentication.JwtBearer)**.

## Comandos

```bash
# Restaurar dependencias
cd CreditosApi && dotnet restore

# Ejecutar en local (requiere PostgreSQL corriendo y appsettings configurado)
dotnet run

# Compilar
dotnet build

# Crear base de datos desde cero
psql -U creditos_app -d creditosdb -f ../db/schema.sql
```

No hay una suite de pruebas automatizadas todavía. Si agregas una (recomendado: `xUnit` + `Microsoft.AspNetCore.Mvc.Testing` para pruebas de integración contra una base de datos de prueba), colócala en un proyecto nuevo `CreditosApi.Tests/` al mismo nivel que `CreditosApi/`.

## Estructura y convenciones

- `Controllers/` — un controller por recurso. No poner lógica de negocio pesada aquí: solo orquestar (validar entrada ya la hace `[ApiController]`, delegar a `Data`/`Services`, mapear a DTO de salida).
- `Models/` — entidades que EF Core mapea a tablas. Si cambias un campo aquí, actualiza también `Data/AppDbContext.cs` (mapeo Fluent API) y `db/schema.sql` (deben quedar sincronizados: no usamos migraciones automáticas por defecto).
- `Dtos/` — nunca exponer las entidades de `Models/` directamente en la API. Los DTO de entrada llevan las validaciones (`DataAnnotations`); los de salida controlan qué campos viajan al cliente.
- `Data/AppDbContext.cs` — única fuente de verdad del mapeo objeto-relacional.
- `Services/` — la cola de correos (`IEmailQueue` / `EmailQueue`) y el worker que la consume (`EmailSenderBackgroundService`). Cualquier tarea nueva que deba ejecutarse "en segundo plano" sigue este mismo patrón productor/consumidor con `System.Threading.Channels`, no llamadas síncronas bloqueantes. También vive aquí `TokenService` (genera los JWT) y las opciones `JwtOptions`/`AuthOptions` (`AuthOptions.PasswordHash` guarda un hash BCrypt, nunca la contraseña en texto plano).
- `Middleware/` — atraviesa todas las peticiones. Cambios aquí afectan a toda la API.
- `Controllers/AuthController.cs` — único controller sin `[Authorize]`. Valida el usuario contra `Auth:Usuario` (comparación en tiempo constante, ver `UsuarioValido`) y la contraseña contra `Auth:PasswordHash` con `BCrypt.Verify` (ver `PasswordValida`), y devuelve el JWT. Tiene su propio límite de intentos (`[EnableRateLimiting("login")]`, más estricto que el resto de la API) para dificultar fuerza bruta. El hash se genera aparte con `dotnet run -- hash-password` (ver el bloque al inicio de `Program.cs`), nunca a mano.

## Reglas al modificar código

1. **Toda entrada de usuario se valida en el DTO** (`Dtos/`), nunca confiar en que el frontend ya validó.
2. **Nunca hacer SQL con concatenación de strings.** Usar siempre LINQ/EF Core (parametriza automáticamente) o, si hace falta SQL crudo, `FormattableString` con `FromSqlInterpolated`.
3. **El envío de correo nunca debe ser `await` dentro del controller.** Si se necesita otro tipo de notificación en segundo plano, seguir el mismo patrón de cola + `BackgroundService`.
4. Si agregas un endpoint nuevo, documenta su contrato con atributos `[ProducesResponseType]` para que seguir apareciendo correctamente en Swagger.
5. Si agregas una dependencia NuGet nueva, agrégala al `.csproj` correspondiente y menciónala en el README (sección de requisitos).
6. **Cualquier controller nuevo que exponga datos de negocio debe llevar `[Authorize]`** (a nivel de clase, como `CreditosController`), salvo que sea deliberadamente público como `AuthController`.
7. `Jwt:SecretKey`, `Auth:PasswordHash` y las credenciales de `ConnectionStrings`/`Smtp` nunca deben quedar con un valor real en `appsettings.json` (ese archivo se sube al repo, solo debe tener placeholders). En desarrollo local, esos valores reales van en `CreditosApi/appsettings.Local.json` (gitignored, cargado automáticamente por `Program.cs` si existe). En producción van como variables de entorno del sistema. Nunca escribir una contraseña a mano en `PasswordHash` — siempre generarla con `dotnet run -- hash-password`.

## Contexto de negocio (del requerimiento original)

- El formulario de registro pide: nombre del cliente, cédula/ID, valor del crédito, tasa de interés, plazo en meses y el comercial que lo registra.
- Al registrar, se debe notificar por correo a `fyasocialcapital@gmail.com` con nombre del cliente, valor del crédito, nombre del comercial y fecha de registro — en segundo plano.
- La consulta de créditos debe permitir filtrar por cliente, ID o comercial, y ordenar por fecha o valor.
- El registro y la consulta deben quedar protegidos (requisito de seguridad opcional del documento original: "uso de JWT o sesiones").

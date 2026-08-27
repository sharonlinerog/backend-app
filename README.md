# Creditos API (Backend)

API REST en **.NET 8 (ASP.NET Core Web API) + Entity Framework Core + PostgreSQL** para registrar y consultar créditos. Al registrar un crédito, se encola automáticamente el envío de un correo de notificación a `creditos@gmail.com`, que se envía en segundo plano (no bloquea la respuesta al usuario).

## Requisitos previos

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 14+](https://www.postgresql.org/download/) corriendo localmente o accesible por red
- Una cuenta SMTP para el envío de correos (puede ser Gmail con una "contraseña de aplicación", ver más abajo)

## 1. Crear la base de datos

```bash
# Conéctate a psql como superusuario y ejecuta:
psql -U postgres -c "CREATE USER creditos_app WITH PASSWORD 'una_clave_segura';"
psql -U postgres -c "CREATE DATABASE creditosdb OWNER creditos_app;"

# Luego crea las tablas:
psql -U creditos_app -d creditosdb -f db/schema.sql

# (Opcional) Datos de ejemplo para probar el módulo de consulta:
psql -U creditos_app -d creditosdb -f db/seed_ejemplo.sql
```

## 2. Configurar la aplicación

**No edites `appsettings.json` con tus credenciales reales** — ese archivo se sube al repositorio y solo debe tener los placeholders de ejemplo. En vez de eso, crea `CreditosApi/appsettings.Local.json` (mismo formato, ya está en `.gitignore`, nunca se sube) con tus valores reales; la app lo carga automáticamente y sus valores pisan a los de `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=creditosdb;Username=creditos_app;Password=TU_CLAVE"
},
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": 587,
  "User": "tu_correo@gmail.com",
  "Password": "TU_APP_PASSWORD",
  "FromAddress": "tu_correo@gmail.com",
  "DestinatarioCreditos": "creditos@gmail.com"
},
"Jwt": {
  "Issuer": "CreditosApi",
  "Audience": "CreditosApiClientes",
  "SecretKey": "UNA_CLAVE_SECRETA_DE_AL_MENOS_32_CARACTERES",
  "ExpirationMinutes": 60
},
"Auth": {
  "Usuario": "comercial",
  "PasswordHash": "PON_AQUI_EL_HASH_GENERADO_ABAJO"
}
```

> **Jwt:SecretKey** debe tener al menos 32 caracteres (se usa para firmar los tokens con HMAC-SHA256). **Auth:Usuario/PasswordHash** son las credenciales que usará todo el equipo comercial para entrar a la app (ver sección de Autenticación más abajo). **Importante:** `PasswordHash` no es la contraseña, es su hash — nunca pongas la contraseña en texto plano ahí.

Para generar el hash de la contraseña que va en `Auth:PasswordHash`, corre esto dentro de `CreditosApi/`:

```bash
dotnet run -- hash-password
# Te pide la contraseña, y te imprime el hash para copiar en appsettings.json
```

> **Gmail:** si usas una cuenta de Gmail para enviar, necesitas activar la verificación en 2 pasos y generar una "Contraseña de aplicación" en https://myaccount.google.com/apppasswords — la contraseña normal de la cuenta no funciona para SMTP.

En un servidor (producción) no se usa `appsettings.Local.json` — ahí es mejor pasar los secretos por variables de entorno del sistema operativo (.NET las lee automáticamente y con esta misma sintaxis de doble guion bajo por cada nivel anidado):

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=creditosdb;Username=creditos_app;Password=TU_CLAVE"
export Smtp__User="tu_correo@gmail.com"
export Smtp__Password="TU_APP_PASSWORD"
export Jwt__SecretKey="TU_CLAVE_DE_PRODUCCION"
export Auth__PasswordHash="TU_HASH_DE_PRODUCCION"
```

En resumen: `appsettings.json` (con placeholders) es el único que se sube al repo; `appsettings.Local.json` es para tu contraseña real en tu propia PC; y las variables de entorno son para el servidor de producción.

## 3. Ejecutar el proyecto

```bash
cd CreditosApi
dotnet restore
dotnet run
```

La API queda disponible en `http://localhost:5080` y la documentación interactiva (Swagger) en `http://localhost:5080/swagger`.

## 4. Endpoints principales

| Método | Ruta | Requiere token | Descripción |
|---|---|---|---|
| `POST` | `/api/auth/login` | No | Recibe usuario/contraseña y devuelve un JWT |
| `POST` | `/api/creditos` | Sí | Registra un crédito y encola el correo de notificación |
| `GET` | `/api/creditos?nombreCliente=&cedula=&comercial=&sortBy=fecha\|valor&sortDir=asc\|desc` | Sí | Lista créditos, con filtros y orden |
| `GET` | `/api/creditos/{id}` | Sí | Consulta un crédito puntual |

Todos los endpoints de créditos exigen el header `Authorization: Bearer {token}`, obtenido primero en `/api/auth/login`.

Ejemplo completo (login + registro):

```bash
# 1. Login: obtiene el token
TOKEN=$(curl -s -X POST http://localhost:5080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"usuario": "comercial", "password": "LA_CONTRASEÑA_SIN_HASHEAR"}' \
  | python3 -c "import sys, json; print(json.load(sys.stdin)['token'])")

# 2. Registro de crédito, usando ese token
curl -X POST http://localhost:5080/api/creditos \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "nombreCliente": "Ana María Gómez",
    "cedula": "1020304050",
    "valorCredito": 5000000,
    "tasaInteres": 1.9,
    "plazoMeses": 12,
    "comercial": "Carlos Pérez"
  }'
```

También puedes probarlo desde Swagger (`/swagger`): primero ejecuta `POST /api/auth/login`, copia el `token` de la respuesta, haz clic en el botón **Authorize** (arriba a la derecha) y pégalo ahí (sin escribir la palabra "Bearer", Swagger ya la agrega).

## 5. (Opcional) Migraciones con Entity Framework Core

Este proyecto trae el esquema listo en `db/schema.sql` para que cualquiera pueda crear la base sin instalar herramientas extra. Si tu equipo prefiere manejar el esquema con migraciones de EF Core en vez del script SQL:

```bash
dotnet tool install --global dotnet-ef
cd CreditosApi
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## 6. Autenticación

Se usa un único usuario compartido por el equipo comercial (configurado en `Auth:Usuario` / `Auth:PasswordHash`), no una tabla de usuarios en la base de datos — es la opción simple, pensada para un equipo pequeño. El flujo es:

1. El frontend manda usuario/contraseña (en texto plano, como cualquier formulario de login — para eso está HTTPS en producción) a `POST /api/auth/login`.
2. El backend compara el usuario recibido contra `Auth:Usuario`, y la contraseña recibida contra `Auth:PasswordHash` usando `BCrypt.Verify` (nunca compara la contraseña en texto plano: recalcula su hash y lo compara contra el guardado). Si ambos coinciden, devuelve un JWT firmado (válido por `Jwt:ExpirationMinutes`, 60 minutos por defecto).
3. El frontend guarda ese token y lo manda en cada petición siguiente (`Authorization: Bearer {token}`).
4. `CreditosController` tiene `[Authorize]` a nivel de clase: sin un token válido, cualquier llamada a `/api/creditos` responde `401 Unauthorized`.

El endpoint de login tiene su propio límite de intentos (5 por minuto, por IP) para dificultar que alguien intente adivinar la contraseña por fuerza bruta. Y como `Auth:PasswordHash` guarda solo el hash (no la contraseña), aunque alguien llegue a ver `appsettings.json` no obtiene la contraseña real.

Si el equipo crece y se necesita distinguir qué comercial hizo login (no solo el campo de texto libre del formulario), el siguiente paso natural es reemplazar `Auth:Usuario/PasswordHash` por una tabla de usuarios en la base de datos (cada uno con su propio hash) — el resto del flujo (pedir token, mandarlo en cada petición) no cambia.

## 7. Seguridad implementada / pendiente

Implementado:
- Autenticación con JWT en todos los endpoints de créditos (ver sección 6).
- La contraseña de login se guarda hasheada con BCrypt (`Auth:PasswordHash`), nunca en texto plano.
- Validación de datos en el backend (además de la del frontend) con Data Annotations.
- Consultas parametrizadas vía EF Core (previene inyección SQL).
- Rate limiting en el registro de créditos (10/min por IP) y en el login (5/min por IP).
- Manejo centralizado de errores (no se exponen detalles internos al cliente).

Pendiente / sugerido como siguiente paso:
- HTTPS obligatorio en producción con certificado válido (en local se usa `dotnet dev-certs`).
- Si el equipo crece: tabla de usuarios individuales en vez del usuario único compartido.

## Estructura del proyecto

```
CreditosApi/
  Controllers/     -> Endpoints HTTP (créditos y autenticación)
  Models/          -> Entidades que EF Core mapea a la base de datos
  Dtos/            -> Formas de entrada/salida de la API (separadas de las entidades)
  Data/            -> DbContext (configuración de EF Core)
  Services/        -> Cola de correos + worker de correo + generación de JWT
  Middleware/       -> Manejo centralizado de errores
db/
  schema.sql       -> Script de creación de la base de datos
  seed_ejemplo.sql -> Datos de ejemplo opcionales
```

Ver `AGENTS.md` para el detalle técnico de qué hace cada archivo.

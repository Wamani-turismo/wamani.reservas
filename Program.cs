using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

// Usar formato de números "neutro" (con punto) internamente, para que los campos
// numéricos de los formularios funcionen igual sin importar el idioma de la compu.
// Los montos en pantalla igual se muestran en formato argentino ($ 35.000) porque
// se formatean con la cultura es-AR explícita donde corresponde.
var culturaNeutra = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culturaNeutra;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culturaNeutra;

// Licencia gratuita de QuestPDF (uso permitido para empresas chicas)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// En internet (Render) el servidor nos dice en qué "puerto" tenemos que escuchar
// con la variable PORT. En tu compu esta variable no existe y se usa la config de siempre.
var puertoInternet = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(puertoInternet))
    builder.WebHost.UseUrls($"http://0.0.0.0:{puertoInternet}");

// Permitir subir varios comprobantes (fotos) en un mismo guardado sin que se corte.
// Por defecto el servidor limita el envío a ~30 MB; lo subimos a 100 MB porque las
// fotos de celular pesan bastante y con 3 o más se pasaba de ese límite.
const long limiteSubida = 100L * 1024 * 1024; // 100 MB
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = limiteSubida);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = limiteSubida;
    o.ValueCountLimit = int.MaxValue;
});

// Páginas web (Razor Pages) — TODAS requieren login, menos la página de entrar
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});

// Login por cookie (un usuario por socio)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

// Base de datos:
//  - En internet (Render): PostgreSQL (los datos NO se borran al actualizar).
//    Render nos da la conexión en la variable de entorno DATABASE_URL.
//  - En tu compu: SQLite (el archivo "wamani.db" al lado del proyecto), como hasta ahora.
var conexionPostgres = Environment.GetEnvironmentVariable("DATABASE_URL");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(conexionPostgres))
        options.UseNpgsql(ConvertirUrlPostgres(conexionPostgres));
    else
        options.UseSqlite(builder.Configuration.GetConnectionString("Default")
            ?? "Data Source=wamani.db");
});

var app = builder.Build();

// Crea el archivo de base de datos y las tablas si no existen todavía
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // --- Arreglos de tablas/columnas que SOLO valen para SQLite (tu compu) ---
    // En Postgres (internet) NO hace falta nada de esto: EnsureCreated() ya crea
    // todas las tablas con todas las columnas a partir del modelo. Este SQL usa
    // sintaxis propia de SQLite (PRAGMA, AUTOINCREMENT, tipos TEXT/INTEGER) y
    // rompería en Postgres, por eso se ejecuta únicamente cuando la base es SQLite.
    if (db.Database.IsSqlite())
    {
    // Agrega columnas nuevas SIN borrar los datos existentes (para bases ya creadas)
    EnsureSqliteColumn(db, "Reservas", "PrecioManual", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(db, "Reservas", "EsTravesia", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(db, "Reservas", "DescuentoMonto", "TEXT NOT NULL DEFAULT '0'");
    EnsureSqliteColumn(db, "Reservas", "DescuentoMotivo", "TEXT NULL");
    EnsureSqliteColumn(db, "Reservas", "CantidadMenores", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(db, "Excursiones", "EsTravesia", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(db, "OperativoGastos", "Comprobante", "TEXT NULL");
    EnsureSqliteColumn(db, "Excursiones", "GuiaBreve", "TEXT NULL");
    EnsureSqliteColumn(db, "Excursiones", "Recomendaciones", "TEXT NULL");
    EnsureSqliteColumn(db, "Excursiones", "LugaresVisitar", "TEXT NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "Sena", "TEXT NOT NULL DEFAULT '0'");
    EnsureSqliteColumn(db, "OperativoProveedores", "Saldo", "TEXT NOT NULL DEFAULT '0'");
    EnsureSqliteColumn(db, "OperativoProveedores", "ComprobanteSena", "TEXT NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "ComprobanteSaldo", "TEXT NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "ParaQuien", "TEXT NULL");
    EnsureSqliteColumn(db, "Proveedores", "Precio", "TEXT NOT NULL DEFAULT '0'");
    EnsureSqliteColumn(db, "Excursiones", "MaximoPersonas", "INTEGER NOT NULL DEFAULT 8");
    EnsureSqliteColumn(db, "Excursiones", "CantidadGuias", "INTEGER NOT NULL DEFAULT 1");
    EnsureSqliteColumn(db, "GastosExcursion", "TipoCalculo", "TEXT NOT NULL DEFAULT 'Por persona'");
    EnsureSqliteColumn(db, "GastosExcursion", "EsProveedor", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(db, "OperativoGastos", "FechaPago", "TEXT NULL");
    EnsureSqliteColumn(db, "OperativoGastos", "PrecioUnitario", "TEXT NULL");
    EnsureSqliteColumn(db, "OperativoGastos", "TipoCalculo", "TEXT NOT NULL DEFAULT 'Por persona'");
    EnsureSqliteColumn(db, "OperativoProveedores", "FechaSena", "TEXT NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "FechaSaldo", "TEXT NULL");

    // Crea las tablas nuevas si no existen (EnsureCreated no las agrega a una base ya creada)
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""GastosExcursion"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_GastosExcursion"" PRIMARY KEY AUTOINCREMENT,
            ""ExcursionId"" INTEGER NOT NULL,
            ""Nombre"" TEXT NOT NULL,
            ""Precio"" TEXT NOT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""OperativoGastos"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_OperativoGastos"" PRIMARY KEY AUTOINCREMENT,
            ""ExcursionId"" INTEGER NOT NULL,
            ""Fecha"" TEXT NOT NULL,
            ""Nombre"" TEXT NOT NULL,
            ""Precio"" TEXT NOT NULL,
            ""Comprado"" INTEGER NOT NULL,
            ""Comprobante"" TEXT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""OperativoSalidas"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_OperativoSalidas"" PRIMARY KEY AUTOINCREMENT,
            ""ExcursionId"" INTEGER NOT NULL,
            ""Fecha"" TEXT NOT NULL,
            ""ServiciosPagados"" INTEGER NOT NULL,
            ""Comprobante"" TEXT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Usuarios"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Usuarios"" PRIMARY KEY AUTOINCREMENT,
            ""Nombre"" TEXT NOT NULL,
            ""NombreUsuario"" TEXT NOT NULL,
            ""PasswordHash"" TEXT NOT NULL,
            ""Activo"" INTEGER NOT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Interesados"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Interesados"" PRIMARY KEY AUTOINCREMENT,
            ""Nombre"" TEXT NOT NULL,
            ""Telefono"" TEXT NULL,
            ""ExcursionId"" INTEGER NULL,
            ""Excursion"" TEXT NOT NULL,
            ""FechaDesde"" TEXT NOT NULL,
            ""FechaHasta"" TEXT NOT NULL,
            ""CreadoEl"" TEXT NOT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""GastosEmpresa"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_GastosEmpresa"" PRIMARY KEY AUTOINCREMENT,
            ""Fecha"" TEXT NOT NULL,
            ""Tipo"" TEXT NOT NULL,
            ""Descripcion"" TEXT NOT NULL,
            ""Monto"" TEXT NOT NULL,
            ""Comprobante"" TEXT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Retiros"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Retiros"" PRIMARY KEY AUTOINCREMENT,
            ""Fecha"" TEXT NOT NULL,
            ""Quien"" TEXT NULL,
            ""Descripcion"" TEXT NULL,
            ""Monto"" TEXT NOT NULL,
            ""Comprobante"" TEXT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Aportes"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Aportes"" PRIMARY KEY AUTOINCREMENT,
            ""Fecha"" TEXT NOT NULL,
            ""Quien"" TEXT NULL,
            ""Descripcion"" TEXT NULL,
            ""Monto"" TEXT NOT NULL,
            ""Comprobante"" TEXT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Pasajeros"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Pasajeros"" PRIMARY KEY AUTOINCREMENT,
            ""ReservaId"" INTEGER NOT NULL,
            ""NombreCompleto"" TEXT NOT NULL,
            ""Dni"" TEXT NULL,
            ""FechaNacimiento"" TEXT NULL,
            ""Telefono"" TEXT NULL,
            ""Email"" TEXT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Proveedores"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Proveedores"" PRIMARY KEY AUTOINCREMENT,
            ""Tipo"" TEXT NOT NULL,
            ""Nombre"" TEXT NOT NULL,
            ""Contacto"" TEXT NULL,
            ""Activo"" INTEGER NOT NULL,
            ""Precio"" TEXT NOT NULL DEFAULT '0'
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""OperativoProveedores"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_OperativoProveedores"" PRIMARY KEY AUTOINCREMENT,
            ""ExcursionId"" INTEGER NOT NULL,
            ""Fecha"" TEXT NOT NULL,
            ""Tipo"" TEXT NOT NULL,
            ""ProveedorId"" INTEGER NULL,
            ""ProveedorNombre"" TEXT NOT NULL,
            ""Total"" TEXT NOT NULL,
            ""Sena"" TEXT NOT NULL DEFAULT '0',
            ""Saldo"" TEXT NOT NULL DEFAULT '0',
            ""ComprobanteSena"" TEXT NULL,
            ""ComprobanteSaldo"" TEXT NULL,
            ""ParaQuien"" TEXT NULL
        );");
    // --- Tablas de la LANDING (contenido de la web pública) ---
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""ContenidoWeb"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ContenidoWeb"" PRIMARY KEY AUTOINCREMENT,
            ""Whatsapp"" TEXT NOT NULL, ""Instagram"" TEXT NOT NULL, ""Facebook"" TEXT NOT NULL,
            ""Linktree"" TEXT NOT NULL, ""Email"" TEXT NOT NULL, ""Ubicacion"" TEXT NOT NULL,
            ""HeroTexto"" TEXT NOT NULL, ""Quienes1"" TEXT NOT NULL, ""Quienes2"" TEXT NOT NULL, ""Quienes3"" TEXT NOT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""ExcursionesWeb"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ExcursionesWeb"" PRIMARY KEY AUTOINCREMENT,
            ""Clave"" TEXT NOT NULL, ""Nombre"" TEXT NOT NULL, ""Chip"" TEXT NOT NULL,
            ""EsTravesia"" INTEGER NOT NULL, ""Color"" TEXT NOT NULL, ""Foto"" TEXT NOT NULL,
            ""Resumen"" TEXT NOT NULL, ""Datos"" TEXT NOT NULL, ""Itinerario"" TEXT NOT NULL,
            ""Incluye"" TEXT NOT NULL, ""Llevar"" TEXT NOT NULL, ""Orden"" INTEGER NOT NULL, ""Activa"" INTEGER NOT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""TestimoniosWeb"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_TestimoniosWeb"" PRIMARY KEY AUTOINCREMENT,
            ""Texto"" TEXT NOT NULL, ""Nombre"" TEXT NOT NULL, ""Lugar"" TEXT NOT NULL,
            ""Orden"" INTEGER NOT NULL, ""Activo"" INTEGER NOT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""IntegrantesWeb"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_IntegrantesWeb"" PRIMARY KEY AUTOINCREMENT,
            ""Nombre"" TEXT NOT NULL, ""Rol"" TEXT NOT NULL, ""Bio"" TEXT NOT NULL,
            ""Foto"" TEXT NOT NULL, ""Orden"" INTEGER NOT NULL
        );");
    } // fin del bloque específico de SQLite

    // En Postgres: corrige las tablas que se hayan creado antes con fechas "con zona
    // horaria" (el primer deploy las creó así y rompía Salidas y Operativo).
    // Es seguro correrlo siempre: si ya están bien, no hace nada.
    if (db.Database.IsNpgsql())
    {
        ArreglarFechasPostgres(db);
        // Columnas nuevas del gasto (precio por persona + tipo) en la base de internet
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoGastos"" ADD COLUMN IF NOT EXISTS ""PrecioUnitario"" numeric;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoGastos"" ADD COLUMN IF NOT EXISTS ""TipoCalculo"" text NOT NULL DEFAULT 'Por persona';");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""GastosExcursion"" ADD COLUMN IF NOT EXISTS ""EsProveedor"" boolean NOT NULL DEFAULT false;");
        // Descuento en pesos + motivo + menores en la reserva
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Reservas"" ADD COLUMN IF NOT EXISTS ""DescuentoMonto"" numeric NOT NULL DEFAULT 0;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Reservas"" ADD COLUMN IF NOT EXISTS ""DescuentoMotivo"" text;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Reservas"" ADD COLUMN IF NOT EXISTS ""CantidadMenores"" integer NOT NULL DEFAULT 0;");
        // Tabla de gastos generales de la empresa (nueva; EnsureCreated no la agrega a una base ya creada)
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""GastosEmpresa"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Fecha"" timestamp without time zone NOT NULL,
                ""Tipo"" text NOT NULL,
                ""Descripcion"" text NOT NULL,
                ""Monto"" numeric NOT NULL,
                ""Comprobante"" text NULL
            );");
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Retiros"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Fecha"" timestamp without time zone NOT NULL,
                ""Quien"" text NULL,
                ""Descripcion"" text NULL,
                ""Monto"" numeric NOT NULL,
                ""Comprobante"" text NULL
            );");
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Aportes"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Fecha"" timestamp without time zone NOT NULL,
                ""Quien"" text NULL,
                ""Descripcion"" text NULL,
                ""Monto"" numeric NOT NULL,
                ""Comprobante"" text NULL
            );");

        // Tablas de la LANDING (contenido de la web pública)
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""ContenidoWeb"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Whatsapp"" text NOT NULL, ""Instagram"" text NOT NULL, ""Facebook"" text NOT NULL,
                ""Linktree"" text NOT NULL, ""Email"" text NOT NULL, ""Ubicacion"" text NOT NULL,
                ""HeroTexto"" text NOT NULL, ""Quienes1"" text NOT NULL, ""Quienes2"" text NOT NULL, ""Quienes3"" text NOT NULL
            );");
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""ExcursionesWeb"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Clave"" text NOT NULL, ""Nombre"" text NOT NULL, ""Chip"" text NOT NULL,
                ""EsTravesia"" boolean NOT NULL, ""Color"" text NOT NULL, ""Foto"" text NOT NULL,
                ""Resumen"" text NOT NULL, ""Datos"" text NOT NULL, ""Itinerario"" text NOT NULL,
                ""Incluye"" text NOT NULL, ""Llevar"" text NOT NULL, ""Orden"" integer NOT NULL, ""Activa"" boolean NOT NULL
            );");
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""TestimoniosWeb"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Texto"" text NOT NULL, ""Nombre"" text NOT NULL, ""Lugar"" text NOT NULL,
                ""Orden"" integer NOT NULL, ""Activo"" boolean NOT NULL
            );");
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""IntegrantesWeb"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Nombre"" text NOT NULL, ""Rol"" text NOT NULL, ""Bio"" text NOT NULL,
                ""Foto"" text NOT NULL, ""Orden"" integer NOT NULL
            );");
    }

    // El tipo de costo "Por guía" se unificó con "Por auto" (en Wamani el chofer es el guía).
    // Pasamos los gastos que hayan quedado como "Por guía" a "Por auto". No pierde datos.
    db.Database.ExecuteSqlRaw(
        "UPDATE \"GastosExcursion\" SET \"TipoCalculo\" = 'Por auto' WHERE \"TipoCalculo\" = 'Por guía';");

    // Carga excursiones de EJEMPLO la primera vez (editables/borrables desde la web)
    if (!db.Excursiones.Any())
    {
        db.Excursiones.AddRange(
            new Wamani.Reservas.Models.Excursion { Nombre = "Salinas Grandes + Purmamarca", PrecioPorPersona = 45000, MinimoPersonas = 2 },
            new Wamani.Reservas.Models.Excursion { Nombre = "Serranías del Hornocal", PrecioPorPersona = 40000, MinimoPersonas = 2 },
            new Wamani.Reservas.Models.Excursion { Nombre = "Quebrada de Humahuaca (día completo)", PrecioPorPersona = 55000, MinimoPersonas = 2 },
            new Wamani.Reservas.Models.Excursion { Nombre = "Travesía Laguna de los Pozuelos", PrecioPorPersona = 120000, MinimoPersonas = 4, EsTravesia = true },
            new Wamani.Reservas.Models.Excursion { Nombre = "Travesía Puna completa (varios días)", PrecioPorPersona = 320000, MinimoPersonas = 6, EsTravesia = true }
        );
        db.SaveChanges();
    }

    // Usuario inicial para poder entrar la primera vez (después creás los reales y lo borrás)
    if (!db.Usuarios.Any())
    {
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        var admin = new Usuario { Nombre = "Admin", NombreUsuario = "admin", Activo = true };
        admin.PasswordHash = hasher.HashPassword(admin, "wamani2026");
        db.Usuarios.Add(admin);
        db.SaveChanges();
    }

    // Carga el contenido actual de la landing la primera vez (si está vacío)
    Wamani.Reservas.Services.SeedWeb.Ejecutar(db);
}

// Configuración del sitio

// En internet estamos detrás del "portero" de Render (que maneja el candado https).
// Esto le dice a la app que confíe en ese portero para saber que la visita vino por https.
var reenvio = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
reenvio.KnownNetworks.Clear();
reenvio.KnownProxies.Clear();
app.UseForwardedHeaders(reenvio);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Hace que una carpeta (ej. /web/) muestre su index.html automáticamente.
// Solo afecta a carpetas que TIENEN index.html; la raíz "/" sigue siendo el
// sistema (no hay index.html en wwwroot), así que no cambia nada del sistema.
app.UseDefaultFiles();

// Servir archivos estáticos, agregando el tipo del manifiesto de la app (PWA)
var tiposArchivo = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
tiposArchivo.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = tiposArchivo });

// Si hay disco persistente (internet), servir los comprobantes guardados ahí
// en la misma dirección /comprobantes de siempre (así los links no cambian).
var carpetaComprobantes = Environment.GetEnvironmentVariable("UPLOADS_DIR");
if (!string.IsNullOrWhiteSpace(carpetaComprobantes))
{
    Directory.CreateDirectory(carpetaComprobantes);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(carpetaComprobantes),
        RequestPath = "/comprobantes"
    });

    // Fotos que suben los chicos para la web pública (persisten en el disco).
    // Se sirven en /web/img; las que vienen con la app siguen en wwwroot/web/img.
    // Debe ser la MISMA carpeta que usa Services/FotosWeb.Carpeta().
    var carpetaFotosWeb = Path.GetFullPath(Path.Combine(carpetaComprobantes, "webfotos"));
    Directory.CreateDirectory(carpetaFotosWeb);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(carpetaFotosWeb),
        RequestPath = "/web/img"
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// La "puerta de entrada" (la dirección raíz) muestra la LANDING pública.
// Así, cuando se conecte el dominio (wamaniturismo.com), los visitantes ven la
// web y NO el sistema. Los chicos entran al sistema por /panel (requiere login).
app.MapGet("/", () => Results.Redirect("/web/")).AllowAnonymous();

// ═══════════════════════════════════════════════════════════════════════
//  "Puente" que le pasa a la LANDING (web pública) su contenido editable:
//  teléfono, redes, textos, excursiones, testimonios y equipo.
//  Se sirve como un archivo JavaScript (define window.CONTENIDO) y es
//  PÚBLICO (sin login), porque lo tiene que leer cualquier visitante.
// ═══════════════════════════════════════════════════════════════════════
app.MapGet("/web/contenido.js", (AppDbContext db) =>
{
    string[] Lineas(string s) => (s ?? "").Replace("\r", "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var c = db.ContenidoWeb.FirstOrDefault() ?? new Wamani.Reservas.Models.ContenidoWeb();
    var exc = db.ExcursionesWeb.Where(e => e.Activa).OrderBy(e => e.Orden).ToList()
        .Select(e => new
        {
            clave = e.Clave, n = e.Nombre, chip = e.Chip, t = e.EsTravesia, c = e.Color, img = e.Foto,
            resumen = e.Resumen, datos = Lineas(e.Datos), itinerario = Lineas(e.Itinerario),
            incluye = Lineas(e.Incluye), llevar = e.Llevar
        });
    var test = db.TestimoniosWeb.Where(t => t.Activo).OrderBy(t => t.Orden).ToList()
        .Select(t => new { texto = t.Texto, nombre = t.Nombre, lugar = t.Lugar });
    var eq = db.IntegrantesWeb.OrderBy(i => i.Orden).ToList()
        .Select(i => new { nombre = i.Nombre, rol = i.Rol, bio = i.Bio, foto = i.Foto });

    var datos = new
    {
        whatsapp = c.Whatsapp, instagram = c.Instagram, facebook = c.Facebook, linktree = c.Linktree,
        email = c.Email, ubicacion = c.Ubicacion, heroTexto = c.HeroTexto,
        quienes = new[] { c.Quienes1, c.Quienes2, c.Quienes3 },
        excursiones = exc, testimonios = test, equipo = eq
    };
    var json = System.Text.Json.JsonSerializer.Serialize(datos,
        new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    return Results.Content("window.CONTENIDO = " + json + ";", "application/javascript; charset=utf-8");
}).AllowAnonymous();

app.Run();

// Render entrega la conexión a Postgres como una direccion tipo
// "postgres://usuario:clave@servidor:puerto/basedatos". .NET necesita otro formato,
// así que la traducimos acá. Se pide conexión segura (SSL), como exige Render.
static string ConvertirUrlPostgres(string url)
{
    var uri = new Uri(url);
    var datos = uri.UserInfo.Split(':', 2);
    var usuario = Uri.UnescapeDataString(datos[0]);
    var clave = datos.Length > 1 ? Uri.UnescapeDataString(datos[1]) : "";
    var baseDatos = uri.AbsolutePath.TrimStart('/');
    var puerto = uri.Port > 0 ? uri.Port : 5432;
    return $"Host={uri.Host};Port={puerto};Database={baseDatos};Username={usuario};" +
           $"Password={clave};SSL Mode=Require;Trust Server Certificate=true";
}

// Pasa a "sin zona horaria" cualquier columna de fecha que haya quedado creada
// "con zona horaria" en Postgres. No borra ni pierde datos: Postgres convierte los
// valores existentes. Si no hay ninguna para arreglar, no hace nada.
static void ArreglarFechasPostgres(AppDbContext db)
{
    var conn = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) conn.Open();

    var aArreglar = new List<(string Tabla, string Columna)>();
    using (var buscar = conn.CreateCommand())
    {
        buscar.CommandText = @"
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND data_type = 'timestamp with time zone';";
        using var reader = buscar.ExecuteReader();
        while (reader.Read())
            aArreglar.Add((reader.GetString(0), reader.GetString(1)));
    }

    foreach (var (tabla, columna) in aArreglar)
    {
        using var alter = conn.CreateCommand();
        alter.CommandText =
            $@"ALTER TABLE ""{tabla}"" ALTER COLUMN ""{columna}"" TYPE timestamp without time zone;";
        alter.ExecuteNonQuery();
    }
}

// Agrega una columna a una tabla SQLite solo si todavía no existe (no borra datos).
static void EnsureSqliteColumn(AppDbContext db, string tabla, string columna, string tipoSql)
{
    var conn = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) conn.Open();

    bool existe = false;
    using (var check = conn.CreateCommand())
    {
        check.CommandText = $"PRAGMA table_info({tabla});";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columna, StringComparison.OrdinalIgnoreCase))
            {
                existe = true;
                break;
            }
        }
    }

    if (!existe)
    {
        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tabla} ADD COLUMN {columna} {tipoSql};";
        alter.ExecuteNonQuery();
    }
}

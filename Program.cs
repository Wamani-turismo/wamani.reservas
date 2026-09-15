using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
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

// ---- Que la sesión NO se cierre en cada actualización ----
//
// La cookie de "estás logueado" va firmada con una llave. Por defecto esa llave vive en la
// memoria del servidor, así que cada vez que se sube un cambio la app se reinicia, la llave
// se pierde y todas las cookies dejan de valer: hay que volver a entrar con usuario y clave.
//
// Guardándola en el disco de Render (el mismo que ya se usa para los comprobantes), la
// llave sobrevive al reinicio y la sesión sigue abierta.
//
// El nombre de la aplicación tiene que quedar FIJO: es parte de la firma, y si cambia, las
// cookies viejas también dejan de valer.
// Antes esto se hacía SÓLO si existía la variable UPLOADS_DIR: si por lo que fuera no
// estaba, no se avisaba en ningún lado y la sesión volvía a cerrarse en cada actualización
// sin que se entendiera por qué. Ahora siempre se guardan en algún lado, y en el arranque
// queda escrito en los registros de Render en qué carpeta, para poder mirarlo.
var carpetaLlaves = Environment.GetEnvironmentVariable("UPLOADS_DIR");
if (string.IsNullOrWhiteSpace(carpetaLlaves) && Directory.Exists("/var/data"))
{
    // El disco persistente de Render, por si la variable se borró de la configuración
    carpetaLlaves = "/var/data";
}
if (string.IsNullOrWhiteSpace(carpetaLlaves))
{
    // Último recurso (la compu de casa): al lado del proyecto
    carpetaLlaves = builder.Environment.ContentRootPath;
}

var dirLlaves = Path.Combine(carpetaLlaves, "llaves-sesion");
try
{
    Directory.CreateDirectory(dirLlaves);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dirLlaves))
        .SetApplicationName("wamani-reservas");
    Console.WriteLine($"[sesion] Las llaves se guardan en: {dirLlaves}");
}
catch (Exception ex)
{
    // Si la carpeta no se puede crear, la app tiene que arrancar igual: lo único que pasa
    // es que hay que volver a entrar con usuario y clave después de cada actualización.
    Console.WriteLine($"[sesion] NO se pudieron guardar las llaves en {dirLlaves}: {ex.Message}");
    Console.WriteLine("[sesion] La sesion se va a cerrar en cada actualizacion hasta que se resuelva.");
}

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
    EnsureSqliteColumn(db, "OperativoGastos", "ReservaId", "INTEGER NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "FechaSena", "TEXT NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "FechaSaldo", "TEXT NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "ReservaId", "INTEGER NULL");
    EnsureSqliteColumn(db, "Excursiones", "EsAMedida", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(db, "Excursiones", "EsPersonalizada", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(db, "GastosEmpresa", "DelFondo", "INTEGER NOT NULL DEFAULT 0");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""IngresosExtra"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_IngresosExtra"" PRIMARY KEY AUTOINCREMENT,
            ""Fecha"" TEXT NOT NULL,
            ""Motivo"" TEXT NOT NULL DEFAULT 'Comisión',
            ""Descripcion"" TEXT NOT NULL DEFAULT '',
            ""DeQuien"" TEXT NULL,
            ""Monto"" TEXT NOT NULL DEFAULT '0',
            ""Comprobante"" TEXT NULL
        );");
    // Travesías: en qué lugar de la ruta es el hospedaje, y el grupo que duerme ahí
    EnsureSqliteColumn(db, "OperativoProveedores", "Lugar", "TEXT NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "Personas", "INTEGER NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "PrecioPorPersona", "TEXT NULL");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""EtapasExcursion"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_EtapasExcursion"" PRIMARY KEY AUTOINCREMENT,
            ""ExcursionId"" INTEGER NOT NULL,
            ""Orden"" INTEGER NOT NULL DEFAULT 1,
            ""Lugar"" TEXT NOT NULL DEFAULT '',
            ""ProveedorId"" INTEGER NULL,
            ""PrecioPorPersona"" TEXT NOT NULL DEFAULT '0',
            ""Incluye"" TEXT NULL
        );");

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
    // Acceso limitado de un colaborador a UNA sola excursión
    EnsureSqliteColumn(db, "Usuarios", "ExcursionesPermitidas", "TEXT NULL");
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
    EnsureSqliteColumn(db, "ExcursionesWeb", "Fotos", "TEXT NOT NULL DEFAULT ''");
    EnsureSqliteColumn(db, "ExcursionesWeb", "EsSelecta", "INTEGER NOT NULL DEFAULT 0");
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
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""VideosWeb"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_VideosWeb"" PRIMARY KEY AUTOINCREMENT,
            ""Titulo"" TEXT NOT NULL, ""Descripcion"" TEXT NOT NULL, ""Archivo"" TEXT NOT NULL,
            ""Poster"" TEXT NOT NULL, ""Vertical"" INTEGER NOT NULL, ""Orden"" INTEGER NOT NULL, ""Activo"" INTEGER NOT NULL
        );");
    // Estas van al FINAL, cuando ya se crearon todas las tablas de arriba: si la columna
    // se pide sobre una tabla que todavía no existe, el ALTER TABLE falla.
    //
    // Noches: cuántas noches seguidas se duerme en un mismo lugar (2 noches en el mismo
    // hospedaje = una sola fila en el operativo, con el total calculado solo).
    // Cantidad: arrieros, caballos, guías y traslados, que se suben y bajan a mano en
    // cada salida porque no salen de ninguna fórmula.
    EnsureSqliteColumn(db, "EtapasExcursion", "Noches", "INTEGER NOT NULL DEFAULT 1");
    EnsureSqliteColumn(db, "EtapasExcursion", "Tipo", "TEXT NOT NULL DEFAULT 'Hospedaje'");
    EnsureSqliteColumn(db, "EtapasExcursion", "Cantidad", "INTEGER NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "Noches", "INTEGER NULL");
    EnsureSqliteColumn(db, "GastosExcursion", "Cantidad", "INTEGER NULL");
    EnsureSqliteColumn(db, "OperativoGastos", "Cantidad", "INTEGER NULL");
    // Nota escrita a mano al lado de cada costo de la excursión (el botón 📋)
    EnsureSqliteColumn(db, "GastosExcursion", "Comentario", "TEXT NULL");
    // La misma nota, pero de UNA salida puntual: en el operativo, por gasto y por proveedor
    EnsureSqliteColumn(db, "OperativoGastos", "Comentario", "TEXT NULL");
    EnsureSqliteColumn(db, "OperativoProveedores", "Comentario", "TEXT NULL");
    // Los ítems de la plantilla que se borraron a mano en una salida (para no recopiarlos)
    EnsureSqliteColumn(db, "OperativoSalidas", "ItemsBorrados", "TEXT NULL");

    // Las consultas que deja la gente con el formulario de la web
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""ConsultasWeb"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ConsultasWeb"" PRIMARY KEY AUTOINCREMENT,
            ""Nombre"" TEXT NOT NULL, ""Email"" TEXT NULL, ""Telefono"" TEXT NULL,
            ""Tipo"" TEXT NOT NULL, ""Mensaje"" TEXT NOT NULL, ""Origen"" TEXT NULL,
            ""CreadaEl"" TEXT NOT NULL, ""Atendida"" INTEGER NOT NULL DEFAULT 0
        );");
    // El archivo que se puede adjuntar a una consulta (va en las dos bases, ver abajo)
    EnsureSqliteColumn(db, "ConsultasWeb", "ArchivoNombre", "TEXT NULL");
    EnsureSqliteColumn(db, "ConsultasWeb", "ArchivoGuardado", "TEXT NULL");
    // Quién movió plata: se anota una línea por cada guardado que cambia dinero
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Actividades"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Actividades"" PRIMARY KEY AUTOINCREMENT,
            ""Fecha"" TEXT NOT NULL, ""Usuario"" TEXT NOT NULL, ""Nombre"" TEXT NOT NULL,
            ""Que"" TEXT NOT NULL, ""Detalle"" TEXT NOT NULL, ""ExcursionId"" INTEGER NULL,
            ""Monto"" TEXT NOT NULL DEFAULT '0', ""EsIngreso"" INTEGER NOT NULL DEFAULT 0
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
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoProveedores"" ADD COLUMN IF NOT EXISTS ""ReservaId"" integer;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoGastos"" ADD COLUMN IF NOT EXISTS ""ReservaId"" integer;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Excursiones"" ADD COLUMN IF NOT EXISTS ""EsAMedida"" boolean NOT NULL DEFAULT false;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Excursiones"" ADD COLUMN IF NOT EXISTS ""EsPersonalizada"" boolean NOT NULL DEFAULT false;");
        // Acceso limitado de un colaborador a UNA sola excursión
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Usuarios"" ADD COLUMN IF NOT EXISTS ""ExcursionesPermitidas"" text;");
        // Quién movió plata: se anota una línea por cada guardado que cambia dinero
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Actividades"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Fecha"" timestamp without time zone NOT NULL,
                ""Usuario"" text NOT NULL DEFAULT '',
                ""Nombre"" text NOT NULL DEFAULT '',
                ""Que"" text NOT NULL DEFAULT '',
                ""Detalle"" text NOT NULL DEFAULT '',
                ""ExcursionId"" integer NULL,
                ""Monto"" numeric NOT NULL DEFAULT 0,
                ""EsIngreso"" boolean NOT NULL DEFAULT false
            );");
        // Fondo del 10%: marca de los gastos que se pagan con ese fondo
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""GastosEmpresa"" ADD COLUMN IF NOT EXISTS ""DelFondo"" boolean NOT NULL DEFAULT false;");
        // Ingresos EXTRA (comisiones, alquileres, servicios sueltos)
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""IngresosExtra"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Fecha"" timestamp without time zone NOT NULL,
                ""Motivo"" text NOT NULL DEFAULT 'Comisión',
                ""Descripcion"" text NOT NULL DEFAULT '',
                ""DeQuien"" text NULL,
                ""Monto"" numeric NOT NULL DEFAULT 0,
                ""Comprobante"" text NULL
            );");
        // Travesías: en qué lugar de la ruta es el hospedaje, y el grupo que duerme ahí
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoProveedores"" ADD COLUMN IF NOT EXISTS ""Lugar"" text;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoProveedores"" ADD COLUMN IF NOT EXISTS ""Personas"" integer;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoProveedores"" ADD COLUMN IF NOT EXISTS ""PrecioPorPersona"" numeric;");
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""EtapasExcursion"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""ExcursionId"" integer NOT NULL,
                ""Orden"" integer NOT NULL DEFAULT 1,
                ""Lugar"" text NOT NULL DEFAULT '',
                ""ProveedorId"" integer NULL,
                ""PrecioPorPersona"" numeric NOT NULL DEFAULT 0,
                ""Incluye"" text NULL
            );");

        // Noches seguidas en un mismo lugar: permite cargar "2 noches en el mismo hospedaje"
        // con una sola fila (Yungas), en vez de escribir a mano el precio de las dos noches.
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""EtapasExcursion"" ADD COLUMN IF NOT EXISTS ""Noches"" integer NOT NULL DEFAULT 1;");

        // Las "etapas" dejan de ser sólo hospedaje: ahora también son los traslados, los
        // arrieros y los caballos. Todos se cuentan igual (cantidad × precio × veces).
        // Las filas que ya existían son hospedaje, que es el valor por defecto.
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""EtapasExcursion"" ADD COLUMN IF NOT EXISTS ""Tipo"" text NOT NULL DEFAULT 'Hospedaje';");

        // Cuántos guías, arrieros o caballos van normalmente: los únicos que no salen de
        // una fórmula. Sirve de referencia en cada salida y para el cálculo del costo.
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""EtapasExcursion"" ADD COLUMN IF NOT EXISTS ""Cantidad"" integer;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoProveedores"" ADD COLUMN IF NOT EXISTS ""Noches"" integer;");

        // Costos que se cuentan por CANTIDAD (arrieros, caballos, guías, traslados): no
        // salen de una fórmula, se suben y se bajan a mano en cada salida.
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""GastosExcursion"" ADD COLUMN IF NOT EXISTS ""Cantidad"" integer;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoGastos"" ADD COLUMN IF NOT EXISTS ""Cantidad"" integer;");

        // Nota escrita a mano al lado de cada costo de la excursión (el botón 📋)
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""GastosExcursion"" ADD COLUMN IF NOT EXISTS ""Comentario"" text;");
        // La misma nota, pero de UNA salida puntual: en el operativo, por gasto y por proveedor
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoGastos"" ADD COLUMN IF NOT EXISTS ""Comentario"" text;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoProveedores"" ADD COLUMN IF NOT EXISTS ""Comentario"" text;");
        // Los ítems de la plantilla que se borraron a mano en una salida (para no recopiarlos)
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""OperativoSalidas"" ADD COLUMN IF NOT EXISTS ""ItemsBorrados"" text;");

        // Las consultas que deja la gente con el formulario de la web
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""ConsultasWeb"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Nombre"" text NOT NULL, ""Email"" text NULL, ""Telefono"" text NULL,
                ""Tipo"" text NOT NULL, ""Mensaje"" text NOT NULL, ""Origen"" text NULL,
                ""CreadaEl"" timestamp without time zone NOT NULL,
                ""Atendida"" boolean NOT NULL DEFAULT false
            );");
        // El archivo que se puede adjuntar a una consulta
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""ConsultasWeb"" ADD COLUMN IF NOT EXISTS ""ArchivoNombre"" text;");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""ConsultasWeb"" ADD COLUMN IF NOT EXISTS ""ArchivoGuardado"" text;");

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
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""ExcursionesWeb"" ADD COLUMN IF NOT EXISTS ""Fotos"" text NOT NULL DEFAULT '';");
        // Wamani Selecta: la linea premium de la web
        db.Database.ExecuteSqlRaw(@"ALTER TABLE ""ExcursionesWeb"" ADD COLUMN IF NOT EXISTS ""EsSelecta"" boolean NOT NULL DEFAULT false;");
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
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""VideosWeb"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Titulo"" text NOT NULL, ""Descripcion"" text NOT NULL, ""Archivo"" text NOT NULL,
                ""Poster"" text NOT NULL, ""Vertical"" boolean NOT NULL, ""Orden"" integer NOT NULL, ""Activo"" boolean NOT NULL
            );");
    }

    // Un gasto del operativo sin tildar NO está pagado: no puede tener fecha de pago.
    // Hasta ahora la fecha se ponía sola con sólo tener precio, así que toda la estimación
    // copiada de la plantilla quedaba marcada como plata pagada y restaba en Finanzas y en
    // Caja sin que nadie hubiera gastado nada. Esto lo deja consistente (y como la regla
    // nueva ya no estampa fechas sin tilde, en adelante no cambia nada).
    {
        var sinTilde = db.OperativoGastos.Where(o => !o.Comprado && o.FechaPago != null).ToList();
        if (sinTilde.Count > 0)
        {
            foreach (var g in sinTilde) g.FechaPago = null;
            db.SaveChanges();
            Console.WriteLine($"[migracion] {sinTilde.Count} gastos del operativo sin tildar: se les quitó la fecha de pago.");
        }
    }

    // Gastos huérfanos: quedaron colgados de una "salida" (excursión + fecha) que no tiene
    // ninguna reserva. Pasa si se abre el operativo de una fecha equivocada, o si después
    // se borran/mueven todas las reservas de esa salida. Sólo se borran los que son
    // claramente basura: sin tildar, sin fecha de pago y sin comprobante. Si alguno tiene
    // plata o papeles cargados NO se toca, para no perder nada por las dudas.
    {
        var salidasReales = db.Reservas
            .Select(r => new { r.ExcursionId, r.FechaDesde })
            .ToList()
            .Select(x => (Exc: x.ExcursionId ?? 0, Fecha: x.FechaDesde.Date))
            .ToHashSet();

        var huerfanos = db.OperativoGastos
            .Where(o => !o.Comprado && o.FechaPago == null && o.Comprobante == null)
            .ToList()
            .Where(o => !salidasReales.Contains((o.ExcursionId, o.Fecha.Date)))
            .ToList();

        if (huerfanos.Count > 0)
        {
            db.OperativoGastos.RemoveRange(huerfanos);
            db.SaveChanges();
            Console.WriteLine($"[migracion] {huerfanos.Count} gasto(s) de salidas sin reservas: borrados.");
        }
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

    // "Excursión a medida": salidas armadas a pedido del cliente, sin precio de catálogo ni
    // itinerario fijo (por ejemplo llevar gente a una zona donde no hacemos salidas regulares).
    // Se crea sola una vez; después se le puede cambiar el nombre o desactivar como a cualquier
    // otra. Al elegirla en una reserva, el precio por persona se escribe siempre a mano.
    if (!db.Excursiones.Any(e => e.EsAMedida))
    {
        db.Excursiones.Add(new Wamani.Reservas.Models.Excursion
        {
            Nombre = Wamani.Reservas.Models.Excursion.NombreAMedida,
            PrecioPorPersona = 0,
            MinimoPersonas = 1,
            MaximoPersonas = 30,
            EsAMedida = true,
            Activa = true
        });
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

    // ---- Los testimonios de ejemplo pasan a ser reseñas REALES de Google ----
    //
    // Cuando se armó la web quedaron tres testimonios inventados, firmados "María —
    // ejemplo", "Juan — ejemplo" y "Lucía — ejemplo". Se reemplazan por tres reseñas de
    // verdad, copiadas tal cual de la ficha de Google (las más recientes).
    //
    // Sólo se tocan los que tienen "ejemplo" en el nombre: si desde el panel se cargan
    // otros, o se editan éstos, no se vuelven a pisar nunca.
    var testimoniosDeEjemplo = db.TestimoniosWeb.Where(t => t.Nombre.Contains("ejemplo")).ToList();
    if (testimoniosDeEjemplo.Count > 0)
    {
        db.TestimoniosWeb.RemoveRange(testimoniosDeEjemplo);
        db.TestimoniosWeb.AddRange(
            new Wamani.Reservas.Models.TestimonioWeb
            {
                Texto = "Muy buena la travesía de dos días a las yungas!!! Facu, nuestro guía, un crack. " +
                        "No solo nos coordinó todo el viaje, sino que nos hizo probar la sopa de maní (un manjar) " +
                        "y alfajores de tomate! La cabaña de San Francisco muy buena y la cena Gourmet también. " +
                        "Con gusto a poco de lo bien que la pasamos!!",
                Nombre = "Marcos Garro",
                Lugar = "Travesía a las Yungas",
                Orden = 1,
                Activo = true
            },
            new Wamani.Reservas.Models.TestimonioWeb
            {
                Texto = "Hermosa travesía realizada por Quebrada - Yungas Jujeñas con Wamani Turismo... " +
                        "conociendo lugares únicos y mágicos y personas maravillosas, disfrutando de la vida " +
                        "y de la naturaleza!! Muchas gracias por todo a nuestro guía Facu (genio!!!), que hizo " +
                        "que conociéramos cada rinconcito y que este viaje sea inolvidable.",
                Nombre = "Claudia Marcela Abatte",
                Lugar = "Quebrada y Yungas jujeñas",
                Orden = 2,
                Activo = true
            },
            new Wamani.Reservas.Models.TestimonioWeb
            {
                Texto = "Increíble experiencia! Hice 3 días en las yungas y fue hermoso todo. Muy amigable " +
                        "para gente que le gusta aventurarse pero con buenos lugares para descansar y comer rico!!! " +
                        "Volveré a verlos para Tilcara-Calilegua que me queda pendiente 💚🌱",
                Nombre = "Guadalupe Mallo",
                Lugar = "3 días en las Yungas",
                Orden = 3,
                Activo = true
            });
        db.SaveChanges();
    }

    // Corrección (una sola vez): SOLO Tilcara-Calilegua, Humahuaca-Yungas e
    // Iruya-Nazareno son travesías. Las demás son excursiones (aunque duren
    // varios días). Como el guard es EsTravesia==true, una vez corregidas no se
    // vuelven a tocar; si los chicos después cambian algo desde el panel, queda.
    var claves = new[] { "yungas", "yungas-express", "ecolodge" };
    foreach (var e in db.ExcursionesWeb.Where(x => x.EsTravesia && claves.Contains(x.Clave)))
    {
        e.EsTravesia = false;
        e.Chip = e.Chip.Replace("Travesía · ", "").Replace("Travesía", "").Trim();
    }
    db.SaveChanges();

    // El video de la bandera no combinaba (tamaño distinto) → se saca. Una sola vez.
    var banderas = db.VideosWeb.Where(v => v.Archivo == "video-bandera.mp4").ToList();
    if (banderas.Count > 0) { db.VideosWeb.RemoveRange(banderas); db.SaveChanges(); }

    // Normaliza las "claves" viejas y cortas a las definitivas (que usan los puntos
    // del mapa), por si alguna base quedó con las cortas. Idempotente: una vez
    // cambiadas, la clave corta ya no existe y no vuelve a tocar nada.
    var clavesMapa = new Dictionary<string, string>
    {
        ["salinas"] = "atardecer-en-las-salinas",
        ["quebrada"] = "recorriendo-la-quebrada",
        ["jordan"] = "termas-de-jordan",
        ["lagunas"] = "ruta-de-lagunas-y-termas",
        ["santuyoc"] = "cascada-santuyoc-y-angosto-de-jaire",
        ["yungas"] = "conociendo-las-yungas",
        ["tilcara"] = "tilcara-calilegua",
        ["iruya"] = "iruya-nazareno",
        ["ecolodge"] = "especial-ecolodge-de-la-selva",
    };
    var huboCambioClave = false;
    foreach (var e in db.ExcursionesWeb.ToList())
        if (clavesMapa.TryGetValue(e.Clave, out var definitiva)) { e.Clave = definitiva; huboCambioClave = true; }
    if (huboCambioClave) db.SaveChanges();
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

// /web/ es la dirección VIEJA de la landing: ahora vive en la raíz. Se manda con 301
// (permanente) para que Google traslade a "/" lo que tenía acumulado en /web/ y para que
// el que la tenga en favoritos no se quede en una copia. Va ANTES de los archivos
// estáticos, que si no entregarían el index.html sin llegar hasta acá.
//
// Sólo la dirección exacta: /web/img/…, /web/contenido.js y /web/consulta siguen
// funcionando igual, y de hecho la página los necesita.
app.Use(async (ctx, siguiente) =>
{
    var ruta = ctx.Request.Path.Value ?? "";
    if (ruta.Equals("/web", StringComparison.OrdinalIgnoreCase) ||
        ruta.Equals("/web/", StringComparison.OrdinalIgnoreCase) ||
        ruta.Equals("/web/index.html", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.Redirect("/" + ctx.Request.QueryString, permanent: true);
        return;
    }
    await siguiente();
});

// Hace que una carpeta (ej. /web/) muestre su index.html automáticamente.
// Solo afecta a carpetas que TIENEN index.html; la raíz "/" sigue siendo el
// sistema (no hay index.html en wwwroot), así que no cambia nada del sistema.
app.UseDefaultFiles();

// Servir archivos estáticos, agregando el tipo del manifiesto de la app (PWA)
var tiposArchivo = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
tiposArchivo.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = tiposArchivo,

    // Las PÁGINAS (.html) se revisan siempre contra el servidor antes de mostrarse.
    //
    // Sin esto el navegador se queda con la copia que bajó la primera vez y no se entera
    // de los cambios: se subía algo a la web y el que ya la había visitado seguía viendo
    // la versión vieja. "no-cache" no significa "no la guardes", sino "preguntá si cambió
    // antes de usarla": si no cambió, el servidor contesta que siga con la que tiene.
    //
    // Las fotos, el CSS y el logo se siguen guardando normalmente (son los que pesan).
    //
    // Los .js van igual que el .html, y por la misma razón. Detectado el 02/09/2026:
    // se subió una traducción nueva, el servidor ya la tenía, pero el navegador seguía
    // usando el idiomas-textos.js que había bajado antes. Cualquier corrección de una
    // traducción habría quedado invisible para el que ya visitó la web. Pesan poco y
    // con "no-cache" el servidor contesta "no cambió" sin mandarlos de nuevo.
    OnPrepareResponse = ctx =>
    {
        var n = ctx.File.Name;
        if (n.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            n.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, must-revalidate";
    }
});

// Si hay disco persistente (internet), servir los comprobantes guardados ahí
// en la misma dirección /comprobantes de siempre (así los links no cambian).
var carpetaComprobantes = Environment.GetEnvironmentVariable("UPLOADS_DIR");
if (!string.IsNullOrWhiteSpace(carpetaComprobantes))
{
    Directory.CreateDirectory(carpetaComprobantes);

    // CANDADO: los archivos que adjunta la gente en el formulario de la web se guardan en
    // UPLOADS_DIR/consultas, que queda ADENTRO de la carpeta que se publica acá abajo como
    // /comprobantes. Sin esto, cualquiera podría bajarlos con la dirección
    // /comprobantes/consultas/7.pdf, sin iniciar sesión.
    // Esos archivos se bajan SÓLO desde el panel, por la pantalla de Consultas.
    app.Use(async (ctx, siguiente) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/comprobantes/consultas"))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await siguiente();
    });

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

// ═══════════════════════════════════════════════════════════════════════
//  CANDADO para los usuarios con acceso limitado a UNA excursión
//
//  Pensado para un colaborador de afuera (por ejemplo, un socio sólo de una
//  travesía): tiene que poder cargar los costos, ver la rentabilidad, manejar el
//  operativo y anotar los pasajeros que consigue de ESA excursión, y NADA MÁS.
//  No tiene que ver la plata de Wamani ni los precios de las otras excursiones.
//
//  El criterio es al revés del habitual: acá está TODO PROHIBIDO y se habilita
//  sólo lo que figura en esta lista. Si mañana se agrega una pantalla nueva al
//  sistema, ese usuario NO la va a ver hasta que alguien la agregue acá a mano.
//  Es a propósito: preferimos que le falte una pantalla y no que se le escape la
//  plata de la empresa.
//
//  Además de la pantalla, se controla el NÚMERO de excursión que viene en la
//  dirección: aunque escriba a mano el id de otra travesía, no entra.
// ═══════════════════════════════════════════════════════════════════════
app.Use(async (ctx, siguiente) =>
{
    // Los permisos se leen de la BASE en cada pedido, NO de la sesión.
    //
    // Es a propósito: la sesión dura 14 días, así que si se guardaran ahí, cambiarle los
    // permisos a alguien —o darlo de baja— no tendría efecto hasta que volviera a entrar.
    // Con esto, un cambio en Usuarios se aplica en el próximo clic.
    List<int> permitidas;
    if (ctx.User?.Identity?.IsAuthenticated != true)
    {
        await siguiente();
        return;
    }
    else
    {
        var idTxt = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var baseDatos = ctx.RequestServices.GetRequiredService<AppDbContext>();
        var quien = int.TryParse(idTxt, out var uid)
            ? await baseDatos.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid)
            : null;

        // Usuario borrado o desactivado mientras tenía la sesión abierta: se lo saca.
        if (quien is null || !quien.Activo)
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            ctx.Response.Redirect("/Login");
            return;
        }

        permitidas = quien.IdsPermitidos();
    }

    if (permitidas.Count == 0)
    {
        await siguiente();          // socio de Wamani: ve todo, como siempre
        return;
    }

    var ruta = (ctx.Request.Path.Value ?? "/").TrimEnd('/');
    if (ruta.Length == 0) ruta = "/";

    // Archivos (css, imágenes, comprobantes): no son pantallas, pasan derecho
    if (Path.HasExtension(ruta)) { await siguiente(); return; }

    // Entrar y salir siempre tienen que funcionar. "/" también: desde el 15/09/2026 es
    // la web pública, que la ve cualquiera —no tiene sentido negársela justo a alguien
    // que además trabaja con nosotros.
    if (ruta.Equals("/", StringComparison.Ordinal) ||
        ruta.Equals("/Login", StringComparison.OrdinalIgnoreCase) ||
        ruta.Equals("/Logout", StringComparison.OrdinalIgnoreCase) ||
        ruta.Equals("/Error", StringComparison.OrdinalIgnoreCase))
    { await siguiente(); return; }

    // Las pantallas habilitadas, y con qué nombre viaja el número de excursión
    // en cada una (null = esa pantalla no lleva número).
    var habilitadas = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["/Operativo"]              = null,          // la lista, ya filtrada a lo suyo
        ["/Operativo/Index"]        = null,
        ["/Operativo/Salida"]       = "excursionId",
        ["/Operativo/Pagar"]        = "excursionId", // detalle de lo que falta pagar
        ["/Operativo/Reservas"]     = "excursionId", // toda la gente anotada en su travesía
        ["/Excursiones"]            = null,          // la lista, filtrada: de ahí entra a los costos
        ["/Excursiones/Index"]      = null,
        ["/Excursiones/Cargar"]     = "id",          // los costos de una travesía suya
        ["/Rentabilidad"]           = null,          // la lista, filtrada
        ["/Rentabilidad/Index"]     = null,
        ["/Rentabilidad/Detalle"]   = "id",
        ["/Rentabilidad/Comparar"]  = "excursionId",
        ["/Reservas/Cargar"]        = null,          // anotar los pasajeros que consigue
    };

    if (!habilitadas.TryGetValue(ruta, out var campoId))
    {
        // Pantalla no habilitada: se lo manda a la suya, sin explicaciones
        ctx.Response.Redirect("/Operativo");
        return;
    }

    // Si la pantalla lleva número de excursión, tiene que ser el suyo
    if (campoId is not null)
    {
        var valor = ctx.Request.Query[campoId].ToString();
        if (!int.TryParse(valor, out var pedida) || !permitidas.Contains(pedida))
        {
            ctx.Response.Redirect("/Operativo");
            return;
        }
    }

    await siguiente();
});

app.MapRazorPages();

// La "puerta de entrada" (la dirección raíz) muestra la LANDING pública.
// Así, cuando se conecte el dominio (wamaniturismo.com), los visitantes ven la
// web y NO el sistema. Los chicos entran al sistema por /panel (requiere login).
// Se conserva lo que venga después del "?" (hoy: ?lang=en / ?lang=fr). Sin esto, el
// enlace en inglés que le damos a Google terminaba en la web en castellano.
// Desde el 15/09/2026 la landing se SIRVE acá, ya no redirige a /web/. El motivo es de
// buscadores: wamaniturismo.com es la dirección que repartimos (tarjetas, folletos,
// Instagram) y la que declaran el sitemap y los hreflang, pero respondía con una
// redirección temporal. Para Google eso significa "no consolides nada acá", así que la
// autoridad quedaba repartida entre dos direcciones y el informe de Indexación marcaba
// páginas duplicadas. Ahora / responde 200 y /web/ manda 301 hacia acá (más abajo).
//
// El archivo sigue viviendo en wwwroot/web/: sólo cambia por dónde se entrega. Por eso
// index.html tuvo que pasar sus rutas a absolutas (/web/img/…, /web/idiomas.js): siendo
// relativas, servido desde la raíz las hubiera buscado en /img/ y no cargaba nada.
app.MapGet("/", (HttpContext ctx, IWebHostEnvironment entorno) =>
{
    // Igual que el resto de las páginas: se revisa contra el servidor antes de usarse,
    // para que nadie siga viendo una versión vieja después de publicar un cambio.
    ctx.Response.Headers.CacheControl = "no-cache";
    return Results.File(Path.Combine(entorno.WebRootPath, "web", "index.html"),
                        "text/html; charset=utf-8");
}).AllowAnonymous();

// La página del QR de la feria (FIT). Sin la barra final el servidor no encuentra la
// carpeta, así que se la agregamos: wamaniturismo.com/receptivo funciona igual.
app.MapGet("/receptivo", () => Results.Redirect("/receptivo/")).AllowAnonymous();

// Dirección corta para el QR de los días profesionales de la feria: entra directo a la
// parte de agencias, sin la pregunta de "¿sos viajero o agencia?". En esos días todo el
// que escanea es del rubro y ese paso de más sólo estorba.
// OJO: se registra UNA sola vez, sin la barra final. Poner las dos versiones hace que el
// servidor no sepa cuál usar y devuelva error 500 (ya pasó, ver el historial).
app.MapGet("/agencias", () => Results.Redirect("/receptivo/#agencia")).AllowAnonymous();

// ═══════════════════════════════════════════════════════════════════════
//  Las direcciones de la WEB ANTERIOR (la de Webnode)
//
//  Aquella web tenía una página por excursión (wamaniturismo.com/conociendo-las-yungas).
//  Ese sitio ya no existe: el dominio ahora apunta acá, así que esas direcciones daban
//  "página no encontrada" — y Google todavía las muestra en los resultados, así que la
//  gente que hacía clic se llevaba un error.
//
//  Con esto, cada una lleva a la web nueva con ESA experiencia abierta. El redirect es
//  "permanente" (301), que es la forma de decirle a Google "esto se mudó acá": con el
//  tiempo reemplaza la dirección vieja por la nueva y le pasa lo que tenía ganado.
//
//  Se listan una por una a propósito, en vez de atrapar cualquier dirección: así no se
//  toca ninguna ruta del sistema y lo que no está en la lista sigue dando 404.
// ═══════════════════════════════════════════════════════════════════════
var paginasWebAnterior = new[]
{
    "conociendo-las-yungas", "yungas-express", "tilcara-calilegua", "iruya-nazareno",
    "humahuaca-yungas", "termas-de-jordan", "recorriendo-la-quebrada",
    "ruta-de-lagunas-y-termas", "atardecer-en-las-salinas",
    "cascada-santuyoc-y-angosto-de-jaire", "especial-ecolodge-de-la-selva",
};

foreach (var pagina in paginasWebAnterior)
{
    var destino = "/web/#" + pagina;
    // Una sola por página: para el servidor, "/pagina" y "/pagina/" son la misma dirección,
    // así que registrar las dos formas hacía que no supiera cuál usar y tiraba error.
    app.MapGet("/" + pagina, () => Results.Redirect(destino, permanent: true)).AllowAnonymous();
}

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
            clave = e.Clave, n = e.Nombre, chip = e.Chip, t = e.EsTravesia, sel = e.EsSelecta, c = e.Color, img = e.Foto,
            // galería: la foto principal primero y después las adicionales (sin repetir)
            fotos = new[] { e.Foto }.Concat(Lineas(e.Fotos)).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToArray(),
            resumen = e.Resumen, datos = Lineas(e.Datos), itinerario = Lineas(e.Itinerario),
            incluye = Lineas(e.Incluye), llevar = e.Llevar
        });
    var test = db.TestimoniosWeb.Where(t => t.Activo).OrderBy(t => t.Orden).ToList()
        .Select(t => new { texto = t.Texto, nombre = t.Nombre, lugar = t.Lugar });
    var eq = db.IntegrantesWeb.OrderBy(i => i.Orden).ToList()
        .Select(i => new { nombre = i.Nombre, rol = i.Rol, bio = i.Bio, foto = i.Foto });
    var vids = db.VideosWeb.Where(v => v.Activo).OrderBy(v => v.Orden).ToList()
        .Select(v => new { titulo = v.Titulo, desc = v.Descripcion, archivo = v.Archivo, poster = v.Poster, vertical = v.Vertical });

    var datos = new
    {
        whatsapp = c.Whatsapp, instagram = c.Instagram, facebook = c.Facebook, linktree = c.Linktree,
        email = c.Email, ubicacion = c.Ubicacion, heroTexto = c.HeroTexto,
        quienes = new[] { c.Quienes1, c.Quienes2, c.Quienes3 },
        excursiones = exc, testimonios = test, equipo = eq, videos = vids
    };
    var json = System.Text.Json.JsonSerializer.Serialize(datos,
        new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    return Results.Content("window.CONTENIDO = " + json + ";", "application/javascript; charset=utf-8");
}).AllowAnonymous();

// ═══════════════════════════════════════════════════════════════════════
//  UNA PÁGINA PROPIA POR EXCURSIÓN  (/excursiones/hornocal, /excursiones/salinas…)
//
//  Por qué existe: la web pública es UNA sola dirección (/web/) y las experiencias
//  se abren en una ventanita adentro. Google ve entonces una única página y no puede
//  mandar a nadie "a la mitad" de ella: en Search Console se comprobó que el 100 % de
//  las visitas de buscador son de gente que escribió "wamani" — de búsquedas como
//  "excursión al Hornocal" no llega nadie, porque no hay ninguna página que hable
//  de eso.
//
//  Estas páginas se arman en el servidor con el MISMO contenido que ya carga Lautaro
//  desde el panel (tabla ExcursionesWeb): no hay que escribir nada dos veces. Cada una
//  tiene su título, su descripción y su itinerario como texto de verdad, que es lo
//  único que Google sabe leer.
//
//  NO reemplazan a la web: son la puerta de entrada desde el buscador. El visitante
//  lee, y de ahí va a WhatsApp o a la web completa.
// ═══════════════════════════════════════════════════════════════════════
app.MapGet("/excursiones/{clave}", (string clave, AppDbContext db) =>
{
    var e = db.ExcursionesWeb.FirstOrDefault(x => x.Activa && x.Clave == clave);
    // Si la clave no existe (o la excursión se dio de baja), al inicio de la web.
    // Mejor eso que un 404 feo para alguien que llegó de Google con un link viejo.
    if (e == null) return Results.Redirect("/", permanent: false);

    var c = db.ContenidoWeb.FirstOrDefault() ?? new Wamani.Reservas.Models.ContenidoWeb();
    var wpp = string.IsNullOrWhiteSpace(c.Whatsapp) ? "5491178898516" : c.Whatsapp;

    string Esc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
    string[] Lineas(string s) => (s ?? "").Replace("\r", "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    // Lista <li> a partir de un campo de varias líneas del panel. Si está vacío
    // devuelve "" y más abajo la sección entera no se dibuja.
    string Items(string s)
    {
        var ls = Lineas(s);
        return ls.Length == 0 ? "" : string.Concat(ls.Select(x => "<li>" + Esc(x) + "</li>"));
    }
    string Bloque(string titulo, string html) =>
        string.IsNullOrEmpty(html) ? "" : $"<h2>{Esc(titulo)}</h2><ul>{html}</ul>";

    var url = "https://wamaniturismo.com/excursiones/" + Uri.EscapeDataString(e.Clave);
    // La base guarda SOLO el nombre del archivo ("iruya-2.webp"); las fotos se sirven en
    // /web/img. Hay que poner esa carpeta adelante: sin ella el navegador la buscaba al
    // lado de esta página (/excursiones/iruya-2.webp) y no la encontraba.
    var foto = string.IsNullOrWhiteSpace(e.Foto)
        ? "/logo/wamani-icono-512.png"
        : (e.Foto.StartsWith("http") || e.Foto.StartsWith("/")) ? e.Foto : "/web/img/" + e.Foto;
    var fotoAbs = foto.StartsWith("http") ? foto : "https://wamaniturismo.com" + foto;
    // El título de la pestaña y del resultado de Google. Se le agrega "Jujuy" porque
    // casi nadie busca el nombre pelado: busca el lugar.
    // Se corta cerca de los 60 caracteres en el resultado de Google, así que el agregado
    // es corto: la palabra que describe qué es, y el lugar.
    var queEs = e.EsTravesia ? " — Travesía en Jujuy" : " — Excursión en Jujuy";
    var titulo = e.Nombre + (e.Nombre.Contains("Jujuy", StringComparison.OrdinalIgnoreCase)
        ? "" : queEs) + " | Wamani Turismo";
    // Descripción para el resultado de Google: Google recorta cerca de 160 caracteres.
    var resumen = (e.Resumen ?? "").Trim();
    var desc = resumen.Length > 155 ? resumen.Substring(0, 152).TrimEnd() + "…" : resumen;
    if (string.IsNullOrWhiteSpace(desc))
        desc = e.Nombre + " con Wamani Turismo: guías locales y grupos reducidos en Jujuy.";
    // Texto distinto al de la web principal a propósito: es la única forma de saber, al
    // leer el WhatsApp, si la consulta salió de estas páginas nuevas o del sitio de siempre.
    var msg = Uri.EscapeDataString("¡Hola! Estuve viendo la página de " + e.Nombre + " y quiero consultar.");

    var llevar = string.IsNullOrWhiteSpace(e.Llevar)
        ? "" : "<h2>Qué llevar</h2><p class='resumen'>" + Esc(e.Llevar) + "</p>";
    var datos = Lineas(e.Datos);
    var chips = string.Concat(datos.Select(d => $"<span class=\"dato\">{Esc(d)}</span>"));
    var plazo = e.EsTravesia ? 7 : 4;
    var tipo = e.EsTravesia ? "travesía" : "excursión";

    // Ficha para Google en su propio formato (JSON-LD). Es lo que le permite mostrar
    // el resultado enriquecido con nombre, foto y a qué agencia pertenece.
    var ficha = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "TouristTrip",
        ["name"] = e.Nombre,
        ["description"] = resumen,
        ["url"] = url,
        ["image"] = fotoAbs,
        ["touristType"] = e.EsTravesia ? "Trekking y travesías de varios días" : "Excursión de un día",
        ["provider"] = new Dictionary<string, object>
        {
            ["@type"] = "TravelAgency",
            ["name"] = "Wamani Turismo",
            ["url"] = "https://wamaniturismo.com/",
            ["areaServed"] = "Jujuy, Argentina"
        }
    }, new System.Text.Json.JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

    var html = $$"""
<!DOCTYPE html>
<html lang="es-AR">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>{{Esc(titulo)}}</title>
<meta name="description" content="{{Esc(desc)}}">
<link rel="canonical" href="{{Esc(url)}}">
<meta property="og:type" content="article">
<meta property="og:title" content="{{Esc(titulo)}}">
<meta property="og:description" content="{{Esc(desc)}}">
<meta property="og:image" content="{{Esc(fotoAbs)}}">
<meta property="og:url" content="{{Esc(url)}}">
<link rel="icon" href="/favicon.ico" sizes="any">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Playfair+Display:wght@600;700&family=Quicksand:wght@400;500;600;700&display=swap" rel="stylesheet">
<script async src="https://www.googletagmanager.com/gtag/js?id=G-X04DXRSG6H"></script>
<script>
  window.dataLayer = window.dataLayer || [];
  function gtag(){dataLayer.push(arguments);}
  gtag('js', new Date());
  gtag('config', 'G-X04DXRSG6H');
</script>
<script type="application/ld+json">{{ficha}}</script>
<style>
:root{
  --fondo:#131c18; --panel:#1c2a24; --verde:#22332f; --dorado:#d8c096;
  --coral:#d2673a; --texto:#ece5d4; --texto-suave:rgba(236,229,212,.72);
  --linea:rgba(216,192,150,.18);
}
*{margin:0;padding:0;box-sizing:border-box}
body{background:var(--fondo);color:var(--texto);font-family:Quicksand,system-ui,sans-serif;line-height:1.65}
img{max-width:100%;display:block}
a{color:var(--dorado)}
.barra{padding:18px 22px;border-bottom:1px solid var(--linea)}
.barra a{color:var(--dorado);text-decoration:none;font-weight:600;font-size:.95rem}
.envoltorio{max-width:820px;margin:0 auto;padding:0 22px 70px}
/* La foto de portada se muestra ENTERA: varias de las fotos cargadas son verticales
   (1080x1920 y parecidas) y recortarlas a un recuadro apaisado les corta media imagen.
   Los costados se rellenan con una copia borrosa de la misma foto. */
.tapa{position:relative;overflow:hidden;border-radius:18px;margin:26px 0 22px;
      height:clamp(210px,42vw,400px);background:#0d1512}
.tapa::before{content:"";position:absolute;inset:-8%;background-image:var(--f);
      background-size:cover;background-position:center;filter:blur(26px);opacity:.5}
.tapa img{position:relative;width:100%;height:100%;object-fit:contain;display:block}
/* En el celular la portada pasa a ser cuadrada: con la altura de 210 px una foto
   vertical entraba a unos 118 px de ancho y no se veía nada. */
@media (max-width:560px){ .tapa{height:auto;aspect-ratio:1/1} }
h1{font-family:'Playfair Display',Georgia,serif;font-size:clamp(1.8rem,5vw,2.7rem);line-height:1.15;margin-bottom:14px}
h2{font-family:'Playfair Display',Georgia,serif;font-size:1.35rem;color:var(--dorado);margin:32px 0 10px}
.chip{display:inline-block;background:var(--verde);border:1px solid var(--linea);color:var(--dorado);
      padding:5px 14px;border-radius:999px;font-size:.82rem;font-weight:600;margin-bottom:16px}
.resumen{font-size:1.1rem;color:var(--texto-suave)}
.datos{display:flex;flex-wrap:wrap;gap:9px;margin:20px 0}
.dato{background:var(--panel);border:1px solid var(--linea);padding:7px 14px;border-radius:12px;font-size:.9rem}
ul{padding-left:22px}
li{margin:7px 0}
.condiciones{background:var(--panel);border:1px solid var(--linea);border-radius:16px;
             padding:18px 20px;margin-top:30px;font-size:.95rem;color:var(--texto-suave)}
.acciones{display:flex;flex-wrap:wrap;gap:12px;margin-top:32px}
.btn{display:inline-block;padding:14px 26px;border-radius:14px;text-decoration:none;font-weight:700}
.btn-coral{background:var(--coral);color:#fff}
.btn-borde{border:1px solid var(--linea);color:var(--texto)}
.pie{border-top:1px solid var(--linea);margin-top:48px;padding-top:22px;font-size:.88rem;color:var(--texto-suave)}
</style>
</head>
<body>
<nav class="barra"><a href="/">← Wamani Turismo · todas las experiencias</a></nav>
<div class="envoltorio">
  <div class="tapa" style="--f:url('{{Esc(foto)}}')">
    <img src="{{Esc(foto)}}" alt="{{Esc(e.Nombre)}}">
  </div>
  <span class="chip">{{Esc(e.Chip)}}</span>
  <h1>{{Esc(e.Nombre)}}</h1>
  <p class="resumen">{{Esc(resumen)}}</p>
  <div class="datos">{{chips}}</div>
  {{Bloque("Itinerario", Items(e.Itinerario))}}
  {{Bloque("Qué incluye", Items(e.Incluye))}}
  {{llevar}}
  <div class="condiciones">
    Se reserva con una seña del 50 % y el saldo se abona hasta {{plazo}} días antes de
    realizar la {{tipo}}. En caso de cancelar, se retendrá la totalidad de la seña.
  </div>
  <div class="acciones">
    <a id="wpp" class="btn btn-coral" target="_blank" rel="noopener"
       href="https://wa.me/{{Esc(wpp)}}?text={{msg}}"
       data-excursion="{{Esc(e.Nombre)}}">💬 Consultar por WhatsApp</a>
    <a class="btn btn-borde" href="/">Ver todas las experiencias</a>
  </div>
  <p class="pie">Wamani Turismo — excursiones, trekking y travesías en Jujuy, Argentina.
  Guías locales y grupos reducidos.</p>
</div>
<script>
  // El mismo evento que mide la web grande, para que las consultas que salen de estas
  // páginas aparezcan en Analytics junto a las otras y con el nombre de la excursión.
  // El nombre viaja en un atributo (y no escrito acá adentro) para que un apóstrofo
  // en el nombre no rompa el script.
  document.getElementById('wpp').addEventListener('click', function(){
    try { if (typeof gtag === 'function')
      gtag('event','clic_whatsapp',{ desde:'ficha', excursion:this.dataset.excursion }); } catch(e){}
  });
</script>
</body>
</html>
""";
    return Results.Content(html, "text/html; charset=utf-8");
}).AllowAnonymous();

// ═══════════════════════════════════════════════════════════════════════
//  El mapa del sitio para los buscadores.
//
//  Antes era un archivo fijo en wwwroot con dos direcciones. Ahora se arma solo,
//  porque las excursiones se dan de alta y de baja desde el panel: si quedara fijo,
//  cada excursión nueva sería invisible para Google hasta que alguien se acordara
//  de editar el XML a mano. El archivo viejo se borró (si no, tapaba a este).
// ═══════════════════════════════════════════════════════════════════════
app.MapGet("/sitemap.xml", (AppDbContext db) =>
{
    const string baseUrl = "https://wamaniturismo.com";
    var hoy = DateTime.UtcNow.ToString("yyyy-MM-dd");
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
    sb.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">""");

    // Las dos páginas de siempre, cada una declarada en los tres idiomas.
    foreach (var (ruta, freq, prio) in new[] { ("/", "weekly", "1.0"), ("/receptivo/", "monthly", "0.8") })
    {
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{baseUrl}{ruta}</loc>");
        foreach (var (idioma, sufijo) in new[] { ("es", ""), ("en", "?lang=en"), ("fr", "?lang=fr"), ("x-default", "") })
            sb.AppendLine($"""    <xhtml:link rel="alternate" hreflang="{idioma}" href="{baseUrl}{ruta}{sufijo}"/>""");
        sb.AppendLine($"    <changefreq>{freq}</changefreq>");
        sb.AppendLine($"    <priority>{prio}</priority>");
        sb.AppendLine("  </url>");
    }

    // Una entrada por excursión activa.
    foreach (var clave in db.ExcursionesWeb.Where(x => x.Activa).OrderBy(x => x.Orden)
                            .Select(x => x.Clave).ToList())
    {
        if (string.IsNullOrWhiteSpace(clave)) continue;
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{baseUrl}/excursiones/{Uri.EscapeDataString(clave)}</loc>");
        sb.AppendLine($"    <lastmod>{hoy}</lastmod>");
        sb.AppendLine("    <changefreq>monthly</changefreq>");
        sb.AppendLine("    <priority>0.9</priority>");
        sb.AppendLine("  </url>");
    }

    sb.AppendLine("</urlset>");
    return Results.Content(sb.ToString(), "application/xml; charset=utf-8");
}).AllowAnonymous();

// ═══════════════════════════════════════════════════════════════════════
//  El formulario de contacto de la web
//
//  La consulta se GUARDA siempre en el sistema (así no se pierde ninguna) y además se
//  manda un aviso por mail. Si el mail falla —o todavía no se configuró— la consulta
//  queda igual: nunca se pierde por un problema de correo.
// ═══════════════════════════════════════════════════════════════════════
app.MapPost("/web/consulta", async (HttpRequest req, AppDbContext db, IWebHostEnvironment env) =>
{
  try
  {
    var f = await req.ReadFormAsync();
    string Campo(string n, int tope) => ((string?)f[n] ?? "").Trim() is var v && v.Length > tope
        ? v.Substring(0, tope) : ((string?)f[n] ?? "").Trim();

    // Trampa para robots: es un campo escondido que una persona nunca ve ni completa.
    // Si viene lleno, es spam: se contesta que salió bien y no se guarda nada.
    if (!string.IsNullOrWhiteSpace((string?)f["apellido2"]))
        return Results.Json(new { ok = true });

    var nombre = Campo("nombre", 160);
    var mensaje = Campo("mensaje", 4000);
    if (nombre.Length < 2 || mensaje.Length < 5)
        return Results.Json(new { ok = false, error = "Faltan el nombre o el mensaje." });

    var email = Campo("email", 160);
    var tipo = Campo("tipo", 20) == "Agencia" ? "Agencia" : "Viajero";

    var consulta = new Wamani.Reservas.Models.ConsultaWeb
    {
        Nombre = nombre,
        Email = string.IsNullOrWhiteSpace(email) ? null : email,
        Telefono = Campo("telefono", 60) is var t && t.Length > 0 ? t : null,
        Tipo = tipo,
        Mensaje = mensaje,
        Origen = Campo("origen", 60) is var o && o.Length > 0 ? o : null,
        CreadaEl = Wamani.Reservas.Services.Reloj.AhoraJujuy()
    };

    try
    {
        db.ConsultasWeb.Add(consulta);
        await db.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        // Si no se puede guardar, se deja escrito en los registros de Render y se le avisa
        // a quien consultó, en vez de mostrarle una pantalla de error.
        Console.WriteLine("[consulta] NO se pudo guardar: " + ex.GetBaseException().Message);
        return Results.Json(new { ok = false, error = "No se pudo guardar la consulta." });
    }

    // El aviso por mail va después de guardar, y si falla no se le avisa a quien consultó:
    // su mensaje ya está a salvo.
    var cuerpo =
        (tipo == "Agencia" ? "CONSULTA DE UNA AGENCIA" : "Consulta de un viajero") + "\n\n" +
        "Nombre: " + consulta.Nombre + "\n" +
        "Mail: " + (consulta.Email ?? "-") + "\n" +
        "Teléfono: " + (consulta.Telefono ?? "-") + "\n" +
        "Vino de: " + (consulta.Origen ?? "la web") + "\n\n" +
        "Mensaje:\n" + consulta.Mensaje + "\n\n" +
        "— Se puede responder directamente a este mail.";

    // Si la persona subió un archivo (por ejemplo, una agencia con su propuesta en PDF):
    // queda GUARDADO en el sistema —se baja después desde la pantalla de Consultas— y
    // además va pegado al mail de aviso, para tenerlo a mano sin entrar al panel.
    (string, byte[], string?)? adjunto = null;
    var archivo = f.Files["archivo"];
    if (archivo is not null && archivo.Length > 0)
    {
        const long TOPE = 10 * 1024 * 1024; // 10 MB: Gmail no acepta mucho más
        var ext = System.IO.Path.GetExtension(archivo.FileName ?? "").ToLowerInvariant();
        // Lista de lo que SÍ se acepta: nada que se pueda ejecutar
        var permitidas = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                                 ".jpg", ".jpeg", ".png", ".webp", ".txt", ".csv", ".zip" };

        if (archivo.Length > TOPE)
            cuerpo += "\n\n⚠️ Adjuntó un archivo de " + (archivo.Length / 1024 / 1024) +
                      " MB y no entró (el tope son 10 MB). Conviene pedírselo.";
        else if (!permitidas.Contains(ext))
            cuerpo += "\n\n⚠️ Intentó adjuntar un archivo \"" + ext + "\", que no se acepta.";
        else
        {
            using var ms = new MemoryStream();
            await archivo.CopyToAsync(ms);
            var datos = ms.ToArray();
            var limpio = System.IO.Path.GetFileName(archivo.FileName ?? "adjunto");
            adjunto = (limpio, datos, archivo.ContentType);

            // Guardarlo en el disco. El nombre lo pone el sistema (el Id de la consulta
            // más la extensión): así dos archivos que se llamen igual no se pisan y el
            // nombre que escribió la persona nunca toca el disco.
            // Si esto falla, la consulta y el mail salen igual: no se pierde nada.
            try
            {
                var guardado = consulta.Id + ext;
                await System.IO.File.WriteAllBytesAsync(
                    System.IO.Path.Combine(Wamani.Reservas.Services.AdjuntosConsulta.Carpeta(env), guardado), datos);
                consulta.ArchivoNombre = limpio.Length > 260 ? limpio.Substring(0, 260) : limpio;
                consulta.ArchivoGuardado = guardado;
                await db.SaveChangesAsync();
                cuerpo += "\n\n📎 Adjuntó el archivo: " + limpio + " (también queda guardado en el sistema).";
            }
            catch (Exception ex)
            {
                Console.WriteLine("[consulta] no se pudo guardar el adjunto: " + ex.GetBaseException().Message);
                cuerpo += "\n\n📎 Adjuntó el archivo: " + limpio +
                          " (no se pudo guardar en el sistema, así que está SÓLO en este mail).";
            }
        }
    }

    await Wamani.Reservas.Services.Correo.EnviarAsync(
        (tipo == "Agencia" ? "🤝 Consulta de agencia — " : "🎒 Consulta web — ") + consulta.Nombre,
        cuerpo, consulta.Email, adjunto);

    return Results.Json(new { ok = true });
  }
  catch (Exception ex)
  {
    // Pase lo que pase, al visitante NUNCA se le muestra una pantalla de error: se le dice
    // que escriba por WhatsApp. El motivo queda escrito en los registros de Render.
    Console.WriteLine("[consulta] FALLO: " + ex.GetBaseException().Message);
    return Results.Json(new { ok = false, error = "No se pudo enviar la consulta." });
  }
}).AllowAnonymous().DisableAntiforgery();

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

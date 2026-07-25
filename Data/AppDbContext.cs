using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Data
{
    // Es la "puerta" a la base de datos. Guarda y trae las reservas y excursiones.
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Reserva> Reservas => Set<Reserva>();
        public DbSet<Excursion> Excursiones => Set<Excursion>();
        public DbSet<GastoExcursion> GastosExcursion => Set<GastoExcursion>();
        public DbSet<OperativoGasto> OperativoGastos => Set<OperativoGasto>();
        public DbSet<OperativoSalida> OperativoSalidas => Set<OperativoSalida>();
        public DbSet<Proveedor> Proveedores => Set<Proveedor>();
        public DbSet<OperativoProveedor> OperativoProveedores => Set<OperativoProveedor>();
        public DbSet<Pasajero> Pasajeros => Set<Pasajero>();
        public DbSet<Interesado> Interesados => Set<Interesado>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<GastoEmpresa> GastosEmpresa => Set<GastoEmpresa>();
        public DbSet<Retiro> Retiros => Set<Retiro>();
        public DbSet<Aporte> Aportes => Set<Aporte>();

        // --- Contenido de la LANDING (página pública), editable desde "🌐 Web" ---
        public DbSet<ContenidoWeb> ContenidoWeb => Set<ContenidoWeb>();
        public DbSet<ExcursionWeb> ExcursionesWeb => Set<ExcursionWeb>();
        public DbSet<TestimonioWeb> TestimoniosWeb => Set<TestimonioWeb>();
        public DbSet<IntegranteWeb> IntegrantesWeb => Set<IntegranteWeb>();
        public DbSet<VideoWeb> VideosWeb => Set<VideoWeb>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // En Postgres (internet) las fechas se guardan SIN zona horaria.
            //
            // Las fechas de este sistema son fechas de agenda: "la excursión sale el 20/07"
            // es el 20/07 en Jujuy, no un momento exacto del planeta. Si no se aclara esto,
            // Postgres las trata como "con zona horaria" y exige que se le manden en horario
            // UTC, lo que rompe todas las consultas que filtran por fecha (Salidas, Operativo)
            // y además podría correr una salida un día para atrás o para adelante.
            //
            // Solo aplica a Postgres: en SQLite (tu compu) no cambia nada.
            if (Database.IsNpgsql())
            {
                foreach (var entidad in modelBuilder.Model.GetEntityTypes())
                    foreach (var propiedad in entidad.GetProperties())
                        if (propiedad.ClrType == typeof(DateTime) || propiedad.ClrType == typeof(DateTime?))
                            propiedad.SetColumnType("timestamp without time zone");
            }
        }
    }
}

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
    }
}

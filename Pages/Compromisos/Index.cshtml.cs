using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Compromisos;

// COMPROMISOS: la plata que todavía está "en el aire" de las salidas ya vendidas.
//   Por cobrar  = saldos de reservas señadas (normalmente el 50% restante).
//   Por pagar   = lo que falta pagarle a los proveedores ya reservados (total − seña − saldo).
//   Proyectado  = Caja de hoy + por cobrar − por pagar.
//
// A propósito NO se toman los gastos del operativo (nafta, entradas, comidas…): esos se
// van cargando a medida que transcurre cada salida, así que meterlos acá sería contar
// una estimación como si fuera un compromiso firme.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    // Una reserva a la que le falta cobrar plata
    public class CobroPendiente
    {
        public string Excursion { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Cliente { get; set; } = "";
        public int Personas { get; set; }
        public decimal Total { get; set; }
        public decimal Cobrado { get; set; }
        public decimal Falta { get; set; }
        public bool YaPaso { get; set; }
    }

    // Un proveedor ya reservado al que le falta pagarle
    public class PagoPendiente
    {
        public string Excursion { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; } = "";
        public string Proveedor { get; set; } = "";
        public decimal Total { get; set; }
        public decimal Pagado { get; set; }
        public decimal Falta { get; set; }
        public bool YaPaso { get; set; }
    }

    public List<CobroPendiente> Cobros { get; set; } = new();
    public List<PagoPendiente> Pagos { get; set; } = new();

    public decimal CajaHoy { get; set; }
    public decimal PorCobrar => Cobros.Sum(c => c.Falta);
    public decimal PorPagar => Pagos.Sum(p => p.Falta);
    public decimal Proyectado => CajaHoy + PorCobrar - PorPagar;

    // Cuánto de lo pendiente corresponde a salidas que YA pasaron (hay que reclamarlo/saldarlo ya)
    public decimal CobrarVencido => Cobros.Where(c => c.YaPaso).Sum(c => c.Falta);
    public decimal PagarVencido => Pagos.Where(p => p.YaPaso).Sum(p => p.Falta);

    public async Task OnGetAsync()
    {
        var hoy = DateTime.Today;

        // ---- Caja de hoy: misma cuenta que la pantalla Caja (sólo plata que ya se movió) ----
        var reservas = await _db.Reservas.ToListAsync();
        var ingresos = reservas.Sum(r => (r.SenaMonto ?? 0) + (r.SaldoMonto ?? 0));

        var egGastos = (await _db.OperativoGastos.ToListAsync())
            .Where(o => o.FechaPago != null)
            .Sum(o => o.Precio);
        var provs = await _db.OperativoProveedores.ToListAsync();
        var egProv = provs.Sum(p => (p.FechaSena != null ? p.Sena : 0) + (p.FechaSaldo != null ? p.Saldo : 0));
        var egEmpresa = (await _db.GastosEmpresa.ToListAsync()).Sum(g => g.Monto);

        var aportes = (await _db.Aportes.ToListAsync()).Sum(a => a.Monto);
        var retiros = (await _db.Retiros.ToListAsync()).Sum(r => r.Monto);

        CajaHoy = ingresos - (egGastos + egProv + egEmpresa) + aportes - retiros;

        // ---- Por cobrar: saldos de reservas ----
        // Las "Reservas Históricas" son registros viejos sin plata cargada: no son una deuda.
        Cobros = reservas
            .Where(r => r.NombreCliente != Reserva.NombreHistorica && r.Pendiente() > 0)
            .Select(r => new CobroPendiente
            {
                Excursion = string.IsNullOrWhiteSpace(r.Excursion) ? "Excursión" : r.Excursion,
                Fecha = r.FechaDesde.Date,
                Cliente = r.NombreCliente,
                Personas = r.CantidadPersonas,
                Total = r.TotalConDescuento(),
                Cobrado = r.Cobrado(),
                Falta = r.Pendiente(),
                YaPaso = r.FechaDesde.Date < hoy
            })
            .OrderBy(c => c.Fecha).ThenBy(c => c.Cliente)
            .ToList();

        // ---- Por pagar: lo que falta de cada proveedor ya reservado ----
        var excNombres = await _db.Excursiones.ToDictionaryAsync(e => e.Id, e => e.Nombre);

        Pagos = provs
            .Where(p => p.TieneDeuda())
            .Select(p => new PagoPendiente
            {
                Excursion = excNombres.TryGetValue(p.ExcursionId, out var n) ? n : "Excursión",
                Fecha = p.Fecha.Date,
                Tipo = p.Tipo,
                Proveedor = string.IsNullOrWhiteSpace(p.ProveedorNombre) ? "— sin asignar —" : p.ProveedorNombre,
                Total = p.Total,
                Pagado = p.Pagado(),
                Falta = p.Pendiente(),
                YaPaso = p.Fecha.Date < hoy
            })
            .OrderBy(p => p.Fecha).ThenBy(p => p.Tipo)
            .ToList();
    }
}

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Compromisos;

// COMPROMISOS: cuánta plata NETA queda de las salidas que ya tienen reservas.
//
//   Caja hoy       = lo ya cobrado menos lo ya pagado.
//   + Por cobrar   = saldos de reservas señadas (normalmente el 50% restante).
//   − Por pagar    = gastos del operativo sin pagar + deuda con proveedores.
//   = Proyectado   = con lo que se va a contar cuando termine de cobrarse y pagarse todo.
//
// Los gastos del operativo se muestran APARTE de la deuda con proveedores porque son
// cosas distintas: el proveedor ya tiene lugar tomado y un precio acordado (deuda firme),
// mientras que los gastos salen de la plantilla de la excursión y son una estimación
// hasta que se cargan de verdad. Sumarlos en un solo número los haría parecer lo mismo.
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
        public string? Lugar { get; set; }
        public decimal Total { get; set; }
        public decimal Pagado { get; set; }
        public decimal Falta { get; set; }
        public bool YaPaso { get; set; }
    }

    // Los gastos del operativo que faltan pagar, agrupados por salida
    public class GastoPendiente
    {
        public string Excursion { get; set; } = "";
        public DateTime Fecha { get; set; }
        public int Items { get; set; }
        public decimal Falta { get; set; }
        public bool YaPaso { get; set; }
    }

    public List<CobroPendiente> Cobros { get; set; } = new();
    public List<PagoPendiente> Pagos { get; set; } = new();
    public List<GastoPendiente> Gastos { get; set; } = new();

    public decimal CajaHoy { get; set; }
    public decimal PorCobrar => Cobros.Sum(c => c.Falta);
    public decimal PorPagarProveedores => Pagos.Sum(p => p.Falta);
    public decimal PorPagarGastos => Gastos.Sum(g => g.Falta);
    public decimal PorPagar => PorPagarProveedores + PorPagarGastos;
    public decimal Proyectado => CajaHoy + PorCobrar - PorPagar;

    // Cuánto de lo pendiente corresponde a salidas que YA pasaron (hay que resolverlo ya)
    public decimal CobrarVencido => Cobros.Where(c => c.YaPaso).Sum(c => c.Falta);
    public decimal PagarVencido => Pagos.Where(p => p.YaPaso).Sum(p => p.Falta)
                                 + Gastos.Where(g => g.YaPaso).Sum(g => g.Falta);

    public async Task OnGetAsync()
    {
        var hoy = DateTime.Today;
        var excNombres = await _db.Excursiones.ToDictionaryAsync(e => e.Id, e => e.Nombre);
        string NombreExc(int id) => excNombres.TryGetValue(id, out var n) ? n : "Excursión";

        // ---- Caja de hoy: misma cuenta que la pantalla Caja (sólo plata que ya se movió) ----
        var reservas = await _db.Reservas.ToListAsync();
        var ingresos = reservas.Sum(r => (r.SenaMonto ?? 0) + (r.SaldoMonto ?? 0))
                     + (await _db.IngresosExtra.ToListAsync()).Sum(e => e.Monto);

        var todosGastos = await _db.OperativoGastos.ToListAsync();
        var egGastos = todosGastos.Where(o => o.FechaPago != null).Sum(o => o.Precio);

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

        // ---- Por pagar (1): deuda con proveedores ya reservados ----
        Pagos = provs
            .Where(p => p.TieneDeuda())
            .Select(p => new PagoPendiente
            {
                Excursion = NombreExc(p.ExcursionId),
                Fecha = p.Fecha.Date,
                Tipo = p.Tipo,
                Proveedor = string.IsNullOrWhiteSpace(p.ProveedorNombre) ? "— sin asignar —" : p.ProveedorNombre,
                Lugar = p.Lugar,
                Total = p.Total,
                Pagado = p.Pagado(),
                Falta = p.Pendiente(),
                YaPaso = p.Fecha.Date < hoy
            })
            .OrderBy(p => p.Fecha).ThenBy(p => p.Tipo)
            .ToList();

        // ---- Por pagar (2): gastos del operativo todavía sin pagar ----
        // Sin fecha de pago = sin tildar = todavía no salió esa plata. Se agrupan por salida
        // para que se lea de un vistazo; el detalle ítem por ítem está en el operativo.
        Gastos = todosGastos
            .Where(o => o.FechaPago == null && o.Precio > 0)
            .GroupBy(o => new { o.ExcursionId, Fecha = o.Fecha.Date })
            .Select(g => new GastoPendiente
            {
                Excursion = NombreExc(g.Key.ExcursionId),
                Fecha = g.Key.Fecha,
                Items = g.Count(),
                Falta = g.Sum(o => o.Precio),
                YaPaso = g.Key.Fecha < hoy
            })
            .OrderBy(g => g.Fecha)
            .ToList();
    }
}

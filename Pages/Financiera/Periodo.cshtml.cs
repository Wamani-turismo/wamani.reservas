using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Financiera;

// Movimiento de plata entre DOS FECHAS cualquiera (un día, una semana, lo que se elija).
// Es la misma cuenta que la Financiera mensual, sólo que el período lo elige el usuario:
//   Ingresos = señas y saldos COBRADOS en el período (por su fecha de cobro).
//   Egresos  = gastos del operativo y pagos a proveedores HECHOS en el período.
//   Gastos   = gastos generales de la empresa cargados en el período.
// No toca ni cambia nada de la vista mensual ni de la anual.
public class PeriodoModel : PageModel
{
    private readonly AppDbContext _db;
    public PeriodoModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public DateTime? Desde { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? Hasta { get; set; }

    public DateTime DesdeReal { get; set; }
    public DateTime HastaReal { get; set; }
    public int CantidadDias => (int)(HastaReal - DesdeReal).TotalDays + 1;

    // Un movimiento de plata suelto (sirve para ingresos y para egresos)
    public class Movimiento
    {
        public DateTime Fecha { get; set; }
        public string Concepto { get; set; } = "";
        public string Detalle { get; set; } = "";
        public decimal Monto { get; set; }
    }

    // Resumen de un día del período
    public class Dia
    {
        public DateTime Fecha { get; set; }
        public int ReservasNuevas { get; set; }
        public decimal Ingresos { get; set; }
        public decimal Egresos { get; set; }
        public decimal GastosEmpresa { get; set; }
        public decimal Neto => Ingresos - Egresos - GastosEmpresa;
        public bool TuvoMovimiento => ReservasNuevas > 0 || Ingresos != 0 || Egresos != 0 || GastosEmpresa != 0;
    }

    // Una reserva cargada dentro del período
    public class ReservaNueva
    {
        public DateTime Cargada { get; set; }
        public string Cliente { get; set; } = "";
        public string Excursion { get; set; } = "";
        public DateTime Sale { get; set; }
        public int Personas { get; set; }
        public decimal Total { get; set; }
        public decimal Cobrado { get; set; }
    }

    public List<Dia> Dias { get; set; } = new();
    public List<ReservaNueva> Reservas { get; set; } = new();
    public List<Movimiento> Ingresos { get; set; } = new();
    public List<Movimiento> Egresos { get; set; } = new();
    public List<GastoEmpresa> GastosEmpresaLista { get; set; } = new();

    public decimal TotalIngresos => Ingresos.Sum(m => m.Monto);
    public decimal TotalEgresos => Egresos.Sum(m => m.Monto);
    public decimal TotalGastosEmpresa => GastosEmpresaLista.Sum(g => g.Monto);
    public decimal Neto => TotalIngresos - TotalEgresos - TotalGastosEmpresa;

    public int CantidadReservas => Reservas.Count;
    public int CantidadPersonas => Reservas.Sum(r => r.Personas);
    public decimal TotalVendido => Reservas.Sum(r => r.Total);

    public async Task OnGetAsync()
    {
        // Por defecto: los últimos 7 días (incluido hoy)
        var hoy = DateTime.Today;
        DesdeReal = (Desde ?? hoy.AddDays(-6)).Date;
        HastaReal = (Hasta ?? hoy).Date;
        if (HastaReal < DesdeReal) (DesdeReal, HastaReal) = (HastaReal, DesdeReal);

        var fin = HastaReal.AddDays(1);   // exclusivo, para comparar por día completo
        bool EnRango(DateTime? f) => f is DateTime d && d.Date >= DesdeReal && d.Date < fin;

        var excNombres = await _db.Excursiones.ToDictionaryAsync(e => e.Id, e => e.Nombre);
        string NombreExc(int id) => excNombres.TryGetValue(id, out var n) ? n : "Excursión";

        var reservas = await _db.Reservas.ToListAsync();

        // ---- INGRESOS: señas y saldos cobrados dentro del período ----
        foreach (var r in reservas)
        {
            if (EnRango(r.SenaFecha) && (r.SenaMonto ?? 0) != 0)
                Ingresos.Add(new Movimiento
                {
                    Fecha = r.SenaFecha!.Value.Date,
                    Concepto = "Seña",
                    Detalle = $"{r.NombreCliente} · {r.Excursion}",
                    Monto = r.SenaMonto!.Value
                });

            if (EnRango(r.SaldoFecha) && (r.SaldoMonto ?? 0) != 0)
                Ingresos.Add(new Movimiento
                {
                    Fecha = r.SaldoFecha!.Value.Date,
                    Concepto = "Saldo",
                    Detalle = $"{r.NombreCliente} · {r.Excursion}",
                    Monto = r.SaldoMonto!.Value
                });
        }
        // Ingresos extra (comisiones, alquileres, servicios sueltos)
        foreach (var e in await _db.IngresosExtra.ToListAsync())
        {
            if (e.Fecha.Date >= DesdeReal && e.Fecha.Date < fin && e.Monto != 0)
                Ingresos.Add(new Movimiento
                {
                    Fecha = e.Fecha.Date,
                    Concepto = "Extra · " + e.Motivo,
                    Detalle = string.IsNullOrWhiteSpace(e.DeQuien) ? e.Descripcion : $"{e.Descripcion} · {e.DeQuien}",
                    Monto = e.Monto
                });
        }

        Ingresos = Ingresos.OrderBy(m => m.Fecha).ThenBy(m => m.Detalle).ToList();

        // ---- EGRESOS: gastos del operativo pagados + pagos a proveedores ----
        foreach (var o in await _db.OperativoGastos.ToListAsync())
        {
            if (EnRango(o.FechaPago) && o.Precio != 0)
                Egresos.Add(new Movimiento
                {
                    Fecha = o.FechaPago!.Value.Date,
                    Concepto = o.Nombre,
                    Detalle = NombreExc(o.ExcursionId),
                    Monto = o.Precio
                });
        }

        foreach (var p in await _db.OperativoProveedores.ToListAsync())
        {
            var quien = string.IsNullOrWhiteSpace(p.ProveedorNombre) ? p.Tipo : $"{p.Tipo}: {p.ProveedorNombre}";
            if (EnRango(p.FechaSena) && p.Sena != 0)
                Egresos.Add(new Movimiento
                {
                    Fecha = p.FechaSena!.Value.Date,
                    Concepto = $"{quien} (seña)",
                    Detalle = NombreExc(p.ExcursionId),
                    Monto = p.Sena
                });
            if (EnRango(p.FechaSaldo) && p.Saldo != 0)
                Egresos.Add(new Movimiento
                {
                    Fecha = p.FechaSaldo!.Value.Date,
                    Concepto = $"{quien} (saldo)",
                    Detalle = NombreExc(p.ExcursionId),
                    Monto = p.Saldo
                });
        }
        Egresos = Egresos.OrderBy(m => m.Fecha).ThenBy(m => m.Concepto).ToList();

        // ---- GASTOS GENERALES DE LA EMPRESA del período ----
        GastosEmpresaLista = (await _db.GastosEmpresa.ToListAsync())
            .Where(g => g.Fecha.Date >= DesdeReal && g.Fecha.Date < fin)
            .OrderBy(g => g.Fecha)
            .ToList();

        // ---- RESERVAS CARGADAS dentro del período (cuándo entró la reserva) ----
        Reservas = reservas
            .Where(r => r.NombreCliente != Reserva.NombreHistorica
                     && r.CreadaEl.Date >= DesdeReal && r.CreadaEl.Date < fin)
            .Select(r => new ReservaNueva
            {
                Cargada = r.CreadaEl.Date,
                Cliente = r.NombreCliente,
                Excursion = string.IsNullOrWhiteSpace(r.Excursion) ? "Excursión" : r.Excursion,
                Sale = r.FechaDesde.Date,
                Personas = r.CantidadPersonas,
                Total = r.TotalConDescuento(),
                Cobrado = r.Cobrado()
            })
            .OrderBy(r => r.Cargada).ThenBy(r => r.Cliente)
            .ToList();

        // ---- Resumen día por día ----
        // Si el período es largo se listan sólo los días que tuvieron movimiento; si es
        // corto (hasta 31 días) se muestran todos, para ver también los días en cero.
        var ingXDia = Ingresos.GroupBy(m => m.Fecha).ToDictionary(g => g.Key, g => g.Sum(x => x.Monto));
        var egrXDia = Egresos.GroupBy(m => m.Fecha).ToDictionary(g => g.Key, g => g.Sum(x => x.Monto));
        var gasXDia = GastosEmpresaLista.GroupBy(g => g.Fecha.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.Monto));
        var resXDia = Reservas.GroupBy(r => r.Cargada).ToDictionary(g => g.Key, g => g.Count());

        var todos = new List<Dia>();
        for (var d = DesdeReal; d < fin; d = d.AddDays(1))
        {
            todos.Add(new Dia
            {
                Fecha = d,
                ReservasNuevas = resXDia.GetValueOrDefault(d),
                Ingresos = ingXDia.GetValueOrDefault(d),
                Egresos = egrXDia.GetValueOrDefault(d),
                GastosEmpresa = gasXDia.GetValueOrDefault(d)
            });
        }
        Dias = CantidadDias <= 31 ? todos : todos.Where(x => x.TuvoMovimiento).ToList();
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Financiera;

public class AnualModel : PageModel
{
    private readonly AppDbContext _db;
    public AnualModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public int Anio { get; set; }

    public class LineaMes
    {
        public int Mes { get; set; }
        public string Nombre { get; set; } = "";
        public int Reservas { get; set; }
        public int Personas { get; set; }
        public decimal Ingreso { get; set; }
        public decimal Gastos { get; set; }
        public decimal GastosEmpresa { get; set; }
        public decimal Neta => Ingreso - Gastos - GastosEmpresa;
    }

    public class LineaExc
    {
        public string Excursion { get; set; } = "";
        public int Reservas { get; set; }
        public int Personas { get; set; }
        public decimal Ingreso { get; set; }
        public decimal Gastos { get; set; }
        public decimal Neta => Ingreso - Gastos;
    }

    public List<LineaMes> Meses { get; set; } = new();
    public List<LineaExc> Ranking { get; set; } = new();

    public decimal Ingreso { get; set; }
    public decimal Gastos { get; set; }                 // egresos de excursiones (operativo)
    public decimal GastosEmpresaTotal { get; set; }     // gastos generales de la empresa del año
    public decimal Neta => Ingreso - Gastos - GastosEmpresaTotal;
    public decimal PorDueno => Math.Round(Neta / 3m, 2);
    public int TotalReservas { get; set; }
    public int TotalPersonas { get; set; }

    // Comparación con el año anterior
    public bool HayAnioPrevio { get; set; }
    public decimal IngresoPrev { get; set; }
    public decimal GastosPrev { get; set; }
    public decimal GastosEmpresaPrev { get; set; }
    public decimal NetaPrev => IngresoPrev - GastosPrev - GastosEmpresaPrev;
    public int ReservasPrev { get; set; }

    public decimal VarNetaPct => NetaPrev == 0 ? 0 : Math.Round((Neta - NetaPrev) / Math.Abs(NetaPrev) * 100, 1);
    public decimal VarIngresoPct => IngresoPrev == 0 ? 0 : Math.Round((Ingreso - IngresoPrev) / Math.Abs(IngresoPrev) * 100, 1);

    public async Task OnGetAsync()
    {
        if (Anio == 0) Anio = DateTime.Today.Year;
        var ci = new System.Globalization.CultureInfo("es-AR");

        var reservas = await _db.Reservas.ToListAsync();
        var ops = await _db.OperativoGastos.ToListAsync();
        var provs = await _db.OperativoProveedores.ToListAsync();
        var gastosEmp = await _db.GastosEmpresa.ToListAsync();
        var extras = await _db.IngresosExtra.ToListAsync();
        var excNombres = await _db.Excursiones.ToDictionaryAsync(e => e.Id, e => e.Nombre);

        // Gastos generales de la empresa (publicidad, botiquín, etc.) por fecha
        decimal GastoEmpresaDe(int anio, int? mes = null)
            => gastosEmp.Where(g => g.Fecha.Year == anio && (mes == null || g.Fecha.Month == mes))
                        .Sum(g => g.Monto);

        // Plata que entró / salió, por fecha de pago
        decimal IngresoDe(int anio, int? mes = null)
        {
            bool Ok(DateTime? f) => f is DateTime d && d.Year == anio && (mes == null || d.Month == mes);
            // Lo cobrado por reservas + los ingresos extra (comisiones, alquileres, etc.)
            return reservas.Sum(r => (Ok(r.SenaFecha) ? (r.SenaMonto ?? 0) : 0)
                                   + (Ok(r.SaldoFecha) ? (r.SaldoMonto ?? 0) : 0))
                 + extras.Sum(e => Ok(e.Fecha) ? e.Monto : 0);
        }
        decimal GastoDe(int anio, int? mes = null)
        {
            bool Ok(DateTime? f) => f is DateTime d && d.Year == anio && (mes == null || d.Month == mes);
            return ops.Sum(o => Ok(o.FechaPago) ? o.Precio : 0)
                 + provs.Sum(p => (Ok(p.FechaSena) ? p.Sena : 0) + (Ok(p.FechaSaldo) ? p.Saldo : 0));
        }

        var resAnio = reservas.Where(r => r.FechaDesde.Year == Anio).ToList();

        for (int m = 1; m <= 12; m++)
        {
            var resM = resAnio.Where(r => r.FechaDesde.Month == m).ToList();
            Meses.Add(new LineaMes
            {
                Mes = m,
                Nombre = new DateTime(Anio, m, 1).ToString("MMMM", ci),
                Reservas = resM.Count,
                Personas = resM.Sum(r => r.CantidadPersonas),
                Ingreso = IngresoDe(Anio, m),
                Gastos = GastoDe(Anio, m),
                GastosEmpresa = GastoEmpresaDe(Anio, m)
            });
        }

        Ingreso = Meses.Sum(x => x.Ingreso);
        Gastos = Meses.Sum(x => x.Gastos);
        GastosEmpresaTotal = Meses.Sum(x => x.GastosEmpresa);
        TotalReservas = resAnio.Count;
        TotalPersonas = resAnio.Sum(r => r.CantidadPersonas);

        // ---- Ranking de excursiones (más vendida → menos vendida) ----
        bool EnAnio(DateTime? f) => f is DateTime d && d.Year == Anio;

        var ingresoPorExc = new Dictionary<int, decimal>();
        foreach (var r in reservas)
        {
            decimal entro = (EnAnio(r.SenaFecha) ? (r.SenaMonto ?? 0) : 0)
                          + (EnAnio(r.SaldoFecha) ? (r.SaldoMonto ?? 0) : 0);
            if (entro != 0)
            {
                var exId = r.ExcursionId ?? 0;
                ingresoPorExc[exId] = ingresoPorExc.GetValueOrDefault(exId) + entro;
            }
        }
        var gastoPorExc = new Dictionary<int, decimal>();
        foreach (var o in ops)
            if (EnAnio(o.FechaPago) && o.Precio != 0)
                gastoPorExc[o.ExcursionId] = gastoPorExc.GetValueOrDefault(o.ExcursionId) + o.Precio;
        foreach (var p in provs)
        {
            decimal salio = (EnAnio(p.FechaSena) ? p.Sena : 0) + (EnAnio(p.FechaSaldo) ? p.Saldo : 0);
            if (salio != 0)
                gastoPorExc[p.ExcursionId] = gastoPorExc.GetValueOrDefault(p.ExcursionId) + salio;
        }

        var porExc = resAnio.GroupBy(r => r.ExcursionId ?? 0)
            .ToDictionary(g => g.Key, g => (Cant: g.Count(), Pers: g.Sum(x => x.CantidadPersonas)));

        var ids = ingresoPorExc.Keys.Concat(gastoPorExc.Keys).Concat(porExc.Keys).Distinct();

        Ranking = ids.Select(exId => new LineaExc
        {
            Excursion = excNombres.TryGetValue(exId, out var n) ? n : "Excursión",
            Reservas = porExc.TryGetValue(exId, out var rr) ? rr.Cant : 0,
            Personas = porExc.TryGetValue(exId, out var pp) ? pp.Pers : 0,
            Ingreso = ingresoPorExc.GetValueOrDefault(exId),
            Gastos = gastoPorExc.GetValueOrDefault(exId)
        })
        .OrderByDescending(l => l.Reservas)
        .ThenByDescending(l => l.Personas)
        .ToList();

        // ---- Año anterior (para comparar) ----
        var resPrev = reservas.Where(r => r.FechaDesde.Year == Anio - 1).ToList();
        ReservasPrev = resPrev.Count;
        IngresoPrev = IngresoDe(Anio - 1);
        GastosPrev = GastoDe(Anio - 1);
        GastosEmpresaPrev = GastoEmpresaDe(Anio - 1);
        HayAnioPrevio = resPrev.Count > 0 || IngresoPrev != 0 || GastosPrev != 0;
    }
}

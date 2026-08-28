using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Financiera;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    // Los 3 dueños (reparto en partes iguales)
    public static readonly string[] Duenos = { "Lautaro", "Facundo", "Luciano" };

    [BindProperty(SupportsGet = true)]
    public string? Mes { get; set; }   // formato "yyyy-MM"

    public DateTime MesActual { get; set; }
    public string MesTexto { get; set; } = "";

    public class LineaExcursion
    {
        public string Excursion { get; set; } = "";
        public int Reservas { get; set; }
        public int Personas { get; set; }
        public decimal Ingreso { get; set; }
        public decimal Gastos { get; set; }
        public decimal Neta => Ingreso - Gastos;
        // % de ganancia sobre el COSTO (cuánto se gana sobre lo gastado), como la planilla
        public decimal MargenPct => Gastos > 0 ? Math.Round(Neta / Gastos * 100, 0) : 0;
        // Salió gente pero no hubo plata: son las reservas viejas (históricas)
        public bool SinPlata => Reservas > 0 && Ingreso == 0 && Gastos == 0;
    }

    public class GastoTipo
    {
        public string Nombre { get; set; } = "";
        public decimal Total { get; set; }
        public List<(string Excursion, decimal Monto)> Detalle { get; set; } = new();
    }

    public List<LineaExcursion> Lineas { get; set; } = new();
    public List<GastoTipo> GastosPorTipo { get; set; } = new();

    public decimal Ingreso { get; set; }                         // cobrado por reservas
    public decimal Gastos { get; set; }                          // egresos de las excursiones
    public decimal GastosEmpresaTotal { get; set; }              // gastos generales de la empresa (publicidad, etc.)
    public List<GastoEmpresa> GastosEmpresaLista { get; set; } = new();

    // Ingresos EXTRA del mes (comisiones, alquileres, servicios sueltos): no son de
    // ninguna excursión, así que van aparte de la tabla por excursión pero suman al neto.
    public List<IngresoExtra> ExtrasLista { get; set; } = new();
    public decimal ExtrasTotal { get; set; }
    public decimal IngresoTotal => Ingreso + ExtrasTotal;

    // Fondo del 10%: lo que se aparta de la ganancia y se acumula mes a mes
    public Wamani.Reservas.Services.FondoReserva.Mes Fondo { get; set; } = new();

    // Los gastos pagados CON EL FONDO no restan de la ganancia del mes: esa plata ya se
    // había apartado de meses anteriores. Restan del saldo del fondo (y de la Caja).
    public decimal GastosEmpresaDelFondo { get; set; }
    public decimal GastosEmpresaPropios => GastosEmpresaTotal - GastosEmpresaDelFondo;

    // Ganancia del mes, antes de apartar el 10%
    public decimal Neta => IngresoTotal - Gastos - GastosEmpresaPropios;

    // El 10% que se aparta para el fondo (sólo si el mes dio ganancia)
    public decimal ParteFondo => Math.Round(Math.Max(0, Neta) * Wamani.Reservas.Services.FondoReserva.Porcentaje, 2);

    // Lo que queda para los socios, ya apartado el 10%
    public decimal GananciaARepartir => Neta - ParteFondo;
    public decimal PorDueno => Math.Round(GananciaARepartir / Duenos.Length, 2);

    // % de ganancia sobre TODO el costo (egresos de excursiones + gastos de empresa)
    public decimal MargenPct => (Gastos + GastosEmpresaPropios) > 0
        ? Math.Round(Neta / (Gastos + GastosEmpresaPropios) * 100, 0) : 0;
    public int TotalReservas { get; set; }
    public int TotalPersonas { get; set; }
    public int HistoricasDelMes { get; set; }   // reservas viejas (sin plata) que salieron este mes

    public async Task OnGetAsync()
    {
        var hoy = DateTime.Today;
        int anio = hoy.Year, mes = hoy.Month;
        if (!string.IsNullOrWhiteSpace(Mes) && DateTime.TryParse(Mes + "-01", out var parsed))
        {
            anio = parsed.Year; mes = parsed.Month;
        }
        MesActual = new DateTime(anio, mes, 1);
        var fin = MesActual.AddMonths(1);
        MesTexto = MesActual.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-AR"));

        bool EnMes(DateTime? f) => f is DateTime d && d.Date >= MesActual && d.Date < fin;

        var excNombres = await _db.Excursiones.ToDictionaryAsync(e => e.Id, e => e.Nombre);
        var reservas = await _db.Reservas.ToListAsync();
        var ops = await _db.OperativoGastos.ToListAsync();
        var provs = await _db.OperativoProveedores.ToListAsync();

        // ---- INGRESOS del mes: la plata que ENTRÓ este mes (por fecha de seña/saldo),
        //      sin importar cuándo sale la excursión ----
        var ingresoPorExc = new Dictionary<int, decimal>();
        foreach (var r in reservas)
        {
            var exId = r.ExcursionId ?? 0;
            decimal entro = 0;
            if (EnMes(r.SenaFecha)) entro += r.SenaMonto ?? 0;
            if (EnMes(r.SaldoFecha)) entro += r.SaldoMonto ?? 0;
            if (entro != 0)
                ingresoPorExc[exId] = ingresoPorExc.GetValueOrDefault(exId) + entro;
        }

        // ---- EGRESOS del mes: la plata que SALIÓ este mes (por fecha de pago) ----
        var gastoPorExc = new Dictionary<int, decimal>();
        foreach (var o in ops)
            if (EnMes(o.FechaPago) && o.Precio != 0)
                gastoPorExc[o.ExcursionId] = gastoPorExc.GetValueOrDefault(o.ExcursionId) + o.Precio;

        foreach (var p in provs)
        {
            decimal salio = 0;
            if (EnMes(p.FechaSena)) salio += p.Sena;
            if (EnMes(p.FechaSaldo)) salio += p.Saldo;
            if (salio != 0)
                gastoPorExc[p.ExcursionId] = gastoPorExc.GetValueOrDefault(p.ExcursionId) + salio;
        }

        // ---- Reservas del mes: las que MOVIERON plata este mes (cobraste seña o saldo)
        //      MÁS las que SALIERON este mes (aunque no tengan plata cargada, como las
        //      reservas históricas). Si una cumple las dos, se cuenta una sola vez. ----
        var reservasDelMes = reservas
            .Where(r => EnMes(r.SenaFecha) || EnMes(r.SaldoFecha)
                     || (r.FechaDesde >= MesActual && r.FechaDesde < fin))
            .ToList();
        TotalReservas = reservasDelMes.Count;
        TotalPersonas = reservasDelMes.Sum(r => r.CantidadPersonas);
        HistoricasDelMes = reservasDelMes.Count(r => r.NombreCliente == Reserva.NombreHistorica);
        var reservasPorExc = reservasDelMes.GroupBy(r => r.ExcursionId ?? 0)
            .ToDictionary(g => g.Key, g => (Cant: g.Count(), Pers: g.Sum(x => x.CantidadPersonas)));

        // ---- Una línea por excursión que tuvo movimiento o salidas este mes ----
        var ids = ingresoPorExc.Keys
            .Concat(gastoPorExc.Keys)
            .Concat(reservasPorExc.Keys)
            .Distinct();

        Lineas = ids.Select(exId => new LineaExcursion
        {
            Excursion = excNombres.TryGetValue(exId, out var n) ? n : "Excursión",
            Reservas = reservasPorExc.TryGetValue(exId, out var rr) ? rr.Cant : 0,
            Personas = reservasPorExc.TryGetValue(exId, out var pp) ? pp.Pers : 0,
            Ingreso = ingresoPorExc.GetValueOrDefault(exId),
            Gastos = gastoPorExc.GetValueOrDefault(exId)
        })
        .OrderByDescending(l => l.Neta)
        .ToList();

        Ingreso = Lineas.Sum(l => l.Ingreso);
        Gastos = Lineas.Sum(l => l.Gastos);

        // ---- Gastos generales de la empresa del mes (publicidad, botiquín, etc.) ----
        GastosEmpresaLista = await _db.GastosEmpresa
            .Where(g => g.Fecha >= MesActual && g.Fecha < fin)
            .OrderByDescending(g => g.Fecha)
            .ToListAsync();
        GastosEmpresaTotal = GastosEmpresaLista.Sum(g => g.Monto);
        GastosEmpresaDelFondo = GastosEmpresaLista.Where(g => g.DelFondo).Sum(g => g.Monto);

        // ---- Ingresos extra del mes (comisiones, alquileres, etc.) ----
        ExtrasLista = await _db.IngresosExtra
            .Where(e => e.Fecha >= MesActual && e.Fecha < fin)
            .OrderByDescending(e => e.Fecha)
            .ToListAsync();
        ExtrasTotal = ExtrasLista.Sum(e => e.Monto);

        // ---- Fondo del 10% acumulado hasta este mes ----
        Fondo = await Wamani.Reservas.Services.FondoReserva.CalcularAsync(_db, MesActual);

        // ---- Egresos por tipo (lo pagado este mes), con detalle por excursión ----
        var porGastos = ops
            .Where(o => EnMes(o.FechaPago) && o.Precio != 0)
            .GroupBy(o => o.Nombre.Trim().ToUpper())
            .Select(g => new GastoTipo
            {
                Nombre = g.First().Nombre.Trim(),
                Total = g.Sum(x => x.Precio),
                Detalle = g.GroupBy(x => x.ExcursionId)
                    .Select(gg => (
                        Excursion: excNombres.TryGetValue(gg.Key, out var n) ? n : "Excursión",
                        Monto: gg.Sum(x => x.Precio)))
                    .Where(d => d.Monto > 0)
                    .OrderByDescending(d => d.Monto)
                    .ToList()
            });

        var porProv = provs
            .Select(p => new
            {
                p.Tipo,
                p.ExcursionId,
                Monto = (EnMes(p.FechaSena) ? p.Sena : 0) + (EnMes(p.FechaSaldo) ? p.Saldo : 0)
            })
            .Where(x => x.Monto != 0)
            .GroupBy(x => x.Tipo)
            .Select(g => new GastoTipo
            {
                Nombre = g.Key,
                Total = g.Sum(x => x.Monto),
                Detalle = g.GroupBy(x => x.ExcursionId)
                    .Select(gg => (
                        Excursion: excNombres.TryGetValue(gg.Key, out var n) ? n : "Excursión",
                        Monto: gg.Sum(x => x.Monto)))
                    .Where(d => d.Monto > 0)
                    .OrderByDescending(d => d.Monto)
                    .ToList()
            });

        GastosPorTipo = porGastos.Concat(porProv)
            .Where(g => g.Total > 0)
            .OrderByDescending(g => g.Total)
            .ToList();
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Financiera;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public IndexModel(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

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
    }

    public class GastoTipo
    {
        public string Nombre { get; set; } = "";
        public decimal Total { get; set; }
        public List<(string Excursion, decimal Monto)> Detalle { get; set; } = new();
    }

    public List<LineaExcursion> Lineas { get; set; } = new();
    public List<GastoTipo> GastosPorTipo { get; set; } = new();

    public decimal Ingreso { get; set; }
    public decimal Gastos { get; set; }                          // egresos de las excursiones
    public decimal GastosEmpresaTotal { get; set; }              // gastos generales de la empresa (publicidad, etc.)
    public List<GastoEmpresa> GastosEmpresaLista { get; set; } = new();
    public decimal Neta => Ingreso - Gastos - GastosEmpresaTotal;
    public decimal PorDueno => Math.Round(Neta / Duenos.Length, 2);
    // % de ganancia sobre TODO el costo (egresos de excursiones + gastos de empresa)
    public decimal MargenPct => (Gastos + GastosEmpresaTotal) > 0
        ? Math.Round(Neta / (Gastos + GastosEmpresaTotal) * 100, 0) : 0;
    public int TotalReservas { get; set; }
    public int TotalPersonas { get; set; }

    // Formulario para agregar un gasto de empresa
    [BindProperty] public DateTime NuevoFecha { get; set; } = DateTime.Today;
    [BindProperty] public string NuevoTipo { get; set; } = "Fijo";
    [BindProperty] public string? NuevoDescripcion { get; set; }
    [BindProperty] public decimal NuevoMonto { get; set; }
    [BindProperty] public IFormFile? NuevoComprobante { get; set; }

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

        // ---- Reservas del mes: las que MOVIERON plata este mes (cobraste seña o saldo),
        //      así las personas/reservas coinciden con la plata que se ve (aunque la
        //      excursión salga otro mes) ----
        var reservasDelMes = reservas.Where(r => EnMes(r.SenaFecha) || EnMes(r.SaldoFecha)).ToList();
        TotalReservas = reservasDelMes.Count;
        TotalPersonas = reservasDelMes.Sum(r => r.CantidadPersonas);
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

    // Agregar un gasto general de la empresa
    public async Task<IActionResult> OnPostAgregarGastoAsync()
    {
        if (!string.IsNullOrWhiteSpace(NuevoDescripcion) && NuevoMonto > 0)
        {
            var g = new GastoEmpresa
            {
                Fecha = NuevoFecha.Date,
                Tipo = GastoEmpresa.Tipos.Contains(NuevoTipo) ? NuevoTipo : "Fijo",
                Descripcion = NuevoDescripcion.Trim(),
                Monto = NuevoMonto
            };

            if (NuevoComprobante is not null && NuevoComprobante.Length > 0)
            {
                var carpeta = Wamani.Reservas.Services.Comprobantes.Carpeta(_env);
                Directory.CreateDirectory(carpeta);
                var nombre = $"{Guid.NewGuid():N}{Path.GetExtension(NuevoComprobante.FileName)}";
                using (var st = new FileStream(Path.Combine(carpeta, nombre), FileMode.Create))
                    await NuevoComprobante.CopyToAsync(st);
                g.Comprobante = $"/comprobantes/{nombre}";
            }

            _db.GastosEmpresa.Add(g);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { Mes });
    }

    // Borrar un gasto general de la empresa
    public async Task<IActionResult> OnPostEliminarGastoAsync(int id)
    {
        var g = await _db.GastosEmpresa.FindAsync(id);
        if (g is not null)
        {
            _db.GastosEmpresa.Remove(g);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { Mes });
    }
}

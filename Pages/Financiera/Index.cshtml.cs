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
    public IndexModel(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    // ---- El informe de cierre del mes, en PDF ----
    //
    // Junta dos cosas que hay que mirar juntas: la plata que se movió en el mes y los
    // compromisos pendientes de hoy. Con una sola de las dos se toman malas decisiones:
    // un mes puede cerrar "ganando" con plata que ya está comprometida para pagar saldos.
    public async Task<IActionResult> OnGetCierreAsync(string? mes)
    {
        var hoy = Wamani.Reservas.Services.Reloj.HoyJujuy();
        int anio = hoy.Year, nmes = hoy.Month;
        if (!string.IsNullOrWhiteSpace(mes) && DateTime.TryParse(mes + "-01", out var p))
        {
            anio = p.Year; nmes = p.Month;
        }
        var desde = new DateTime(anio, nmes, 1);
        var hasta = desde.AddMonths(1);
        bool EnMes(DateTime? f) => f is DateTime d && d.Date >= desde && d.Date < hasta;

        var reservas = await _db.Reservas.ToListAsync();
        var ops = await _db.OperativoGastos.ToListAsync();
        var provs = await _db.OperativoProveedores.ToListAsync();
        var extras = await _db.IngresosExtra.ToListAsync();
        var gastosEmp = await _db.GastosEmpresa.ToListAsync();

        var d = new Wamani.Reservas.Services.CierrePdf.Datos
        {
            Mes = desde,
            MesTexto = desde.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-AR")),
        };
        // Primera letra en mayúscula: "Septiembre 2026", no "septiembre 2026".
        if (d.MesTexto.Length > 0)
            d.MesTexto = char.ToUpper(d.MesTexto[0]) + d.MesTexto[1..];

        // ---- Lo que se movió este mes ----
        d.CobradoReservas = reservas.Sum(r => (EnMes(r.SenaFecha) ? r.SenaMonto ?? 0 : 0)
                                            + (EnMes(r.SaldoFecha) ? r.SaldoMonto ?? 0 : 0));
        d.IngresosExtra = extras.Where(e => EnMes(e.Fecha)).Sum(e => e.Monto);
        d.PagadoOperativo = ops.Where(o => EnMes(o.FechaPago)).Sum(o => o.Precio)
                          + provs.Sum(x => (EnMes(x.FechaSena) ? x.Sena : 0) + (EnMes(x.FechaSaldo) ? x.Saldo : 0));
        d.GastosEmpresa = gastosEmp.Where(g => EnMes(g.Fecha)).Sum(g => g.Monto);

        var delMes = reservas.Where(r => EnMes(r.SenaFecha) || EnMes(r.SaldoFecha)).ToList();
        d.Reservas = delMes.Count;
        d.Personas = delMes.Sum(r => r.CantidadPersonas);

        // ---- Lo que se llevaron los socios ----
        d.Retiros = (await _db.Retiros.ToListAsync()).Where(r => EnMes(r.Fecha))
            .Select(r => (r.Quien ?? "", r.Monto)).ToList();
        d.Aportes = (await _db.Aportes.ToListAsync()).Where(a => EnMes(a.Fecha))
            .Select(a => (a.Quien ?? "", a.Monto)).ToList();

        // ---- Compromisos: igual que la pantalla de Compromisos ----
        var ingresosHist = reservas.Sum(r => (r.SenaMonto ?? 0) + (r.SaldoMonto ?? 0)) + extras.Sum(e => e.Monto);
        var egHist = ops.Where(o => o.FechaPago != null).Sum(o => o.Precio)
                   + provs.Sum(x => (x.FechaSena != null ? x.Sena : 0) + (x.FechaSaldo != null ? x.Saldo : 0))
                   + gastosEmp.Sum(g => g.Monto);
        var aportesHist = (await _db.Aportes.ToListAsync()).Sum(a => a.Monto);
        var retirosHist = (await _db.Retiros.ToListAsync()).Sum(r => r.Monto);
        var movsFondo = await _db.MovimientosFondo.ToListAsync();
        var alFondo = movsFondo.Where(m => m.SaleDeLaCaja).Sum(m => m.Pesos);
        d.CajaHoy = ingresosHist - egHist + aportesHist - retirosHist - alFondo;

        // Las "Reservas Históricas" no son deuda de nadie: ver Pages/Compromisos.
        var cobrables = reservas.Where(r => r.NombreCliente != Reserva.NombreHistorica && r.Pendiente() > 0).ToList();
        d.FaltaCobrar = cobrables.Sum(r => r.Pendiente());
        d.CuantosSaldos = cobrables.Count;

        var salidasHistoricas = reservas.Where(r => r.ExcursionId != null)
            .GroupBy(r => (Exc: r.ExcursionId!.Value, Fecha: r.FechaDesde.Date))
            .Where(g => g.All(r => r.NombreCliente == Reserva.NombreHistorica))
            .Select(g => g.Key).ToHashSet();
        bool EsHistorica(int excId, DateTime f) => salidasHistoricas.Contains((excId, f.Date));

        d.FaltaPagarGastos = ops.Where(o => o.FechaPago == null && o.Precio > 0 && !EsHistorica(o.ExcursionId, o.Fecha))
            .Sum(o => o.Precio);
        d.FaltaPagarProveedores = provs.Where(x => x.TieneDeuda() && !EsHistorica(x.ExcursionId, x.Fecha))
            .Sum(x => x.Pendiente());

        d.FondoDolares = movsFondo.Sum(m => m.SignoDolares);

        var logo = Path.Combine(_env.WebRootPath, "logo", "logo-pdf.png");
        var pdf = Wamani.Reservas.Services.CierrePdf.Generar(d, logo);
        return File(pdf, "application/pdf", $"Wamani - Cierre {desde:yyyy-MM}.pdf");
    }

    // Los 3 dueños (reparto en partes iguales)
    public static readonly string[] Duenos = { "Lautaro", "Facundo", "Luciano" };

    [BindProperty(SupportsGet = true)]
    public string? Mes { get; set; }   // formato "yyyy-MM"

    public DateTime MesActual { get; set; }

    // Quién movió plata este mes: se llena desde la tabla Actividad
    public class QuienCargo
    {
        public string Nombre { get; set; } = "";
        public decimal Ingresos { get; set; }
        public decimal Egresos { get; set; }
        public int Movimientos { get; set; }
    }
    public List<QuienCargo> CargadoPor { get; set; } = new();
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

    // Cuenta de cada socio: cuánto ganó en total, cuánto ya retiró y cuánto le queda
    public Wamani.Reservas.Services.CuentaSocios.Resultado Cuentas { get; set; } = new();

    public decimal GastosEmpresaPropios => GastosEmpresaTotal;

    // Ganancia del mes. Desde octubre de 2026 no se aparta un 10% automático: lo que se
    // reinvierte se decide a mano al cerrar el mes y va al fondo de inversión, que vive en
    // dólares y tiene su propia pantalla.
    public decimal Neta => IngresoTotal - Gastos - GastosEmpresaPropios;

    public decimal GananciaARepartir => Neta;
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

        // ---- Quién cargó plata este mes (informativo, ver Services/Registro.cs) ----
        var actividad = await _db.Actividades.AsNoTracking()
            .Where(a => a.Fecha >= MesActual && a.Fecha < fin)
            .ToListAsync();
        CargadoPor = actividad
            .GroupBy(a => a.Nombre)
            .Select(g => new QuienCargo
            {
                Nombre      = g.Key,
                Ingresos    = g.Where(x => x.EsIngreso).Sum(x => x.Monto),
                Egresos     = g.Where(x => !x.EsIngreso).Sum(x => x.Monto),
                Movimientos = g.Count()
            })
            .OrderByDescending(x => x.Movimientos)
            .ToList();

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

        // ---- Ingresos extra del mes (comisiones, alquileres, etc.) ----
        ExtrasLista = await _db.IngresosExtra
            .Where(e => e.Fecha >= MesActual && e.Fecha < fin)
            .OrderByDescending(e => e.Fecha)
            .ToListAsync();
        ExtrasTotal = ExtrasLista.Sum(e => e.Monto);

        // ---- La cuenta de cada socio, acumulada hasta este mes ----
        Cuentas = await Wamani.Reservas.Services.CuentaSocios.CalcularAsync(_db, Duenos, MesActual);

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

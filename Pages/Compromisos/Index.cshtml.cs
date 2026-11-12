using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;
using Wamani.Reservas.Services;

namespace Wamani.Reservas.Pages.Compromisos;

// COMPROMISOS: la plata que Wamani tiene comprometida de acá en adelante.
//
//   Caja hoy       = lo ya cobrado menos lo ya pagado.
//   + Por cobrar   = saldos de las reservas de salidas que TODAVÍA NO SALIERON.
//   − Por pagar    = lo que va a costar cada una de esas salidas, menos lo que ya se pagó.
//   = Proyectado   = con lo que se va a contar cuando termine de cobrarse y pagarse todo.
//
// DOS DECISIONES DE FONDO, tomadas con Lautaro el 12/11/2026:
//
// 1) EL COSTO SE PROYECTA CON EL TEÓRICO DE LA EXCURSIÓN, no con lo cargado en el
//    operativo. Antes esta pantalla sumaba solamente los gastos y proveedores que
//    estaban cargados, así que una salida vendida pero sin operativo armado aportaba
//    su saldo a cobrar y CERO de costo: la proyección cobraba por ella y no pagaba por
//    ella. El 12/11 eso escondía $2.601.000 de cinco salidas de diciembre y verano más
//    el Yungas Express del 21/11, y la pantalla mostraba $3.732.141 cuando lo real era
//    $1.131.141. Que todavía no se haya elegido el guía no significa que no haya que
//    pagarlo. El teórico sale de la plantilla de la excursión —el mismo cálculo de
//    Rentabilidad y de "Teórica vs real"— multiplicado por los pasajeros de la salida.
//
// 2) SÓLO CUENTAN LAS SALIDAS QUE TODAVÍA NO SALIERON. Lo que ya pasó no es un
//    compromiso a futuro: o está pago, o es un arreglo del operativo de esa salida. Si
//    queda algo colgado se avisa arriba, con su monto, pero NO entra en la proyección.
//
// "Ya pagado" significa exactamente lo mismo que en la pantalla Caja: plata que ya salió,
// o sea con fecha de pago puesta. Un proveedor con el monto cargado pero sin fecha cuenta
// como no pagado, igual que en la caja. Así las dos pantallas nunca se contradicen.
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
    }

    // Una salida futura: lo que va a costar, lo que ya se pagó y lo que falta
    public class SalidaPendiente
    {
        public string Excursion { get; set; } = "";
        public DateTime Fecha { get; set; }
        public int Personas { get; set; }
        public int ExcursionId { get; set; }
        public decimal Teorico { get; set; }
        public decimal Cargado { get; set; }
        public decimal Pagado { get; set; }
        public decimal Falta { get; set; }
        // Si no hay nada cargado en el operativo, el costo es íntegramente una
        // estimación de la plantilla. Se marca para saber qué falta armar.
        public bool OperativoVacio { get; set; }
    }

    // Un proveedor ya reservado al que le falta pagarle (detalle, no se suma aparte)
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
    }

    public List<CobroPendiente> Cobros { get; set; } = new();
    public List<SalidaPendiente> Salidas { get; set; } = new();
    public List<PagoPendiente> Pagos { get; set; } = new();

    public decimal CajaHoy { get; set; }

    // Cuánta plata de salidas históricas se dejó AFUERA de la proyección, y de cuántas
    // salidas. Se muestra como aclaración: si no, el número cambia y no se sabe por qué.
    public decimal HistoricoIgnorado { get; set; }
    public int SalidasHistoricas { get; set; }

    // Lo de salidas que YA PASARON: se avisa pero no entra en la proyección.
    public decimal CobrarVencido { get; set; }
    public decimal PagarVencido { get; set; }
    public int SalidasVencidas { get; set; }

    public decimal PorCobrar => Cobros.Sum(c => c.Falta);
    public decimal PorPagar => Salidas.Sum(s => s.Falta);
    public decimal Proyectado => CajaHoy + PorCobrar - PorPagar;

    // De lo que falta pagar, cuánto es deuda firme con un proveedor ya elegido
    public decimal DeudaProveedores => Pagos.Sum(p => p.Falta);
    // Cuánto del costo proyectado sale de salidas sin nada cargado en el operativo
    public decimal FaltaEstimado => Salidas.Where(s => s.OperativoVacio).Sum(s => s.Falta);
    public int SalidasSinOperativo => Salidas.Count(s => s.OperativoVacio);

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
        var egGastos = todosGastos.Sum(o => o.PagadoReal());

        var provs = await _db.OperativoProveedores.ToListAsync();
        // Misma definición que la caja: sólo lo que tiene fecha de pago puesta.
        decimal PagadoProv(OperativoProveedor p)
            => (p.FechaSena != null ? p.Sena : 0) + (p.FechaSaldo != null ? p.Saldo : 0);
        var egProv = provs.Sum(PagadoProv);

        var egEmpresa = (await _db.GastosEmpresa.ToListAsync()).Sum(g => g.Monto);
        var aportes = (await _db.Aportes.ToListAsync()).Sum(a => a.Monto);
        var retiros = (await _db.Retiros.ToListAsync()).Sum(r => r.Monto);

        CajaHoy = ingresos - (egGastos + egProv + egEmpresa) + aportes - retiros;

        // ---- Salidas viejas, anteriores al sistema ----
        // Se cargaron como "Reservas Históricas": quedan como registro pero sin plata, por
        // eso facturan $0. El operativo igual les armó los gastos de la plantilla de la
        // excursión, pero esa plata nunca se debió ni se va a pagar: ya se pagó en su
        // momento, fuera del sistema. Contarla acá inventaba una deuda que no existe y
        // hundía la proyección. Si a una de esas salidas se le suma una reserva de verdad,
        // deja de ser histórica y vuelve a contar como cualquier otra.
        var salidasHistoricas = reservas
            .Where(r => r.ExcursionId != null)
            .GroupBy(r => (Exc: r.ExcursionId!.Value, Fecha: r.FechaDesde.Date))
            .Where(g => g.All(r => r.NombreCliente == Reserva.NombreHistorica))
            .Select(g => g.Key)
            .ToHashSet();
        bool EsHistorica(int excursionId, DateTime fecha)
            => salidasHistoricas.Contains((excursionId, fecha.Date));

        // ---- Por cobrar: saldos de reservas de salidas que todavía no salieron ----
        Cobros = reservas
            .Where(r => r.NombreCliente != Reserva.NombreHistorica
                     && r.Pendiente() > 0
                     && r.FechaDesde.Date >= hoy)
            .Select(r => new CobroPendiente
            {
                Excursion = string.IsNullOrWhiteSpace(r.Excursion) ? "Excursión" : r.Excursion,
                Fecha = r.FechaDesde.Date,
                Cliente = r.NombreCliente,
                Personas = r.CantidadPersonas,
                Total = r.TotalConDescuento(),
                Cobrado = r.Cobrado(),
                Falta = r.Pendiente()
            })
            .OrderBy(c => c.Fecha).ThenBy(c => c.Cliente)
            .ToList();

        // ---- Por pagar: el teórico de cada salida futura menos lo que ya se pagó ----
        var plantillaPorExc = (await _db.GastosExcursion.ToListAsync())
            .GroupBy(g => g.ExcursionId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var etapasPorExc = (await _db.EtapasExcursion.ToListAsync())
            .GroupBy(e => e.ExcursionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var gastosPorSalida = todosGastos
            .GroupBy(o => (o.ExcursionId, Fecha: o.Fecha.Date))
            .ToDictionary(g => g.Key, g => g.ToList());
        var provsPorSalida = provs
            .GroupBy(p => (p.ExcursionId, Fecha: p.Fecha.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        Salidas = reservas
            .Where(r => r.ExcursionId is int
                     && r.NombreCliente != Reserva.NombreHistorica
                     && r.FechaDesde.Date >= hoy)
            .GroupBy(r => (ExcursionId: r.ExcursionId!.Value, Fecha: r.FechaDesde.Date))
            .Where(g => !EsHistorica(g.Key.ExcursionId, g.Key.Fecha))
            .Select(g =>
            {
                var pax = g.Sum(r => r.CantidadPersonas);
                var items = plantillaPorExc.GetValueOrDefault(g.Key.ExcursionId) ?? new();
                var etapas = etapasPorExc.GetValueOrDefault(g.Key.ExcursionId) ?? new();
                var teorico = RentabilidadCalc.Costo(items, etapas, pax);

                var ops = gastosPorSalida.GetValueOrDefault(g.Key) ?? new();
                var pvs = provsPorSalida.GetValueOrDefault(g.Key) ?? new();
                var pagado = ops.Sum(o => o.PagadoReal()) + pvs.Sum(PagadoProv);
                var cargado = ops.Sum(o => o.Precio) + pvs.Sum(p => p.Total);

                return new SalidaPendiente
                {
                    ExcursionId = g.Key.ExcursionId,
                    Excursion = NombreExc(g.Key.ExcursionId),
                    Fecha = g.Key.Fecha,
                    Personas = pax,
                    Teorico = teorico,
                    Cargado = cargado,
                    Pagado = pagado,
                    // Si se pagó más que el teórico, no falta nada: el excedente es un
                    // tema del operativo de esa salida, no plata a favor de la proyección.
                    Falta = Math.Max(0, teorico - pagado),
                    OperativoVacio = ops.Count == 0 && pvs.Count == 0
                };
            })
            .Where(s => s.Falta > 0 || s.Teorico > 0)
            .OrderBy(s => s.Fecha)
            .ToList();

        // ---- Detalle: proveedores ya elegidos que todavía deben saldo ----
        // Es parte de lo que falta pagar, no se suma aparte: sirve para saber cuánto de
        // ese total ya tiene nombre, precio acordado y lugar tomado.
        Pagos = provs
            .Where(p => p.TieneDeuda()
                     && p.Fecha.Date >= hoy
                     && !EsHistorica(p.ExcursionId, p.Fecha))
            .Select(p => new PagoPendiente
            {
                Excursion = NombreExc(p.ExcursionId),
                Fecha = p.Fecha.Date,
                Tipo = p.Tipo,
                Proveedor = string.IsNullOrWhiteSpace(p.ProveedorNombre) ? "— sin asignar —" : p.ProveedorNombre,
                Lugar = p.Lugar,
                Total = p.Total,
                Pagado = PagadoProv(p),
                Falta = Math.Max(0, p.Total - PagadoProv(p))
            })
            .OrderBy(p => p.Fecha).ThenBy(p => p.Tipo)
            .ToList();

        // ---- Lo que quedó colgado de salidas que ya pasaron (aviso, no proyección) ----
        CobrarVencido = reservas
            .Where(r => r.NombreCliente != Reserva.NombreHistorica
                     && r.Pendiente() > 0
                     && r.FechaDesde.Date < hoy)
            .Sum(r => r.Pendiente());

        var gastosVencidos = todosGastos
            .Where(o => o.Pendiente() > 0 && o.Fecha.Date < hoy && !EsHistorica(o.ExcursionId, o.Fecha))
            .ToList();
        var provsVencidos = provs
            .Where(p => p.TieneDeuda() && p.Fecha.Date < hoy && !EsHistorica(p.ExcursionId, p.Fecha))
            .ToList();
        PagarVencido = gastosVencidos.Sum(o => o.Pendiente()) + provsVencidos.Sum(p => p.Pendiente());
        SalidasVencidas = gastosVencidos.Select(o => (o.ExcursionId, o.Fecha.Date))
            .Concat(provsVencidos.Select(p => (p.ExcursionId, p.Fecha.Date)))
            .Distinct().Count();

        // ---- Salidas anteriores al sistema, para la aclaración ----
        var gastosHist = todosGastos
            .Where(o => o.Pendiente() > 0 && EsHistorica(o.ExcursionId, o.Fecha))
            .ToList();
        var provsHist = provs
            .Where(p => p.TieneDeuda() && EsHistorica(p.ExcursionId, p.Fecha))
            .ToList();
        HistoricoIgnorado = gastosHist.Sum(o => o.Pendiente()) + provsHist.Sum(p => p.Pendiente());
        SalidasHistoricas = gastosHist.Select(o => (o.ExcursionId, o.Fecha.Date))
            .Concat(provsHist.Select(p => (p.ExcursionId, p.Fecha.Date)))
            .Distinct().Count();
    }
}

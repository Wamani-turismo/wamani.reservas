using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public class ProximaSalida
    {
        public string Excursion { get; set; } = "";
        public DateTime FechaDesde { get; set; }
        public DateTime FechaHasta { get; set; }
        public int TotalPasajeros { get; set; }
        public int Minimo { get; set; }
        public List<Reserva> Reservas { get; set; } = new();
        public bool Sale => TotalPasajeros >= Minimo;
        public int Faltan => Math.Max(0, Minimo - TotalPasajeros);
        public bool EsRango => FechaHasta.Date > FechaDesde.Date;
    }

    public List<ProximaSalida> ProximasSalidas { get; set; } = new();

    // Total pendiente de cobrar en salidas próximas (resumen del inicio)
    public int CobrosPendientesCount { get; set; }

    // ── La salida de HOY y cómo están los pagos ──────────────────────────────
    //
    // Es el aviso que salta al entrar: hoy sale tal excursión, y si queda algo por
    // pagar, qué y cuánto. La regla de "falta pagar" es EXACTAMENTE la misma que usa
    // la pantalla "Detalle a pagar" (Pages/Operativo/Pagar.cshtml.cs), para que los
    // dos números coincidan siempre:
    //   · un gasto falta si NO está tildado como comprado;
    //   · un proveedor falta si tiene deuda (total mayor que seña + saldo).
    public class SalidaDeHoy
    {
        public int ExcursionId { get; set; }
        public string Excursion { get; set; } = "";
        public DateTime Fecha { get; set; }
        public int Pasajeros { get; set; }
        public List<string> Clientes { get; set; } = new();

        // ¿Se llegó a armar el operativo? Sin operativo no se puede decir que esté pago.
        public bool HayOperativo { get; set; }

        public int GastosPendientes { get; set; }
        public decimal FaltaGastos { get; set; }
        public int ProveedoresPendientes { get; set; }
        public decimal FaltaProveedores { get; set; }

        // Algo quedó en $0: no se puede pagar ni saber cuánto es. Se avisa aparte para
        // que "falta $0" no se lea como "no falta nada".
        public bool HaySinPrecio { get; set; }
        public bool HayProveedorSinAsignar { get; set; }

        public decimal FaltaTotal => FaltaGastos + FaltaProveedores;
        public int CosasPendientes => GastosPendientes + ProveedoresPendientes;
        public bool TodoPago => HayOperativo && CosasPendientes == 0 && !HaySinPrecio;
    }

    public List<SalidaDeHoy> SalidasDeHoy { get; set; } = new();

    public async Task OnGetAsync()
    {
        var hoy = DateTime.Today;

        var reservas = await _db.Reservas.ToListAsync();

        ProximasSalidas = reservas
            // "Próximas" = las que todavía no salieron (el día de salida es hoy o más adelante).
            // Las que ya salieron quedan guardadas en el Historial.
            .Where(r => r.FechaDesde.Date >= hoy)
            .GroupBy(r => new { r.ExcursionId, r.Excursion, Fecha = r.FechaDesde.Date })
            .Select(g => new ProximaSalida
            {
                Excursion = g.Key.Excursion,
                FechaDesde = g.Key.Fecha,
                FechaHasta = g.Max(r => r.FechaHasta),
                TotalPasajeros = g.Sum(r => r.CantidadPersonas),
                Minimo = g.Max(r => r.MinimoPersonas),
                Reservas = g.OrderBy(r => r.NombreCliente).ToList()
            })
            .OrderBy(s => s.FechaDesde)
            .ToList();

        CobrosPendientesCount = reservas.Count(r => r.HayQueCobrarSaldo(hoy));

        await CargarSalidasDeHoyAsync(reservas, hoy);
    }

    // Arma el aviso de la(s) salida(s) que arrancan hoy.
    private async Task CargarSalidasDeHoyAsync(List<Reserva> reservas, DateTime hoy)
    {
        var deHoy = reservas.Where(r => r.FechaDesde.Date == hoy).ToList();
        if (deHoy.Count == 0) return;

        var ids = deHoy.Select(r => r.ExcursionId ?? 0).Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0) return;

        // La fecha se compara EN MEMORIA, igual que en Pagar y en Comparar: en Postgres
        // el .Date adentro del SQL da problemas y son pocas filas por excursión.
        var gastos = (await _db.OperativoGastos
                .Where(o => ids.Contains(o.ExcursionId)).AsNoTracking().ToListAsync())
            .Where(o => o.Fecha.Date == hoy).ToList();

        var provs = (await _db.OperativoProveedores
                .Where(o => ids.Contains(o.ExcursionId)).AsNoTracking().ToListAsync())
            .Where(o => o.Fecha.Date == hoy).ToList();

        SalidasDeHoy = deHoy
            .GroupBy(r => new { r.ExcursionId, r.Excursion })
            .Select(g =>
            {
                var exId = g.Key.ExcursionId ?? 0;
                var gs = gastos.Where(x => x.ExcursionId == exId).ToList();
                var ps = provs.Where(x => x.ExcursionId == exId).ToList();

                var gPend = gs.Where(x => !x.Comprado).ToList();
                var pPend = ps.Where(x => x.TieneDeuda()).ToList();

                return new SalidaDeHoy
                {
                    ExcursionId = exId,
                    Excursion = g.Key.Excursion,
                    Fecha = hoy,
                    Pasajeros = g.Sum(r => r.CantidadPersonas),
                    Clientes = g.OrderBy(r => r.NombreCliente)
                                .Select(r => $"{r.NombreCliente} ({r.CantidadPersonas})").ToList(),
                    HayOperativo = gs.Count > 0 || ps.Count > 0,
                    GastosPendientes = gPend.Count,
                    FaltaGastos = gPend.Sum(x => x.Precio),
                    ProveedoresPendientes = pPend.Count,
                    FaltaProveedores = pPend.Sum(x => x.Pendiente()),
                    HaySinPrecio = gPend.Any(x => x.Precio <= 0),
                    HayProveedorSinAsignar = pPend.Any(x => string.IsNullOrWhiteSpace(x.ProveedorNombre)),
                };
            })
            .OrderBy(s => s.Excursion)
            .ToList();
    }
}

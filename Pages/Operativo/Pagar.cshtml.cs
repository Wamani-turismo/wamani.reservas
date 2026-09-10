using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Operativo;

// Qué falta pagar de una salida, item por item: los gastos que todavía no se tildaron
// como listos y los proveedores a los que les queda saldo.
//
// En el operativo se ve "6 / 14 listos" y "Falta $X" de proveedores, pero para salir a
// pagar hace falta el detalle: QUÉ falta y CUÁNTO de cada cosa. En una travesía, además,
// cada noche y cada traslado es un proveedor distinto y hay que saber a quién se le debe.
public class PagarModel : PageModel
{
    private readonly AppDbContext _db;
    public PagarModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int ExcursionId { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime Fecha { get; set; }

    public string ExcursionNombre { get; set; } = "";
    public int Pasajeros { get; set; }
    public bool HayOperativo { get; set; }

    // Un gasto que todavía no se pagó
    public class GastoPendiente
    {
        public string Nombre { get; set; } = "";
        public string DeQuien { get; set; } = "";     // de qué reserva es (vacío = de toda la salida)
        public string ComoSeCuenta { get; set; } = "";
        public decimal Monto { get; set; }
        public bool SinPrecio { get; set; }            // está en cero: hay que ponerle el precio
    }

    // Un proveedor al que le queda algo por pagar
    public class ProveedorPendiente
    {
        public string Tipo { get; set; } = "";
        public string Quien { get; set; } = "";
        public string Donde { get; set; } = "";        // el lugar de la travesía, si lo tiene
        public string ParaQuien { get; set; } = "";
        public decimal Total { get; set; }
        public decimal Pagado { get; set; }
        public decimal Falta { get; set; }
        public bool SinAsignar { get; set; }           // no se eligió el proveedor todavía
    }

    public List<GastoPendiente> Gastos { get; set; } = new();
    public List<ProveedorPendiente> Proveedores { get; set; } = new();

    // Totales
    public decimal FaltaGastos => Gastos.Sum(g => g.Monto);
    public decimal FaltaProveedores => Proveedores.Sum(p => p.Falta);
    public decimal FaltaTotal => FaltaGastos + FaltaProveedores;

    // Lo que ya se pagó, para ver cuánto queda del total de la salida
    public decimal PagadoGastos { get; set; }
    public decimal PagadoProveedores { get; set; }
    public decimal PagadoTotal => PagadoGastos + PagadoProveedores;
    public decimal TotalSalida => PagadoTotal + FaltaTotal;

    public int GastosListos { get; set; }
    public int GastosEnTotal { get; set; }

    // ¿Hay algo con el precio en cero? Eso no se puede pagar y tampoco se puede sumar.
    public bool HayGastosSinPrecio => Gastos.Any(g => g.SinPrecio);
    public bool HayProveedoresSinAsignar => Proveedores.Any(p => p.SinAsignar);

    public bool EstaTodoPagado => Gastos.Count == 0 && Proveedores.Count == 0;

    public async Task<IActionResult> OnGetAsync()
    {
        var exc = await _db.Excursiones.FindAsync(ExcursionId);
        if (exc is null) return RedirectToPage("/Operativo/Index");
        ExcursionNombre = exc.Nombre;

        // Se filtra la fecha EN MEMORIA, como en Comparar: evita problemas de fecha en el
        // SQL de Postgres y son pocos registros por excursión.
        var reservas = (await _db.Reservas.Where(r => r.ExcursionId == ExcursionId).ToListAsync())
            .Where(r => r.FechaDesde.Date == Fecha.Date).ToList();
        Pasajeros = reservas.Sum(r => r.CantidadPersonas);
        var nombrePorReserva = reservas.ToDictionary(r => r.Id, r => r.NombreCliente);

        var gastos = (await _db.OperativoGastos.Where(o => o.ExcursionId == ExcursionId).ToListAsync())
            .Where(o => o.Fecha.Date == Fecha.Date).OrderBy(o => o.Id).ToList();

        var provs = (await _db.OperativoProveedores.Where(o => o.ExcursionId == ExcursionId).ToListAsync())
            .Where(o => o.Fecha.Date == Fecha.Date).OrderBy(o => o.Id).ToList();

        HayOperativo = gastos.Count > 0 || provs.Count > 0;

        GastosEnTotal = gastos.Count;
        GastosListos = gastos.Count(g => g.Comprado);
        PagadoGastos = gastos.Where(g => g.Comprado).Sum(g => g.Precio);

        // ---- Gastos que faltan: los que NO están tildados como listos ----
        // (la regla del operativo es siempre la misma: tilde = pagado)
        foreach (var g in gastos.Where(x => !x.Comprado))
        {
            Gastos.Add(new GastoPendiente
            {
                Nombre = g.Nombre,
                DeQuien = g.ReservaId is int rid && nombrePorReserva.TryGetValue(rid, out var n) ? n : "",
                ComoSeCuenta = ComoSeCuenta(g),
                Monto = g.Precio,
                SinPrecio = g.Precio <= 0
            });
        }

        // ---- Proveedores a los que les queda saldo ----
        PagadoProveedores = provs.Sum(p => p.Pagado());

        foreach (var p in provs.Where(x => x.TieneDeuda()))
        {
            Proveedores.Add(new ProveedorPendiente
            {
                Tipo = p.Tipo,
                Quien = string.IsNullOrWhiteSpace(p.ProveedorNombre) ? "— sin asignar —" : p.ProveedorNombre,
                Donde = p.Lugar ?? "",
                ParaQuien = p.ParaQuien ?? "",
                Total = p.Total,
                Pagado = p.Pagado(),
                Falta = p.Pendiente(),
                SinAsignar = string.IsNullOrWhiteSpace(p.ProveedorNombre)
            });
        }

        // Primero lo más caro: es por dónde conviene arrancar a pagar.
        Gastos = Gastos.OrderByDescending(g => g.Monto).ToList();
        Proveedores = Proveedores.OrderByDescending(p => p.Falta).ToList();

        return Page();
    }

    // Cómo se cuenta el gasto, dicho en criollo, para entender de dónde sale el monto.
    private string ComoSeCuenta(OperativoGasto g)
    {
        if (g.PrecioUnitario is not decimal u) return "cargado a mano";
        var cuantos = g.ReservaId is null ? g.MultiplicadorPropio(Pasajeros) : 0;

        return g.TipoCalculo switch
        {
            "Por auto" => Money(u) + " × " + cuantos + " auto(s)",
            "Cantidad" => Money(u) + " × " + g.CantidadReal(),
            "Fijo"     => "fijo de la salida",
            _           => Money(u) + " por persona",
        };
    }

    public static string Money(decimal m) =>
        "$ " + m.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-AR"));
}

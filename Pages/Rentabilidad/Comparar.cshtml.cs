using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Rentabilidad;

// Compara la rentabilidad TEÓRICA (la plantilla de costos de la excursión, calculada a la
// cantidad de gente de la salida) contra lo REAL que se cargó en el operativo de esa salida
// (gastos + proveedores). Marca ítem por ítem qué se sumó de más, qué de menos y qué faltó.
public class CompararModel : PageModel
{
    private readonly AppDbContext _db;
    public CompararModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int ExcursionId { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime Fecha { get; set; }

    public Excursion Excursion { get; set; } = new();
    public int Pax { get; set; }
    public int Autos { get; set; }
    public bool HayOperativo { get; set; }
    public bool HayReservas { get; set; }

    // Una fila de la comparación de GASTOS (matcheada por nombre)
    public class FilaGasto
    {
        public string Concepto { get; set; } = "";
        public decimal Teorica { get; set; }
        public decimal Real { get; set; }
        public decimal Dif => Real - Teorica;
        // "igual" | "dif" | "soloReal" (está en el operativo pero no en la plantilla)
        //         | "soloTeorica" (está en la plantilla pero no se cargó en el operativo)
        public string Estado { get; set; } = "igual";
    }

    // Un ítem suelto (para las listas de proveedores)
    public class ItemMonto
    {
        public string Nombre { get; set; } = "";
        public decimal Monto { get; set; }
        public string? Extra { get; set; }   // ej: "falta $X" en un proveedor
    }

    public List<FilaGasto> Gastos { get; set; } = new();
    public List<ItemMonto> ProvTeorica { get; set; } = new();
    public List<ItemMonto> ProvReal { get; set; } = new();

    // Totales
    public decimal TeoricaGastos { get; set; }
    public decimal RealGastos { get; set; }
    public decimal TeoricaProv { get; set; }
    public decimal RealProv { get; set; }
    public decimal TeoricaTotal => TeoricaGastos + TeoricaProv;
    public decimal RealTotal => RealGastos + RealProv;
    public decimal Diferencia => RealTotal - TeoricaTotal;   // + = gastaste de más que lo estimado

    // Contexto
    public decimal Ingreso { get; set; }
    public decimal GananciaTeorica => Ingreso - TeoricaTotal;
    public decimal GananciaReal => Ingreso - RealTotal;

    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

    public async Task<IActionResult> OnGetAsync()
    {
        var exc = await _db.Excursiones.FindAsync(ExcursionId);
        if (exc is null) return RedirectToPage("/Rentabilidad/Index");
        Excursion = exc;

        // Cargamos por ExcursionId y filtramos la fecha EN MEMORIA (evita problemas de
        // fecha en el SQL de Postgres). Son pocos registros por excursión.
        var reservas = (await _db.Reservas.Where(r => r.ExcursionId == ExcursionId).ToListAsync())
            .Where(r => r.FechaDesde.Date == Fecha.Date).ToList();
        HayReservas = reservas.Count > 0;
        Pax = reservas.Sum(r => r.CantidadPersonas);
        Autos = Pax <= 0 ? 0 : (int)Math.Ceiling(Pax / (double)Models.Excursion.PersonasPorAuto);

        Ingreso = exc.PrecioPorPersona * Pax;

        var plantilla = await _db.GastosExcursion
            .Where(g => g.ExcursionId == ExcursionId).OrderBy(g => g.Id).ToListAsync();

        var opGastos = (await _db.OperativoGastos.Where(o => o.ExcursionId == ExcursionId).ToListAsync())
            .Where(o => o.Fecha.Date == Fecha.Date).ToList();
        var opProv = (await _db.OperativoProveedores.Where(o => o.ExcursionId == ExcursionId).ToListAsync())
            .Where(o => o.Fecha.Date == Fecha.Date).ToList();
        HayOperativo = opGastos.Count > 0 || opProv.Count > 0;

        // Costo teórico de un ítem de la plantilla, a la gente de la salida
        decimal CostoItem(GastoExcursion g) => g.TipoCalculo switch
        {
            "Por auto" => g.Precio * Autos,
            "Por guía" => g.Precio * Autos,
            "Cantidad" => g.Precio * (g.Cantidad ?? 0),
            "Fijo"     => g.Precio,
            _           => g.Precio * Pax,   // Por persona
        };

        // ---------- GASTOS (plantilla NO-proveedor  vs  operativo gastos), por nombre ----------
        var teoGastos = plantilla.Where(g => !g.EsProveedor)
            .GroupBy(g => Norm(g.Nombre))
            .ToDictionary(gr => gr.Key, gr => (Nombre: gr.First().Nombre.Trim(), Monto: gr.Sum(CostoItem)));

        var realGastos = opGastos
            .GroupBy(o => Norm(o.Nombre))
            .ToDictionary(gr => gr.Key, gr => (Nombre: gr.First().Nombre.Trim(), Monto: gr.Sum(o => o.Precio)));

        var nombres = teoGastos.Keys.Union(realGastos.Keys);
        foreach (var k in nombres)
        {
            var hayT = teoGastos.TryGetValue(k, out var t);
            var hayR = realGastos.TryGetValue(k, out var r);
            var fila = new FilaGasto
            {
                Concepto = hayR ? r.Nombre : t.Nombre,
                Teorica = hayT ? t.Monto : 0,
                Real = hayR ? r.Monto : 0,
                Estado = !hayT ? "soloReal" : !hayR ? "soloTeorica"
                        : (t.Monto == r.Monto ? "igual" : "dif")
            };
            Gastos.Add(fila);
        }
        // Ordenar: primero los que tienen diferencia, después por monto real desc
        Gastos = Gastos
            .OrderByDescending(f => f.Estado == "soloReal")
            .ThenByDescending(f => f.Estado == "soloTeorica")
            .ThenByDescending(f => Math.Abs(f.Dif))
            .ThenByDescending(f => f.Real)
            .ToList();

        TeoricaGastos = teoGastos.Values.Sum(v => v.Monto);
        RealGastos = realGastos.Values.Sum(v => v.Monto);

        // ---------- PROVEEDORES / SERVICIOS (listas lado a lado, se comparan los subtotales) ----------
        ProvTeorica = plantilla.Where(g => g.EsProveedor)
            .Select(g => new ItemMonto { Nombre = g.Nombre.Trim(), Monto = CostoItem(g) })
            .Where(i => i.Monto > 0)
            .OrderByDescending(i => i.Monto).ToList();
        TeoricaProv = ProvTeorica.Sum(i => i.Monto);

        ProvReal = opProv
            .Where(p => p.Total > 0 || p.Pagado() > 0)
            .Select(p => new ItemMonto
            {
                Nombre = $"{p.Tipo}: {(string.IsNullOrWhiteSpace(p.ProveedorNombre) ? "—" : p.ProveedorNombre)}",
                Monto = p.Total,
                Extra = p.Pendiente() > 0 ? $"falta pagar {Money(p.Pendiente())}" : null
            })
            .OrderByDescending(i => i.Monto).ToList();
        RealProv = opProv.Sum(p => p.Total);

        return Page();
    }

    public static string Money(decimal m) =>
        "$ " + m.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-AR"));
}

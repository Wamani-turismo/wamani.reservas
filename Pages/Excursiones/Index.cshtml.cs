using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Excursiones;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Excursion> Excursiones { get; set; } = new();

    // La lista se muestra en dos partes: arriba las de siempre y abajo, aparte, los viajes
    // armados a medida. Así no se mezcla el catálogo con lo que se hizo para un grupo
    // puntual. La "Excursión a medida" genérica de toda la vida también va abajo.
    public bool EsDeAMedida(Excursion e) => e.EsPersonalizada || e.EsAMedida;
    public List<Excursion> DelCatalogo => Excursiones.Where(e => !EsDeAMedida(e)).ToList();
    public List<Excursion> AMedida => Excursiones.Where(EsDeAMedida).ToList();

    [BindProperty(SupportsGet = true)]
    public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        Excursiones = await _db.Excursiones
            .OrderBy(e => e.Nombre)
            .ToListAsync();
    }

    // ---------- Viaje a medida ----------
    //
    // "Queremos Conociendo las Yungas pero también la Quebrada." En vez de cargar todo de
    // cero, se elige una excursión que ya existe y el sistema hace una COPIA con todos sus
    // costos y etapas, más una tanda de renglones vacíos por cada día que se agrega.
    //
    // La copia es una excursión común y corriente: por eso funciona sola en Operativo,
    // Rentabilidad, Finanzas, Compromisos y el comprobante. No hubo que tocar nada de eso.
    //
    // Los días extra se agregan como COSTOS (no como etapas) a propósito: los costos andan
    // igual en las excursiones de un día y en las travesías, y no cambian la forma en que el
    // operativo agrupa los gastos. Nacen en cero, listos para escribirles el precio.
    [BindProperty] public int BaseId { get; set; }
    [BindProperty] public string? NombreMedida { get; set; }
    [BindProperty] public int DiasExtra { get; set; } = 1;

    // Lo que se paga en un día más de viaje. Nacen en $0 y se completan a mano.
    private static readonly (string Nombre, string Tipo)[] RenglonesDelDia =
    {
        ("Hospedaje",      "Por persona"),
        ("Comidas",        "Por persona"),
        ("Entradas",       "Por persona"),
        ("Chofer y guía",  "Por auto"),
        ("Nafta",          "Por auto"),
    };

    public async Task<IActionResult> OnPostAMedidaAsync()
    {
        var nombre = (NombreMedida ?? "").Trim();
        if (BaseId == 0 || nombre.Length < 3)
            return RedirectToPage(new { Aviso = "Para armar un viaje a medida hace falta elegir la excursión de base y ponerle un nombre." });

        var origen = await _db.Excursiones.FirstOrDefaultAsync(e => e.Id == BaseId);
        if (origen is null) return RedirectToPage(new { Aviso = "No se encontró la excursión de base." });

        var dias = DiasExtra < 0 ? 0 : (DiasExtra > 15 ? 15 : DiasExtra);

        // 1) La excursión nueva, con los mismos datos que la de base
        var nueva = new Excursion
        {
            Nombre = nombre.StartsWith("A medida", StringComparison.OrdinalIgnoreCase) ? nombre : "A medida · " + nombre,
            PrecioPorPersona = origen.PrecioPorPersona,
            MinimoPersonas = origen.MinimoPersonas,
            MaximoPersonas = origen.MaximoPersonas,
            CantidadGuias = origen.CantidadGuias,
            EsTravesia = origen.EsTravesia,
            EsAMedida = false,          // se cotiza como cualquier otra: así Rentabilidad da bien
            EsPersonalizada = true,     // para mostrarla aparte y poder repetirla más adelante
            Activa = true,
            GuiaBreve = origen.GuiaBreve,
            Recomendaciones = origen.Recomendaciones,
            LugaresVisitar = origen.LugaresVisitar
        };
        _db.Excursiones.Add(nueva);
        await _db.SaveChangesAsync();   // necesito el Id para colgarle costos y etapas

        // 2) Los costos de la excursión de base, tal cual
        var gastos = await _db.GastosExcursion.Where(g => g.ExcursionId == BaseId).ToListAsync();
        foreach (var g in gastos)
            _db.GastosExcursion.Add(new GastoExcursion
            {
                ExcursionId = nueva.Id, Nombre = g.Nombre, Precio = g.Precio,
                TipoCalculo = g.TipoCalculo, Cantidad = g.Cantidad,
                Comentario = g.Comentario, EsProveedor = g.EsProveedor
            });

        // 3) Las etapas, si la de base es una travesía
        var etapas = await _db.EtapasExcursion.Where(x => x.ExcursionId == BaseId).ToListAsync();
        foreach (var x in etapas)
            _db.EtapasExcursion.Add(new EtapaExcursion
            {
                ExcursionId = nueva.Id, Orden = x.Orden, Tipo = x.Tipo, Lugar = x.Lugar,
                ProveedorId = x.ProveedorId, Noches = x.Noches,
                PrecioPorPersona = x.PrecioPorPersona, Cantidad = x.Cantidad, Incluye = x.Incluye
            });

        // 4) Los días que se agregan: una tanda de renglones en cero por cada uno
        for (int d = 1; d <= dias; d++)
            foreach (var (nom, tipo) in RenglonesDelDia)
                _db.GastosExcursion.Add(new GastoExcursion
                {
                    ExcursionId = nueva.Id,
                    Nombre = $"Día extra {d} · {nom}",
                    Precio = 0,
                    TipoCalculo = tipo,
                    Comentario = "Día agregado al viaje a medida. Poné el precio y, si no va, borralo."
                });

        await _db.SaveChangesAsync();

        // Se abre directo la excursión nueva, para completar los precios de los días extra.
        // Va con aviso: el sistema no cuenta días, así que hay cosas que se suben a mano.
        return RedirectToPage("/Excursiones/Cargar",
            new { id = nueva.Id, aMedida = true, dias = dias, @base = origen.Nombre });
    }
}

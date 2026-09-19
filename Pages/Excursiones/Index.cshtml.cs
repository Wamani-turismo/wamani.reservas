using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;
using Wamani.Reservas.Services;

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

    // Tres grupos, cada uno con su lógica: el catálogo de siempre, los viajes armados para
    // un grupo puntual, y La Combi (salidas regulares en traffic, vendidas por butaca).
    // Una combi nunca es "a medida": si alguna vez tuviera las dos marcas, manda la combi.
    public List<Excursion> Combis => Excursiones.Where(e => e.EsCombi).ToList();
    public List<Excursion> DelCatalogo =>
        Excursiones.Where(e => !e.EsCombi && !EsDeAMedida(e)).ToList();
    public List<Excursion> AMedida =>
        Excursiones.Where(e => !e.EsCombi && EsDeAMedida(e)).ToList();

    [BindProperty(SupportsGet = true)]
    public string? Aviso { get; set; }

    // Para cada salida de La Combi: cuántas butacas hay que vender para no perder plata,
    // y cuánto sale la salida vacía. Es LO que hay que mirar en este producto, porque la
    // traffic y la guía se pagan igual vaya la gente que vaya.
    public class NumerosCombi
    {
        public int ButacasParaEmpatar { get; set; }   // 0 = no cierra ni con la combi llena
        public decimal CostoSalidaVacia { get; set; } // lo fijo: traffic, guía y sus viáticos
        public decimal CostoPorButaca { get; set; }   // lo que suma cada pasajero (snacks, seguro)
        public decimal GananciaLlena { get; set; }
    }
    public Dictionary<int, NumerosCombi> Numeros { get; set; } = new();

    public async Task OnGetAsync()
    {
        Excursiones = await _db.Excursiones
            .OrderBy(e => e.Nombre)
            .ToListAsync();

        // Colaborador con acceso limitado: ve SÓLO sus excursiones, con sus precios.
        // Las demás no aparecen en la lista.
        var permitidas = Wamani.Reservas.Services.Permisos.Excursiones(User);
        if (permitidas.Count > 0)
            Excursiones = Excursiones.Where(e => permitidas.Contains(e.Id)).ToList();

        await CargarNumerosCombiAsync();
    }

    // Las cuentas de La Combi. Se calculan sólo para las combis: el resto de la lista no
    // las usa y no tiene sentido leer toda la base para nada.
    private async Task CargarNumerosCombiAsync()
    {
        var combis = Combis;
        if (combis.Count == 0) return;

        var ids = combis.Select(e => e.Id).ToList();
        var gastos = (await _db.GastosExcursion.Where(g => ids.Contains(g.ExcursionId)).ToListAsync())
            .GroupBy(g => g.ExcursionId).ToDictionary(g => g.Key, g => g.ToList());
        var etapas = (await _db.EtapasExcursion.Where(x => ids.Contains(x.ExcursionId)).ToListAsync())
            .GroupBy(x => x.ExcursionId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var e in combis)
        {
            var gs = gastos.GetValueOrDefault(e.Id) ?? new();
            var ets = etapas.GetValueOrDefault(e.Id) ?? new();
            var lleno = e.MaximoPersonas > 0 ? e.MaximoPersonas : 19;

            // El costo de la salida vacía es lo fijo. Lo que agrega cada butaca sale de la
            // diferencia entre una persona y dos: así no hay que adivinar qué renglón es
            // fijo y cuál es por persona, lo dice la misma cuenta que usa todo el sistema.
            var vacia = RentabilidadCalc.Costo(gs, ets, 0);
            var porButaca = RentabilidadCalc.Costo(gs, ets, 2) - RentabilidadCalc.Costo(gs, ets, 1);

            Numeros[e.Id] = new NumerosCombi
            {
                ButacasParaEmpatar = RentabilidadCalc.PersonasParaEmpatar(e, gs, ets),
                CostoSalidaVacia = vacia,
                CostoPorButaca = porButaca,
                GananciaLlena = e.PrecioPorPersona * lleno - RentabilidadCalc.Costo(gs, ets, lleno)
            };
        }
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

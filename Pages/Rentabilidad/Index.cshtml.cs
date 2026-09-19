using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;
using Wamani.Reservas.Services;

namespace Wamani.Reservas.Pages.Rentabilidad;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public class Fila
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public decimal PrecioPorPersona { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public decimal GananciaMin { get; set; }
        public decimal GananciaMax { get; set; }
        public decimal MargenMax { get; set; }
        public bool SinCostos { get; set; }

        // Cuánta gente hace falta para no perder plata. 0 = no cierra ni lleno.
        public int ParaEmpatar { get; set; }
    }

    public List<Fila> Filas { get; set; } = new();

    // La lista se muestra en tres bloques, igual que en Excursiones: el catálogo de
    // siempre, La Combi (que se lee por butacas) y los viajes armados a medida. Mezclados
    // no se entiende nada: una combi al lado de una travesía de 5 días no se compara.
    public class Grupo
    {
        public string Titulo { get; set; } = "";
        public string Subtitulo { get; set; } = "";
        public List<Fila> Filas { get; set; } = new();
    }
    public List<Grupo> Grupos { get; set; } = new();

    public async Task OnGetAsync()
    {
        var excs = await _db.Excursiones.Where(e => e.Activa).OrderBy(e => e.Nombre).ToListAsync();

        // Colaborador con acceso limitado: sólo la rentabilidad de SUS excursiones
        var permitidas = Wamani.Reservas.Services.Permisos.Excursiones(User);
        if (permitidas.Count > 0)
            excs = excs.Where(e => permitidas.Contains(e.Id)).ToList();
        var gastos = (await _db.GastosExcursion.ToListAsync())
            .GroupBy(g => g.ExcursionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Las etapas (noches, traslados, pasajes, guía, arrieros, caballos) también son
        // costo: en una travesía son casi toda la plata. Sin esto la ganancia sale inflada.
        var etapas = (await _db.EtapasExcursion.ToListAsync())
            .GroupBy(e => e.ExcursionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var e in excs)
        {
            var items = gastos.GetValueOrDefault(e.Id) ?? new();
            var etps = etapas.GetValueOrDefault(e.Id) ?? new();
            var alMin = RentabilidadCalc.Calcular(e, items, etps, e.MinimoPersonas);
            var alMax = RentabilidadCalc.Calcular(e, items, etps, e.MaximoPersonas);
            Filas.Add(new Fila
            {
                Id = e.Id,
                Nombre = e.Nombre,
                PrecioPorPersona = e.PrecioPorPersona,
                Min = e.MinimoPersonas,
                Max = e.MaximoPersonas,
                GananciaMin = alMin.Ganancia,
                GananciaMax = alMax.Ganancia,
                MargenMax = alMax.MargenPct,
                SinCostos = items.Count == 0 && etps.Count == 0,
                ParaEmpatar = RentabilidadCalc.PersonasParaEmpatar(e, items, etps)
            });
        }

        // ---- Los tres bloques ----
        var porId = excs.ToDictionary(e => e.Id);
        bool EsCombi(Fila f) => porId[f.Id].EsCombi;
        bool EsAMedida(Fila f) => !EsCombi(f) &&
            (porId[f.Id].EsPersonalizada || porId[f.Id].EsAMedida);

        Grupos.Add(new Grupo
        {
            Titulo = "Excursiones y travesías",
            Subtitulo = "El catálogo de siempre.",
            Filas = Filas.Where(f => !EsCombi(f) && !EsAMedida(f)).ToList()
        });
        Grupos.Add(new Grupo
        {
            Titulo = "🚐 La Combi de Wamani",
            Subtitulo = "Se venden por butaca. Lo que manda es cuántas hace falta vender "
                      + "para empatar: la traffic y la guía se pagan igual vaya quien vaya.",
            Filas = Filas.Where(EsCombi).ToList()
        });
        Grupos.Add(new Grupo
        {
            Titulo = "Viajes a medida",
            Subtitulo = "Armados para un grupo puntual.",
            Filas = Filas.Where(EsAMedida).ToList()
        });
        Grupos.RemoveAll(g => g.Filas.Count == 0);
    }
}

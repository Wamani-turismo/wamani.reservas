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
    }

    public List<Fila> Filas { get; set; } = new();

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
                SinCostos = items.Count == 0 && etps.Count == 0
            });
        }
    }
}

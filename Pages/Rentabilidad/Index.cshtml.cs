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
        var gastos = (await _db.GastosExcursion.ToListAsync())
            .GroupBy(g => g.ExcursionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var e in excs)
        {
            var items = gastos.GetValueOrDefault(e.Id) ?? new();
            var alMin = RentabilidadCalc.Calcular(e, items, e.MinimoPersonas);
            var alMax = RentabilidadCalc.Calcular(e, items, e.MaximoPersonas);
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
                SinCostos = items.Count == 0
            });
        }
    }
}

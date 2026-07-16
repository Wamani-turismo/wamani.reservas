using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Operativo;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public class SalidaResumen
    {
        public int ExcursionId { get; set; }
        public string Excursion { get; set; } = "";
        public DateTime Fecha { get; set; }
        public int Pasajeros { get; set; }
        public int GastosTotal { get; set; }
        public int GastosListos { get; set; }
        public decimal Presupuesto { get; set; }
        public decimal DeudaProveedores { get; set; }
        public bool Completo => GastosTotal > 0 && GastosListos >= GastosTotal;
    }

    public List<SalidaResumen> Salidas { get; set; } = new();

    public async Task OnGetAsync()
    {
        var hoy = DateTime.Today;

        var reservas = await _db.Reservas
            .Where(r => r.FechaHasta.Date >= hoy)
            .ToListAsync();

        var plantillaPorExc = (await _db.GastosExcursion.ToListAsync())
            .GroupBy(g => g.ExcursionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var operativoPorSalida = (await _db.OperativoGastos.ToListAsync())
            .GroupBy(o => (o.ExcursionId, o.Fecha.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        var deudaProvPorSalida = (await _db.OperativoProveedores.ToListAsync())
            .GroupBy(o => (o.ExcursionId, o.Fecha.Date))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Pendiente()));

        Salidas = reservas
            .GroupBy(r => new { r.ExcursionId, r.Excursion, Fecha = r.FechaDesde.Date })
            .Select(g =>
            {
                var exId = g.Key.ExcursionId ?? 0;
                var res = new SalidaResumen
                {
                    ExcursionId = exId,
                    Excursion = g.Key.Excursion,
                    Fecha = g.Key.Fecha,
                    Pasajeros = g.Sum(r => r.CantidadPersonas)
                };

                res.DeudaProveedores = deudaProvPorSalida.TryGetValue((exId, g.Key.Fecha), out var dp) ? dp : 0m;

                if (operativoPorSalida.TryGetValue((exId, g.Key.Fecha), out var ops))
                {
                    res.GastosTotal = ops.Count;
                    res.GastosListos = ops.Count(o => o.Comprado);
                    res.Presupuesto = ops.Sum(o => o.Precio);
                }
                else if (plantillaPorExc.TryGetValue(exId, out var plant))
                {
                    res.GastosTotal = plant.Count;
                    res.GastosListos = 0;
                    res.Presupuesto = plant.Sum(p => p.Precio);
                }

                return res;
            })
            .OrderBy(s => s.Fecha)
            .ToList();
    }
}

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
    }
}

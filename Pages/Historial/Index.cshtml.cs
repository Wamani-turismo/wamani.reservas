using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;

namespace Wamani.Reservas.Pages.Historial;

// Registro histórico: una card por cada MES que tuvo salidas ya realizadas.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public class MesResumen
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string Clave => $"{Anio:0000}-{Mes:00}";   // yyyy-MM para el link
        public string Nombre { get; set; } = "";
        public int Salidas { get; set; }
        public int Pasajeros { get; set; }
        public decimal Facturado { get; set; }
        public decimal Cobrado { get; set; }
    }

    public List<MesResumen> Meses { get; set; } = new();

    public async Task OnGetAsync()
    {
        var hoy = DateTime.Today;
        var reservas = await _db.Reservas.ToListAsync();

        // Solo salidas que YA pasaron (el día de salida quedó atrás)
        var pasadas = reservas.Where(r => r.FechaDesde.Date < hoy).ToList();

        var ci = System.Globalization.CultureInfo.GetCultureInfo("es-AR");

        Meses = pasadas
            .GroupBy(r => new { r.FechaDesde.Year, r.FechaDesde.Month })
            .Select(g => new MesResumen
            {
                Anio = g.Key.Year,
                Mes = g.Key.Month,
                Nombre = ci.TextInfo.ToTitleCase(
                    new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy", ci)),
                // Cada salida = una excursión en un día puntual
                Salidas = g.Select(r => new { r.ExcursionId, Fecha = r.FechaDesde.Date }).Distinct().Count(),
                Pasajeros = g.Sum(r => r.CantidadPersonas),
                Facturado = g.Sum(r => r.TotalConDescuento()),
                Cobrado = g.Sum(r => r.Cobrado())
            })
            .OrderByDescending(m => m.Anio).ThenByDescending(m => m.Mes)
            .ToList();
    }
}

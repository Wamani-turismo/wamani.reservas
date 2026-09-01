using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Operativo;

// Todas las reservas de UNA excursión, agrupadas por fecha de salida.
// Pensada para las travesías, donde las fechas están fijadas de antemano y lo único
// que cambia es cuánta gente se va sumando a cada una.
// Un colaborador con acceso limitado entra acá sólo con el excursionId de las suyas:
// el candado de Program.cs verifica el permiso antes de que llegue la pantalla.
public class ReservasModel : PageModel
{
    private readonly AppDbContext _db;
    public ReservasModel(AppDbContext db) => _db = db;

    public int ExcursionId { get; set; }
    public string ExcursionNombre { get; set; } = "";
    public int MinimoPersonas { get; set; }

    public class Salida
    {
        public DateTime Fecha { get; set; }
        public List<Reserva> Reservas { get; set; } = new();
        public int Pasajeros => Reservas.Sum(r => r.CantidadPersonas);
        public decimal Facturado => Reservas.Sum(r => r.TotalConDescuento());
        public decimal Cobrado => Reservas.Sum(r => r.Cobrado());
        public decimal Pendiente => Reservas.Sum(r => r.Pendiente());
    }

    public List<Salida> Salidas { get; set; } = new();

    public int TotalPasajeros => Salidas.Sum(s => s.Pasajeros);

    public async Task<IActionResult> OnGetAsync(int excursionId)
    {
        var exc = await _db.Excursiones.AsNoTracking().FirstOrDefaultAsync(e => e.Id == excursionId);
        if (exc is null) return RedirectToPage("/Operativo/Index");

        ExcursionId     = exc.Id;
        ExcursionNombre = exc.Nombre;
        MinimoPersonas  = exc.MinimoPersonas;

        var reservas = await _db.Reservas.AsNoTracking()
            .Where(r => r.ExcursionId == excursionId)
            .ToListAsync();

        Salidas = reservas
            .GroupBy(r => r.FechaDesde.Date)
            .OrderBy(g => g.Key)
            .Select(g => new Salida
            {
                Fecha    = g.Key,
                Reservas = g.OrderBy(r => r.NombreCliente).ToList()
            })
            .ToList();

        return Page();
    }
}

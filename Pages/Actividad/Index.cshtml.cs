using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Actividad;

// Quién movió plata en el sistema. Sólo la ven los socios: el candado de
// Program.cs no tiene esta dirección en la lista de los accesos limitados.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Quien { get; set; }
    [BindProperty(SupportsGet = true)] public int Dias { get; set; } = 30;

    public List<Wamani.Reservas.Models.Actividad> Lineas { get; set; } = new();
    public List<string> Usuarios { get; set; } = new();

    public class ResumenPersona
    {
        public string Nombre { get; set; } = "";
        public decimal Ingresos { get; set; }
        public decimal Egresos { get; set; }
        public int Movimientos { get; set; }
        public decimal Neto => Ingresos - Egresos;
    }

    public List<ResumenPersona> Resumen { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (Dias < 1) Dias = 1;
        if (Dias > 3650) Dias = 3650;
        var desde = DateTime.Today.AddDays(-Dias);

        var q = _db.Actividades.AsNoTracking().Where(a => a.Fecha >= desde);

        Usuarios = await _db.Actividades.AsNoTracking()
            .Select(a => a.Nombre).Distinct().OrderBy(n => n).ToListAsync();

        if (!string.IsNullOrWhiteSpace(Quien))
            q = q.Where(a => a.Nombre == Quien);

        Lineas = await q.OrderByDescending(a => a.Fecha).Take(400).ToListAsync();

        Resumen = Lineas
            .GroupBy(a => a.Nombre)
            .Select(g => new ResumenPersona
            {
                Nombre      = g.Key,
                Ingresos    = g.Where(x => x.EsIngreso).Sum(x => x.Monto),
                Egresos     = g.Where(x => !x.EsIngreso).Sum(x => x.Monto),
                Movimientos = g.Count()
            })
            .OrderByDescending(r => r.Movimientos)
            .ToList();
    }
}

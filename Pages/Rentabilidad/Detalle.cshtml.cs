using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Rentabilidad;

public class DetalleModel : PageModel
{
    private readonly AppDbContext _db;
    public DetalleModel(AppDbContext db) => _db = db;

    public Excursion Excursion { get; set; } = new();
    public List<GastoExcursion> Items { get; set; } = new();
    public int PersonasPorAuto => Excursion.PersonasPorAuto;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var e = await _db.Excursiones.FindAsync(id);
        if (e is null) return RedirectToPage("/Rentabilidad/Index");
        Excursion = e;
        Items = await _db.GastosExcursion
            .Where(g => g.ExcursionId == id)
            .OrderBy(g => g.Id)
            .ToListAsync();
        return Page();
    }
}

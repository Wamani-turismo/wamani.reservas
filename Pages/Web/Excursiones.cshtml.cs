using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Web;

public class ExcursionesModel : PageModel
{
    private readonly AppDbContext _db;
    public ExcursionesModel(AppDbContext db) => _db = db;

    public List<ExcursionWeb> Lista { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        Lista = await _db.ExcursionesWeb.OrderBy(e => e.Orden).ThenBy(e => e.Nombre).ToListAsync();
    }

    public async Task<IActionResult> OnPostActivarAsync(int id)
    {
        var e = await _db.ExcursionesWeb.FindAsync(id);
        if (e is not null) { e.Activa = !e.Activa; await _db.SaveChangesAsync(); }
        return RedirectToPage(new { Aviso = "Listo." });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var e = await _db.ExcursionesWeb.FindAsync(id);
        if (e is not null) { _db.ExcursionesWeb.Remove(e); await _db.SaveChangesAsync(); }
        return RedirectToPage(new { Aviso = "Excursión eliminada." });
    }
}

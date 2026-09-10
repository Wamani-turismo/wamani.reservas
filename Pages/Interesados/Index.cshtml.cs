using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Interesados;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Interesado> Lista { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        Lista = await _db.Interesados
            .OrderBy(i => i.FechaDesde)
            .ThenBy(i => i.Id)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var i = await _db.Interesados.FindAsync(id);
        if (i is not null)
        {
            _db.Interesados.Remove(i);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("/Interesados/Index", new { Aviso = "Interesado eliminado." });
    }
}

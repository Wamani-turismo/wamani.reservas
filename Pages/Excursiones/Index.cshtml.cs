using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Excursiones;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Excursion> Excursiones { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        Excursiones = await _db.Excursiones
            .OrderBy(e => e.Nombre)
            .ToListAsync();
    }
}

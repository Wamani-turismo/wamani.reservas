using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Proveedores;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Proveedor> Todos { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        Todos = await _db.Proveedores
            .OrderBy(p => p.Tipo).ThenBy(p => p.Nombre)
            .ToListAsync();
    }
}

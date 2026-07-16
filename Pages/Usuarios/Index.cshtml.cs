using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Usuarios;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Usuario> Lista { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        Lista = await _db.Usuarios.OrderBy(u => u.Nombre).ToListAsync();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        // No dejar borrar el último usuario activo (para no quedar afuera)
        var activos = await _db.Usuarios.CountAsync(u => u.Activo);
        var u = await _db.Usuarios.FindAsync(id);
        if (u is not null)
        {
            if (u.Activo && activos <= 1)
                return RedirectToPage("/Usuarios/Index", new { Aviso = "No podés borrar el único usuario activo." });
            _db.Usuarios.Remove(u);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("/Usuarios/Index", new { Aviso = "Usuario eliminado." });
    }
}

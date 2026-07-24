using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;
using Wamani.Reservas.Services;

namespace Wamani.Reservas.Pages.Web;

public class EquipoModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public EquipoModel(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public List<IntegranteWeb> Lista { get; set; } = new();

    [BindProperty] public IntegranteWeb Item { get; set; } = new();
    [BindProperty] public IFormFile? FotoArchivo { get; set; }

    [BindProperty(SupportsGet = true)] public string? Aviso { get; set; }

    public async Task OnGetAsync(int? editar)
    {
        Lista = await _db.IntegrantesWeb.OrderBy(i => i.Orden).ToListAsync();
        if (editar is not null)
            Item = await _db.IntegrantesWeb.FindAsync(editar.Value) ?? new IntegranteWeb();
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        var foto = await FotosWeb.Guardar(FotoArchivo, null, _env);
        if (Item.Id == 0)
        {
            if (!string.IsNullOrEmpty(foto)) Item.Foto = foto;
            _db.IntegrantesWeb.Add(Item);
        }
        else
        {
            var a = await _db.IntegrantesWeb.FindAsync(Item.Id);
            if (a is not null)
            {
                a.Nombre = Item.Nombre; a.Rol = Item.Rol; a.Bio = Item.Bio; a.Orden = Item.Orden;
                if (!string.IsNullOrEmpty(foto)) a.Foto = foto;
            }
        }
        await _db.SaveChangesAsync();
        return RedirectToPage(new { Aviso = "Guardado." });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var a = await _db.IntegrantesWeb.FindAsync(id);
        if (a is not null) { _db.IntegrantesWeb.Remove(a); await _db.SaveChangesAsync(); }
        return RedirectToPage(new { Aviso = "Eliminado." });
    }
}

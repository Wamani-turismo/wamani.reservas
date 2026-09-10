using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Web;

public class TestimoniosModel : PageModel
{
    private readonly AppDbContext _db;
    public TestimoniosModel(AppDbContext db) => _db = db;

    public List<TestimonioWeb> Lista { get; set; } = new();

    // Para el formulario (nuevo o editar)
    [BindProperty] public TestimonioWeb Item { get; set; } = new();

    [BindProperty(SupportsGet = true)] public string? Aviso { get; set; }

    public async Task OnGetAsync(int? editar)
    {
        Lista = await _db.TestimoniosWeb.OrderBy(t => t.Orden).ToListAsync();
        if (editar is not null)
            Item = await _db.TestimoniosWeb.FindAsync(editar.Value) ?? new TestimonioWeb();
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        if (Item.Id == 0) _db.TestimoniosWeb.Add(Item);
        else
        {
            var a = await _db.TestimoniosWeb.FindAsync(Item.Id);
            if (a is not null)
            {
                a.Texto = Item.Texto; a.Nombre = Item.Nombre; a.Lugar = Item.Lugar;
                a.Orden = Item.Orden; a.Activo = Item.Activo;
            }
        }
        await _db.SaveChangesAsync();
        return RedirectToPage(new { Aviso = "Guardado." });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var a = await _db.TestimoniosWeb.FindAsync(id);
        if (a is not null) { _db.TestimoniosWeb.Remove(a); await _db.SaveChangesAsync(); }
        return RedirectToPage(new { Aviso = "Eliminado." });
    }
}

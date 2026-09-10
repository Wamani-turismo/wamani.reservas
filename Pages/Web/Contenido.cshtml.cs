using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Web;

public class ContenidoModel : PageModel
{
    private readonly AppDbContext _db;
    public ContenidoModel(AppDbContext db) => _db = db;

    [BindProperty]
    public ContenidoWeb Datos { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public bool Guardado { get; set; }

    public void OnGet()
    {
        Datos = _db.ContenidoWeb.FirstOrDefault() ?? new ContenidoWeb();
    }

    public IActionResult OnPost()
    {
        var actual = _db.ContenidoWeb.FirstOrDefault();
        if (actual is null)
        {
            _db.ContenidoWeb.Add(Datos);
        }
        else
        {
            actual.Whatsapp = (Datos.Whatsapp ?? "").Trim();
            actual.Instagram = (Datos.Instagram ?? "").Trim().TrimStart('@');
            actual.Facebook = (Datos.Facebook ?? "").Trim();
            actual.Linktree = (Datos.Linktree ?? "").Trim();
            actual.Email = (Datos.Email ?? "").Trim();
            actual.Ubicacion = (Datos.Ubicacion ?? "").Trim();
            actual.HeroTexto = Datos.HeroTexto ?? "";
            actual.Quienes1 = Datos.Quienes1 ?? "";
            actual.Quienes2 = Datos.Quienes2 ?? "";
            actual.Quienes3 = Datos.Quienes3 ?? "";
        }
        _db.SaveChanges();
        return RedirectToPage(new { Guardado = true });
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Consultas;

// Las consultas que deja la gente con el formulario de la web.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<ConsultaWeb> Lista { get; set; } = new();
    public int SinAtender { get; set; }

    // ¿Está configurado el envío de mails? Si no, se avisa en la pantalla: las consultas
    // se guardan igual, pero nadie recibe el aviso.
    public bool MailConfigurado => Wamani.Reservas.Services.Correo.Configurado;
    public string MailDestino => Wamani.Reservas.Services.Correo.Destino;

    public async Task OnGetAsync()
    {
        Lista = await _db.ConsultasWeb
            .OrderByDescending(c => c.CreadaEl)
            .Take(300)
            .ToListAsync();
        SinAtender = Lista.Count(c => !c.Atendida);
    }

    // Tildar / destildar "ya la contesté"
    public async Task<IActionResult> OnPostAtenderAsync(int id)
    {
        var c = await _db.ConsultasWeb.FindAsync(id);
        if (c is not null)
        {
            c.Atendida = !c.Atendida;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostBorrarAsync(int id)
    {
        var c = await _db.ConsultasWeb.FindAsync(id);
        if (c is not null)
        {
            _db.ConsultasWeb.Remove(c);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}

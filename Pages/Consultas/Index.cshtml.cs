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
    private readonly IWebHostEnvironment _env;
    public IndexModel(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

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

    // Bajar el archivo que adjuntó la persona. Va por acá y no como archivo suelto en una
    // dirección pública, porque estos los sube cualquiera desde la web: así hay que tener
    // la sesión iniciada para verlos.
    public async Task<IActionResult> OnGetArchivoAsync(int id)
    {
        var c = await _db.ConsultasWeb.FindAsync(id);
        if (c is null || !c.TieneArchivo) return NotFound();

        var ruta = Wamani.Reservas.Services.AdjuntosConsulta.Ruta(_env, c.ArchivoGuardado);
        if (ruta is null || !System.IO.File.Exists(ruta)) return NotFound();

        var nombre = string.IsNullOrWhiteSpace(c.ArchivoNombre) ? c.ArchivoGuardado! : c.ArchivoNombre!;
        return PhysicalFile(ruta, "application/octet-stream", nombre);
    }

    public async Task<IActionResult> OnPostBorrarAsync(int id)
    {
        var c = await _db.ConsultasWeb.FindAsync(id);
        if (c is not null)
        {
            // Que no queden archivos sueltos ocupando el disco
            if (c.TieneArchivo)
            {
                var ruta = Wamani.Reservas.Services.AdjuntosConsulta.Ruta(_env, c.ArchivoGuardado);
                try { if (ruta is not null && System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta); }
                catch { /* si no se puede borrar el archivo, igual se borra la consulta */ }
            }
            _db.ConsultasWeb.Remove(c);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}

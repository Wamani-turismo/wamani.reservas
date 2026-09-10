using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;
using Wamani.Reservas.Services;

namespace Wamani.Reservas.Pages.Web;

public class VideosModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public VideosModel(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public List<VideoWeb> Lista { get; set; } = new();

    [BindProperty] public VideoWeb Item { get; set; } = new();
    [BindProperty] public IFormFile? VideoArchivo { get; set; }
    [BindProperty] public IFormFile? PosterArchivo { get; set; }

    [BindProperty(SupportsGet = true)] public string? Aviso { get; set; }

    public async Task OnGetAsync(int? editar)
    {
        Lista = await _db.VideosWeb.OrderBy(v => v.Orden).ToListAsync();
        if (editar is not null)
            Item = await _db.VideosWeb.FindAsync(editar.Value) ?? new VideoWeb();
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        var video = await FotosWeb.Guardar(VideoArchivo, null, _env);
        var poster = await FotosWeb.Guardar(PosterArchivo, null, _env);

        if (Item.Id == 0)
        {
            if (!string.IsNullOrEmpty(video)) Item.Archivo = video;
            if (!string.IsNullOrEmpty(poster)) Item.Poster = poster;
            _db.VideosWeb.Add(Item);
        }
        else
        {
            var a = await _db.VideosWeb.FindAsync(Item.Id);
            if (a is not null)
            {
                a.Titulo = Item.Titulo; a.Descripcion = Item.Descripcion;
                a.Vertical = Item.Vertical; a.Orden = Item.Orden; a.Activo = Item.Activo;
                if (!string.IsNullOrEmpty(video)) a.Archivo = video;
                if (!string.IsNullOrEmpty(poster)) a.Poster = poster;
            }
        }
        await _db.SaveChangesAsync();
        return RedirectToPage(new { Aviso = "Guardado." });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var a = await _db.VideosWeb.FindAsync(id);
        if (a is not null) { _db.VideosWeb.Remove(a); await _db.SaveChangesAsync(); }
        return RedirectToPage(new { Aviso = "Eliminado." });
    }
}

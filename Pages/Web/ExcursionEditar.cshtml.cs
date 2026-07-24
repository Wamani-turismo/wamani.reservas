using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;
using Wamani.Reservas.Services;

namespace Wamani.Reservas.Pages.Web;

public class ExcursionEditarModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public ExcursionEditarModel(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    [BindProperty]
    public ExcursionWeb Ex { get; set; } = new();

    [BindProperty]
    public IFormFile? FotoArchivo { get; set; }

    public bool EsNueva => Ex.Id == 0;

    public IActionResult OnGet(int? id)
    {
        if (id is not null)
        {
            var e = _db.ExcursionesWeb.Find(id.Value);
            if (e is null) return RedirectToPage("/Web/Excursiones");
            Ex = e;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Clave automática a partir del nombre si viene vacía
        if (string.IsNullOrWhiteSpace(Ex.Clave))
            Ex.Clave = Slug(Ex.Nombre);

        var foto = await FotosWeb.Guardar(FotoArchivo, null, _env);

        if (Ex.Id == 0)
        {
            if (!string.IsNullOrEmpty(foto)) Ex.Foto = foto;
            _db.ExcursionesWeb.Add(Ex);
        }
        else
        {
            var actual = _db.ExcursionesWeb.Find(Ex.Id);
            if (actual is null) return RedirectToPage("/Web/Excursiones");
            actual.Clave = Ex.Clave;
            actual.Nombre = Ex.Nombre;
            actual.Chip = Ex.Chip;
            actual.EsTravesia = Ex.EsTravesia;
            actual.Color = Ex.Color;
            actual.Resumen = Ex.Resumen;
            actual.Datos = Ex.Datos;
            actual.Itinerario = Ex.Itinerario;
            actual.Incluye = Ex.Incluye;
            actual.Llevar = Ex.Llevar;
            actual.Orden = Ex.Orden;
            actual.Activa = Ex.Activa;
            if (!string.IsNullOrEmpty(foto)) actual.Foto = foto;   // solo si subieron una nueva
        }
        await _db.SaveChangesAsync();
        return RedirectToPage("/Web/Excursiones", new { Aviso = "Excursión guardada." });
    }

    // Convierte "Termas de Jordán" -> "termas-de-jordan"
    private static string Slug(string s)
    {
        s = (s ?? "").ToLowerInvariant().Trim();
        var sb = new System.Text.StringBuilder();
        foreach (var ch in s)
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(ch);
            else if (ch is 'á' or 'à' or 'ä') sb.Append('a');
            else if (ch is 'é' or 'è' or 'ë') sb.Append('e');
            else if (ch is 'í' or 'ì' or 'ï') sb.Append('i');
            else if (ch is 'ó' or 'ò' or 'ö') sb.Append('o');
            else if (ch is 'ú' or 'ù' or 'ü') sb.Append('u');
            else if (ch == 'ñ') sb.Append('n');
            else if (char.IsWhiteSpace(ch) || ch == '-') sb.Append('-');
        }
        var r = sb.ToString();
        while (r.Contains("--")) r = r.Replace("--", "-");
        return r.Trim('-');
    }
}

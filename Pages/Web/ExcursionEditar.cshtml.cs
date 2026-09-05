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

    // Fotos adicionales para la galería (se pueden subir varias a la vez)
    [BindProperty]
    public List<IFormFile>? FotosNuevas { get; set; }

    // Nombres de fotos adicionales que se marcaron para quitar
    [BindProperty]
    public List<string>? Quitar { get; set; }

    public bool EsNueva => Ex.Id == 0;

    // Lista de fotos adicionales ya guardadas (para mostrarlas en el form)
    public List<string> FotosExtra => (Ex.Fotos ?? "")
        .Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

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
        // Un campo de texto que se deja EN BLANCO llega como null (así bindea ASP.NET),
        // y las columnas de la tabla no aceptan null: sin esto, guardar una excursión con
        // cualquier campo vacío rompía con un error 500. Pasaba sobre todo al crear una
        // nueva, que es cuando más campos quedan sin llenar.
        static string T(string? s) => s ?? "";
        Ex.Clave      = T(Ex.Clave);
        Ex.Nombre     = T(Ex.Nombre);
        Ex.Chip       = T(Ex.Chip);
        Ex.Color      = T(Ex.Color);
        Ex.Foto       = T(Ex.Foto);
        Ex.Fotos      = T(Ex.Fotos);
        Ex.Resumen    = T(Ex.Resumen);
        Ex.Datos      = T(Ex.Datos);
        Ex.Itinerario = T(Ex.Itinerario);
        Ex.Incluye    = T(Ex.Incluye);
        Ex.Llevar     = T(Ex.Llevar);

        // Sin color la tarjeta de la web queda sin su tinte: se pone el verde de siempre.
        if (string.IsNullOrWhiteSpace(Ex.Color)) Ex.Color = "58,110,90";

        // El nombre es lo único imprescindible: de ahí sale la clave.
        if (string.IsNullOrWhiteSpace(Ex.Nombre))
        {
            ModelState.AddModelError("Ex.Nombre", "Poné el nombre de la excursión.");
            return Page();
        }

        // Clave automática a partir del nombre si viene vacía
        if (string.IsNullOrWhiteSpace(Ex.Clave))
            Ex.Clave = Slug(Ex.Nombre);

        var foto = await FotosWeb.Guardar(FotoArchivo, null, _env);

        // Guardar las fotos adicionales que subieron ahora
        var subidas = new List<string>();
        if (FotosNuevas != null)
            foreach (var f in FotosNuevas)
            {
                var n = await FotosWeb.Guardar(f, null, _env);
                if (!string.IsNullOrEmpty(n)) subidas.Add(n);
            }

        if (Ex.Id == 0)
        {
            if (!string.IsNullOrEmpty(foto)) Ex.Foto = foto;
            Ex.Fotos = string.Join("\n", subidas);
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
            actual.EsSelecta = Ex.EsSelecta;
            actual.Color = Ex.Color;
            actual.Resumen = Ex.Resumen;
            actual.Datos = Ex.Datos;
            actual.Itinerario = Ex.Itinerario;
            actual.Incluye = Ex.Incluye;
            actual.Llevar = Ex.Llevar;
            actual.Orden = Ex.Orden;
            actual.Activa = Ex.Activa;
            if (!string.IsNullOrEmpty(foto)) actual.Foto = foto;   // solo si subieron una nueva

            // Fotos adicionales: quitar las marcadas y sumar las nuevas
            var lista = (actual.Fotos ?? "")
                .Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (Quitar != null) lista = lista.Where(x => !Quitar.Contains(x)).ToList();
            lista.AddRange(subidas);
            actual.Fotos = string.Join("\n", lista);
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

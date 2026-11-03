using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Alias;

// Libreta de alias: a quién le transferimos, con qué alias, qué hace y dónde queda.
// Está agrupada por región para que no sea un choclo. No toca costos ni salidas.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<AliasProveedor> Todos { get; set; } = new();

    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }

    [BindProperty] public AliasProveedor Nuevo { get; set; } = new();

    [TempData] public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        var q = _db.AliasProveedores.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = Buscar.Trim();
            q = q.Where(a => a.Nombre.Contains(b)
                || (a.Alias != null && a.Alias.Contains(b))
                || (a.Tipo != null && a.Tipo.Contains(b))
                || (a.Lugar != null && a.Lugar.Contains(b))
                || (a.Titular != null && a.Titular.Contains(b)));
        }
        // Ordenado por lo que hace (guía, hospedaje, entradas…) y dentro de cada
        // actividad por nombre. Los que no tienen actividad cargada van al final.
        Todos = await q
            .OrderBy(a => a.Tipo == null ? 1 : 0)
            .ThenBy(a => a.Tipo)
            .ThenBy(a => a.Nombre)
            .ToListAsync();
    }

    private static string RegionValida(string? r)
        => AliasProveedor.Regiones.Contains(r) ? r! : "Otro";

    private static string? Limpio(string? t)
        => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    public async Task<IActionResult> OnPostAgregarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nuevo.Nombre))
        {
            Aviso = "Poné al menos el nombre.";
            return RedirectToPage();
        }
        _db.AliasProveedores.Add(new AliasProveedor
        {
            Region = RegionValida(Nuevo.Region),
            Nombre = Nuevo.Nombre.Trim(),
            Tipo = Limpio(Nuevo.Tipo),
            Alias = Limpio(Nuevo.Alias),
            Entidad = Limpio(Nuevo.Entidad),
            Titular = Limpio(Nuevo.Titular),
            Telefono = Limpio(Nuevo.Telefono),
            Lugar = Limpio(Nuevo.Lugar),
            Nota = Limpio(Nuevo.Nota)
        });
        await _db.SaveChangesAsync();
        Aviso = "Proveedor agregado ✔";
        return RedirectToPage();
    }

    // Guardar los cambios de una fila ya cargada (el botón ✏️).
    public async Task<IActionResult> OnPostEditarAsync(int id, string? region, string? nombre,
        string? tipo, string? alias, string? entidad, string? titular, string? telefono, string? lugar)
    {
        var a = await _db.AliasProveedores.FindAsync(id);
        if (a is not null && !string.IsNullOrWhiteSpace(nombre))
        {
            a.Region = RegionValida(region);
            a.Nombre = nombre.Trim();
            a.Tipo = Limpio(tipo);
            a.Alias = Limpio(alias);
            a.Entidad = Limpio(entidad);
            a.Titular = Limpio(titular);
            a.Telefono = Limpio(telefono);
            a.Lugar = Limpio(lugar);
            await _db.SaveChangesAsync();
            Aviso = "Cambios guardados ✔";
        }
        return RedirectToPage();
    }

    // La nota de cada proveedor (el botón 📋). Se guarda sola, no toca nada más.
    public async Task<IActionResult> OnPostNotaAsync(int id, string? nota)
    {
        var a = await _db.AliasProveedores.FindAsync(id);
        if (a is not null)
        {
            a.Nota = Limpio(nota);
            await _db.SaveChangesAsync();
            Aviso = a.Nota is null ? "Nota borrada." : "Nota guardada.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var a = await _db.AliasProveedores.FindAsync(id);
        if (a is not null)
        {
            _db.AliasProveedores.Remove(a);
            await _db.SaveChangesAsync();
            Aviso = "Proveedor borrado.";
        }
        return RedirectToPage();
    }
}

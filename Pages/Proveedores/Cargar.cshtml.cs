using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Proveedores;

public class CargarModel : PageModel
{
    private readonly AppDbContext _db;
    public CargarModel(AppDbContext db) => _db = db;

    [BindProperty]
    public Proveedor Proveedor { get; set; } = new();

    public bool EsNuevo => Proveedor.Id == 0;

    public IActionResult OnGet(int? id, string? tipo)
    {
        if (id is null)
        {
            Proveedor = new Proveedor { Activo = true, Tipo = tipo ?? "Guía" };
            return Page();
        }
        var existente = _db.Proveedores.Find(id);
        if (existente is null) return RedirectToPage("/Proveedores/Index");
        Proveedor = existente;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (Proveedor.Id == 0)
        {
            _db.Proveedores.Add(Proveedor);
        }
        else
        {
            var db = await _db.Proveedores.FindAsync(Proveedor.Id);
            if (db is null) return RedirectToPage("/Proveedores/Index");
            db.Tipo = Proveedor.Tipo;
            db.Nombre = Proveedor.Nombre;
            db.Contacto = Proveedor.Contacto;
            db.Precio = Proveedor.Precio;
            db.Activo = Proveedor.Activo;
        }
        await _db.SaveChangesAsync();
        return RedirectToPage("/Proveedores/Index", new { Aviso = "Proveedor guardado ✔" });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var p = await _db.Proveedores.FindAsync(id);
        if (p is not null)
        {
            bool enUso = _db.OperativoProveedores.Any(x => x.ProveedorId == id);
            if (enUso)
            {
                p.Activo = false;
                await _db.SaveChangesAsync();
                return RedirectToPage("/Proveedores/Index",
                    new { Aviso = "El proveedor ya se usó en salidas, así que se ocultó (no se borró)." });
            }
            _db.Proveedores.Remove(p);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("/Proveedores/Index", new { Aviso = "Proveedor eliminado." });
    }
}

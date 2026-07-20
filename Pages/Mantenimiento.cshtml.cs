using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;

namespace Wamani.Reservas.Pages;

// Página para "empezar de cero" con las reservas: borra todas las reservas y los
// datos que dependen de ellas (pasajeros, gastos/proveedores del operativo,
// interesados). NO toca el catálogo (excursiones, proveedores, usuarios).
public class MantenimientoModel : PageModel
{
    private readonly AppDbContext _db;
    public MantenimientoModel(AppDbContext db) => _db = db;

    public int Reservas { get; set; }
    public int Pasajeros { get; set; }
    public int GastosOperativo { get; set; }
    public int ProveedoresOperativo { get; set; }
    public int Interesados { get; set; }

    [BindProperty] public string? Confirmacion { get; set; }

    [TempData] public string? Aviso { get; set; }

    public async Task OnGetAsync() => await ContarAsync();

    private async Task ContarAsync()
    {
        Reservas = await _db.Reservas.CountAsync();
        Pasajeros = await _db.Pasajeros.CountAsync();
        GastosOperativo = await _db.OperativoGastos.CountAsync();
        ProveedoresOperativo = await _db.OperativoProveedores.CountAsync();
        Interesados = await _db.Interesados.CountAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!string.Equals((Confirmacion ?? "").Trim(), "BORRAR", StringComparison.OrdinalIgnoreCase))
        {
            await ContarAsync();
            ModelState.AddModelError("", "Para confirmar tenés que escribir BORRAR en el casillero.");
            return Page();
        }

        // Borra todo lo relacionado a reservas. El catálogo (excursiones, plantillas de
        // gastos, proveedores, usuarios) queda intacto.
        await _db.OperativoProveedores.ExecuteDeleteAsync();
        await _db.OperativoGastos.ExecuteDeleteAsync();
        await _db.OperativoSalidas.ExecuteDeleteAsync();
        await _db.Pasajeros.ExecuteDeleteAsync();
        await _db.Interesados.ExecuteDeleteAsync();
        await _db.Reservas.ExecuteDeleteAsync();

        Aviso = "Listo. Se borraron todas las reservas y sus datos. Ya podés cargarlas de nuevo desde cero.";
        return RedirectToPage();
    }
}

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
    public int GastosProveedor { get; set; }   // gastos que en realidad son proveedores

    // Nombres de gastos que en realidad son proveedores (se manejan con seña + saldo).
    private static readonly HashSet<string> NombresProveedor = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto", "chofer", "guia", "guía", "hospedaje", "restaurante", "cena"
    };
    private static bool EsGastoProveedor(string? nombre)
        => NombresProveedor.Contains((nombre ?? "").Trim());

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
        GastosProveedor = (await _db.GastosExcursion.ToListAsync())
            .Count(g => EsGastoProveedor(g.Nombre) && !g.EsProveedor);
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

    // Marca como "es proveedor" los gastos de las excursiones que en realidad son proveedores
    // (Auto, Chofer, Guía, Hospedaje, Restaurante, Cena). Así SIGUEN contando en la Rentabilidad,
    // pero dejan de aparecer en la lista del operativo (se pagan en la sección Proveedores).
    public async Task<IActionResult> OnPostLimpiarGastosAsync()
    {
        var plantilla = await _db.GastosExcursion.ToListAsync();
        int marcados = 0;
        foreach (var g in plantilla.Where(g => EsGastoProveedor(g.Nombre) && !g.EsProveedor))
        {
            g.EsProveedor = true;
            marcados++;
        }

        // Sacarlos de los operativos ya cargados (para que desaparezcan de las salidas abiertas)
        var ops = await _db.OperativoGastos.ToListAsync();
        var aBorrarOps = ops.Where(o => EsGastoProveedor(o.Nombre)).ToList();
        _db.OperativoGastos.RemoveRange(aBorrarOps);

        await _db.SaveChangesAsync();

        Aviso = $"Listo. Se marcaron {marcados} costo(s) como proveedor (siguen contando en Rentabilidad) y se sacaron {aBorrarOps.Count} de las salidas ya abiertas.";
        return RedirectToPage();
    }
}

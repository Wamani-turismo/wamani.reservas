using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Interesados;

public class CargarModel : PageModel
{
    private readonly AppDbContext _db;
    public CargarModel(AppDbContext db) => _db = db;

    [BindProperty]
    public Interesado Interesado { get; set; } = new();

    public List<Excursion> Excursiones { get; set; } = new();

    public bool EsNuevo => Interesado.Id == 0;

    private async Task CargarExcursionesAsync()
    {
        Excursiones = await _db.Excursiones
            .Where(e => e.Activa || Interesado.ExcursionId == e.Id)
            .OrderBy(e => e.Nombre)
            .ToListAsync();
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            Interesado = new Interesado { FechaDesde = DateTime.Today, FechaHasta = DateTime.Today };
        }
        else
        {
            var existente = await _db.Interesados.FindAsync(id);
            if (existente is null) return RedirectToPage("/Interesados/Index");
            Interesado = existente;
        }
        await CargarExcursionesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var exc = Interesado.ExcursionId is null
            ? null
            : await _db.Excursiones.FindAsync(Interesado.ExcursionId);

        if (exc is null)
            ModelState.AddModelError("Interesado.ExcursionId", "Elegí una excursión de la lista.");

        if (Interesado.FechaHasta < Interesado.FechaDesde)
            ModelState.AddModelError("Interesado.FechaHasta", "La fecha 'hasta' no puede ser anterior a la 'desde'.");

        if (!ModelState.IsValid)
        {
            await CargarExcursionesAsync();
            return Page();
        }

        Interesado.Excursion = exc!.Nombre;

        if (Interesado.Id == 0)
        {
            Interesado.CreadoEl = DateTime.Now;
            _db.Interesados.Add(Interesado);
        }
        else
        {
            var db = await _db.Interesados.FindAsync(Interesado.Id);
            if (db is null) return RedirectToPage("/Interesados/Index");
            db.Nombre = Interesado.Nombre;
            db.Telefono = Interesado.Telefono;
            db.ExcursionId = Interesado.ExcursionId;
            db.Excursion = Interesado.Excursion;
            db.FechaDesde = Interesado.FechaDesde;
            db.FechaHasta = Interesado.FechaHasta;
        }

        await _db.SaveChangesAsync();

        // ¿Coincide con una reserva o con otro interesado? → mostrar el modal en Reservas
        bool hayCoincidencia =
            await _db.Reservas.AnyAsync(r => r.ExcursionId == Interesado.ExcursionId
                && r.FechaDesde.Date <= Interesado.FechaHasta.Date
                && r.FechaHasta.Date >= Interesado.FechaDesde.Date)
            || await _db.Interesados.AnyAsync(o => o.Id != Interesado.Id
                && o.ExcursionId == Interesado.ExcursionId
                && o.FechaDesde.Date <= Interesado.FechaHasta.Date
                && o.FechaHasta.Date >= Interesado.FechaDesde.Date);

        if (hayCoincidencia)
            return RedirectToPage("/Reservas/Index", new { verUniones = true });

        return RedirectToPage("/Interesados/Index", new { Aviso = "Interesado guardado ✔" });
    }
}

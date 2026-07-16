using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Excursiones;

public class CargarModel : PageModel
{
    private readonly AppDbContext _db;
    public CargarModel(AppDbContext db) => _db = db;

    [BindProperty]
    public Excursion Excursion { get; set; } = new();

    // Lista de gastos de la excursión (nombre + precio), enviados como arrays desde el form
    [BindProperty]
    public List<string> GastoNombres { get; set; } = new();

    [BindProperty]
    public List<string> GastoPrecios { get; set; } = new();

    [BindProperty]
    public List<string> GastoTipos { get; set; } = new();

    // Para mostrar los gastos ya cargados al abrir la página
    public List<GastoExcursion> Gastos { get; set; } = new();

    public bool EsNueva => Excursion.Id == 0;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            Excursion = new Excursion { MinimoPersonas = 2, Activa = true };
            return Page();
        }

        var existente = await _db.Excursiones.FindAsync(id);
        if (existente is null) return RedirectToPage("/Excursiones/Index");
        Excursion = existente;

        Gastos = await _db.GastosExcursion
            .Where(g => g.ExcursionId == id)
            .OrderBy(g => g.Id)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await RecargarGastosAsync();
            return Page();
        }

        int excursionId;
        if (Excursion.Id == 0)
        {
            _db.Excursiones.Add(Excursion);
            await _db.SaveChangesAsync();
            excursionId = Excursion.Id;
        }
        else
        {
            var db = await _db.Excursiones.FindAsync(Excursion.Id);
            if (db is null) return RedirectToPage("/Excursiones/Index");

            db.Nombre = Excursion.Nombre;
            db.PrecioPorPersona = Excursion.PrecioPorPersona;
            db.MinimoPersonas = Excursion.MinimoPersonas;
            db.MaximoPersonas = Excursion.MaximoPersonas;
            db.CantidadGuias = Excursion.CantidadGuias;
            db.EsTravesia = Excursion.EsTravesia;
            db.Activa = Excursion.Activa;
            db.GuiaBreve = Excursion.GuiaBreve;
            db.Recomendaciones = Excursion.Recomendaciones;
            db.LugaresVisitar = Excursion.LugaresVisitar;
            excursionId = db.Id;
        }

        // Reescribir la lista de gastos de esta excursión
        var viejos = await _db.GastosExcursion.Where(g => g.ExcursionId == excursionId).ToListAsync();
        _db.GastosExcursion.RemoveRange(viejos);

        for (int i = 0; i < GastoNombres.Count; i++)
        {
            var nombre = (GastoNombres[i] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nombre)) continue;
            var precioTxt = i < GastoPrecios.Count ? GastoPrecios[i] : "0";
            decimal precio = ParsePrecio(precioTxt);
            var tipo = i < GastoTipos.Count && GastoExcursion.Tipos.Contains(GastoTipos[i])
                ? GastoTipos[i] : "Por persona";
            _db.GastosExcursion.Add(new GastoExcursion
            {
                ExcursionId = excursionId,
                Nombre = nombre,
                Precio = precio,
                TipoCalculo = tipo
            });
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("/Excursiones/Index", new { Aviso = "Excursión guardada ✔" });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var e = await _db.Excursiones.FindAsync(id);
        if (e is not null)
        {
            bool tieneReservas = _db.Reservas.Any(r => r.ExcursionId == id);
            if (tieneReservas)
            {
                e.Activa = false;
                await _db.SaveChangesAsync();
                return RedirectToPage("/Excursiones/Index",
                    new { Aviso = "La excursión tiene reservas, así que se ocultó (no se borró) para no perder el historial." });
            }

            var gastos = await _db.GastosExcursion.Where(g => g.ExcursionId == id).ToListAsync();
            _db.GastosExcursion.RemoveRange(gastos);
            _db.Excursiones.Remove(e);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("/Excursiones/Index", new { Aviso = "Excursión eliminada." });
    }

    private static decimal ParsePrecio(string? txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return 0m;
        txt = txt.Trim().Replace(",", ".");
        return decimal.TryParse(txt, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    private async Task RecargarGastosAsync()
    {
        if (Excursion.Id != 0)
            Gastos = await _db.GastosExcursion
                .Where(g => g.ExcursionId == Excursion.Id)
                .OrderBy(g => g.Id)
                .ToListAsync();
    }
}

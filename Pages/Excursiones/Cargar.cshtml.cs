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

    [BindProperty]
    public List<string> GastoEsProveedor { get; set; } = new();   // "1" si el costo es un proveedor

    // Sólo para los costos de tipo "Cantidad": con cuántos arranca cada salida
    [BindProperty]
    public List<string> GastoCantidades { get; set; } = new();

    // Etapas de la travesía (dónde se duerme cada noche), enviadas como arrays desde el form
    [BindProperty] public List<string> EtapaLugares { get; set; } = new();
    [BindProperty] public List<int> EtapaProveedorIds { get; set; } = new();
    [BindProperty] public List<string> EtapaPrecios { get; set; } = new();
    [BindProperty] public List<string> EtapaIncluye { get; set; } = new();
    [BindProperty] public List<string> EtapaNoches { get; set; } = new();   // cuántas noches seguidas en ese lugar

    // Para mostrar los gastos ya cargados al abrir la página
    public List<GastoExcursion> Gastos { get; set; } = new();

    // Etapas ya cargadas + catálogo de hospedajes para elegir el refugio de cada lugar
    public List<EtapaExcursion> Etapas { get; set; } = new();
    public List<Proveedor> Hospedajes { get; set; } = new();

    public bool EsNueva => Excursion.Id == 0;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await CargarHospedajesAsync();

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

        Etapas = await _db.EtapasExcursion
            .Where(e => e.ExcursionId == id)
            .OrderBy(e => e.Orden)
            .ToListAsync();

        return Page();
    }

    private async Task CargarHospedajesAsync()
        => Hospedajes = await _db.Proveedores
            .Where(p => p.Activo && p.Tipo == "Hospedaje")
            .OrderBy(p => p.Nombre)
            .ToListAsync();

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
            var esProv = i < GastoEsProveedor.Count && GastoEsProveedor[i] == "1";

            // La cantidad sólo se guarda en los costos de tipo "Cantidad" (arrieros,
            // caballos, guías, traslados). En los demás queda en null.
            int? cantidad = null;
            if (tipo == "Cantidad")
            {
                var cantTxt = i < GastoCantidades.Count ? GastoCantidades[i] : null;
                cantidad = int.TryParse(cantTxt, out var c) && c >= 0 ? c : 1;
            }

            _db.GastosExcursion.Add(new GastoExcursion
            {
                ExcursionId = excursionId,
                Nombre = nombre,
                Precio = precio,
                TipoCalculo = tipo,
                Cantidad = cantidad,
                EsProveedor = esProv
            });
        }

        // Reescribir las etapas (las noches de la travesía). El orden es el de la pantalla:
        // la primera fila es la noche 1, la segunda la noche 2, y así.
        var etapasViejas = await _db.EtapasExcursion.Where(e => e.ExcursionId == excursionId).ToListAsync();
        _db.EtapasExcursion.RemoveRange(etapasViejas);

        // Para poder usar el nombre del refugio como lugar cuando no se escribió ninguno
        var nombresProv = await _db.Proveedores.ToDictionaryAsync(p => p.Id, p => p.Nombre);

        int noche = 0;
        for (int i = 0; i < EtapaLugares.Count; i++)
        {
            var lugar = (EtapaLugares[i] ?? "").Trim();
            var provId = i < EtapaProveedorIds.Count ? EtapaProveedorIds[i] : 0;

            // Si no se escribió el lugar pero sí se eligió el refugio, se usa su nombre.
            // (Antes la fila se descartaba en silencio y parecía que no guardaba nada.)
            if (string.IsNullOrWhiteSpace(lugar) && provId != 0)
                lugar = nombresProv.TryGetValue(provId, out var pn) ? pn : "";

            // Fila realmente vacía (ni lugar ni refugio) → se ignora
            if (string.IsNullOrWhiteSpace(lugar)) continue;

            noche++;
            var incluye = (i < EtapaIncluye.Count ? EtapaIncluye[i] : null)?.Trim();

            // Cuántas noches seguidas se para en este lugar (mínimo 1). Es lo que permite
            // cargar "2 noches en el mismo hospedaje" con una sola fila.
            var nochesTxt = i < EtapaNoches.Count ? EtapaNoches[i] : null;
            var noches = int.TryParse(nochesTxt, out var nn) && nn > 0 ? nn : 1;

            _db.EtapasExcursion.Add(new EtapaExcursion
            {
                ExcursionId = excursionId,
                Orden = noche,
                Lugar = lugar,
                Noches = noches,
                ProveedorId = provId == 0 ? null : provId,
                PrecioPorPersona = ParsePrecio(i < EtapaPrecios.Count ? EtapaPrecios[i] : "0"),
                Incluye = string.IsNullOrWhiteSpace(incluye) ? null : incluye
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
            var etapas = await _db.EtapasExcursion.Where(e => e.ExcursionId == id).ToListAsync();
            _db.EtapasExcursion.RemoveRange(etapas);
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
        await CargarHospedajesAsync();
        if (Excursion.Id != 0)
        {
            Gastos = await _db.GastosExcursion
                .Where(g => g.ExcursionId == Excursion.Id)
                .OrderBy(g => g.Id)
                .ToListAsync();
            Etapas = await _db.EtapasExcursion
                .Where(e => e.ExcursionId == Excursion.Id)
                .OrderBy(e => e.Orden)
                .ToListAsync();
        }
    }
}

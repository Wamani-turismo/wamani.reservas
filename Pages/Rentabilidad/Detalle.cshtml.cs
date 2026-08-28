using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Rentabilidad;

public class DetalleModel : PageModel
{
    private readonly AppDbContext _db;
    public DetalleModel(AppDbContext db) => _db = db;

    public Excursion Excursion { get; set; } = new();
    public List<GastoExcursion> Items { get; set; } = new();

    // Lo que se contrata para el grupo: noches, traslados, pasajes, guía, arrieros y
    // caballos. Cuenta igual que los costos sueltos y en una travesía es casi toda la plata.
    public List<EtapaExcursion> Etapas { get; set; } = new();

    public int PersonasPorAuto => Excursion.PersonasPorAuto;

    // Cuántos guías van según la plantilla (viajan en los autos y sacan boleto).
    public int Guias => Wamani.Reservas.Services.RentabilidadCalc.GuiasDe(Etapas);

    // ¿Hay algo con lo que calcular?
    public bool SinCostos => Items.Count == 0 && Etapas.Count == 0;

    // Cuántas veces se cobra una fila del grupo (noches, o días si va por día).
    public int Veces(EtapaExcursion et) => et.Noches > 0 ? et.Noches : 1;

    // Cómo se llama la fila en la pantalla.
    public string TituloEtapa(EtapaExcursion et)
        => EtapaExcursion.Icono(et.Tipo) + " " +
           (string.IsNullOrWhiteSpace(et.Lugar) ? EtapaExcursion.Seccion(et.Tipo) : et.Lugar.Trim());

    // Cómo se cuenta cada costo suelto, dicho en criollo.
    public string ComoSeCuenta(GastoExcursion g) => g.TipoCalculo switch
    {
        "Por auto" => "por auto (1 cada " + PersonasPorAuto + " personas)",
        "Por guía" => "por auto (1 cada " + PersonasPorAuto + " personas)",
        "Cantidad" => (g.Cantidad ?? 0) + " × el precio",
        "Fijo"     => "una vez por salida",
        _           => "por persona",
    };

    // Lo mismo para lo que se contrata para el grupo.
    public string ComoSeCuentaEtapa(EtapaExcursion et) => et.Tipo switch
    {
        EtapaExcursion.Traslado => "1 auto cada " + PersonasPorAuto + " (con los guías)",
        EtapaExcursion.Pasaje   => "un boleto por cabeza (con los guías)",
        EtapaExcursion.Guia     => et.CantidadReferencia() + " × " + Veces(et) + " día(s)",
        EtapaExcursion.Arriero  => et.CantidadReferencia() + " × " + Veces(et) + " día(s)",
        EtapaExcursion.Caballo  => et.CantidadReferencia() + " × " + Veces(et) + " día(s)",
        _                        => "por persona × " + Veces(et) + " noche(s)",
    };

    // Para el JavaScript de la pantalla: el tipo sin acentos, así la cuenta de allá no
    // depende de cómo se escriba "Guía".
    public string ModoJs(EtapaExcursion et) => et.Tipo switch
    {
        EtapaExcursion.Traslado => "traslado",
        EtapaExcursion.Pasaje   => "pasaje",
        EtapaExcursion.Guia     => "pordia",
        EtapaExcursion.Arriero  => "pordia",
        EtapaExcursion.Caballo  => "pordia",
        _                        => "hospedaje",
    };

    // Avisos: lo que está sin cargar hace que el costo salga más bajo de lo que es.
    public bool FaltanPrecios => Etapas.Any(x => x.PrecioPorPersona <= 0);
    public bool FaltanCantidades =>
        Etapas.Any(x => EtapaExcursion.EsPorDia(x.Tipo) && x.CantidadReferencia() == 0);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var e = await _db.Excursiones.FindAsync(id);
        if (e is null) return RedirectToPage("/Rentabilidad/Index");
        Excursion = e;
        Items = await _db.GastosExcursion
            .Where(g => g.ExcursionId == id)
            .OrderBy(g => g.Id)
            .ToListAsync();
        Etapas = await _db.EtapasExcursion
            .Where(x => x.ExcursionId == id)
            .OrderBy(x => x.Orden)
            .ToListAsync();
        return Page();
    }
}

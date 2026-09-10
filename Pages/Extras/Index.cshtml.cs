using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Extras;

// Ingresos EXTRA: plata que entra por fuera de las reservas (comisiones por alquilar un
// auto o conseguir un hospedaje, servicios sueltos, etc.). Se cargan acá y suman como
// ingreso en Finanzas y en la Caja, por su fecha.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public IndexModel(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [BindProperty(SupportsGet = true)]
    public string? Mes { get; set; }   // "yyyy-MM"

    public DateTime MesActual { get; set; }
    public string MesTexto { get; set; } = "";
    public List<IngresoExtra> Lista { get; set; } = new();
    public decimal Total { get; set; }
    public decimal TotalHistorico { get; set; }

    [BindProperty] public DateTime NuevoFecha { get; set; } = DateTime.Today;
    [BindProperty] public string NuevoMotivo { get; set; } = "Comisión";
    [BindProperty] public string? NuevoDescripcion { get; set; }
    [BindProperty] public string? NuevoDeQuien { get; set; }
    [BindProperty] public decimal NuevoMonto { get; set; }
    [BindProperty] public List<IFormFile> NuevoComprobante { get; set; } = new();

    [TempData] public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        var hoy = DateTime.Today;
        int anio = hoy.Year, mes = hoy.Month;
        if (!string.IsNullOrWhiteSpace(Mes) && DateTime.TryParse(Mes + "-01", out var p))
        {
            anio = p.Year; mes = p.Month;
        }
        MesActual = new DateTime(anio, mes, 1);
        var fin = MesActual.AddMonths(1);
        MesTexto = MesActual.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-AR"));

        Lista = await _db.IngresosExtra
            .Where(e => e.Fecha >= MesActual && e.Fecha < fin)
            .OrderByDescending(e => e.Fecha)
            .ToListAsync();
        Total = Lista.Sum(e => e.Monto);

        TotalHistorico = (await _db.IngresosExtra.ToListAsync()).Sum(e => e.Monto);
    }

    public async Task<IActionResult> OnPostAgregarAsync()
    {
        if (!string.IsNullOrWhiteSpace(NuevoDescripcion) && NuevoMonto > 0)
        {
            var e = new IngresoExtra
            {
                Fecha = NuevoFecha.Date,
                Motivo = IngresoExtra.Motivos.Contains(NuevoMotivo) ? NuevoMotivo : "Otro",
                Descripcion = NuevoDescripcion.Trim(),
                DeQuien = string.IsNullOrWhiteSpace(NuevoDeQuien) ? null : NuevoDeQuien.Trim(),
                Monto = NuevoMonto
            };

            e.Comprobante = await Wamani.Reservas.Services.Adjuntos.AgregarAsync(
                NuevoComprobante, Wamani.Reservas.Services.Comprobantes.Carpeta(_env), null);

            _db.IngresosExtra.Add(e);
            await _db.SaveChangesAsync();
            Aviso = "Ingreso extra agregado.";
        }
        return RedirectToPage(new { Mes = NuevoFecha.ToString("yyyy-MM") });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var e = await _db.IngresosExtra.FindAsync(id);
        if (e is not null)
        {
            _db.IngresosExtra.Remove(e);
            await _db.SaveChangesAsync();
            Aviso = "Ingreso extra borrado.";
        }
        return RedirectToPage(new { Mes });
    }
}

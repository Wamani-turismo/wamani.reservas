using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Gastos;

// Gastos generales de la empresa (publicidad, botiquín, etc.): se cargan acá y se
// descuentan del neto del mes en Finanzas.
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
    public List<GastoEmpresa> Lista { get; set; } = new();
    public decimal Total { get; set; }

    [BindProperty] public DateTime NuevoFecha { get; set; } = DateTime.Today;
    [BindProperty] public string NuevoTipo { get; set; } = "Fijo";
    [BindProperty] public string? NuevoDescripcion { get; set; }
    [BindProperty] public decimal NuevoMonto { get; set; }
    [BindProperty] public bool NuevoDelFondo { get; set; }
    [BindProperty] public bool NuevoEsInversion { get; set; }
    [BindProperty] public List<IFormFile> NuevoComprobante { get; set; } = new();

    // Fondo del 10% acumulado, para saber cuánto hay disponible antes de gastarlo
    public Wamani.Reservas.Services.FondoReserva.Mes Fondo { get; set; } = new();
    public decimal TotalDelFondo { get; set; }   // lo gastado del fondo este mes

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

        Lista = await _db.GastosEmpresa
            .Where(g => g.Fecha >= MesActual && g.Fecha < fin)
            .OrderByDescending(g => g.Fecha)
            .ToListAsync();
        Total = Lista.Sum(g => g.Monto);
        TotalDelFondo = Lista.Where(g => g.DelFondo).Sum(g => g.Monto);

        Fondo = await Wamani.Reservas.Services.FondoReserva.CalcularAsync(_db, MesActual);
    }

    public async Task<IActionResult> OnPostAgregarAsync()
    {
        if (!string.IsNullOrWhiteSpace(NuevoDescripcion) && NuevoMonto > 0)
        {
            var g = new GastoEmpresa
            {
                Fecha = NuevoFecha.Date,
                Tipo = GastoEmpresa.Tipos.Contains(NuevoTipo) ? NuevoTipo : "Fijo",
                Descripcion = NuevoDescripcion.Trim(),
                Monto = NuevoMonto,
                DelFondo = NuevoDelFondo,
                EsInversion = NuevoEsInversion
            };

            g.Comprobante = await Wamani.Reservas.Services.Adjuntos.AgregarAsync(
                NuevoComprobante, Wamani.Reservas.Services.Comprobantes.Carpeta(_env), null);

            _db.GastosEmpresa.Add(g);
            await _db.SaveChangesAsync();
            Aviso = "Gasto agregado.";
        }
        return RedirectToPage(new { Mes = NuevoFecha.ToString("yyyy-MM") });
    }

    // Corregir la fecha de un gasto ya cargado, sin borrarlo ni volver a subir el
    // comprobante. Hace falta porque la fecha que importa es la del CONSUMO y no la del
    // pago: el resumen de la tarjeta se paga al mes siguiente, y si queda con la fecha
    // del pago le infla la ganancia a un mes y se la hunde al otro. Sólo cambia el día:
    // no toca el monto, la descripción ni el comprobante.
    public async Task<IActionResult> OnPostFechaAsync(int id, DateTime fecha)
    {
        var g = await _db.GastosEmpresa.FindAsync(id);
        if (g is not null && fecha != default)
        {
            g.Fecha = fecha.Date;
            await _db.SaveChangesAsync();
            Aviso = $"Gasto movido al {fecha:dd/MM/yyyy}.";
            return RedirectToPage(new { Mes = fecha.ToString("yyyy-MM") });
        }
        return RedirectToPage(new { Mes });
    }

    // Marcar (o desmarcar) un gasto ya cargado como inversión. Hace falta para los que
    // ya estaban antes de que existiera el tilde, como la FIT.
    public async Task<IActionResult> OnPostInversionAsync(int id)
    {
        var g = await _db.GastosEmpresa.FindAsync(id);
        if (g is not null)
        {
            g.EsInversion = !g.EsInversion;
            await _db.SaveChangesAsync();
            Aviso = g.EsInversion
                ? "Marcado como inversión: ya no baja la ganancia del mes."
                : "Vuelve a ser un gasto corriente: baja la ganancia del mes.";
        }
        return RedirectToPage(new { Mes });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var g = await _db.GastosEmpresa.FindAsync(id);
        if (g is not null)
        {
            _db.GastosEmpresa.Remove(g);
            await _db.SaveChangesAsync();
            Aviso = "Gasto borrado.";
        }
        return RedirectToPage(new { Mes });
    }
}

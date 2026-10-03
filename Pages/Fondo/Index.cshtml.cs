using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Fondo;

// FONDO DE INVERSIÓN: la plata apartada para que Wamani crezca, guardada en dólares.
// Ver Models/MovimientoFondo.cs para qué hace cada tipo de movimiento.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public IndexModel(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public List<MovimientoFondo> Lista { get; set; } = new();

    public decimal SaldoDolares { get; set; }
    public decimal SaldoPesos { get; set; }        // la suma histórica, al cambio de cada día
    public decimal EntroDesdeCaja { get; set; }    // lo que se mandó desde la caja operativa
    public decimal Gastado { get; set; }           // en pesos, lo que salió del fondo
    public decimal Aportado { get; set; }          // en pesos, lo que pusieron los socios

    // A cuánto estaba el dólar la última vez que se cargó algo. Sirve para proponerlo en el
    // formulario y para mostrar cuánto valen hoy los dólares del fondo.
    public decimal UltimoTipoCambio { get; set; }
    public decimal SaldoPesosHoy => Math.Round(SaldoDolares * UltimoTipoCambio, 2);

    [BindProperty] public DateTime NuevoFecha { get; set; } = Wamani.Reservas.Services.Reloj.HoyJujuy();
    [BindProperty] public string NuevoTipo { get; set; } = MovimientoFondo.Aporte;
    [BindProperty] public string? NuevoConcepto { get; set; }
    [BindProperty] public string? NuevoQuien { get; set; }
    [BindProperty] public decimal NuevoDolares { get; set; }
    [BindProperty] public decimal NuevoPesos { get; set; }
    [BindProperty] public decimal NuevoTipoCambio { get; set; }
    [BindProperty] public string? NuevoNota { get; set; }
    [BindProperty] public List<IFormFile> NuevoComprobante { get; set; } = new();

    [TempData] public string? Aviso { get; set; }

    public async Task OnGetAsync() => await CargarAsync();

    private async Task CargarAsync()
    {
        Lista = await _db.MovimientosFondo.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id).ToListAsync();

        SaldoDolares = Lista.Sum(m => m.SignoDolares);
        SaldoPesos = Lista.Sum(m => m.SignoPesos);
        EntroDesdeCaja = Lista.Where(m => m.SaleDeLaCaja).Sum(m => m.Pesos);
        Gastado = Lista.Where(m => m.Tipo == MovimientoFondo.Gasto).Sum(m => m.Pesos);
        Aportado = Lista.Where(m => m.Tipo == MovimientoFondo.Aporte).Sum(m => m.Pesos);

        UltimoTipoCambio = Lista.Where(m => m.TipoCambio > 0)
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Select(m => m.TipoCambio).FirstOrDefault();
    }

    public async Task<IActionResult> OnPostAgregarAsync()
    {
        if (string.IsNullOrWhiteSpace(NuevoConcepto) || (NuevoDolares <= 0 && NuevoPesos <= 0))
        {
            Aviso = "Poné el concepto y un monto (en dólares, en pesos, o los dos).";
            return RedirectToPage();
        }

        // Si cargó uno solo de los dos montos y hay tipo de cambio, se completa el otro.
        // No se inventa nada: si no hay cotización, queda en cero y se ve que falta.
        var tc = NuevoTipoCambio > 0 ? NuevoTipoCambio : 0;
        var dolares = NuevoDolares;
        var pesos = NuevoPesos;
        if (tc > 0 && dolares > 0 && pesos <= 0) pesos = Math.Round(dolares * tc, 2);
        if (tc > 0 && pesos > 0 && dolares <= 0) dolares = Math.Round(pesos / tc, 2);

        var m = new MovimientoFondo
        {
            Fecha = NuevoFecha.Date,
            Tipo = MovimientoFondo.Tipos.Contains(NuevoTipo) ? NuevoTipo : MovimientoFondo.Aporte,
            Concepto = NuevoConcepto.Trim(),
            Quien = string.IsNullOrWhiteSpace(NuevoQuien) ? null : NuevoQuien.Trim(),
            Dolares = dolares,
            Pesos = pesos,
            TipoCambio = tc,
            Nota = string.IsNullOrWhiteSpace(NuevoNota) ? null : NuevoNota.Trim(),
        };
        m.Comprobante = await Wamani.Reservas.Services.Adjuntos.AgregarAsync(
            NuevoComprobante, Wamani.Reservas.Services.Comprobantes.Carpeta(_env), null);

        _db.MovimientosFondo.Add(m);
        await _db.SaveChangesAsync();
        Aviso = "Movimiento cargado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var m = await _db.MovimientosFondo.FindAsync(id);
        if (m is not null)
        {
            _db.MovimientosFondo.Remove(m);
            await _db.SaveChangesAsync();
            Aviso = "Movimiento borrado.";
        }
        return RedirectToPage();
    }
}

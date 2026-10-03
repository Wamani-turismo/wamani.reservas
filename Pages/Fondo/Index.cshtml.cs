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

    // Cuando se está editando un movimiento, el formulario de arriba se llena con sus
    // datos y guarda encima en vez de crear otro. Hacía falta para poder corregir una
    // fecha sin tener que borrar y volver a cargar todo.
    [BindProperty] public int NuevoId { get; set; }
    public bool Editando => NuevoId > 0;

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

    public async Task OnGetAsync(int? editar)
    {
        await CargarAsync();

        if (editar is int id && Lista.FirstOrDefault(m => m.Id == id) is MovimientoFondo m)
        {
            NuevoId = m.Id;
            NuevoFecha = m.Fecha;
            NuevoTipo = m.Tipo;
            NuevoConcepto = m.Concepto;
            NuevoQuien = m.Quien;
            NuevoDolares = m.Dolares;
            NuevoPesos = m.Pesos;
            NuevoTipoCambio = m.TipoCambio;
            NuevoNota = m.Nota;
        }
    }

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

        // Editando: se guarda encima del que ya estaba. Si no suben un comprobante nuevo,
        // se conserva el que tenía.
        var m = NuevoId > 0 ? await _db.MovimientosFondo.FindAsync(NuevoId) : new MovimientoFondo();
        if (m is null) return RedirectToPage();

        m.Fecha = NuevoFecha.Date;
        m.Tipo = MovimientoFondo.Tipos.Contains(NuevoTipo) ? NuevoTipo : MovimientoFondo.Aporte;
        m.Concepto = NuevoConcepto.Trim();
        m.Quien = string.IsNullOrWhiteSpace(NuevoQuien) ? null : NuevoQuien.Trim();
        m.Dolares = dolares;
        m.Pesos = pesos;
        m.TipoCambio = tc;
        m.Nota = string.IsNullOrWhiteSpace(NuevoNota) ? null : NuevoNota.Trim();

        var comp = await Wamani.Reservas.Services.Adjuntos.AgregarAsync(
            NuevoComprobante, Wamani.Reservas.Services.Comprobantes.Carpeta(_env), m.Comprobante);
        if (!string.IsNullOrEmpty(comp)) m.Comprobante = comp;

        if (NuevoId == 0) _db.MovimientosFondo.Add(m);
        await _db.SaveChangesAsync();
        Aviso = NuevoId > 0 ? "Movimiento actualizado." : "Movimiento cargado.";
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

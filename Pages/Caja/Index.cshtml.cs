using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Caja;

// Caja / Patrimonio de la empresa:
//   Caja = todo lo que entró − todo lo que salió (operativo).
//   Patrimonio = Caja + Aportes de los socios − Retiros de los socios.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public IndexModel(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public decimal Ingresos { get; set; }        // todo lo cobrado (histórico)
    public decimal Egresos { get; set; }          // todo lo pagado (excursiones + proveedores + gastos empresa)
    public decimal Caja => Ingresos - Egresos;    // plata que generó la operación
    public decimal AportesTotal { get; set; }
    public decimal RetirosTotal { get; set; }
    public decimal Patrimonio => Caja + AportesTotal - RetirosTotal;   // capital actual de la empresa

    public List<Aporte> Aportes { get; set; } = new();
    public List<Retiro> Retiros { get; set; } = new();

    // Form aportes
    [BindProperty] public DateTime ApFecha { get; set; } = DateTime.Today;
    [BindProperty] public string? ApQuien { get; set; }
    [BindProperty] public string? ApDescripcion { get; set; }
    [BindProperty] public decimal ApMonto { get; set; }
    [BindProperty] public List<IFormFile> ApComprobante { get; set; } = new();

    // Form retiros
    [BindProperty] public DateTime RetFecha { get; set; } = DateTime.Today;
    [BindProperty] public string? RetQuien { get; set; }
    [BindProperty] public string? RetDescripcion { get; set; }
    [BindProperty] public decimal RetMonto { get; set; }
    [BindProperty] public List<IFormFile> RetComprobante { get; set; } = new();

    [TempData] public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        var reservas = await _db.Reservas.ToListAsync();
        Ingresos = reservas.Sum(r => (r.SenaMonto ?? 0) + (r.SaldoMonto ?? 0));

        var egGastos = (await _db.OperativoGastos.ToListAsync()).Sum(o => o.Precio);
        var egProv = (await _db.OperativoProveedores.ToListAsync()).Sum(p => p.Sena + p.Saldo);
        var egEmpresa = (await _db.GastosEmpresa.ToListAsync()).Sum(g => g.Monto);
        Egresos = egGastos + egProv + egEmpresa;

        Aportes = await _db.Aportes.OrderByDescending(a => a.Fecha).ToListAsync();
        AportesTotal = Aportes.Sum(a => a.Monto);

        Retiros = await _db.Retiros.OrderByDescending(r => r.Fecha).ToListAsync();
        RetirosTotal = Retiros.Sum(r => r.Monto);
    }

    // Guarda uno o varios comprobantes conservando el nombre original
    private async Task<string?> GuardarComprobanteAsync(IEnumerable<IFormFile>? archivos)
    {
        var carpeta = Wamani.Reservas.Services.Comprobantes.Carpeta(_env);
        return await Wamani.Reservas.Services.Adjuntos.AgregarAsync(archivos, carpeta, null);
    }

    public async Task<IActionResult> OnPostAgregarAporteAsync()
    {
        if (ApMonto > 0)
        {
            _db.Aportes.Add(new Aporte
            {
                Fecha = ApFecha.Date,
                Quien = string.IsNullOrWhiteSpace(ApQuien) ? null : ApQuien.Trim(),
                Descripcion = string.IsNullOrWhiteSpace(ApDescripcion) ? null : ApDescripcion.Trim(),
                Monto = ApMonto,
                Comprobante = await GuardarComprobanteAsync(ApComprobante)
            });
            await _db.SaveChangesAsync();
            Aviso = "Aporte registrado.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAporteAsync(int id)
    {
        var a = await _db.Aportes.FindAsync(id);
        if (a is not null) { _db.Aportes.Remove(a); await _db.SaveChangesAsync(); Aviso = "Aporte borrado."; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAgregarRetiroAsync()
    {
        if (RetMonto > 0)
        {
            _db.Retiros.Add(new Retiro
            {
                Fecha = RetFecha.Date,
                Quien = string.IsNullOrWhiteSpace(RetQuien) ? null : RetQuien.Trim(),
                Descripcion = string.IsNullOrWhiteSpace(RetDescripcion) ? null : RetDescripcion.Trim(),
                Monto = RetMonto,
                Comprobante = await GuardarComprobanteAsync(RetComprobante)
            });
            await _db.SaveChangesAsync();
            Aviso = "Retiro registrado.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarRetiroAsync(int id)
    {
        var r = await _db.Retiros.FindAsync(id);
        if (r is not null) { _db.Retiros.Remove(r); await _db.SaveChangesAsync(); Aviso = "Retiro borrado."; }
        return RedirectToPage();
    }
}

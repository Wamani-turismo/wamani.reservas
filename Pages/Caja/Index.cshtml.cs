using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Caja;

// Caja / Patrimonio de la empresa: todo lo que entró menos todo lo que salió (caja),
// y menos los retiros de los socios = capital actual de la empresa.
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
    public decimal Caja => Ingresos - Egresos;    // plata que generó la empresa
    public decimal RetirosTotal { get; set; }
    public decimal Patrimonio => Caja - RetirosTotal;   // capital actual (lo que tiene la empresa)

    public List<Retiro> Lista { get; set; } = new();

    [BindProperty] public DateTime NuevoFecha { get; set; } = DateTime.Today;
    [BindProperty] public string? NuevoQuien { get; set; }
    [BindProperty] public string? NuevoDescripcion { get; set; }
    [BindProperty] public decimal NuevoMonto { get; set; }
    [BindProperty] public IFormFile? NuevoComprobante { get; set; }

    [TempData] public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        var reservas = await _db.Reservas.ToListAsync();
        Ingresos = reservas.Sum(r => (r.SenaMonto ?? 0) + (r.SaldoMonto ?? 0));

        var egGastos = (await _db.OperativoGastos.ToListAsync()).Sum(o => o.Precio);
        var egProv = (await _db.OperativoProveedores.ToListAsync()).Sum(p => p.Sena + p.Saldo);
        var egEmpresa = (await _db.GastosEmpresa.ToListAsync()).Sum(g => g.Monto);
        Egresos = egGastos + egProv + egEmpresa;

        Lista = await _db.Retiros.OrderByDescending(r => r.Fecha).ToListAsync();
        RetirosTotal = Lista.Sum(r => r.Monto);
    }

    public async Task<IActionResult> OnPostAgregarAsync()
    {
        if (NuevoMonto > 0)
        {
            var r = new Retiro
            {
                Fecha = NuevoFecha.Date,
                Quien = string.IsNullOrWhiteSpace(NuevoQuien) ? null : NuevoQuien.Trim(),
                Descripcion = string.IsNullOrWhiteSpace(NuevoDescripcion) ? null : NuevoDescripcion.Trim(),
                Monto = NuevoMonto
            };

            if (NuevoComprobante is not null && NuevoComprobante.Length > 0)
            {
                var carpeta = Wamani.Reservas.Services.Comprobantes.Carpeta(_env);
                Directory.CreateDirectory(carpeta);
                var nombre = $"{Guid.NewGuid():N}{Path.GetExtension(NuevoComprobante.FileName)}";
                using (var st = new FileStream(Path.Combine(carpeta, nombre), FileMode.Create))
                    await NuevoComprobante.CopyToAsync(st);
                r.Comprobante = $"/comprobantes/{nombre}";
            }

            _db.Retiros.Add(r);
            await _db.SaveChangesAsync();
            Aviso = "Retiro registrado.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var r = await _db.Retiros.FindAsync(id);
        if (r is not null)
        {
            _db.Retiros.Remove(r);
            await _db.SaveChangesAsync();
            Aviso = "Retiro borrado.";
        }
        return RedirectToPage();
    }
}

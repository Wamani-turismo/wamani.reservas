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

    // Sirve para controlar: los dos números tienen que sumar exactamente el patrimonio.
    public decimal DeudaSocios { get; set; }

    // ---- Las dos cajas ----
    // La operativa es el día a día (Mercado Pago). El fondo de inversión está en la cuenta
    // de reserva, en dólares. Separarlas es lo que evita gastar sin querer la plata que se
    // había apartado para crecer.
    public decimal FondoDolares { get; set; }
    public decimal FondoPesos { get; set; }        // al cambio de cada movimiento
    public decimal MandadoAlFondo { get; set; }    // lo que salió de la caja operativa
    public decimal GastadoDelFondo { get; set; }

    // Lo que queda en el día a día: la caja de siempre menos lo que se mandó al fondo.
    public decimal CajaOperativa => Caja + AportesTotal - RetirosTotal - MandadoAlFondo;

    // El último control de caja (ver Pages/Caja/Arqueo.cshtml). Puede no haber ninguno.
    public Models.ArqueoCaja? UltimoArqueo { get; set; }

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
        // La cuenta de la caja vive en Services/CajaCalc.cs: la hacen igual esta pantalla,
        // la Financiera y el control de caja, así que no puede estar copiada tres veces.
        var foto = await Wamani.Reservas.Services.CajaCalc.CalcularAsync(_db);
        Ingresos = foto.Ingresos;
        Egresos = foto.Egresos;
        AportesTotal = foto.Aportes;
        RetirosTotal = foto.Retiros;

        Aportes = await _db.Aportes.OrderByDescending(a => a.Fecha).ToListAsync();
        Retiros = await _db.Retiros.OrderByDescending(r => r.Fecha).ToListAsync();

        // Desglose del patrimonio: cuánto es de los socios y cuánto está apartado en el fondo
        var hoy = DateTime.Today;
        var cuentas = await Wamani.Reservas.Services.CuentaSocios.CalcularAsync(
            _db, Pages.Financiera.IndexModel.Duenos, new DateTime(hoy.Year, hoy.Month, 1));
        DeudaSocios = cuentas.Socios.Sum(s => s.Saldo);

        // ---- Fondo de inversión: las dos cajas ----
        // La plata que se mandó al fondo salió de la caja operativa y está en la cuenta de
        // reserva. Sigue siendo de Wamani, por eso no es un gasto: sólo cambió de bolsillo.
        // Lo gastado DEL fondo sí salió de Wamani, pero nunca pasó por la caja del día a
        // día: se descuenta del fondo y de ningún otro lado.
        var movs = await _db.MovimientosFondo.ToListAsync();
        FondoDolares = movs.Sum(m => m.SignoDolares);
        FondoPesos = movs.Sum(m => m.SignoPesos);
        MandadoAlFondo = foto.MandadoAlFondo;
        GastadoDelFondo = movs.Where(m => m.Tipo == Models.MovimientoFondo.Gasto).Sum(m => m.Pesos);

        // El último control de caja: si la plata contada no coincidía con el sistema, acá se
        // ve de un saque, porque es el número que hay que mirar antes de creerle a la caja.
        UltimoArqueo = await _db.ArqueosCaja
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id).FirstOrDefaultAsync();
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Caja;

// CONTROL DE CAJA: contar la plata de verdad y compararla con el sistema.
// Ver Models/ArqueoCaja.cs para qué significa un sobrante y qué un faltante.
public class ArqueoModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public ArqueoModel(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public List<ArqueoCaja> Lista { get; set; } = new();

    // Lo que dice el sistema HOY. Es lo que se propone en el formulario.
    public decimal CajaOperativaHoy { get; set; }

    // El último control cargado: sirve para ver si la diferencia es nueva o venía de antes.
    public ArqueoCaja? Ultimo => Lista.FirstOrDefault();

    [BindProperty] public int NuevoId { get; set; }
    public bool Editando => NuevoId > 0;

    [BindProperty] public DateTime NuevoFecha { get; set; } = Wamani.Reservas.Services.Reloj.HoyJujuy();
    [BindProperty] public decimal NuevoSaldoReal { get; set; }
    [BindProperty] public decimal NuevoSaldoSistema { get; set; }
    [BindProperty] public string NuevoMotivo { get; set; } = ArqueoCaja.NoSabemos;
    [BindProperty] public string? NuevoNota { get; set; }
    [BindProperty] public bool NuevoAjustar { get; set; }
    [BindProperty] public List<IFormFile> NuevoComprobante { get; set; } = new();

    [TempData] public string? Aviso { get; set; }

    public async Task OnGetAsync(int? editar)
    {
        await CargarAsync();

        if (editar is int id && Lista.FirstOrDefault(a => a.Id == id) is ArqueoCaja a)
        {
            NuevoId = a.Id;
            NuevoFecha = a.Fecha;
            NuevoSaldoReal = a.SaldoReal;
            NuevoSaldoSistema = a.SaldoSistema;
            NuevoMotivo = a.Motivo;
            NuevoNota = a.Nota;
            NuevoAjustar = a.Ajustado;
        }
        else
        {
            // Formulario nuevo: se propone lo que dice el sistema hoy, así sólo hay que
            // escribir lo que muestra la cuenta del banco.
            NuevoSaldoSistema = CajaOperativaHoy;
        }
    }

    private async Task CargarAsync()
    {
        Lista = await _db.ArqueosCaja
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id).ToListAsync();

        var foto = await Wamani.Reservas.Services.CajaCalc.CalcularAsync(_db);
        CajaOperativaHoy = foto.CajaOperativa;
    }

    public async Task<IActionResult> OnPostAgregarAsync()
    {
        if (NuevoSaldoReal <= 0)
        {
            Aviso = "Poné cuánta plata hay de verdad en la cuenta.";
            return RedirectToPage();
        }

        var a = NuevoId > 0 ? await _db.ArqueosCaja.FindAsync(NuevoId) : new ArqueoCaja();
        if (a is null) return RedirectToPage();

        // Si se cambia el monto o el motivo de un control que ya estaba ajustado, el ajuste
        // viejo se borra y se vuelve a crear con los números nuevos. Dejarlo como estaba
        // sería peor: el ajuste diría una cosa y el control otra.
        await BorrarAjusteAsync(a);

        a.Fecha = NuevoFecha.Date;
        a.SaldoReal = NuevoSaldoReal;
        a.SaldoSistema = NuevoSaldoSistema;
        a.Motivo = ArqueoCaja.Motivos.Contains(NuevoMotivo) ? NuevoMotivo : ArqueoCaja.NoSabemos;
        a.Nota = string.IsNullOrWhiteSpace(NuevoNota) ? null : NuevoNota.Trim();

        var comp = await Wamani.Reservas.Services.Adjuntos.AgregarAsync(
            NuevoComprobante, Wamani.Reservas.Services.Comprobantes.Carpeta(_env), a.Comprobante);
        if (!string.IsNullOrEmpty(comp)) a.Comprobante = comp;

        if (NuevoId == 0) _db.ArqueosCaja.Add(a);
        await _db.SaveChangesAsync();

        if (NuevoAjustar && a.Diferencia != 0)
            await CrearAjusteAsync(a);
        else
        {
            a.Ajustado = false;
            await _db.SaveChangesAsync();
        }

        Aviso = NuevoId > 0 ? "Control actualizado." : "Control de caja cargado.";
        return RedirectToPage();
    }

    // Acomoda la caja: carga el ingreso o el gasto que hace que el sistema coincida con el
    // banco. Queda atado a este control por los Ids, para poder deshacerlo.
    private async Task CrearAjusteAsync(ArqueoCaja a)
    {
        var detalle = $"Ajuste de caja {a.Fecha:dd/MM/yy} · {a.Motivo}";

        if (a.EsSobrante)
        {
            // Los rendimientos de la cuenta remunerada son plata ganada de verdad, así que
            // entran como ingreso y suman a la ganancia del mes. Lo mismo cualquier otro
            // sobrante: si la plata está, entró de algún lado.
            var ing = new IngresoExtra
            {
                Fecha = a.Fecha,
                Motivo = a.Motivo == ArqueoCaja.Rendimientos ? "Rendimientos" : "Otro",
                Descripcion = detalle,
                Monto = a.Diferencia,
            };
            _db.IngresosExtra.Add(ing);
            await _db.SaveChangesAsync();
            a.AjusteIngresoId = ing.Id;
            a.AjusteGastoId = null;
        }
        else
        {
            var g = new GastoEmpresa
            {
                Fecha = a.Fecha,
                Tipo = "Variable",
                Descripcion = detalle,
                Monto = -a.Diferencia,   // la diferencia es negativa: el gasto va en positivo
            };
            _db.GastosEmpresa.Add(g);
            await _db.SaveChangesAsync();
            a.AjusteGastoId = g.Id;
            a.AjusteIngresoId = null;
        }

        a.Ajustado = true;
        await _db.SaveChangesAsync();
    }

    private async Task BorrarAjusteAsync(ArqueoCaja a)
    {
        if (a.AjusteIngresoId is int ii && await _db.IngresosExtra.FindAsync(ii) is IngresoExtra ing)
            _db.IngresosExtra.Remove(ing);
        if (a.AjusteGastoId is int gi && await _db.GastosEmpresa.FindAsync(gi) is GastoEmpresa g)
            _db.GastosEmpresa.Remove(g);

        a.AjusteIngresoId = null;
        a.AjusteGastoId = null;
        a.Ajustado = false;
        if (_db.ChangeTracker.HasChanges()) await _db.SaveChangesAsync();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var a = await _db.ArqueosCaja.FindAsync(id);
        if (a is not null)
        {
            await BorrarAjusteAsync(a);
            _db.ArqueosCaja.Remove(a);
            await _db.SaveChangesAsync();
            Aviso = "Control borrado (y el ajuste que había cargado, también).";
        }
        return RedirectToPage();
    }
}

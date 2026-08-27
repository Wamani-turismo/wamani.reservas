using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Reservas;

public class CargarModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public CargarModel(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [BindProperty]
    public Reserva Reserva { get; set; } = new();

    // Pueden ser VARIOS comprobantes (ej: una seña pagada en 2 transferencias)
    [BindProperty]
    public List<IFormFile> SenaArchivo { get; set; } = new();

    [BindProperty]
    public List<IFormFile> SaldoArchivo { get; set; } = new();

    // Lista de excursiones para el desplegable (con precio y mínimo en data-attributes)
    public List<Excursion> Excursiones { get; set; } = new();

    // Pasajeros de la reserva (datos para el seguro)
    public List<Pasajero> Pasajeros { get; set; } = new();

    [BindProperty] public List<int> PasIds { get; set; } = new();
    [BindProperty] public List<string?> PasNombres { get; set; } = new();
    [BindProperty] public List<string?> PasDnis { get; set; } = new();
    [BindProperty] public List<string?> PasFechasNac { get; set; } = new();
    [BindProperty] public List<string?> PasTelefonos { get; set; } = new();
    [BindProperty] public List<string?> PasEmails { get; set; } = new();

    public bool EsNueva => Reserva.Id == 0;

    private async Task CargarExcursionesAsync()
    {
        // La "a medida" va primera: es la que se elige cuando el cliente pide algo que no
        // está en el catálogo, así no hay que buscarla entre todas.
        Excursiones = await _db.Excursiones
            .Where(e => e.Activa || Reserva.ExcursionId == e.Id)
            .OrderByDescending(e => e.EsAMedida)
            .ThenBy(e => e.Nombre)
            .ToListAsync();
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            Reserva = new Reserva { FechaDesde = DateTime.Today, FechaHasta = DateTime.Today };
        }
        else
        {
            var existente = await _db.Reservas.FindAsync(id);
            if (existente is null) return RedirectToPage("/Index");
            Reserva = existente;

            Pasajeros = await _db.Pasajeros
                .Where(p => p.ReservaId == existente.Id)
                .OrderBy(p => p.Id)
                .ToListAsync();
        }

        await CargarExcursionesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Buscar la excursión elegida para congelar nombre/precio/mínimo
        var exc = Reserva.ExcursionId is null
            ? null
            : await _db.Excursiones.FindAsync(Reserva.ExcursionId);

        if (exc is null)
            ModelState.AddModelError("Reserva.ExcursionId", "Elegí una excursión de la lista.");

        if (Reserva.FechaHasta < Reserva.FechaDesde)
            ModelState.AddModelError("Reserva.FechaHasta", "La fecha 'hasta' no puede ser anterior a la fecha 'desde'.");

        if (!ModelState.IsValid)
        {
            await CargarExcursionesAsync();
            return Page();
        }

        // Congelar datos de la excursión en la reserva
        Reserva.Excursion = exc!.Nombre;
        Reserva.MinimoPersonas = exc.MinimoPersonas;
        Reserva.EsTravesia = exc.EsTravesia;

        // La "a medida" no tiene precio de catálogo: siempre se escribe a mano.
        if (exc.EsAMedida) Reserva.PrecioManual = true;

        // Si NO es precio manual, se toma el del catálogo. Si es manual, se respeta el escrito.
        if (!Reserva.PrecioManual)
            Reserva.PrecioPorPersona = exc.PrecioPorPersona;

        // Normalizar estado manual: "" => null (automático)
        if (string.IsNullOrWhiteSpace(Reserva.EstadoManual))
            Reserva.EstadoManual = null;

        // Si cargaste un monto sin poner el día, se toma el de hoy (la Financiera usa esa
        // fecha para saber en qué MES entró la plata).
        if ((Reserva.SenaMonto ?? 0) > 0 && Reserva.SenaFecha is null) Reserva.SenaFecha = DateTime.Today;
        if ((Reserva.SaldoMonto ?? 0) > 0 && Reserva.SaldoFecha is null) Reserva.SaldoFecha = DateTime.Today;

        if (Reserva.Id == 0)
        {
            // ---- Alta ----
            Reserva.CreadaEl = DateTime.Now;
            Reserva.SenaComprobante = await GuardarArchivosAsync(SenaArchivo, null);
            Reserva.SaldoComprobante = await GuardarArchivosAsync(SaldoArchivo, null);
            _db.Reservas.Add(Reserva);
        }
        else
        {
            // ---- Edición ----
            var db = await _db.Reservas.FindAsync(Reserva.Id);
            if (db is null) return RedirectToPage("/Index");

            db.ExcursionId = Reserva.ExcursionId;
            db.Excursion = Reserva.Excursion;
            db.PrecioPorPersona = Reserva.PrecioPorPersona;
            db.PrecioManual = Reserva.PrecioManual;
            db.MinimoPersonas = Reserva.MinimoPersonas;
            db.EsTravesia = Reserva.EsTravesia;
            db.NombreCliente = Reserva.NombreCliente;
            db.CantidadPersonas = Reserva.CantidadPersonas;
            db.Telefono = Reserva.Telefono;
            db.Email = Reserva.Email;
            db.FechaDesde = Reserva.FechaDesde;
            db.FechaHasta = Reserva.FechaHasta;
            db.DescuentoPct = Reserva.DescuentoPct;
            db.DescuentoMonto = Reserva.DescuentoMonto;
            db.DescuentoMotivo = Reserva.DescuentoMotivo;
            db.CantidadMenores = Reserva.CantidadMenores;

            db.SenaMonto = Reserva.SenaMonto;
            db.SenaFecha = Reserva.SenaFecha;
            db.SenaRecibioPor = Reserva.SenaRecibioPor;

            db.SaldoMonto = Reserva.SaldoMonto;
            db.SaldoFecha = Reserva.SaldoFecha;
            db.SaldoRecibioPor = Reserva.SaldoRecibioPor;

            db.EstadoManual = Reserva.EstadoManual;

            // Los comprobantes nuevos se AGREGAN a los que ya había (no los pisan)
            db.SenaComprobante = await GuardarArchivosAsync(SenaArchivo, db.SenaComprobante);
            db.SaldoComprobante = await GuardarArchivosAsync(SaldoArchivo, db.SaldoComprobante);
        }

        await _db.SaveChangesAsync();

        // ---- Pasajeros (datos para el seguro) ----
        var reservaId = Reserva.Id;
        var pasExistentes = await _db.Pasajeros.Where(p => p.ReservaId == reservaId).ToListAsync();
        var pasIdsEnviados = new HashSet<int>();

        for (int i = 0; i < PasNombres.Count; i++)
        {
            var nombre = (PasNombres[i] ?? "").Trim();
            var pasId = i < PasIds.Count ? PasIds[i] : 0;
            var dni = (i < PasDnis.Count ? PasDnis[i] : null)?.Trim();
            var tel = (i < PasTelefonos.Count ? PasTelefonos[i] : null)?.Trim();
            var mail = (i < PasEmails.Count ? PasEmails[i] : null)?.Trim();
            DateTime? fnac = null;
            var fnacTxt = i < PasFechasNac.Count ? PasFechasNac[i] : null;
            if (!string.IsNullOrWhiteSpace(fnacTxt) && DateTime.TryParse(fnacTxt, out var f)) fnac = f;

            var vacio = string.IsNullOrWhiteSpace(nombre) && string.IsNullOrWhiteSpace(dni)
                     && string.IsNullOrWhiteSpace(tel) && string.IsNullOrWhiteSpace(mail) && fnac is null;

            Pasajero? p;
            if (pasId == 0)
            {
                if (vacio) continue;   // fila nueva vacía → ignorar
                p = new Pasajero { ReservaId = reservaId };
                _db.Pasajeros.Add(p);
            }
            else
            {
                p = pasExistentes.FirstOrDefault(x => x.Id == pasId);
                if (p is null) continue;
                pasIdsEnviados.Add(pasId);
                if (vacio) { _db.Pasajeros.Remove(p); continue; }
            }

            p.NombreCompleto = nombre;
            p.Dni = string.IsNullOrWhiteSpace(dni) ? null : dni;
            p.FechaNacimiento = fnac;
            p.Telefono = string.IsNullOrWhiteSpace(tel) ? null : tel;
            p.Email = string.IsNullOrWhiteSpace(mail) ? null : mail;
        }

        // Borrar los pasajeros que se quitaron en la pantalla
        foreach (var e in pasExistentes)
            if (!pasIdsEnviados.Contains(e.Id))
                _db.Pasajeros.Remove(e);

        await _db.SaveChangesAsync();

        // Le pasamos el id al listado para que revise si hay otras reservas en la misma
        // salida (excursión + fecha) y muestre el modal de aviso si corresponde.
        var idGuardado = Reserva.Id;
        return RedirectToPage("/Reservas/Index", new
        {
            Aviso = "Reserva guardada correctamente ✔",
            AvisoCoincidenciaId = idGuardado
        });
    }

    // Descarga el PDF con los datos de los pasajeros para mandarle al seguro
    public async Task<IActionResult> OnGetPdfSeguroAsync(int id)
    {
        var r = await _db.Reservas.FindAsync(id);
        if (r is null) return RedirectToPage("/Reservas/Index");

        var pas = await _db.Pasajeros
            .Where(p => p.ReservaId == id)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var pdf = Services.SeguroPdf.Generar(r, pas);
        var nombre = $"Seguro - {r.NombreCliente} - {r.FechaDesde:dd-MM-yyyy}.pdf";
        return File(pdf, "application/pdf", nombre);
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var r = await _db.Reservas.FindAsync(id);
        if (r is not null)
        {
            var excursionId = r.ExcursionId;
            var fecha = r.FechaDesde;
            var pas = await _db.Pasajeros.Where(p => p.ReservaId == id).ToListAsync();
            _db.Pasajeros.RemoveRange(pas);
            _db.Reservas.Remove(r);
            await _db.SaveChangesAsync();
            await Wamani.Reservas.Services.LimpiezaSalida
                .BorrarOperativoSiSalidaVaciaAsync(_db, excursionId, fecha);
        }
        return RedirectToPage("/Reservas/Index", new { Aviso = "Reserva eliminada." });
    }

    // Guarda uno o varios comprobantes conservando el nombre original,
    // y los agrega a los que ya estaban cargados.
    private async Task<string?> GuardarArchivosAsync(IEnumerable<IFormFile>? archivos, string? actual)
    {
        var carpeta = Wamani.Reservas.Services.Comprobantes.Carpeta(_env);
        return await Wamani.Reservas.Services.Adjuntos.AgregarAsync(archivos, carpeta, actual);
    }
}

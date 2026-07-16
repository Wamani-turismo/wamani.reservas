using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Operativo;

// Datos para renderizar una fila de proveedor (partial _ProvRow)
public class ProvRowVm
{
    public string Tipo { get; set; } = "";
    public OperativoProveedor? Asig { get; set; }
    public List<Proveedor> Cat { get; set; } = new();
    public bool ConPasajero { get; set; }   // hospedaje/restaurante: se pueden agregar varios (por persona)
}

public class SalidaModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public SalidaModel(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [BindProperty(SupportsGet = true)]
    public int ExcursionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime Fecha { get; set; }

    public string ExcursionNombre { get; set; } = "";
    public List<OperativoGasto> Gastos { get; set; } = new();
    public List<Reserva> Reservas { get; set; } = new();
    public OperativoSalida Salida { get; set; } = new();

    // Proveedores: catálogo por tipo y lo asignado a esta salida (puede haber varios por tipo)
    public Dictionary<string, List<Proveedor>> CatalogoPorTipo { get; set; } = new();
    public Dictionary<string, List<OperativoProveedor>> ProvPorTipo { get; set; } = new();

    // Enviados desde el form al guardar
    [BindProperty] public List<int> Ids { get; set; } = new();
    [BindProperty] public List<string> Nombres { get; set; } = new();
    [BindProperty] public List<string> Precios { get; set; } = new();
    [BindProperty] public List<int> Comprados { get; set; } = new();  // ids tildados
    [BindProperty] public bool ServiciosPagados { get; set; }
    [BindProperty] public IFormFile? ComprobanteArchivo { get; set; }

    // Proveedores enviados (una fila puede repetirse por tipo; alineadas por índice)
    [BindProperty] public List<int> ProvIds { get; set; } = new();
    [BindProperty] public List<string> ProvTipos { get; set; } = new();
    [BindProperty] public List<int> ProvProveedorIds { get; set; } = new();
    [BindProperty] public List<string> ProvTotales { get; set; } = new();
    [BindProperty] public List<string> ProvSenas { get; set; } = new();
    [BindProperty] public List<string> ProvSaldos { get; set; } = new();
    [BindProperty] public List<string?> ProvParaQuien { get; set; } = new();

    private async Task CargarAsync()
    {
        var exc = await _db.Excursiones.FindAsync(ExcursionId);
        ExcursionNombre = exc?.Nombre ?? "Excursión";

        Reservas = await _db.Reservas
            .Where(r => r.ExcursionId == ExcursionId && r.FechaDesde.Date == Fecha.Date)
            .OrderBy(r => r.NombreCliente)
            .ToListAsync();

        Gastos = await _db.OperativoGastos
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .OrderBy(o => o.Id)
            .ToListAsync();

        Salida = await _db.OperativoSalidas
            .FirstOrDefaultAsync(s => s.ExcursionId == ExcursionId && s.Fecha.Date == Fecha.Date)
            ?? new OperativoSalida { ExcursionId = ExcursionId, Fecha = Fecha.Date };

        ServiciosPagados = Salida.ServiciosPagados;

        // Proveedores: catálogo activo por tipo + lo ya asignado a la salida
        var catalogo = await _db.Proveedores.Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync();
        CatalogoPorTipo = Proveedor.Tipos.ToDictionary(
            t => t, t => catalogo.Where(p => p.Tipo == t).ToList());

        var asignados = await _db.OperativoProveedores
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .OrderBy(o => o.Id)
            .ToListAsync();
        ProvPorTipo = asignados.GroupBy(o => o.Tipo).ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // La primera vez, copiar los gastos desde la plantilla de la excursión
        bool hayOperativo = await _db.OperativoGastos
            .AnyAsync(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date);

        if (!hayOperativo)
        {
            // Solo los consumibles (por persona / fijo). Los "por auto" y "por guía" son
            // costos de rentabilidad y en el operativo se manejan como Proveedores.
            var plantilla = await _db.GastosExcursion
                .Where(g => g.ExcursionId == ExcursionId
                         && g.TipoCalculo != "Por auto" && g.TipoCalculo != "Por guía")
                .OrderBy(g => g.Id)
                .ToListAsync();

            foreach (var p in plantilla)
            {
                _db.OperativoGastos.Add(new OperativoGasto
                {
                    ExcursionId = ExcursionId,
                    Fecha = Fecha.Date,
                    Nombre = p.Nombre,
                    Precio = p.Precio,
                    Comprado = false
                });
            }
            if (plantilla.Count > 0) await _db.SaveChangesAsync();
        }

        await CargarAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existentes = await _db.OperativoGastos
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .ToListAsync();

        var idsEnviados = new HashSet<int>();

        for (int i = 0; i < Nombres.Count; i++)
        {
            var nombre = (Nombres[i] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nombre)) continue;

            var id = i < Ids.Count ? Ids[i] : 0;
            var precio = ParsePrecio(i < Precios.Count ? Precios[i] : "0");
            var comprado = id != 0 && Comprados.Contains(id);

            if (id == 0)
            {
                _db.OperativoGastos.Add(new OperativoGasto
                {
                    ExcursionId = ExcursionId,
                    Fecha = Fecha.Date,
                    Nombre = nombre,
                    Precio = precio,
                    Comprado = false
                });
            }
            else
            {
                var g = existentes.FirstOrDefault(x => x.Id == id);
                if (g is not null)
                {
                    g.Nombre = nombre;
                    g.Precio = precio;
                    g.Comprado = comprado;

                    // La fecha del gasto se toma sola el día que se carga el monto
                    if (precio > 0 && g.FechaPago is null) g.FechaPago = DateTime.Today;
                    if (precio == 0) g.FechaPago = null;

                    // Comprobante de pago de ESTE gasto (input file "comp_{id}")
                    var archivo = Request.Form.Files[$"comp_{id}"];
                    var guardado = await GuardarArchivoAsync(archivo);
                    if (guardado is not null) g.Comprobante = guardado;

                    idsEnviados.Add(id);
                }
            }
        }

        // Borrar los que se quitaron en la pantalla
        foreach (var g in existentes)
            if (!idsEnviados.Contains(g.Id))
                _db.OperativoGastos.Remove(g);

        // Estado de la salida: servicios pagados + comprobante
        var salida = await _db.OperativoSalidas
            .FirstOrDefaultAsync(s => s.ExcursionId == ExcursionId && s.Fecha.Date == Fecha.Date);
        if (salida is null)
        {
            salida = new OperativoSalida { ExcursionId = ExcursionId, Fecha = Fecha.Date };
            _db.OperativoSalidas.Add(salida);
        }
        salida.ServiciosPagados = ServiciosPagados;

        var nuevoComp = await GuardarArchivoAsync(ComprobanteArchivo);
        if (nuevoComp is not null) salida.Comprobante = nuevoComp;

        // ---- Proveedores por tipo ----
        var provExistentes = await _db.OperativoProveedores
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .ToListAsync();
        var catalogo = await _db.Proveedores.ToDictionaryAsync(p => p.Id, p => p.Nombre);

        var provIdsEnviados = new HashSet<int>();
        for (int i = 0; i < ProvTipos.Count; i++)
        {
            var tipo = ProvTipos[i];
            var rowId = i < ProvIds.Count ? ProvIds[i] : 0;
            var provId = i < ProvProveedorIds.Count ? ProvProveedorIds[i] : 0;
            var total = ParsePrecio(i < ProvTotales.Count ? ProvTotales[i] : "0");
            var sena = ParsePrecio(i < ProvSenas.Count ? ProvSenas[i] : "0");
            var saldo = ParsePrecio(i < ProvSaldos.Count ? ProvSaldos[i] : "0");
            var paraQuien = (i < ProvParaQuien.Count ? ProvParaQuien[i] : null)?.Trim();

            var vacia = provId == 0 && total == 0 && sena == 0 && saldo == 0 && string.IsNullOrWhiteSpace(paraQuien);

            OperativoProveedor? row;
            if (rowId == 0)
            {
                if (vacia) continue;  // fila nueva vacía → ignorar
                row = new OperativoProveedor { ExcursionId = ExcursionId, Fecha = Fecha.Date, Tipo = tipo };
                _db.OperativoProveedores.Add(row);
            }
            else
            {
                row = provExistentes.FirstOrDefault(x => x.Id == rowId);
                if (row is null) continue;
                provIdsEnviados.Add(rowId);
                if (vacia) { _db.OperativoProveedores.Remove(row); continue; }
            }

            row.Tipo = tipo;
            row.ProveedorId = provId == 0 ? null : provId;
            row.ProveedorNombre = provId != 0 && catalogo.TryGetValue(provId, out var n) ? n : "";
            row.Total = total;
            row.Sena = sena;
            row.Saldo = saldo;
            row.ParaQuien = string.IsNullOrWhiteSpace(paraQuien) ? null : paraQuien;

            // Las fechas de pago se toman solas el día que se carga cada monto
            if (sena > 0 && row.FechaSena is null) row.FechaSena = DateTime.Today;
            if (sena == 0) row.FechaSena = null;
            if (saldo > 0 && row.FechaSaldo is null) row.FechaSaldo = DateTime.Today;
            if (saldo == 0) row.FechaSaldo = null;

            // Comprobantes por id de fila (solo filas ya guardadas)
            if (rowId != 0)
            {
                var gSena = await GuardarArchivoAsync(Request.Form.Files[$"provcompsena_{rowId}"]);
                if (gSena is not null) row.ComprobanteSena = gSena;
                var gSaldo = await GuardarArchivoAsync(Request.Form.Files[$"provcompsaldo_{rowId}"]);
                if (gSaldo is not null) row.ComprobanteSaldo = gSaldo;
            }
        }

        // Borrar filas que se quitaron en la pantalla (existían y no volvieron)
        foreach (var e in provExistentes)
            if (!provIdsEnviados.Contains(e.Id))
                _db.OperativoProveedores.Remove(e);

        await _db.SaveChangesAsync();
        return RedirectToPage("/Operativo/Salida",
            new { ExcursionId, Fecha = Fecha.ToString("yyyy-MM-dd"), guardado = true });
    }

    private async Task<string?> GuardarArchivoAsync(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0) return null;
        var carpeta = Wamani.Reservas.Services.Comprobantes.Carpeta(_env);
        Directory.CreateDirectory(carpeta);
        var nombre = $"{Guid.NewGuid():N}{Path.GetExtension(archivo.FileName)}";
        var ruta = Path.Combine(carpeta, nombre);
        using (var stream = new FileStream(ruta, FileMode.Create))
            await archivo.CopyToAsync(stream);
        return $"/comprobantes/{nombre}";
    }

    [BindProperty(SupportsGet = true)]
    public bool Guardado { get; set; }

    private static decimal ParsePrecio(string? txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return 0m;
        txt = txt.Trim().Replace(",", ".");
        return decimal.TryParse(txt, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }
}

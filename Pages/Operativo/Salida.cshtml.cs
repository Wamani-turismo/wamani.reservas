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
    public string Key { get; set; } = "";   // clave para asociar el comprobante a la fila (id real o temporal si es nueva)
    public List<Reserva> Reservas { get; set; } = new();   // reservas de la salida (para elegir de quién es el servicio)
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
    public int PasajerosSalida { get; set; }   // total de personas de la salida (para multiplicar)
    public int PersonasPorAuto => Wamani.Reservas.Models.Excursion.PersonasPorAuto;
    public OperativoSalida Salida { get; set; } = new();

    // Proveedores: catálogo por tipo y lo asignado a esta salida (puede haber varios por tipo)
    public Dictionary<string, List<Proveedor>> CatalogoPorTipo { get; set; } = new();
    public Dictionary<string, List<OperativoProveedor>> ProvPorTipo { get; set; } = new();

    // Enviados desde el form al guardar
    [BindProperty] public List<int> Ids { get; set; } = new();
    [BindProperty] public List<string> Keys { get; set; } = new();    // clave para asociar el comprobante a la fila (id real, o temporal si es nueva)
    [BindProperty] public List<string> Nombres { get; set; } = new();
    [BindProperty] public List<string> Precios { get; set; } = new();   // unitario (auto) o total (a mano) según el modo
    [BindProperty] public List<string> EsManual { get; set; } = new();  // "1" si el monto se cargó a mano
    [BindProperty] public List<int> Comprados { get; set; } = new();  // ids tildados
    [BindProperty] public bool ServiciosPagados { get; set; }
    [BindProperty] public IFormFile? ComprobanteArchivo { get; set; }

    // Proveedores enviados (una fila puede repetirse por tipo; alineadas por índice)
    [BindProperty] public List<int> ProvIds { get; set; } = new();
    [BindProperty] public List<string> ProvKeys { get; set; } = new();   // clave para el comprobante de cada proveedor
    [BindProperty] public List<string> ProvTipos { get; set; } = new();
    [BindProperty] public List<int> ProvProveedorIds { get; set; } = new();
    [BindProperty] public List<string> ProvTotales { get; set; } = new();
    [BindProperty] public List<string> ProvSenas { get; set; } = new();
    [BindProperty] public List<string> ProvSaldos { get; set; } = new();
    [BindProperty] public List<string?> ProvParaQuien { get; set; } = new();
    [BindProperty] public List<int> ProvReservaIds { get; set; } = new();   // a qué reserva pertenece (hospedaje/restaurante)

    private async Task CargarAsync()
    {
        var exc = await _db.Excursiones.FindAsync(ExcursionId);
        ExcursionNombre = exc?.Nombre ?? "Excursión";

        Reservas = await _db.Reservas
            .Where(r => r.ExcursionId == ExcursionId && r.FechaDesde.Date == Fecha.Date)
            .OrderBy(r => r.NombreCliente)
            .ToListAsync();
        PasajerosSalida = Reservas.Sum(r => r.CantidadPersonas);

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
        // Sincronizar los gastos de la excursión con esta salida: agrega los que falten.
        // Así, si después de abrir el operativo agregás gastos a la excursión, aparecen
        // acá la próxima vez que entrás (antes solo se copiaban la PRIMERA vez).
        // Aparecen los gastos a comprar/preparar. Los marcados "es proveedor" (guía, auto,
        // hospedaje, restaurante) NO se materializan acá: cuentan en la Rentabilidad pero se
        // pagan en la sección Proveedores (con seña + saldo).
        int pasajeros = await _db.Reservas
            .Where(r => r.ExcursionId == ExcursionId && r.FechaDesde.Date == Fecha.Date)
            .SumAsync(r => r.CantidadPersonas);

        var plantilla = await _db.GastosExcursion
            .Where(g => g.ExcursionId == ExcursionId && !g.EsProveedor)
            .OrderBy(g => g.Id)
            .ToListAsync();

        var yaCargados = await _db.OperativoGastos
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .ToListAsync();
        var nombresCargados = yaCargados
            .Select(o => (o.Nombre ?? "").Trim().ToLowerInvariant())
            .ToHashSet();

        bool cambio = false;
        foreach (var p in plantilla)
        {
            if (nombresCargados.Contains((p.Nombre ?? "").Trim().ToLowerInvariant())) continue;
            var tipo = string.IsNullOrWhiteSpace(p.TipoCalculo) ? "Por persona" : p.TipoCalculo;
            _db.OperativoGastos.Add(new OperativoGasto
            {
                ExcursionId = ExcursionId,
                Fecha = Fecha.Date,
                Nombre = p.Nombre ?? "",
                TipoCalculo = tipo,
                PrecioUnitario = p.Precio,   // lo que se carga como "por persona/por auto"
                Precio = p.Precio * OperativoGasto.Multiplicador(tipo, pasajeros),  // total
                Comprado = false
            });
            cambio = true;
        }

        // Refrescar el total de los gastos automáticos por si cambió la cantidad de gente
        foreach (var g in yaCargados)
        {
            if (g.PrecioUnitario is decimal u)
            {
                var nuevoTotal = u * OperativoGasto.Multiplicador(g.TipoCalculo, pasajeros);
                if (g.Precio != nuevoTotal) { g.Precio = nuevoTotal; cambio = true; }
            }
        }
        if (cambio) await _db.SaveChangesAsync();

        await CargarAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existentes = await _db.OperativoGastos
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .ToListAsync();

        int pasajeros = await _db.Reservas
            .Where(r => r.ExcursionId == ExcursionId && r.FechaDesde.Date == Fecha.Date)
            .SumAsync(r => r.CantidadPersonas);

        var idsEnviados = new HashSet<int>();

        for (int i = 0; i < Nombres.Count; i++)
        {
            var nombre = (Nombres[i] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nombre)) continue;

            var id = i < Ids.Count ? Ids[i] : 0;
            var valor = ParsePrecio(i < Precios.Count ? Precios[i] : "0");  // unitario (auto) o total (a mano)
            var esManual = i < EsManual.Count && EsManual[i] == "1";
            var comprado = id != 0 && Comprados.Contains(id);

            // El comprobante viaja como archivo "comp_{clave}". La clave es el id real de la
            // fila, o una temporal (ej "n1") si es una fila recién agregada que todavía no
            // tiene id. Así una fila NUEVA también puede traer su comprobante en el mismo guardado.
            var clave = i < Keys.Count ? Keys[i] : id.ToString();

            if (id == 0)
            {
                // Fila nueva agregada a mano en el operativo → el monto es el total directo.
                var nuevo = new OperativoGasto
                {
                    ExcursionId = ExcursionId,
                    Fecha = Fecha.Date,
                    Nombre = nombre,
                    TipoCalculo = "Por persona",
                    PrecioUnitario = null,   // a mano
                    Precio = valor,
                    Comprado = false
                };
                if (valor > 0) nuevo.FechaPago = DateTime.Today;
                nuevo.Comprobante = await GuardarArchivosAsync($"comp_{clave}", null);
                _db.OperativoGastos.Add(nuevo);
            }
            else
            {
                var g = existentes.FirstOrDefault(x => x.Id == id);
                if (g is not null)
                {
                    g.Nombre = nombre;
                    g.Comprado = comprado;

                    if (esManual)
                    {
                        g.PrecioUnitario = null;
                        g.Precio = valor;   // total escrito a mano
                    }
                    else
                    {
                        g.PrecioUnitario = valor;   // precio unitario (por persona / por auto)
                        g.Precio = valor * OperativoGasto.Multiplicador(g.TipoCalculo, pasajeros);
                    }

                    // La fecha del gasto se toma sola el día que se carga el monto
                    if (g.Precio > 0 && g.FechaPago is null) g.FechaPago = DateTime.Today;
                    if (g.Precio == 0) g.FechaPago = null;

                    g.Comprobante = await GuardarArchivosAsync($"comp_{clave}", g.Comprobante);

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

            var resId = i < ProvReservaIds.Count ? ProvReservaIds[i] : 0;

            row.Tipo = tipo;
            row.ReservaId = resId == 0 ? null : resId;
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

            // Comprobantes por clave de fila (funciona también en filas nuevas y admite varios)
            var provKey = i < ProvKeys.Count ? ProvKeys[i] : rowId.ToString();
            row.ComprobanteSena = await GuardarArchivosAsync($"provcompsena_{provKey}", row.ComprobanteSena);
            row.ComprobanteSaldo = await GuardarArchivosAsync($"provcompsaldo_{provKey}", row.ComprobanteSaldo);
        }

        // Borrar filas que se quitaron en la pantalla (existían y no volvieron)
        foreach (var e in provExistentes)
            if (!provIdsEnviados.Contains(e.Id))
                _db.OperativoProveedores.Remove(e);

        await _db.SaveChangesAsync();
        return RedirectToPage("/Operativo/Salida",
            new { ExcursionId, Fecha = Fecha.ToString("yyyy-MM-dd"), guardado = true });
    }

    // Guarda TODOS los archivos que vinieron en ese campo y los agrega a los que ya había
    // (así una seña puede tener 2 o más comprobantes). Devuelve el valor para la columna.
    private async Task<string?> GuardarArchivosAsync(string campo, string? actual)
    {
        var carpeta = Wamani.Reservas.Services.Comprobantes.Carpeta(_env);
        return await Wamani.Reservas.Services.Adjuntos
            .AgregarAsync(Request.Form.Files.GetFiles(campo), carpeta, actual);
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

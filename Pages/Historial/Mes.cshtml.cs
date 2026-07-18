using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;

namespace Wamani.Reservas.Pages.Historial;

// Detalle de un mes: una card por cada salida (excursión + día) de ese mes.
// Adentro, al desplegar: pasajeros (cuánto pagó cada uno) y gastos (cada cosa que se pagó).
public class MesModel : PageModel
{
    private readonly AppDbContext _db;
    public MesModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public string Mes { get; set; } = "";   // formato yyyy-MM

    public bool MesValido { get; set; }
    public string MesTexto { get; set; } = "";

    public class ReservaLinea
    {
        public string Nombre = "";
        public int Personas;
        public decimal Facturado;
        public decimal Cobrado;
        public decimal Pendiente;
        public string Estado = "";
    }

    public class GastoLinea
    {
        public string Nombre = "";
        public decimal Precio;
        public bool Comprado;
        public string? Comprobante;
    }

    public class ProvLinea
    {
        public string Tipo = "";
        public string Nombre = "";
        public decimal Total;
        public decimal Pagado;
    }

    public class SalidaDetalle
    {
        public string Excursion = "";
        public DateTime Fecha;
        public int Pasajeros;
        public decimal Facturado;
        public decimal Cobrado;
        public decimal Gastos;                 // gastos + proveedores
        public decimal Ganancia => Facturado - Gastos;
        public List<ReservaLinea> Reservas = new();
        public List<GastoLinea> ListaGastos = new();
        public List<ProvLinea> Proveedores = new();
    }

    public List<SalidaDetalle> Salidas { get; set; } = new();

    public async Task OnGetAsync()
    {
        var ci = System.Globalization.CultureInfo.GetCultureInfo("es-AR");

        if (!DateTime.TryParse(Mes + "-01", out var primerDia))
        {
            MesValido = false;
            return;
        }
        MesValido = true;
        MesTexto = ci.TextInfo.ToTitleCase(primerDia.ToString("MMMM yyyy", ci));
        var finMes = primerDia.AddMonths(1);

        var reservas = await _db.Reservas
            .Where(r => r.FechaDesde >= primerDia && r.FechaDesde < finMes)
            .ToListAsync();

        var gastos = await _db.OperativoGastos
            .Where(o => o.Fecha >= primerDia && o.Fecha < finMes)
            .ToListAsync();

        var provs = await _db.OperativoProveedores
            .Where(o => o.Fecha >= primerDia && o.Fecha < finMes)
            .ToListAsync();

        Salidas = reservas
            .GroupBy(r => new { r.ExcursionId, r.Excursion, Fecha = r.FechaDesde.Date })
            .Select(g =>
            {
                var exId = g.Key.ExcursionId ?? 0;
                var fecha = g.Key.Fecha;

                var d = new SalidaDetalle
                {
                    Excursion = g.Key.Excursion,
                    Fecha = fecha,
                    Pasajeros = g.Sum(r => r.CantidadPersonas),
                    Facturado = g.Sum(r => r.TotalConDescuento()),
                    Cobrado = g.Sum(r => r.Cobrado()),
                    Reservas = g.OrderBy(r => r.NombreCliente).Select(r => new ReservaLinea
                    {
                        Nombre = r.NombreCliente,
                        Personas = r.CantidadPersonas,
                        Facturado = r.TotalConDescuento(),
                        Cobrado = r.Cobrado(),
                        Pendiente = r.Pendiente(),
                        Estado = r.EstadoActual()
                    }).ToList()
                };

                var gs = gastos.Where(o => o.ExcursionId == exId && o.Fecha.Date == fecha).ToList();
                d.ListaGastos = gs.Select(o => new GastoLinea
                {
                    Nombre = o.Nombre,
                    Precio = o.Precio,
                    Comprado = o.Comprado,
                    Comprobante = o.Comprobante
                }).ToList();

                var ps = provs.Where(o => o.ExcursionId == exId && o.Fecha.Date == fecha).ToList();
                d.Proveedores = ps.Select(o => new ProvLinea
                {
                    Tipo = o.Tipo,
                    Nombre = string.IsNullOrWhiteSpace(o.ProveedorNombre) ? o.Tipo : o.ProveedorNombre,
                    Total = o.Total,
                    Pagado = o.Pagado()
                }).ToList();

                d.Gastos = gs.Sum(o => o.Precio) + ps.Sum(o => o.Total);
                return d;
            })
            .OrderBy(s => s.Fecha)
            .ToList();
    }
}

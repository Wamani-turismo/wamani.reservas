using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Operativo;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public class SalidaResumen
    {
        public int ExcursionId { get; set; }
        public string Excursion { get; set; } = "";
        public DateTime Fecha { get; set; }
        public int Pasajeros { get; set; }
        public int GastosTotal { get; set; }
        public int GastosListos { get; set; }
        public decimal Presupuesto { get; set; }     // total de los gastos a comprar/preparar
        public decimal ProveedoresTotal { get; set; } // total de lo que cuestan los proveedores
        public decimal DeudaProveedores { get; set; } // lo que falta pagarles
        public int Comprobantes { get; set; }        // cuántos comprobantes hay subidos
        // ---- Cuanto sale la salida segun la excursion ----
        // Previsto: la plantilla de costos de la excursion (gastos Y proveedores) mas lo que
        // se contrata para el grupo, a la gente de esta salida. Es el mismo numero de
        // Rentabilidad y de la columna Teorica de "Teorica vs real".
        // Cargado: lo que hay cargado en el operativo (gastos + proveedores).
        // Sin cargar: la diferencia, que es lo que todavia hay que ir a designar. Va aparte,
        // NO sumado a los totales de plata a pagar (ver el comentario largo en Salida.cshtml.cs).
        public decimal Previsto { get; set; }
        public decimal Cargado { get; set; }
        public decimal SinCargar => Math.Max(0, Previsto - Cargado);
        public decimal CargadoDeMas => Math.Max(0, Cargado - Previsto);
        public bool Completo => GastosTotal > 0 && GastosListos >= GastosTotal;
    }

    public List<SalidaResumen> Salidas { get; set; } = new();

    public async Task OnGetAsync()
    {
        var hoy = DateTime.Today;

        var reservas = await _db.Reservas
            .Where(r => r.FechaHasta.Date >= hoy)
            .ToListAsync();

        // Colaborador con acceso limitado: sólo ve las salidas de SUS excursiones.
        // Sin esto vería en la lista los nombres y las salidas de todo Wamani.
        var permitidas = Wamani.Reservas.Services.Permisos.Excursiones(User);
        if (permitidas.Count > 0)
            reservas = reservas.Where(r => r.ExcursionId is int ex && permitidas.Contains(ex)).ToList();

        var plantillaPorExc = (await _db.GastosExcursion.ToListAsync())
            .GroupBy(g => g.ExcursionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var etapasPorExc = (await _db.EtapasExcursion.ToListAsync())
            .GroupBy(e => e.ExcursionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var operativoPorSalida = (await _db.OperativoGastos.ToListAsync())
            .GroupBy(o => (o.ExcursionId, o.Fecha.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        var provPorSalida = (await _db.OperativoProveedores.ToListAsync())
            .GroupBy(o => (o.ExcursionId, o.Fecha.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        Salidas = reservas
            .GroupBy(r => new { r.ExcursionId, r.Excursion, Fecha = r.FechaDesde.Date })
            .Select(g =>
            {
                var exId = g.Key.ExcursionId ?? 0;
                var res = new SalidaResumen
                {
                    ExcursionId = exId,
                    Excursion = g.Key.Excursion,
                    Fecha = g.Key.Fecha,
                    Pasajeros = g.Sum(r => r.CantidadPersonas)
                };

                var provs = provPorSalida.GetValueOrDefault((exId, g.Key.Fecha)) ?? new();
                res.ProveedoresTotal = provs.Sum(x => x.Total);
                res.DeudaProveedores = provs.Sum(x => x.Pendiente());
                // Comprobantes subidos: los de proveedores (seña/saldo)…
                res.Comprobantes = provs.Count(p => !string.IsNullOrEmpty(p.ComprobanteSena))
                                 + provs.Count(p => !string.IsNullOrEmpty(p.ComprobanteSaldo));

                bool hayOperativo = false;
                if (operativoPorSalida.TryGetValue((exId, g.Key.Fecha), out var ops))
                {
                    hayOperativo = true;
                    res.GastosTotal = ops.Count;
                    res.GastosListos = ops.Count(o => o.Comprado);
                    res.Presupuesto = ops.Sum(o => o.Precio);
                    res.Comprobantes += ops.Count(o => !string.IsNullOrEmpty(o.Comprobante));  // …y los de gastos
                }
                else if (plantillaPorExc.TryGetValue(exId, out var plant))
                {
                    res.GastosTotal = plant.Count;
                    res.GastosListos = 0;
                    res.Presupuesto = plant.Sum(p => p.Precio);
                }

                // Cuanto sale la salida segun la excursion, y cuanto de eso esta cargado.
                var itemsPrev = plantillaPorExc.GetValueOrDefault(exId) ?? new();
                var etapasPrev = etapasPorExc.GetValueOrDefault(exId) ?? new();
                res.Previsto = Wamani.Reservas.Services.RentabilidadCalc
                    .Costo(itemsPrev, etapasPrev, res.Pasajeros);
                // OJO: "Cargado" es SOLO lo que esta cargado de verdad en el operativo. Si la
                // salida todavia no tiene operativo armado, el Presupuesto de arriba es la
                // plantilla en crudo (sin multiplicar por la gente) y usarlo aca haria que
                // "sin cargar" diera de menos justo en la salida que no tiene nada cargado.
                res.Cargado = (hayOperativo ? res.Presupuesto : 0) + res.ProveedoresTotal;

                return res;
            })
            .OrderBy(s => s.Fecha)
            .ToList();
    }
}

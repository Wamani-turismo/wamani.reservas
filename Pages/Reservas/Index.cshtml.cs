using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Reservas;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Reserva> Reservas { get; set; } = new();
    public List<SelectListItem> ExcursionesOpciones { get; set; } = new();

    // Total de pasajeros por SALIDA (misma excursión + misma fecha de check-in).
    // Sirve para el aviso de mínimo: una reserva "sola" deja de faltarle gente si
    // otra reserva de la misma excursión y fecha la completa.
    public Dictionary<string, int> PasajerosPorSalida { get; set; } = new();

    public static string ClaveSalida(Reserva r) => $"{r.ExcursionId}|{r.FechaDesde:yyyy-MM-dd}";

    // Salidas con los servicios marcados como pagados a mano (flag de salida)
    public HashSet<string> ServiciosPagados { get; set; } = new();

    // Servicios (gastos) que faltan pagar por salida
    public Dictionary<string, List<string>> ServiciosFaltantes { get; set; } = new();
    public Dictionary<string, int> ServiciosTotal { get; set; } = new();

    public bool TieneServicios(Reserva r) => ServiciosTotal.GetValueOrDefault(ClaveSalida(r), 0) > 0;
    public List<string> ServiciosQueFaltan(Reserva r) => ServiciosFaltantes.GetValueOrDefault(ClaveSalida(r)) ?? new();
    public int FaltanServicios(Reserva r) => ServiciosQueFaltan(r).Count;
    public bool ServiciosTodoPagado(Reserva r)
        => ServiciosPagados.Contains(ClaveSalida(r)) || (TieneServicios(r) && FaltanServicios(r) == 0);

    // Pasajeros totales de la salida de esta reserva (excursión + fecha check-in)
    public int PasajerosDeLaSalida(Reserva r)
        => PasajerosPorSalida.TryGetValue(ClaveSalida(r), out var t) ? t : r.CantidadPersonas;

    // Cuántos faltan para el mínimo, MIRANDO TODA la salida (0 si ya llega)
    public int FaltanEnLaSalida(Reserva r)
        => Math.Max(0, r.MinimoPersonas - PasajerosDeLaSalida(r));

    [BindProperty(SupportsGet = true)]
    public string? Buscar { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FiltroExcursionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FiltroEstado { get; set; }

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateTime? FiltroFecha { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Aviso { get; set; }

    // Id de la reserva recién guardada, para revisar si comparte salida con otras
    [BindProperty(SupportsGet = true)]
    public int? AvisoCoincidenciaId { get; set; }

    public class CoincidenciaInfo
    {
        public string Excursion { get; set; } = "";
        public DateTime Fecha { get; set; }
        public int TotalPasajeros { get; set; }
        public int Minimo { get; set; }
        public int CantidadReservas { get; set; }
        public bool LlegaAlMinimo => TotalPasajeros >= Minimo;
        public List<Reserva> Manuales { get; set; } = new();
        public decimal PrecioNormal { get; set; }
        public int ExcursionId { get; set; }
        // Hospedaje/restaurante ya reservados en esta salida (para acordarse de sumarlos)
        public List<(string Tipo, string Nombre)> ProveedoresReservados { get; set; } = new();
    }

    public CoincidenciaInfo? Coincidencia { get; set; }

    // Las reservas se muestran AGRUPADAS por salida (misma excursión + misma fecha de
    // check-in). Cada reserva sigue siendo independiente (su plata no se mezcla).
    public class GrupoSalida
    {
        public string Clave { get; set; } = "";
        public int ExcursionId { get; set; }
        public string Excursion { get; set; } = "";
        public DateTime FechaDesde { get; set; }
        public DateTime FechaHasta { get; set; }
        public List<Reserva> Reservas { get; set; } = new();
        public int TotalPasajeros { get; set; }   // de TODA la salida (aunque el filtro esconda alguna)
        public int Minimo { get; set; }
        public int Faltan => Math.Max(0, Minimo - TotalPasajeros);
        public bool Sale => Faltan == 0;
    }

    public List<GrupoSalida> Grupos { get; set; } = new();

    // ---- Interesados que podrían unirse a una reserva o a otro interesado ----
    [BindProperty(SupportsGet = true)]
    public bool VerUniones { get; set; }

    public class Candidato
    {
        public string Nombre { get; set; } = "";
        public string? Telefono { get; set; }
        public string Que { get; set; } = "";       // "Reserva" | "Interesado"
        public string Fechas { get; set; } = "";
    }

    public class UnionPosible
    {
        public Interesado Interesado { get; set; } = new();
        public List<Candidato> Con { get; set; } = new();
    }

    public List<UnionPosible> Uniones { get; set; } = new();

    public async Task OnGetAsync()
    {
        ExcursionesOpciones = await _db.Excursiones
            .OrderBy(e => e.Nombre)
            .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Nombre })
            .ToListAsync();

        var precios = await _db.Excursiones.ToDictionaryAsync(e => e.Id, e => e.PrecioPorPersona);

        // Ordenado de la fecha más cercana a la más lejana
        var todas = await _db.Reservas
            .OrderBy(r => r.FechaDesde)
            .ThenBy(r => r.Id)
            .ToListAsync();

        // Sumar pasajeros por salida (excursión + fecha de check-in) usando TODAS las
        // reservas, para que el aviso de mínimo no dependa de los filtros aplicados.
        PasajerosPorSalida = todas
            .GroupBy(r => ClaveSalida(r))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.CantidadPersonas));

        ServiciosPagados = (await _db.OperativoSalidas.Where(s => s.ServiciosPagados).ToListAsync())
            .Select(s => $"{s.ExcursionId}|{s.Fecha:yyyy-MM-dd}")
            .ToHashSet();

        // Servicios que faltan pagar por salida: si el operativo ya se abrió, los gastos
        // NO tildados; si nunca se abrió, todos los gastos de la plantilla de la excursión.
        var opsPorSalida = (await _db.OperativoGastos.ToListAsync())
            .GroupBy(o => $"{o.ExcursionId}|{o.Fecha:yyyy-MM-dd}")
            .ToDictionary(g => g.Key, g => g.ToList());
        var plantillaPorExc = (await _db.GastosExcursion.ToListAsync())
            .GroupBy(g => g.ExcursionId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Nombre).ToList());

        foreach (var r in todas)
        {
            var clave = ClaveSalida(r);
            if (ServiciosFaltantes.ContainsKey(clave)) continue;

            if (opsPorSalida.TryGetValue(clave, out var ops))
            {
                ServiciosFaltantes[clave] = ops.Where(o => !o.Comprado).Select(o => o.Nombre).ToList();
                ServiciosTotal[clave] = ops.Count;
            }
            else if (r.ExcursionId is int exId && plantillaPorExc.TryGetValue(exId, out var plant))
            {
                ServiciosFaltantes[clave] = new List<string>(plant);
                ServiciosTotal[clave] = plant.Count;
            }
            else
            {
                ServiciosFaltantes[clave] = new List<string>();
                ServiciosTotal[clave] = 0;
            }
        }

        // ---- Detección de coincidencias para el modal ----
        if (AvisoCoincidenciaId is int cid)
        {
            var r = todas.FirstOrDefault(x => x.Id == cid);
            if (r is not null && r.ExcursionId is int exId)
            {
                var grupo = todas.Where(o =>
                    o.ExcursionId == exId &&
                    o.FechaDesde.Date <= r.FechaHasta.Date &&
                    o.FechaHasta.Date >= r.FechaDesde.Date).ToList();

                if (grupo.Count > 1)
                {
                    // ¿Esta salida ya tiene hospedaje/restaurante reservado? (para acordarse
                    // de sumarlos a la persona nueva)
                    var provReservados = await _db.OperativoProveedores
                        .Where(o => o.ExcursionId == exId && o.Fecha.Date == r.FechaDesde.Date
                                 && (o.Tipo == "Hospedaje" || o.Tipo == "Restaurante"))
                        .ToListAsync();

                    Coincidencia = new CoincidenciaInfo
                    {
                        Excursion = r.Excursion,
                        Fecha = r.FechaDesde,
                        ExcursionId = exId,
                        TotalPasajeros = grupo.Sum(x => x.CantidadPersonas),
                        Minimo = grupo.Max(x => x.MinimoPersonas),
                        CantidadReservas = grupo.Count,
                        Manuales = grupo.Where(x => x.PrecioManual).ToList(),
                        PrecioNormal = precios.TryGetValue(exId, out var p) ? p : 0m,
                        ProveedoresReservados = provReservados
                            .Where(o => o.Total > 0 || !string.IsNullOrWhiteSpace(o.ProveedorNombre))
                            .Select(o => (o.Tipo, string.IsNullOrWhiteSpace(o.ProveedorNombre) ? "(sin nombre)" : o.ProveedorNombre))
                            .ToList()
                    };
                }
            }
        }

        // ---- Filtros ----
        IEnumerable<Reserva> q = todas;

        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = Buscar.Trim().ToLower();
            q = q.Where(x => x.NombreCliente.ToLower().Contains(b) ||
                             x.Excursion.ToLower().Contains(b));
        }

        if (FiltroExcursionId is int fex)
            q = q.Where(x => x.ExcursionId == fex);

        if (!string.IsNullOrWhiteSpace(FiltroEstado))
            q = q.Where(x => x.EstadoActual() == FiltroEstado);

        if (FiltroFecha is DateTime f)
            q = q.Where(x => x.FechaDesde.Date <= f.Date && x.FechaHasta.Date >= f.Date);

        Reservas = q.ToList();

        // ---- Agrupar por salida (excursión + fecha de check-in) ----
        Grupos = Reservas
            .GroupBy(r => new { Ex = r.ExcursionId ?? 0, F = r.FechaDesde.Date })
            .Select(g =>
            {
                var primera = g.First();
                var clave = ClaveSalida(primera);
                return new GrupoSalida
                {
                    Clave = clave,
                    ExcursionId = g.Key.Ex,
                    Excursion = primera.Excursion,
                    FechaDesde = g.Key.F,
                    FechaHasta = g.Max(r => r.FechaHasta),
                    Reservas = g.OrderBy(r => r.NombreCliente).ToList(),
                    // el total de pasajeros sale de TODA la salida, no de lo filtrado
                    TotalPasajeros = PasajerosPorSalida.GetValueOrDefault(clave, g.Sum(x => x.CantidadPersonas)),
                    Minimo = g.Max(r => r.MinimoPersonas)
                };
            })
            .OrderBy(g => g.FechaDesde)
            .ToList();

        // ---- Interesados que se podrían unir (misma excursión + fechas que se pisan) ----
        var interesados = await _db.Interesados.OrderBy(i => i.FechaDesde).ToListAsync();
        foreach (var i in interesados)
        {
            bool Pisa(DateTime d1, DateTime h1) =>
                d1.Date <= i.FechaHasta.Date && h1.Date >= i.FechaDesde.Date;

            var con = new List<Candidato>();

            // …con reservas ya cargadas
            con.AddRange(todas
                .Where(r => r.ExcursionId == i.ExcursionId && Pisa(r.FechaDesde, r.FechaHasta))
                .Select(r => new Candidato
                {
                    Nombre = r.NombreCliente,
                    Telefono = r.Telefono,
                    Que = "Reserva",
                    Fechas = $"{r.FechaDesde:dd/MM/yy} → {r.FechaHasta:dd/MM/yy}"
                }));

            // …con otros interesados
            con.AddRange(interesados
                .Where(o => o.Id != i.Id && o.ExcursionId == i.ExcursionId && Pisa(o.FechaDesde, o.FechaHasta))
                .Select(o => new Candidato
                {
                    Nombre = o.Nombre,
                    Telefono = o.Telefono,
                    Que = "Interesado",
                    Fechas = $"{o.FechaDesde:dd/MM/yy} → {o.FechaHasta:dd/MM/yy}"
                }));

            if (con.Count > 0)
                Uniones.Add(new UnionPosible { Interesado = i, Con = con });
        }
    }

    // Eliminar una reserva directamente desde la lista (ej: se canceló)
    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        var r = await _db.Reservas.FindAsync(id);
        if (r is not null)
        {
            var pas = await _db.Pasajeros.Where(p => p.ReservaId == id).ToListAsync();
            _db.Pasajeros.RemoveRange(pas);
            _db.Reservas.Remove(r);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("/Reservas/Index", new { Aviso = "Reserva eliminada." });
    }

    // Botón del modal: pone el precio normal de la excursión a UNA o VARIAS reservas juntas
    public async Task<IActionResult> OnPostAjustarPreciosAsync(int[] ids)
    {
        foreach (var id in ids ?? Array.Empty<int>())
        {
            var r = await _db.Reservas.FindAsync(id);
            if (r is not null && r.ExcursionId is int exId)
            {
                var exc = await _db.Excursiones.FindAsync(exId);
                if (exc is not null)
                {
                    r.PrecioManual = false;
                    r.PrecioPorPersona = exc.PrecioPorPersona;
                }
            }
        }
        await _db.SaveChangesAsync();
        return RedirectToPage("/Reservas/Index", new { Aviso = "Precios actualizados al valor normal de la excursión ✔" });
    }
}

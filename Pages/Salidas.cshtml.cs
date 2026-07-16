using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages;

public class SalidasModel : PageModel
{
    private readonly AppDbContext _db;
    public SalidasModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateTime Fecha { get; set; } = DateTime.Today;

    // Una fila = una excursión ese día, con el total de pasajeros sumando todas las reservas.
    public class GrupoSalida
    {
        public string Excursion { get; set; } = "";
        public int MinimoPersonas { get; set; }
        public int TotalPasajeros { get; set; }
        public List<Reserva> Reservas { get; set; } = new();
        public bool Sale => TotalPasajeros >= MinimoPersonas;
        public int Faltan => Math.Max(0, MinimoPersonas - TotalPasajeros);
    }

    public List<GrupoSalida> Grupos { get; set; } = new();

    public async Task OnGetAsync()
    {
        var delDia = await _db.Reservas
            .Where(r => r.FechaDesde.Date <= Fecha.Date && r.FechaHasta.Date >= Fecha.Date)
            .ToListAsync();

        Grupos = delDia
            .GroupBy(r => new { r.ExcursionId, r.Excursion })
            .Select(g => new GrupoSalida
            {
                Excursion = g.Key.Excursion,
                MinimoPersonas = g.Max(r => r.MinimoPersonas),
                TotalPasajeros = g.Sum(r => r.CantidadPersonas),
                Reservas = g.OrderBy(r => r.NombreCliente).ToList()
            })
            .OrderBy(g => g.Excursion)
            .ToList();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Services;

// Calcula costo, ingreso y ganancia de una excursión según la cantidad de pasajeros,
// respetando cómo se cuenta cada costo (por persona / por auto / por guía / fijo).
public static class RentabilidadCalc
{
    public static int AutosPara(int pax)
        => pax <= 0 ? 0 : (int)Math.Ceiling(pax / (double)Excursion.PersonasPorAuto);

    public static decimal Costo(IEnumerable<GastoExcursion> items, int pax)
    {
        int autos = AutosPara(pax);
        decimal total = 0;
        foreach (var g in items)
        {
            total += g.TipoCalculo switch
            {
                "Por auto"  => g.Precio * autos,
                "Por guía"  => g.Precio * autos,  // el chofer es el guía → cuenta como auto
                // Arrieros, caballos, guías, traslados: la cantidad se decide en cada
                // salida. Para la rentabilidad TEÓRICA se usa la cantidad de referencia
                // que se cargó en la excursión.
                "Cantidad"  => g.Precio * (g.Cantidad ?? 0),
                "Fijo"      => g.Precio,
                _            => g.Precio * pax,   // Por persona
            };
        }
        return total;
    }

    public static (decimal Ingreso, decimal Costo, decimal Ganancia, decimal MargenPct)
        Calcular(Excursion exc, IEnumerable<GastoExcursion> items, int pax)
    {
        var costo = Costo(items, pax);
        var ingreso = exc.PrecioPorPersona * pax;
        var ganancia = ingreso - costo;
        var margen = costo > 0 ? Math.Round(ganancia / costo * 100, 0) : 0;
        return (ingreso, costo, ganancia, margen);
    }
}

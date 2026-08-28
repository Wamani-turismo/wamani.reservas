using System;
using System.Collections.Generic;
using System.Linq;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Services;

// Calcula costo, ingreso y ganancia de una excursión según la cantidad de pasajeros.
//
// El costo de una salida tiene DOS partes y hay que sumar las dos:
//
//   1) Los COSTOS SUELTOS de la excursión (GastoExcursion): nafta, entradas, viáticos…
//      Cada uno dice cómo se cuenta: por persona, por auto, por cantidad o fijo.
//
//   2) Lo que se contrata PARA EL GRUPO (EtapaExcursion): las noches de hospedaje, los
//      traslados, los pasajes, el guía, los arrieros y los caballos. En una travesía esto
//      es casi toda la plata, así que sin esta parte la ganancia sale inflada.
//
// La cuenta de cada etapa es la MISMA que hace el operativo cuando arma la salida
// (ver Pages/Operativo/Salida.cshtml.cs) y la que muestra el cartel "cuánto nos sale"
// al pie de la excursión. Si acá se cambia algo, hay que cambiarlo en los tres lados.
public static class RentabilidadCalc
{
    // Autos que hacen falta para la gente que va. Los costos sueltos "Por auto" cuentan
    // sólo a los pasajeros (el chofer es el guía y va igual); los TRASLADOS de las etapas
    // cuentan también a los guías, que ocupan asiento. Es la misma diferencia que hace el
    // operativo, así que los tres números coinciden.
    public static int AutosPara(int pax)
        => pax <= 0 ? 0 : (int)Math.Ceiling(pax / (double)Excursion.PersonasPorAuto);

    public static int AutosPara(int pax, int guias)
        => pax <= 0 ? 0 : (int)Math.Ceiling((pax + guias) / (double)Excursion.PersonasPorAuto);

    // Cuántos guías van según la plantilla. Si la excursión no tiene cargada una etapa de
    // guía, se cuenta 1: nunca sale una salida sin guía.
    public static int GuiasDe(IEnumerable<EtapaExcursion> etapas)
    {
        var g = etapas
            .Where(e => e.Tipo == EtapaExcursion.Guia)
            .Sum(e => e.CantidadReferencia());
        return g <= 0 ? 1 : g;
    }

    // Lo que sale UNA etapa con esa cantidad de gente.
    //
    // Las etapas que todavía no tienen precio cargado NO se cuentan: no se puede adivinar
    // lo que va a salir un refugio, y contarlas en cero mentiría menos que inventarlas.
    public static decimal CostoEtapa(EtapaExcursion e, int pax, int guias)
    {
        if (e.PrecioPorPersona <= 0) return 0;

        var veces = e.Noches > 0 ? e.Noches : 1;

        return e.Tipo switch
        {
            // Un auto cada 4, contando a los guías que viajan
            EtapaExcursion.Traslado => e.PrecioPorPersona * AutosPara(pax, guias) * veces,
            // El micro se paga por boleto: uno por cada cabeza, guías incluidos
            EtapaExcursion.Pasaje   => e.PrecioPorPersona * (pax <= 0 ? 0 : pax + guias) * veces,
            // Guías, arrieros y caballos se contratan POR DÍA y cuántos van no sale de
            // ninguna fórmula: lo deciden los chicos y queda cargado en "Cuántos".
            EtapaExcursion.Guia     => e.PrecioPorPersona * e.CantidadReferencia() * veces,
            EtapaExcursion.Arriero  => e.PrecioPorPersona * e.CantidadReferencia() * veces,
            EtapaExcursion.Caballo  => e.PrecioPorPersona * e.CantidadReferencia() * veces,
            // Hospedaje: duerme toda la gente de la salida, tantas noches como diga la fila
            _                        => e.PrecioPorPersona * pax * veces,
        };
    }

    public static decimal CostoEtapas(IEnumerable<EtapaExcursion> etapas, int pax)
    {
        var lista = etapas as ICollection<EtapaExcursion> ?? etapas.ToList();
        if (lista.Count == 0) return 0;

        var guias = GuiasDe(lista);
        return lista.Sum(e => CostoEtapa(e, pax, guias));
    }

    // Los costos sueltos de la excursión (sin las etapas).
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

    // El costo COMPLETO: los costos sueltos más todo lo que se contrata para el grupo.
    public static decimal Costo(IEnumerable<GastoExcursion> items, IEnumerable<EtapaExcursion> etapas, int pax)
        => Costo(items, pax) + CostoEtapas(etapas, pax);

    public static (decimal Ingreso, decimal Costo, decimal Ganancia, decimal MargenPct)
        Calcular(Excursion exc, IEnumerable<GastoExcursion> items, int pax)
        => Calcular(exc, items, Array.Empty<EtapaExcursion>(), pax);

    public static (decimal Ingreso, decimal Costo, decimal Ganancia, decimal MargenPct)
        Calcular(Excursion exc, IEnumerable<GastoExcursion> items, IEnumerable<EtapaExcursion> etapas, int pax)
    {
        var costo = Costo(items, etapas, pax);
        var ingreso = exc.PrecioPorPersona * pax;
        var ganancia = ingreso - costo;
        var margen = costo > 0 ? Math.Round(ganancia / costo * 100, 0) : 0;
        return (ingreso, costo, ganancia, margen);
    }
}

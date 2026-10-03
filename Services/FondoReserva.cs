using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;

namespace Wamani.Reservas.Services;

// GANANCIA ACUMULADA, mes por mes.
//
// Antes esta clase también llevaba el "fondo del 10%": de la ganancia de cada mes se
// apartaba un 10% automático. Se sacó en octubre de 2026 porque ahora la reinversión se
// decide a mano al cerrar el mes, y el fondo de verdad vive en dólares en otra pantalla
// (ver Models/MovimientoFondo.cs). Dos fondos que decían cosas distintas sobre la misma
// plata confundían más de lo que ayudaban.
//
// Lo que quedó es lo único que se usaba de verdad: cuánto ganó la empresa en total, que es
// la base de la cuenta de cada socio.
public static class FondoReserva
{
    public class Acumulado
    {
        public decimal Ganancia { get; set; }   // suma de las ganancias de todos los meses
        // Toda la ganancia es de los socios: ya no se aparta nada automático.
        public decimal ARepartir => Ganancia;
    }

    public static async Task<Acumulado> AcumuladoAsync(AppDbContext db, DateTime hastaMes)
    {
        var porMes = await GananciaPorMesAsync(db);
        var tope = new DateTime(hastaMes.Year, hastaMes.Month, 1);

        var acu = new Acumulado();
        foreach (var m in porMes.Keys.Where(m => m <= tope))
            acu.Ganancia += porMes[m];
        return acu;
    }

    // Ganancia de cada mes (misma cuenta que la Financiera).
    private static async Task<Dictionary<DateTime, decimal>> GananciaPorMesAsync(AppDbContext db)
    {
        var porMes = new Dictionary<DateTime, decimal>();
        void Sumar(DateTime? f, decimal monto)
        {
            if (f is not DateTime d || monto == 0) return;
            var k = new DateTime(d.Year, d.Month, 1);
            porMes[k] = porMes.GetValueOrDefault(k) + monto;
        }

        foreach (var r in await db.Reservas.ToListAsync())
        {
            Sumar(r.SenaFecha, r.SenaMonto ?? 0);
            Sumar(r.SaldoFecha, r.SaldoMonto ?? 0);
        }
        foreach (var e in await db.IngresosExtra.ToListAsync())
            Sumar(e.Fecha, e.Monto);

        foreach (var o in await db.OperativoGastos.ToListAsync())
            Sumar(o.FechaPago, -o.Precio);

        foreach (var p in await db.OperativoProveedores.ToListAsync())
        {
            Sumar(p.FechaSena, -p.Sena);
            Sumar(p.FechaSaldo, -p.Saldo);
        }

        foreach (var g in await db.GastosEmpresa.ToListAsync())
            Sumar(g.Fecha, -g.Monto);

        return porMes;
    }
}

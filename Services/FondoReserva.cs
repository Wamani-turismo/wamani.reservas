using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;

namespace Wamani.Reservas.Services;

// FONDO DEL 10%: de la ganancia de cada mes se aparta un 10% y se va acumulando.
// Lo que no se gasta queda para el mes siguiente, y así sucesivamente.
//
//   Saldo del mes = saldo que venía de antes + 10% de la ganancia de este mes
//                   − lo que se gastó del fondo este mes.
//
// Un gasto sale del fondo cuando se tilda "Sale del fondo del 10%" en Gastos.
// Ese gasto sigue restando de la ganancia como cualquier otro: lo único que agrega el
// tilde es que además se descuenta del saldo del fondo.
//
// Los meses con pérdida no aportan al fondo (no se aparta el 10% de un número negativo)
// pero tampoco lo achican: el fondo sólo baja cuando se gasta.
public static class FondoReserva
{
    public const decimal Porcentaje = 0.10m;

    public class Mes
    {
        public DateTime MesActual { get; set; }
        public decimal GananciaDelMes { get; set; }   // neta del mes (puede ser negativa)
        public decimal AportadoDelMes { get; set; }   // 10% de la ganancia (0 si hubo pérdida)
        public decimal VieneDeAntes { get; set; }     // saldo acumulado de los meses anteriores
        public decimal GastadoDelMes { get; set; }    // gastos tildados "del fondo" en este mes
        public decimal Saldo => VieneDeAntes + AportadoDelMes - GastadoDelMes;
    }

    // Calcula el fondo hasta el mes indicado (inclusive), recorriendo todos los meses
    // anteriores desde el primer movimiento que exista.
    public static async Task<Mes> CalcularAsync(AppDbContext db, DateTime mes)
    {
        var hasta = new DateTime(mes.Year, mes.Month, 1);

        // ---- Todos los movimientos de plata, agrupados por mes ----
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

        var gastosEmpresa = await db.GastosEmpresa.ToListAsync();
        foreach (var g in gastosEmpresa)
            Sumar(g.Fecha, -g.Monto);

        // ---- Lo gastado del fondo, por mes ----
        var gastadoPorMes = gastosEmpresa
            .Where(g => g.DelFondo)
            .GroupBy(g => new DateTime(g.Fecha.Year, g.Fecha.Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Monto));

        // ---- Acumular mes a mes hasta el mes pedido ----
        var resultado = new Mes
        {
            MesActual = hasta,
            GananciaDelMes = porMes.GetValueOrDefault(hasta),
            AportadoDelMes = Math.Round(Math.Max(0, porMes.GetValueOrDefault(hasta)) * Porcentaje, 2),
            GastadoDelMes = gastadoPorMes.GetValueOrDefault(hasta)
        };

        decimal acumulado = 0;
        var meses = porMes.Keys.Concat(gastadoPorMes.Keys).Distinct().Where(m => m < hasta).OrderBy(m => m);
        foreach (var m in meses)
        {
            acumulado += Math.Round(Math.Max(0, porMes.GetValueOrDefault(m)) * Porcentaje, 2);
            acumulado -= gastadoPorMes.GetValueOrDefault(m);
        }
        resultado.VieneDeAntes = acumulado;

        return resultado;
    }

    // Saldo del fondo a día de hoy (contando todos los meses hasta el actual).
    public static async Task<decimal> SaldoHoyAsync(AppDbContext db)
    {
        var hoy = DateTime.Today;
        var m = await CalcularAsync(db, new DateTime(hoy.Year, hoy.Month, 1));
        return m.Saldo;
    }
}

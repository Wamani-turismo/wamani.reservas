using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;

namespace Wamani.Reservas.Services;

// LA PLATA QUE HAY, en un solo lugar.
//
// Esta cuenta se hacía copiada en tres pantallas (Caja, Financiera y el control de caja).
// Tres copias de la misma fórmula es la receta para que un día den números distintos y no
// se sepa cuál creer, así que vive acá y las tres la llaman.
//
//   Caja            lo que entró menos lo que salió de verdad.
//   Patrimonio      la Caja más lo que pusieron los socios menos lo que se llevaron.
//   Caja operativa  lo que queda en el día a día (Mercado Pago): el patrimonio menos lo
//                   que ya se apartó al fondo de inversión, que está en otra cuenta.
//
// OJO con los egresos: sólo cuenta la plata que REALMENTE salió. Un gasto del operativo
// sin fecha de pago es una estimación copiada de la plantilla de la excursión (todavía no
// se compró ni se pagó), así que no puede restar. Igual con la seña y el saldo de cada
// proveedor: sólo restan si tienen su fecha de pago cargada.
public static class CajaCalc
{
    public class Foto
    {
        public decimal Ingresos { get; set; }
        public decimal Egresos { get; set; }
        public decimal Aportes { get; set; }
        public decimal Retiros { get; set; }
        public decimal MandadoAlFondo { get; set; }

        public decimal Caja => Ingresos - Egresos;
        public decimal Patrimonio => Caja + Aportes - Retiros;
        public decimal CajaOperativa => Patrimonio - MandadoAlFondo;
    }

    public static async Task<Foto> CalcularAsync(AppDbContext db)
    {
        var reservas = await db.Reservas.ToListAsync();
        var extras = await db.IngresosExtra.ToListAsync();

        var f = new Foto
        {
            Ingresos = reservas.Sum(r => (r.SenaMonto ?? 0) + (r.SaldoMonto ?? 0))
                     + extras.Sum(e => e.Monto),
        };

        var egGastos = (await db.OperativoGastos.ToListAsync())
            .Where(o => o.FechaPago != null).Sum(o => o.Precio);
        var egProv = (await db.OperativoProveedores.ToListAsync())
            .Sum(p => (p.FechaSena != null ? p.Sena : 0) + (p.FechaSaldo != null ? p.Saldo : 0));
        var egEmpresa = (await db.GastosEmpresa.ToListAsync()).Sum(g => g.Monto);
        f.Egresos = egGastos + egProv + egEmpresa;

        f.Aportes = (await db.Aportes.ToListAsync()).Sum(a => a.Monto);
        f.Retiros = (await db.Retiros.ToListAsync()).Sum(r => r.Monto);
        f.MandadoAlFondo = (await db.MovimientosFondo.ToListAsync())
            .Where(m => m.SaleDeLaCaja).Sum(m => m.Pesos);

        return f;
    }
}

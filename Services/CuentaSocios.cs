using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;

namespace Wamani.Reservas.Services;

// LA CUENTA DE CADA SOCIO: cuánto ganó, cuánto ya se llevó y cuánto le queda.
//
//   Ganado   = su parte de todas las ganancias acumuladas (ya apartado el fondo del 10%).
//   Retirado = lo que sacó, cargado en Caja → "Retiros de los socios".
//   Aportado = la plata que puso, cargada en Caja → "Aportes / Inversiones".
//   Saldo    = ganado − retirado + aportado. Es lo que la empresa todavía le debe.
//
// Los retiros y aportes se asocian al socio por el nombre que se escribió en "Quién".
// Si ese nombre no coincide con ninguno de los socios, el monto queda como "sin asignar"
// para que se vea que hay algo mal cargado en vez de desaparecer de la cuenta.
public static class CuentaSocios
{
    public class Socio
    {
        public string Nombre { get; set; } = "";
        public decimal Ganado { get; set; }
        public decimal Retirado { get; set; }
        public decimal Aportado { get; set; }
        public decimal Saldo => Ganado - Retirado + Aportado;
    }

    public class Resultado
    {
        public decimal GananciaAcumulada { get; set; }   // de toda la operación
        public decimal AlFondo { get; set; }             // 10% apartado
        public decimal ARepartir { get; set; }           // ganancia − fondo
        public decimal PorSocio { get; set; }            // a repartir / cantidad de socios

        public List<Socio> Socios { get; set; } = new();

        public decimal RetiradoTotal => Socios.Sum(s => s.Retirado) + RetirosSinAsignar;
        public decimal AportadoTotal => Socios.Sum(s => s.Aportado) + AportesSinAsignar;

        // Retiros/aportes cuyo "Quién" no coincide con ningún socio
        public decimal RetirosSinAsignar { get; set; }
        public decimal AportesSinAsignar { get; set; }
        public bool HaySinAsignar => RetirosSinAsignar != 0 || AportesSinAsignar != 0;
    }

    public static async Task<Resultado> CalcularAsync(AppDbContext db, string[] duenos, DateTime hastaMes)
    {
        var tope = new DateTime(hastaMes.Year, hastaMes.Month, 1).AddMonths(1);   // exclusivo

        var acu = await FondoReserva.AcumuladoAsync(db, hastaMes);

        var r = new Resultado
        {
            GananciaAcumulada = acu.Ganancia,
            AlFondo = acu.AlFondo,
            ARepartir = acu.ARepartir,
            PorSocio = duenos.Length == 0 ? 0 : Math.Round(acu.ARepartir / duenos.Length, 2)
        };

        var retiros = (await db.Retiros.ToListAsync()).Where(x => x.Fecha < tope).ToList();
        var aportes = (await db.Aportes.ToListAsync()).Where(x => x.Fecha < tope).ToList();

        static bool EsDe(string? quien, string dueno)
            => !string.IsNullOrWhiteSpace(quien)
               && quien.Trim().Contains(dueno, StringComparison.OrdinalIgnoreCase);

        foreach (var d in duenos)
        {
            r.Socios.Add(new Socio
            {
                Nombre = d,
                Ganado = r.PorSocio,
                Retirado = retiros.Where(x => EsDe(x.Quien, d)).Sum(x => x.Monto),
                Aportado = aportes.Where(x => EsDe(x.Quien, d)).Sum(x => x.Monto)
            });
        }

        r.RetirosSinAsignar = retiros.Where(x => !duenos.Any(d => EsDe(x.Quien, d))).Sum(x => x.Monto);
        r.AportesSinAsignar = aportes.Where(x => !duenos.Any(d => EsDe(x.Quien, d))).Sum(x => x.Monto);

        return r;
    }
}

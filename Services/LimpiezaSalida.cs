using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;

namespace Wamani.Reservas.Services;

public static class LimpiezaSalida
{
    // Al borrar una reserva: si en esa SALIDA (excursión + día) ya no queda ninguna
    // otra reserva, borra los datos del operativo de esa salida (gastos, proveedores y
    // estado) para que no queden "colgados" sumando en Finanzas.
    // Si todavía quedan otras reservas en la misma salida, NO se borra nada.
    public static async Task BorrarOperativoSiSalidaVaciaAsync(AppDbContext db, int? excursionId, DateTime fechaDesde)
    {
        if (excursionId is null) return;
        var exId = excursionId.Value;
        var desde = fechaDesde.Date;
        var hasta = desde.AddDays(1);

        bool quedanReservas = await db.Reservas
            .AnyAsync(r => r.ExcursionId == exId && r.FechaDesde >= desde && r.FechaDesde < hasta);
        if (quedanReservas) return;

        await db.OperativoGastos
            .Where(o => o.ExcursionId == exId && o.Fecha >= desde && o.Fecha < hasta)
            .ExecuteDeleteAsync();
        await db.OperativoProveedores
            .Where(o => o.ExcursionId == exId && o.Fecha >= desde && o.Fecha < hasta)
            .ExecuteDeleteAsync();
        await db.OperativoSalidas
            .Where(o => o.ExcursionId == exId && o.Fecha >= desde && o.Fecha < hasta)
            .ExecuteDeleteAsync();

        // Los INGRESOS EXTRA de esa salida son otra cosa: no son una estimación copiada de
        // la plantilla, son plata que entró de verdad y está en la caja. Borrarlos acá
        // descuadraría la caja sin que nadie se enterara.
        //
        // Así que no se borran: se les saca la salida (que ya no existe) y quedan como un
        // ingreso extra suelto en la pantalla Extras, con una línea que dice de dónde venía.
        // La plata sigue estando y se puede ver; lo que desaparece es la salida.
        var extras = await db.IngresosExtra
            .Where(o => o.ExcursionId == exId && o.FechaSalida >= desde && o.FechaSalida < hasta)
            .ToListAsync();

        if (extras.Count > 0)
        {
            var nombre = await db.Excursiones.AsNoTracking()
                .Where(x => x.Id == exId).Select(x => x.Nombre).FirstOrDefaultAsync();

            foreach (var x in extras)
            {
                var rastro = $"Era de la salida de «{nombre ?? "excursión"}» del {desde:dd/MM/yyyy}, " +
                             "que se borró al quedar sin reservas.";
                x.Comentario = string.IsNullOrWhiteSpace(x.Comentario)
                    ? rastro
                    : x.Comentario + System.Environment.NewLine + rastro;
                x.ExcursionId = null;
                x.FechaSalida = null;
            }
            await db.SaveChangesAsync();
        }
    }
}

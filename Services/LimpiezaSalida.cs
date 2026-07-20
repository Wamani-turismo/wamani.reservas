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
    }
}

using System;

namespace Wamani.Reservas.Services;

// Qué día es HOY en Jujuy.
//
// Render corría sus máquinas con la hora de Londres (UTC), tres horas adelante de acá:
// entre las 21:00 y la medianoche, para el servidor ya era el día siguiente y un pago
// cargado a las 22:00 del lunes quedaba fechado el martes. Esto se hizo para el
// operativo, que es donde se estampa la plata.
//
// Desde el 30/08/2026 el contenedor entero está en la hora de Jujuy (TZ y tzdata en el
// Dockerfile), así que DateTime.Today y DateTime.Now ya dan bien en todo el sistema y
// esto devuelve exactamente lo mismo que DateTime.Today.
//
// Se deja igual a propósito: no depende de que el servidor tenga la tabla de zonas
// instalada, así que si algún día se rompe esa parte del Dockerfile, las fechas de los
// pagos siguen saliendo bien. Argentina está en UTC-3 todo el año (no movemos el reloj
// desde 2009), así que restar tres horas alcanza.
public static class Reloj
{
    public const int HorasDeDiferencia = -3;

    // OJO con esto: Postgres RECHAZA que le escriban una fecha marcada como "hora de
    // Londres" (UTC) en sus columnas de fecha, y tira un error que tumba el guardado.
    // Como DateTime.UtcNow viene marcada así, hay que sacarle la marca: la fecha ya está
    // convertida a la hora de Jujuy, no necesita zona. DateTime.Today y DateTime.Now,
    // que es lo que usa el resto del sistema, tampoco vienen marcadas como UTC.
    private static DateTime SinZona(DateTime f) => DateTime.SpecifyKind(f, DateTimeKind.Unspecified);

    // El día de hoy en Jujuy (sin hora).
    public static DateTime HoyJujuy() => SinZona(DateTime.UtcNow.AddHours(HorasDeDiferencia).Date);

    // El momento exacto, en hora de Jujuy (con hora y minutos).
    public static DateTime AhoraJujuy() => SinZona(DateTime.UtcNow.AddHours(HorasDeDiferencia));
}

using System;

namespace Wamani.Reservas.Services;

// Qué día es HOY en Jujuy.
//
// El servidor de Render corre con la hora de Londres (UTC), tres horas adelante de acá.
// Entre las 21:00 y la medianoche, para el servidor ya es el día siguiente: un pago
// cargado a las 22:00 del lunes quedaba fechado el martes. Con las fechas de pago ahora
// a la vista y editables, eso se vería como un error todo el tiempo.
//
// Argentina está en UTC-3 todo el año (no movemos el reloj desde 2009), así que restar
// tres horas alcanza y no depende de que el servidor tenga instalada la tabla de zonas.
public static class Reloj
{
    public const int HorasDeDiferencia = -3;

    public static DateTime HoyJujuy() => DateTime.UtcNow.AddHours(HorasDeDiferencia).Date;
}

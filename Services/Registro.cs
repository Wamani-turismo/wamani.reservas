using System.Security.Claims;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Services;

// Anota en la tabla Actividad quién movió plata. Nunca frena el guardado:
// si algo falla acá, se ignora, porque perder el registro es mucho menos grave
// que perder la reserva o el operativo que la persona acaba de cargar.
public static class Registro
{
    public static void Anotar(AppDbContext db, ClaimsPrincipal user,
                              string que, string detalle, int? excursionId, decimal diferencia)
    {
        try
        {
            if (diferencia == 0m) return;   // no se movió plata: no hay nada que anotar

            db.Actividades.Add(new Actividad
            {
                Fecha       = DateTime.Now,
                Usuario     = user?.Identity?.Name ?? "?",
                Nombre      = user?.Identity?.Name ?? "?",
                Que         = que,
                Detalle     = detalle.Length > 300 ? detalle[..300] : detalle,
                ExcursionId = excursionId,
                Monto       = Math.Abs(diferencia),
                EsIngreso   = diferencia > 0m
            });
        }
        catch { /* el registro es informativo: nunca puede romper el guardado */ }
    }
}

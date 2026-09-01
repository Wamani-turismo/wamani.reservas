using System.Security.Claims;

namespace Wamani.Reservas.Services
{
    // Lee, desde la sesión, a qué excursiones puede entrar el que está usando el sistema.
    //
    // Vacío = socio de Wamani: ve todo, como siempre.
    // Con números = colaborador de afuera: sólo esas excursiones.
    //
    // El candado de verdad está en Program.cs (todo prohibido salvo una lista corta de
    // pantallas). Esto es para las pantallas que además muestran VARIAS excursiones y hay
    // que filtrar por dentro: el operativo, la lista de excursiones, la de rentabilidad y
    // el desplegable de reservas.
    public static class Permisos
    {
        public const string Claim = "excursiones_permitidas";

        public static List<int> Excursiones(ClaimsPrincipal? usuario)
        {
            var txt = usuario?.FindFirst(Claim)?.Value;
            if (string.IsNullOrWhiteSpace(txt)) return new List<int>();
            return txt.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Select(x => int.TryParse(x, out var n) ? n : 0)
                      .Where(n => n > 0).Distinct().ToList();
        }

        // ¿Es un colaborador con acceso limitado?
        public static bool EsLimitado(ClaimsPrincipal? usuario) => Excursiones(usuario).Count > 0;

        // ¿Puede entrar a esta excursión? Un socio de Wamani puede entrar a todas.
        public static bool Puede(ClaimsPrincipal? usuario, int excursionId)
        {
            var lista = Excursiones(usuario);
            return lista.Count == 0 || lista.Contains(excursionId);
        }
    }
}

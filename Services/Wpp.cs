using System;
using System.Linq;

namespace Wamani.Reservas.Services;

// Arma links de WhatsApp a partir de un teléfono suelto (formato Argentina).
public static class Wpp
{
    // "388 777 8899" -> "5493887778899". Devuelve null si no hay número.
    public static string? Numero(string? tel)
    {
        if (string.IsNullOrWhiteSpace(tel)) return null;
        var digitos = new string(tel.Where(char.IsDigit).ToArray());
        if (digitos.Length == 0) return null;
        digitos = digitos.TrimStart('0');
        if (digitos.StartsWith("15")) digitos = digitos.Substring(2);
        if (!digitos.StartsWith("54")) digitos = "549" + digitos;
        return digitos;
    }

    public static string? Link(string? tel, string mensaje)
    {
        var n = Numero(tel);
        if (n is null) return null;
        return $"https://wa.me/{n}?text={Uri.EscapeDataString(mensaje)}";
    }
}

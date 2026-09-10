using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.ViewComponents;

// La campanita de arriba a la derecha: lista de a quiénes hay que cobrarles el saldo.
// Regla: siempre se cobra seña; el saldo se cobra 4 días antes (excursión) o 7 días
// antes (travesía) del inicio. Aparecen las reservas con saldo pendiente cuya ventana
// de cobro ya empezó y todavía no salieron.
public class RecordatoriosViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public RecordatoriosViewComponent(AppDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var hoy = DateTime.Today;
        var reservas = await _db.Reservas.ToListAsync();

        var aCobrar = reservas
            .Where(r => r.HayQueCobrarSaldo(hoy))
            .OrderBy(r => r.FechaDesde)
            .ToList();

        return View(aCobrar);
    }

    // Convierte un teléfono suelto a número apto para WhatsApp (formato Argentina).
    // Ej: "388 777 8899" -> "5493887778899". Devuelve null si no hay número.
    public static string? WhatsAppNumero(string? tel)
    {
        if (string.IsNullOrWhiteSpace(tel)) return null;
        var digitos = new string(tel.Where(char.IsDigit).ToArray());
        if (digitos.Length == 0) return null;
        digitos = digitos.TrimStart('0');
        // Sacar el "15" de celulares locales si quedó al principio del número
        if (digitos.StartsWith("15")) digitos = digitos.Substring(2);
        if (!digitos.StartsWith("54")) digitos = "549" + digitos;
        return digitos;
    }

    // Arma el link completo de WhatsApp con un mensaje ya escrito para cobrar el saldo.
    public static string? WhatsAppLink(Reserva r)
    {
        var num = WhatsAppNumero(r.Telefono);
        if (num is null) return null;

        var ci = CultureInfo.GetCultureInfo("es-AR");
        var saldo = "$ " + r.Pendiente().ToString("N0", ci);
        var msg = $"¡Hola {r.NombreCliente.Trim()}! Te escribo de Wamani por tu reserva de " +
                  $"{r.Excursion.Trim()} del {r.FechaDesde:dd/MM}. Te recuerdo que queda pendiente " +
                  $"el saldo de {saldo}. ¡Cualquier cosa avisame!";
        return $"https://wa.me/{num}?text={Uri.EscapeDataString(msg)}";
    }
}

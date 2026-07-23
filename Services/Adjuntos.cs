using System.Text;

namespace Wamani.Reservas.Services;

// Manejo de comprobantes adjuntos. Permite guardar VARIOS archivos en un mismo campo
// (ej: una seña pagada con 2 comprobantes) y conservar el NOMBRE original del archivo.
//
// En la base se guarda un texto con las direcciones separadas por "|", por ejemplo:
//   /comprobantes/a1b2c3d4-Recibo_banco.jpg|/comprobantes/e5f6g7h8-Transferencia.pdf
// El nombre original queda dentro del nombre del archivo (después del primer guion),
// así se puede mostrar sin cambiar la base de datos.
public static class Adjuntos
{
    private const char SEP = '|';

    // ---------- Leer ----------

    public static List<string> Urls(string? valor)
        => string.IsNullOrWhiteSpace(valor)
            ? new List<string>()
            : valor.Split(SEP, StringSplitOptions.RemoveEmptyEntries).ToList();

    public static int Cantidad(string? valor) => Urls(valor).Count;

    // Nombre lindo para mostrar (el original que tenía el archivo cuando se subió)
    public static string Nombre(string url)
    {
        var archivo = Path.GetFileName(url ?? "");
        var guion = archivo.IndexOf('-');

        // Comprobantes viejos (se guardaban con un código, sin el nombre real):
        // mostrar algo legible en vez del código.
        if (guion < 0)
        {
            var sinExt = Path.GetFileNameWithoutExtension(archivo);
            if (sinExt.Length >= 24 && sinExt.All(Uri.IsHexDigit))
                return "Comprobante" + Path.GetExtension(archivo);
            return archivo;
        }

        var nombre = guion < archivo.Length - 1 ? archivo[(guion + 1)..] : archivo;
        return Uri.UnescapeDataString(nombre).Replace('_', ' ');
    }

    // Lista lista para mostrar: (dirección, nombre)
    public static List<(string Url, string Nombre)> Listar(string? valor)
        => Urls(valor).Select(u => (u, Nombre(u))).ToList();

    // ---------- Guardar ----------

    // Guarda los archivos nuevos y los AGREGA a los que ya había.
    // Devuelve el valor final para la columna (null si no hay nada).
    public static async Task<string?> AgregarAsync(IEnumerable<IFormFile>? archivos, string carpeta, string? actual)
    {
        var urls = Urls(actual);
        if (archivos is not null)
        {
            foreach (var a in archivos)
            {
                if (a is null || a.Length == 0) continue;
                Directory.CreateDirectory(carpeta);
                var nombreArchivo = $"{Guid.NewGuid():N}"[..8] + "-" + Limpiar(a.FileName);
                var ruta = Path.Combine(carpeta, nombreArchivo);
                using (var stream = new FileStream(ruta, FileMode.Create))
                    await a.CopyToAsync(stream);
                urls.Add("/comprobantes/" + nombreArchivo);
            }
        }
        return urls.Count == 0 ? null : string.Join(SEP, urls);
    }

    // Deja el nombre del archivo apto para una dirección web (sin espacios ni raros)
    private static string Limpiar(string nombreOriginal)
    {
        var solo = Path.GetFileName(nombreOriginal ?? "archivo");
        var sb = new StringBuilder();
        foreach (var c in solo)
        {
            if (char.IsLetterOrDigit(c) || c == '.' || c == '_') sb.Append(c);
            else if (c == ' ' || c == '-') sb.Append('_');
        }
        var limpio = sb.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(limpio)) limpio = "archivo";
        if (limpio.Length > 60) limpio = limpio[^60..];   // que no quede kilométrico
        return limpio;
    }
}

using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Wamani.Reservas.Services;

// Manda los avisos por mail cuando alguien deja una consulta en la web.
//
// La clave NO está en el código ni en el repositorio: se lee de las variables de entorno
// que se cargan en Render (Environment). Son dos:
//
//   MAIL_USUARIO = wamaniturismo@gmail.com
//   MAIL_CLAVE   = la "contraseña de aplicación" de 16 letras que da Google
//                  (NO es la clave con la que se entra a Gmail)
//
// Opcionales, por si algún día se cambia de correo:
//   MAIL_SERVIDOR (por defecto smtp.gmail.com)
//   MAIL_PUERTO   (por defecto 587)
//   MAIL_DESTINO  (a quién le llega el aviso; por defecto, el mismo MAIL_USUARIO)
//
// Si las variables no están cargadas, NO se rompe nada: simplemente no manda el mail y la
// consulta igual queda guardada en el sistema.
public static class Correo
{
    private static string? Var(string nombre) =>
        Environment.GetEnvironmentVariable(nombre) is string v && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    public static string? Usuario => Var("MAIL_USUARIO");
    public static string? Clave => Var("MAIL_CLAVE");
    public static string Destino => Var("MAIL_DESTINO") ?? Usuario ?? "";
    private static string Servidor => Var("MAIL_SERVIDOR") ?? "smtp.gmail.com";
    private static int Puerto => int.TryParse(Var("MAIL_PUERTO"), out var p) ? p : 587;

    // ¿Está configurado el envío?
    public static bool Configurado => Usuario is not null && Clave is not null;

    // Manda un mail. Devuelve null si salió bien, o el motivo del error si falló.
    // NUNCA tira una excepción hacia afuera: que no ande el mail no puede tumbar la web.
    public static async Task<string?> EnviarAsync(string asunto, string cuerpo, string? responderA = null)
    {
        if (!Configurado) return "El envío de mails todavía no está configurado.";

        try
        {
            using var mensaje = new MailMessage
            {
                From = new MailAddress(Usuario!, "Web de Wamani"),
                Subject = asunto,
                Body = cuerpo,
                IsBodyHtml = false
            };
            mensaje.To.Add(Destino);

            // Así, al apretar "Responder" se le contesta directo a quien consultó
            if (!string.IsNullOrWhiteSpace(responderA))
            {
                try { mensaje.ReplyToList.Add(new MailAddress(responderA)); } catch { /* mail mal escrito */ }
            }

            using var cliente = new SmtpClient(Servidor, Puerto)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(Usuario, Clave),
                Timeout = 15000
            };

            await cliente.SendMailAsync(mensaje);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}

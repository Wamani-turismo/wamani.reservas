using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // ═══════════════════════════════════════════════════════════════════
    //  Contenido editable de la LANDING (la página pública de Wamani).
    //  Se edita desde el menú "🌐 Web" del sistema. La página lo lee y
    //  lo muestra a los visitantes. NO tiene nada que ver con las reservas.
    // ═══════════════════════════════════════════════════════════════════

    // Datos generales de la web (hay una sola fila, Id = 1)
    public class ContenidoWeb
    {
        public int Id { get; set; }

        [Display(Name = "WhatsApp (con código de país, ej: 5491178898516)")]
        public string Whatsapp { get; set; } = "5491178898516";

        [Display(Name = "Usuario de Instagram (sin @)")]
        public string Instagram { get; set; } = "wamaniturismo";

        [Display(Name = "Link de Facebook")]
        public string Facebook { get; set; } = "https://www.facebook.com/profile.php?id=61573110601017";

        [Display(Name = "Link de Linktree")]
        public string Linktree { get; set; } = "https://linktr.ee/Wamaniturismo";

        [Display(Name = "Email")]
        public string Email { get; set; } = "wamaniturismo@gmail.com";

        [Display(Name = "Ubicación (texto)")]
        public string Ubicacion { get; set; } = "San Salvador de Jujuy, Argentina";

        [Display(Name = "Frase del inicio (debajo del nombre WAMANI)")]
        public string HeroTexto { get; set; } =
            "Conocemos los caminos secretos de la Quebrada, la Puna y la selva de las Yungas. Vení a descubrirlos con nosotros.";

        [Display(Name = "Quiénes somos — párrafo 1")]
        public string Quienes1 { get; set; } =
            "Wamani nació hace más de 4 años del amor de tres amigos jujeños por su tierra. Crecimos entre estos cerros: conocemos los colores de la Quebrada, la inmensidad de la Puna y cada sendero escondido de la selva de las Yungas.";

        [Display(Name = "Quiénes somos — párrafo 2")]
        public string Quienes2 { get; set; } =
            "No somos una agencia más: somos los que caminamos estos paisajes toda la vida, y queremos mostrarte el Jujuy que no aparece en las postales — las termas secretas, los pueblos donde el tiempo se detuvo, los atardeceres que no se olvidan.";

        [Display(Name = "Quiénes somos — frase final")]
        public string Quienes3 { get; set; } =
            "Vos elegís la experiencia. Nosotros nos ocupamos del resto.";
    }

    // Una excursión o travesía que se muestra en la web (aparte de las de reservas)
    public class ExcursionWeb
    {
        public int Id { get; set; }

        [Display(Name = "Clave interna (sin espacios, ej: termas-jordan)")]
        public string Clave { get; set; } = "";

        [Required(ErrorMessage = "Poné el nombre")]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = "";

        [Display(Name = "Etiqueta (ej: Día completo · Travesía · 3 días)")]
        public string Chip { get; set; } = "Día completo";

        [Display(Name = "Es travesía (varios días)")]
        public bool EsTravesia { get; set; } = false;

        [Display(Name = "Color (R,G,B — ej: 58,110,90)")]
        public string Color { get; set; } = "58,110,90";

        [Display(Name = "Foto principal (nombre de archivo)")]
        public string Foto { get; set; } = "";

        // Fotos adicionales para la galería de la ficha (un nombre de archivo por renglón)
        [Display(Name = "Fotos adicionales")]
        public string Fotos { get; set; } = "";

        [Display(Name = "Descripción corta")]
        public string Resumen { get; set; } = "";

        // Estos tres se escriben con UN dato por renglón (una línea = un ítem)
        [Display(Name = "Datos (uno por renglón, ej: 🕐 10 hs)")]
        public string Datos { get; set; } = "";

        [Display(Name = "Itinerario (un paso por renglón)")]
        public string Itinerario { get; set; } = "";

        [Display(Name = "Qué incluye (uno por renglón)")]
        public string Incluye { get; set; } = "";

        [Display(Name = "Qué llevar (texto)")]
        public string Llevar { get; set; } = "";

        [Display(Name = "Orden (número; más chico aparece primero)")]
        public int Orden { get; set; } = 100;

        [Display(Name = "Mostrar en la web")]
        public bool Activa { get; set; } = true;
    }

    // Un testimonio de un viajero
    public class TestimonioWeb
    {
        public int Id { get; set; }

        [Display(Name = "Comentario")]
        public string Texto { get; set; } = "";

        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = "";

        [Display(Name = "De dónde es / qué hizo")]
        public string Lugar { get; set; } = "";

        [Display(Name = "Orden")]
        public int Orden { get; set; } = 100;

        [Display(Name = "Mostrar")]
        public bool Activo { get; set; } = true;
    }

    // Un video (testimonio o momento) que se muestra en la web
    public class VideoWeb
    {
        public int Id { get; set; }

        [Display(Name = "Título")]
        public string Titulo { get; set; } = "";

        [Display(Name = "Descripción corta")]
        public string Descripcion { get; set; } = "";

        [Display(Name = "Archivo de video (mp4)")]
        public string Archivo { get; set; } = "";

        [Display(Name = "Foto de portada (opcional)")]
        public string Poster { get; set; } = "";

        [Display(Name = "Es vertical (video de celular)")]
        public bool Vertical { get; set; } = true;

        [Display(Name = "Orden")]
        public int Orden { get; set; } = 100;

        [Display(Name = "Mostrar en la web")]
        public bool Activo { get; set; } = true;
    }

    // Un integrante del equipo
    public class IntegranteWeb
    {
        public int Id { get; set; }

        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = "";

        [Display(Name = "Rol (ej: Guía y fundador)")]
        public string Rol { get; set; } = "";

        [Display(Name = "Descripción")]
        public string Bio { get; set; } = "";

        [Display(Name = "Foto (nombre de archivo)")]
        public string Foto { get; set; } = "";

        [Display(Name = "Orden")]
        public int Orden { get; set; } = 100;
    }
}

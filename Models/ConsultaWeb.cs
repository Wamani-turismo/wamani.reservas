using System;
using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Una consulta que alguien dejó en la web, con el formulario de contacto.
    //
    // Se guarda SIEMPRE, aunque el mail de aviso falle: la idea es que no se pierda ni una.
    // Se ven en el panel, en "Consultas".
    public class ConsultaWeb
    {
        public int Id { get; set; }

        [Required, MaxLength(160)]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = "";

        [MaxLength(160)]
        [Display(Name = "Mail")]
        public string? Email { get; set; }

        [MaxLength(60)]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        // "Viajero" o "Agencia": de dónde salió la consulta. La de agencia viene de la
        // landing de /receptivo y conviene contestarla distinto.
        [MaxLength(20)]
        public string Tipo { get; set; } = "Viajero";

        [Required, MaxLength(4000)]
        [Display(Name = "Mensaje")]
        public string Mensaje { get; set; } = "";

        // De qué página vino, para saber si la trajo el QR de la feria
        [MaxLength(60)]
        public string? Origen { get; set; }

        public DateTime CreadaEl { get; set; } = DateTime.Now;

        // Se tilda cuando ya se le contestó
        public bool Atendida { get; set; }

        // Sólo para mostrar en pantalla: no es una columna de la tabla.
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool EsAgencia => Tipo == "Agencia";
    }
}

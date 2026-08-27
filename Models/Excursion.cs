using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Catálogo de excursiones/travesías. Editable desde la web (no está fijo en el código).
    public class Excursion
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Poné el nombre de la excursión")]
        [MaxLength(120)]
        [Display(Name = "Excursión / Travesía")]
        public string Nombre { get; set; } = "";

        [Range(0, 999999999)]
        [Display(Name = "Precio por persona")]
        public decimal PrecioPorPersona { get; set; }

        [Range(1, 200)]
        [Display(Name = "Mínimo de personas")]
        public int MinimoPersonas { get; set; } = 2;

        [Range(1, 200)]
        [Display(Name = "Máximo de personas")]
        public int MaximoPersonas { get; set; } = 8;

        // Cantidad de guías que van (fijo, editable). Se puede cambiar.
        [Range(0, 20)]
        [Display(Name = "Cantidad de guías")]
        public int CantidadGuias { get; set; } = 1;

        // Cuántas personas entran por auto (para dividir el costo del auto)
        public const int PersonasPorAuto = 4;

        // Si es travesía (varios días), el saldo se cobra 7 días antes; si no, 4 días antes.
        [Display(Name = "Es travesía (el saldo se cobra 7 días antes en vez de 4)")]
        public bool EsTravesia { get; set; } = false;

        // Salida armada a pedido del cliente: no tiene precio de catálogo ni itinerario fijo.
        // Al elegirla en una reserva, el precio por persona se escribe siempre a mano.
        // Sirve para llevar gente a lugares donde no hacemos excursiones regulares.
        [Display(Name = "Excursión a medida (el precio se carga a mano en cada reserva)")]
        public bool EsAMedida { get; set; } = false;

        // Nombre de la excursión a medida que se crea sola la primera vez.
        public const string NombreAMedida = "Excursión a medida";

        // Si se desactiva, no aparece para cargar reservas nuevas (pero no se pierde el historial)
        [Display(Name = "Activa")]
        public bool Activa { get; set; } = true;

        // ---------- Contenido para la Guía (PDF) ----------
        // Se cargan a mano por excursión y quedan guardados (varían en cada una).
        [Display(Name = "Breve guía de la excursión / travesía")]
        public string? GuiaBreve { get; set; }

        [Display(Name = "Recomendaciones (qué llevar)")]
        public string? Recomendaciones { get; set; }

        [Display(Name = "Lugares para visitar")]
        public string? LugaresVisitar { get; set; }
    }
}

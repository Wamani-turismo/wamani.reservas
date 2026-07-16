using System;
using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Gente que consultó por una excursión pero todavía no reservó.
    // Si coincide en excursión y fechas con una reserva (u otro interesado),
    // el sistema avisa para poder unirlos y completar la salida.
    public class Interesado
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Poné el nombre")]
        [MaxLength(160)]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = "";

        [MaxLength(40)]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [Display(Name = "Excursión")]
        public int? ExcursionId { get; set; }

        [MaxLength(120)]
        public string Excursion { get; set; } = "";   // snapshot del nombre

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha desde")]
        public DateTime FechaDesde { get; set; } = DateTime.Today;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha hasta")]
        public DateTime FechaHasta { get; set; } = DateTime.Today;

        public DateTime CreadoEl { get; set; } = DateTime.Now;
    }
}

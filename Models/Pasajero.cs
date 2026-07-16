using System;
using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Datos de cada pasajero de una reserva (los que pide el SEGURO).
    public class Pasajero
    {
        public int Id { get; set; }

        public int ReservaId { get; set; }

        [Required(ErrorMessage = "Poné el nombre completo")]
        [MaxLength(160)]
        [Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; } = "";

        [MaxLength(20)]
        [Display(Name = "DNI")]
        public string? Dni { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha de nacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [MaxLength(40)]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [MaxLength(160)]
        [Display(Name = "Mail")]
        public string? Email { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Un aporte / inversión a la empresa: un socio pone plata de su bolsillo, o se
    // separa parte de lo ganado para invertir (indumentaria, auto, publicidad, etc.).
    // Suma al patrimonio de la empresa.
    public class Aporte
    {
        public int Id { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        [MaxLength(80)]
        [Display(Name = "Quién aportó")]
        public string? Quien { get; set; }

        [MaxLength(160)]
        [Display(Name = "En qué / nota")]
        public string? Descripcion { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Monto")]
        public decimal Monto { get; set; }

        public string? Comprobante { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Un retiro de dinero de la caja de la empresa (un socio saca plata del capital).
    // Reduce el patrimonio de la empresa.
    public class Retiro
    {
        public int Id { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        [MaxLength(80)]
        [Display(Name = "Quién retiró")]
        public string? Quien { get; set; }

        [MaxLength(160)]
        [Display(Name = "Motivo / nota")]
        public string? Descripcion { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Monto")]
        public decimal Monto { get; set; }

        public string? Comprobante { get; set; }
    }
}

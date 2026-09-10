using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Plata que entra por fuera de las reservas: comisiones por alquilar un auto o
    // conseguir un hospedaje, venta de algo, un servicio suelto, etc. Se carga a mano
    // y suma como ingreso en Finanzas y en la Caja, por su fecha.
    public class IngresoExtra
    {
        public int Id { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        // Para poder agrupar después (no limita nada: siempre se puede escribir el detalle)
        [MaxLength(40)]
        [Display(Name = "Motivo")]
        public string Motivo { get; set; } = "Comisión";

        [Required(ErrorMessage = "Poné una descripción")]
        [MaxLength(160)]
        [Display(Name = "Descripción")]
        public string Descripcion { get; set; } = "";

        // De quién vino (cliente, agencia, proveedor). Opcional.
        [MaxLength(120)]
        [Display(Name = "De quién")]
        public string? DeQuien { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Monto")]
        public decimal Monto { get; set; }

        public string? Comprobante { get; set; }

        public static readonly string[] Motivos =
            { "Comisión", "Alquiler de auto", "Hospedaje", "Servicio suelto", "Venta", "Otro" };
    }
}

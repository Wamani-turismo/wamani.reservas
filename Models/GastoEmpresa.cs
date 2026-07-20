using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Gasto GENERAL de la empresa (publicidad, botiquín, etc.), no atado a una excursión.
    // Se carga a mano y se descuenta del neto del mes según su fecha.
    public class GastoEmpresa
    {
        public int Id { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        // "Fijo" (todos los meses) | "Variable" (una vez)
        [MaxLength(20)]
        [Display(Name = "Tipo")]
        public string Tipo { get; set; } = "Fijo";

        [Required(ErrorMessage = "Poné una descripción")]
        [MaxLength(160)]
        [Display(Name = "Descripción")]
        public string Descripcion { get; set; } = "";

        [Range(0, 999999999)]
        [Display(Name = "Monto")]
        public decimal Monto { get; set; }

        public string? Comprobante { get; set; }

        public static readonly string[] Tipos = { "Fijo", "Variable" };
    }
}

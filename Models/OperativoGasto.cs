using System;
using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Estado de un gasto para una SALIDA puntual (excursión + fecha de check-in).
    // Se copia de la plantilla (GastoExcursion) la primera vez que se abre el operativo
    // de esa salida. El monto se puede ajustar y se tilda cuando está comprado/preparado.
    public class OperativoGasto
    {
        public int Id { get; set; }

        public int ExcursionId { get; set; }

        // Fecha de salida (check-in) de la excursión
        public DateTime Fecha { get; set; }

        [Required, MaxLength(80)]
        public string Nombre { get; set; } = "";

        [Range(0, 999999999)]
        public decimal Precio { get; set; }

        public bool Comprado { get; set; }

        // Comprobante de pago de ESTE gasto (foto o PDF)
        public string? Comprobante { get; set; }

        // Día en que se cargó/pagó este gasto (se toma solo). La Financiera lo usa para
        // saber en qué MES impacta el egreso, sin importar cuándo sale la excursión.
        public DateTime? FechaPago { get; set; }
    }
}

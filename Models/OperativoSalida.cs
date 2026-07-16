using System;

namespace Wamani.Reservas.Models
{
    // Estado a nivel SALIDA (excursión + fecha): si ya se pagaron los servicios/gastos
    // y el comprobante de ese pago.
    public class OperativoSalida
    {
        public int Id { get; set; }
        public int ExcursionId { get; set; }
        public DateTime Fecha { get; set; }
        public bool ServiciosPagados { get; set; }
        public string? Comprobante { get; set; }
    }
}

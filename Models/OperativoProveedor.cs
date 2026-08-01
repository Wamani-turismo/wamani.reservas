using System;

namespace Wamani.Reservas.Models
{
    // Proveedor asignado a una SALIDA (excursión + fecha), con su pago (seña + saldo),
    // cada pago con su propio comprobante.
    public class OperativoProveedor
    {
        public int Id { get; set; }
        public int ExcursionId { get; set; }
        public DateTime Fecha { get; set; }

        // "Guía" | "Hospedaje" | "Restaurante" | "Auto"
        public string Tipo { get; set; } = "";

        // A qué RESERVA pertenece este servicio (hospedaje/restaurante son por persona).
        // Los compartidos (guía, auto) van sin reserva (null) = a nivel salida.
        public int? ReservaId { get; set; }

        public int? ProveedorId { get; set; }
        public string ProveedorNombre { get; set; } = "";   // snapshot del nombre

        // Para qué pasajero es (hospedaje/restaurante pueden ir por persona; ej: "Guada")
        public string? ParaQuien { get; set; }

        public decimal Total { get; set; }   // costo total del proveedor
        public decimal Sena { get; set; }    // monto de la seña
        public decimal Saldo { get; set; }   // monto del saldo

        public string? ComprobanteSena { get; set; }
        public string? ComprobanteSaldo { get; set; }

        // Días en que se pagaron (se toman solos). La Financiera los usa para saber en qué
        // MES impacta cada egreso.
        public DateTime? FechaSena { get; set; }
        public DateTime? FechaSaldo { get; set; }

        public decimal Pagado() => Sena + Saldo;
        public decimal Pendiente() => Math.Max(0, Total - Pagado());
        public bool TieneDeuda() => Total > 0 && Pendiente() > 0;
    }
}

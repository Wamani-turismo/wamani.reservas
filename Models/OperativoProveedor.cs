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

        // En una TRAVESÍA: en qué lugar de la ruta es este hospedaje (ej. "YUTO PAMPA").
        // Sale de las etapas de la excursión. Permite ver una sola fila por lugar
        // (personas × precio) en vez de una fila por pasajero en cada refugio.
        // En las excursiones de un día queda en null y no cambia nada.
        public string? Lugar { get; set; }

        // Cuánta gente y a qué precio, cuando la fila representa a TODO el grupo en un
        // lugar de la travesía. El Total se calcula solo: personas × precio × noches.
        //
        // Estos dos campos también se usan en las filas de GUÍA y de AUTO, donde significan
        // "cuántos" y "cuánto cobra cada uno" (2 guías × $60.000). La cuenta es la misma;
        // lo único que cambia es cómo se llaman en la pantalla.
        public int? Personas { get; set; }
        public decimal? PrecioPorPersona { get; set; }

        // Cuántas noches cubre esta fila (sale de la etapa). null o 0 se toman como 1,
        // así las filas viejas siguen valiendo exactamente lo mismo que antes.
        public int? Noches { get; set; }

        // Las noches que hay que contar de verdad (nunca menos de 1).
        public int NochesReales() => Noches is int n && n > 0 ? n : 1;

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

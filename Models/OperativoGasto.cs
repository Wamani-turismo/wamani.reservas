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

        // A qué RESERVA pertenece este gasto. Los gastos "por persona" (hospedaje, comidas,
        // entradas…) van por reserva. Los compartidos ("por auto" / "fijo": nafta, guía…)
        // van sin reserva (null) = una sola vez para toda la salida.
        public int? ReservaId { get; set; }

        [Required, MaxLength(80)]
        public string Nombre { get; set; } = "";

        // "Precio" es el TOTAL de lo que se pagó por este ítem en esta salida (lo que
        // suma en Finanzas). Si el gasto es "por persona"/"por auto", el total se calcula
        // solo = precio unitario × cantidad (personas o autos).
        [Range(0, 999999999)]
        public decimal Precio { get; set; }

        // Precio por persona o por auto (lo que se carga a mano). Si es null, el gasto se
        // cargó "a mano" (el total se escribe directo en Precio, sin multiplicar).
        public decimal? PrecioUnitario { get; set; }

        // Cómo se cuenta: "Por persona" | "Por auto" | "Fijo" (copiado de la excursión).
        [MaxLength(20)]
        public string TipoCalculo { get; set; } = "Por persona";

        // Cuántas veces se multiplica el precio unitario según el tipo y la gente de la salida.
        public static int Multiplicador(string tipo, int pasajeros) => tipo switch
        {
            "Por auto" => pasajeros <= 0 ? 0 : (int)System.Math.Ceiling(pasajeros / (double)Excursion.PersonasPorAuto),
            "Fijo"     => 1,
            _           => pasajeros,   // Por persona
        };

        // El total a pagar por este ítem: unitario × multiplicador; o el Precio directo si es "a mano".
        public decimal TotalPara(int pasajeros)
            => PrecioUnitario is decimal u ? u * Multiplicador(TipoCalculo, pasajeros) : Precio;

        public bool Comprado { get; set; }

        // Comprobante de pago de ESTE gasto (foto o PDF)
        public string? Comprobante { get; set; }

        // Día en que se cargó/pagó este gasto (se toma solo). La Financiera lo usa para
        // saber en qué MES impacta el egreso, sin importar cuándo sale la excursión.
        public DateTime? FechaPago { get; set; }
    }
}

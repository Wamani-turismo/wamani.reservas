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

        // Desglose escrito a mano (el botón 📋), igual que en los gastos de la empresa.
        // NO entra en ninguna cuenta.
        [Display(Name = "Detalle")]
        public string? Comentario { get; set; }

        // "Rendimientos" son los intereses que paga Mercado Pago por tener la plata en la
        // cuenta remunerada. Es plata ganada de verdad, no un ajuste: suma a la ganancia del
        // mes como cualquier otro ingreso. El control de caja los carga con este motivo.
        public static readonly string[] Motivos =
            { "Comisión", "Rendimientos", "Alquiler de auto", "Hospedaje", "Traslado",
              "Servicio suelto", "Venta", "Otro" };

        // ---- De qué SALIDA es este ingreso (opcional) ----
        //
        // Hay ingresos extra que nacen de una salida puntual: un traslado que hubo que
        // sumar porque el avión aterrizó en otra provincia, una noche más, una entrada que
        // no estaba. Esos se cargan DESDE el operativo de la salida y quedan atados a ella,
        // así se puede ver, salida por salida, cuánta plata de más entró y con qué gasto.
        //
        // Los que no tienen salida dueña (venta de remeras, alquiler de bastones, una
        // comisión de la rentadora) se siguen cargando en Extras y quedan en null.
        //
        // Una salida es "excursión + día", igual que en el operativo: hacen falta los dos
        // datos para identificarla.
        public int? ExcursionId { get; set; }

        [DataType(DataType.Date)]
        public DateTime? FechaSalida { get; set; }

        public bool EsDeSalida => ExcursionId is int && FechaSalida is DateTime;
    }
}

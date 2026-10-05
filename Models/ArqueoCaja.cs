using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // CONTROL DE CAJA (arqueo)
    //
    // Al cerrar el mes se cuenta la plata que hay de verdad en Mercado Pago y se compara
    // con lo que dice el sistema. Casi nunca da exacto, y la diferencia es un dato, no un
    // error que haya que tapar:
    //
    //   SOBRANTE   hay más plata de la que el sistema dice. Suele ser plata que entró y no
    //              se anotó: los rendimientos que paga Mercado Pago por tener la plata en
    //              la cuenta remunerada, una comisión que se cobró y nadie cargó.
    //   FALTANTE   hay menos. Suele ser un gasto que se pagó y no se cargó.
    //
    // Guardar cada control deja el rastro: la próxima vez se compara contra el anterior y
    // se ve si la diferencia es nueva o viene arrastrada de antes.
    //
    // Y si se sabe de dónde viene, se puede ACOMODAR LA CAJA con un solo tilde: el sistema
    // carga el ingreso extra (si sobró) o el gasto de empresa (si faltó) y a partir de ahí
    // la caja coincide con el banco. El ajuste queda atado a este control: si se borra el
    // control, se borra el ajuste, así que nunca queda plata inventada dando vueltas.
    public class ArqueoCaja
    {
        public const string Rendimientos = "Rendimientos de Mercado Pago";
        public const string NoAnotado = "Algo no se anotó";
        public const string Duplicado = "Algo quedó cargado dos veces";
        public const string Redondeo = "Diferencia de redondeo";
        public const string NoSabemos = "No sabemos de dónde viene";
        public const string Otro = "Otro";

        public static readonly string[] Motivos =
            { Rendimientos, NoAnotado, Duplicado, Redondeo, NoSabemos, Otro };

        public int Id { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha del control")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        // La plata que hay de verdad: lo que muestra la cuenta de Mercado Pago ese día.
        [Range(0, 999999999)]
        [Display(Name = "Lo que hay de verdad")]
        public decimal SaldoReal { get; set; }

        // Lo que decía el sistema ese día. Se guarda la foto porque mañana cambia: si sólo
        // guardáramos la diferencia, dentro de un mes no se podría reconstruir de dónde salió.
        [Range(-999999999, 999999999)]
        [Display(Name = "Lo que decía el sistema")]
        public decimal SaldoSistema { get; set; }

        [MaxLength(60)]
        [Display(Name = "De dónde viene")]
        public string Motivo { get; set; } = NoSabemos;

        [MaxLength(300)]
        [Display(Name = "Nota")]
        public string? Nota { get; set; }

        public string? Comprobante { get; set; }

        // ---- El ajuste, si se decidió acomodar la caja ----
        // Se guarda de qué lado se cargó y con qué Id, para poder borrarlo después.
        [Display(Name = "Se acomodó la caja")]
        public bool Ajustado { get; set; }
        public int? AjusteIngresoId { get; set; }
        public int? AjusteGastoId { get; set; }

        // Positiva: sobra plata. Negativa: falta.
        public decimal Diferencia => SaldoReal - SaldoSistema;
        public bool EsSobrante => Diferencia > 0;
        public bool EsFaltante => Diferencia < 0;
        public bool Cuadra => Diferencia == 0;

        public string Etiqueta => Cuadra ? "Cuadra" : (EsSobrante ? "Sobrante" : "Faltante");
    }
}

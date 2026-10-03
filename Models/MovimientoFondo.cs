using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // FONDO DE INVERSIÓN
    //
    // La plata que Wamani aparta para crecer: la FIT, una computadora, las radios, lo que
    // sea que no es el costo de vender una excursión. Se guarda en dólares, aparte de la
    // caja del día a día, para que no se gaste sin querer.
    //
    // Tres movimientos y cada uno toca cosas distintas:
    //
    //   Aporte      un socio pone plata de afuera. Entra al fondo. NO sale de la caja,
    //               porque esa plata nunca estuvo ahí.
    //   Desde caja  se cerró un buen mes y se manda parte al fondo. La plata sigue siendo
    //               de Wamani, sólo cambia de bolsillo: baja la caja operativa y sube el
    //               fondo. La caja TOTAL no cambia.
    //   Gasto       se compra algo con la plata del fondo. Baja el fondo y nada más: no
    //               toca la caja operativa ni la ganancia del mes, porque esa plata ya se
    //               había apartado antes. Descontarla de nuevo sería contarla dos veces.
    public class MovimientoFondo
    {
        public const string Aporte = "Aporte";
        public const string DesdeCaja = "Desde caja";
        public const string Gasto = "Gasto";
        public static readonly string[] Tipos = { Aporte, DesdeCaja, Gasto };

        public int Id { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        [MaxLength(20)]
        [Display(Name = "Qué es")]
        public string Tipo { get; set; } = Aporte;

        [Required(ErrorMessage = "Poné de qué se trata")]
        [MaxLength(160)]
        [Display(Name = "Concepto")]
        public string Concepto { get; set; } = "";

        // Quién lo puso (sólo en los aportes de un socio)
        [MaxLength(80)]
        [Display(Name = "Quién")]
        public string? Quien { get; set; }

        // El fondo vive en DÓLARES: ésa es la plata de verdad. Los pesos son el reflejo al
        // cambio del día en que se movió, y quedan guardados para no perder el dato.
        [Range(0, 99999999)]
        [Display(Name = "Dólares")]
        public decimal Dolares { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Pesos")]
        public decimal Pesos { get; set; }

        // A cuánto estaba el dólar ese día. Es informativo: sirve para entender de dónde
        // salió el monto en pesos cuando se mire esto dentro de un año.
        [Range(0, 9999999)]
        [Display(Name = "Tipo de cambio")]
        public decimal TipoCambio { get; set; }

        public string? Comprobante { get; set; }

        [MaxLength(300)]
        [Display(Name = "Nota")]
        public string? Nota { get; set; }

        // Cuánto suma o resta al fondo: los gastos restan, lo demás suma.
        public decimal SignoDolares => Tipo == Gasto ? -Dolares : Dolares;
        public decimal SignoPesos => Tipo == Gasto ? -Pesos : Pesos;

        // ¿Esta plata salió de la caja operativa? Sólo cuando se mandó desde la caja.
        public bool SaleDeLaCaja => Tipo == DesdeCaja;
    }
}

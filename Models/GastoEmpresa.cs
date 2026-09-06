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

        // Si se tilda, este gasto se pagó con el FONDO (el 10% que se aparta de la ganancia
        // de cada mes). Sigue siendo un gasto normal de la empresa: lo único que cambia es
        // que además se descuenta del saldo del fondo, para saber cuánto queda ahí.
        [Display(Name = "Sale del fondo del 10%")]
        public bool DelFondo { get; set; } = false;

        // INVERSIÓN: la FIT, la computadora, las radios, el stand… todo lo que se compra
        // para que la empresa crezca y queda como algo de la empresa. No es un costo de
        // vender excursiones, así que NO baja la ganancia del mes: si no, un mes bueno
        // parece malo nada más porque se compró algo. Sale del fondo de inversión (la
        // plata que pusieron los socios más las ganancias que se van reinvirtiendo).
        [Display(Name = "Es una inversión")]
        public bool EsInversion { get; set; } = false;

        // ¿Este gasto baja la ganancia del mes? Sólo los gastos corrientes (publicidad,
        // suscripciones, viáticos…). Los del fondo del 10% y los de inversión no, porque
        // esa plata ya se había apartado antes: descontarla de nuevo sería contarla dos
        // veces. En la Caja los tres restan igual, porque ahí se mide la plata que salió.
        //
        // La regla vive acá y no en cada pantalla a propósito: la ganancia se calcula en
        // cinco lugares distintos (Financiera del mes, del año, por período, el fondo del
        // 10% y la cuenta de los socios) y si se escribe cinco veces, tarde o temprano una
        // queda vieja y los números dejan de cerrar entre sí.
        public bool RestaDeLaGanancia => !DelFondo && !EsInversion;

        public static readonly string[] Tipos = { "Fijo", "Variable" };
    }
}

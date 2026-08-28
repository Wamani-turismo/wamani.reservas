using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Un gasto/ítem que implica una excursión (AUTO, NAFTA, GUÍA, ENTRADAS, ...).
    // El precio es POR SALIDA (por excursión), no por persona. Se define una vez por
    // excursión y se puede ajustar después en cada salida (ver OperativoGasto).
    public class GastoExcursion
    {
        public int Id { get; set; }

        public int ExcursionId { get; set; }

        [Required, MaxLength(80)]
        public string Nombre { get; set; } = "";

        [Range(0, 999999999)]
        public decimal Precio { get; set; }

        // Cómo se cuenta este costo en la rentabilidad:
        // "Por persona" (× pasajeros) | "Por auto" (× cantidad de autos, 1 cada 4; incluye
        // al guía porque el chofer es el guía) | "Cantidad" (× las unidades que se carguen
        // a mano en cada salida) | "Fijo" (una vez por salida).
        [MaxLength(20)]
        public string TipoCalculo { get; set; } = "Por persona";

        // Sólo para el tipo "Cantidad": cuántas unidades se suelen llevar. Es la sugerencia
        // con la que arranca cada salida; ahí se sube o se baja con los botones + y −.
        //
        // Existe porque arrieros, caballos, guías y traslados NO salen de una cuenta fija:
        // dependen de cuánta gente va y de cómo se arme esa salida en particular. Una
        // travesía puede salir con 3 personas para no dejar colgado a nadie, y ahí la
        // cantidad de arrieros la deciden los chicos, no una fórmula.
        [Range(0, 999)]
        [Display(Name = "Cantidad")]
        public int? Cantidad { get; set; }

        // Si es true, este costo es un SERVICIO de proveedor (guía, auto, hospedaje,
        // restaurante). Se sigue contando en la Rentabilidad, pero NO aparece en la lista
        // de gastos del operativo (se maneja en la sección Proveedores con seña + saldo).
        public bool EsProveedor { get; set; } = false;

        // "Por auto" incluye al guía: en Wamani el chofer es el guía, así que van juntos.
        public static readonly string[] Tipos = { "Por persona", "Por auto", "Cantidad", "Fijo" };

        // Nombre lindo para mostrar en los desplegables.
        public static string Etiqueta(string tipo) => tipo switch
        {
            "Por auto" => "Por auto (chofer-guía)",
            "Cantidad" => "Cantidad (se ajusta en cada salida)",
            _          => tipo,
        };
    }
}

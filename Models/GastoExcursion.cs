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
        // al guía porque el chofer es el guía) | "Fijo" (una vez por salida).
        [MaxLength(20)]
        public string TipoCalculo { get; set; } = "Por persona";

        // "Por auto" incluye al guía: en Wamani el chofer es el guía, así que van juntos.
        public static readonly string[] Tipos = { "Por persona", "Por auto", "Fijo" };
    }
}

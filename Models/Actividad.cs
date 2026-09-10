using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Registro de quién movió plata en el sistema.
    // Se anota una línea cada vez que alguien guarda una reserva o un operativo y
    // eso hace que entre o salga dinero. El monto es la DIFERENCIA con lo que había
    // antes: si Fran cobra una seña de 200.000, queda un ingreso de 200.000.
    // Si después corrige y la baja a 150.000, queda un egreso de 50.000.
    public class Actividad
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        [MaxLength(80)]
        public string Usuario { get; set; } = "";      // el nombre de usuario con que entró

        [MaxLength(120)]
        public string Nombre { get; set; } = "";       // el nombre de la persona, para mostrar

        [MaxLength(40)]
        public string Que { get; set; } = "";          // "Reserva" u "Operativo"

        [MaxLength(300)]
        public string Detalle { get; set; } = "";      // excursión, fecha y cliente

        public int? ExcursionId { get; set; }

        public decimal Monto { get; set; }             // siempre positivo

        public bool EsIngreso { get; set; }            // true = entró plata, false = salió
    }
}

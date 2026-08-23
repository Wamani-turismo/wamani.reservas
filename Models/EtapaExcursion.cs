using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Una NOCHE de una travesía: el lugar donde se duerme ese día.
    //
    // Una travesía no es como una excursión de un día: se camina de un punto a otro y se
    // para en varios lugares (ej. Tilcara → Yuto Pampa → Molulo → San Lucas). Los LUGARES
    // son siempre los mismos; lo que puede cambiar de una salida a otra es el refugio o
    // el hostel que se consigue en cada lugar.
    //
    // Cargando las etapas una vez por travesía, en el operativo el hospedaje se ve como
    // UNA fila por lugar (personas × precio), en vez de una fila por cada pasajero en
    // cada uno de los lugares.
    public class EtapaExcursion
    {
        public int Id { get; set; }

        public int ExcursionId { get; set; }

        // Qué noche de la travesía es (1 = la primera). Ordena las etapas.
        [Range(1, 60)]
        [Display(Name = "Noche")]
        public int Orden { get; set; } = 1;

        // El lugar donde se duerme (ej. "TILCARA", "YUTO PAMPA", "MOLULO", "SAN LUCAS").
        // Es lo fijo de la travesía: el refugio concreto puede cambiar, el lugar no.
        [Required(ErrorMessage = "Poné el lugar donde se duerme esa noche")]
        [MaxLength(80)]
        [Display(Name = "Lugar")]
        public string Lugar { get; set; } = "";

        // Refugio/hostel que se usa normalmente en ese lugar. Es sólo la sugerencia que
        // aparece cargada por defecto en cada salida: ahí se puede cambiar por otro.
        [Display(Name = "Refugio / hospedaje habitual")]
        public int? ProveedorId { get; set; }

        // Lo que cobra por persona esa noche. También es sugerencia: en la salida se ajusta.
        [Range(0, 999999999)]
        [Display(Name = "Precio por persona")]
        public decimal PrecioPorPersona { get; set; }

        // Qué comidas vienen incluidas en el precio de esa noche (ej. "merienda + cena +
        // desayuno"). No se desglosa como gasto aparte: es sólo para saber qué le toca a
        // la gente cada día y no volver a comprarlo por las dudas.
        [MaxLength(160)]
        [Display(Name = "Comidas incluidas")]
        public string? Incluye { get; set; }
    }
}

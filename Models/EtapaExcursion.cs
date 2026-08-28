using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // UNA COSA QUE LLEVA LA SALIDA Y SE CONTRATA PARA EL GRUPO ENTERO: una noche de
    // hospedaje, un traslado, los arrieros o los caballos.
    //
    // Una travesía no es como una excursión de un día: se camina de un punto a otro y se
    // para en varios lugares (ej. Tilcara → Yuto Pampa → Molulo → San Lucas). Esas paradas
    // son siempre las mismas; lo que cambia de una salida a otra es el refugio, la agencia
    // o el arriero que se consigue, y cuánta gente va.
    //
    // Cargándolas una vez acá, el operativo de cada salida sale armado: UNA fila por parada
    // con todo el grupo junto (cantidad × precio × veces), en vez de una fila por cada
    // pasajero en cada uno de los lugares.
    public class EtapaExcursion
    {
        public int Id { get; set; }

        public int ExcursionId { get; set; }

        // Qué es esta fila. Todas se cuentan igual —cantidad × precio × veces— pero cada
        // una se llama distinto en la pantalla:
        //
        //   Hospedaje → personas × precio por persona × NOCHES
        //   Traslado  → autos    × precio por auto    × 1        (1 auto cada 4, guías incluidos)
        //   Pasaje    → pasajes  × precio del boleto  × 1        (micro: uno por cabeza, guías incluidos)
        //   Arriero   → arrieros × precio por día     × DÍAS     (nos acompañan toda la travesía)
        //   Caballo   → caballos × precio por día     × DÍAS
        //
        // Traslado y Pasaje son los dos modos de moverse: el auto se paga por vehículo y el
        // micro por boleto. En los dos casos los guías cuentan, porque también viajan.
        //
        // Arrieros y caballos NO salen de una fórmula: se contratan según la gente que se
        // anota, y una travesía puede salir con menos del mínimo. Por eso van acá y no
        // como un costo fijo de la plantilla.
        [MaxLength(20)]
        [Display(Name = "Qué es")]
        public string Tipo { get; set; } = Hospedaje;

        public const string Hospedaje = "Hospedaje";
        public const string Traslado  = "Traslado";
        public const string Pasaje    = "Pasaje";
        public const string Arriero   = "Arriero";
        public const string Caballo   = "Caballo";

        public static readonly string[] Tipos = { Hospedaje, Traslado, Pasaje, Arriero, Caballo };

        // De qué lista de Proveedores se elige el que presta este servicio.
        // Los caballos se le contratan a los mismos arrieros, así que comparten catálogo.
        public static string CatalogoDe(string tipo) => tipo switch
        {
            Traslado => "Auto",
            Pasaje   => "Auto",
            Arriero  => "Arriero",
            Caballo  => "Arriero",
            _        => "Hospedaje",
        };

        // Cómo se llama cada columna en la pantalla, según el tipo.
        public static string EtiquetaCantidad(string tipo) => tipo switch
        {
            Traslado => "Autos",
            Pasaje   => "Pasajes",
            Arriero  => "Arrieros",
            Caballo  => "Caballos",
            _        => "Personas",
        };

        public static string EtiquetaPrecio(string tipo) => tipo switch
        {
            Traslado => "Precio por auto",
            Pasaje   => "Precio del boleto",
            Arriero  => "Precio por día",
            Caballo  => "Precio por día",
            _        => "Precio por persona",
        };

        public static string EtiquetaVeces(string tipo) => tipo switch
        {
            Arriero => "Días",
            Caballo => "Días",
            _       => "Noches",
        };

        public static string Icono(string tipo) => tipo switch
        {
            Traslado => "🚐",
            Pasaje   => "🎟️",
            Arriero  => "🧑‍🌾",
            Caballo  => "🐴",
            _        => "🏨",
        };

        // Título de la sección en el operativo
        public static string Seccion(string tipo) => tipo switch
        {
            Traslado => "Traslados",
            Pasaje   => "Pasajes",
            Arriero  => "Arrieros",
            Caballo  => "Caballos",
            _        => "Hospedaje",
        };

        // Qué noche de la travesía es (1 = la primera). Ordena las etapas.
        [Range(1, 60)]
        [Display(Name = "Noche")]
        public int Orden { get; set; } = 1;

        // Cómo se llama esta fila. Según el tipo es el lugar donde se duerme ("MOLULO"),
        // el tramo del traslado ("Micro de Humahuaca a Iruya") o simplemente "Arrieros".
        // Es lo fijo: el proveedor concreto puede cambiar de una salida a otra, esto no.
        [Required(ErrorMessage = "Poné un nombre para esta fila")]
        [MaxLength(80)]
        [Display(Name = "Lugar / tramo")]
        public string Lugar { get; set; } = "";

        // Refugio/hostel que se usa normalmente en ese lugar. Es sólo la sugerencia que
        // aparece cargada por defecto en cada salida: ahí se puede cambiar por otro.
        [Display(Name = "Refugio / hospedaje habitual")]
        public int? ProveedorId { get; set; }

        // Cuántas NOCHES seguidas se duerme en este mismo lugar. Casi siempre es 1, pero
        // hay salidas que paran dos o tres noches en el mismo lado (ej. "Conociendo las
        // Yungas": 2 noches en el mismo hospedaje). Poniendo 2 acá, en el operativo sale
        // UNA sola fila y el total se calcula solo: personas × precio × noches. Antes había
        // que escribir a mano el precio de las dos noches en el lugar del precio de una.
        [Range(1, 60)]
        [Display(Name = "Noches")]
        public int Noches { get; set; } = 1;

        // Lo que cobra por persona Y POR NOCHE. También es sugerencia: en la salida se ajusta.
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

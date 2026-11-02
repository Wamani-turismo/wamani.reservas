using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Libreta de alias para transferir: a quién le pagamos, con qué alias y dónde queda.
    // No tiene nada que ver con los costos ni con las salidas: es sólo la información
    // que antes había que ir a preguntar.
    public class AliasProveedor
    {
        public int Id { get; set; }

        // Valle | Quebrada | Puna | Yungas | Otro
        [Required, MaxLength(20)]
        [Display(Name = "Región")]
        public string Region { get; set; } = "Quebrada";

        [Required(ErrorMessage = "Poné el nombre")]
        [MaxLength(140)]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = "";

        // A qué se dedica: hospedaje, auto, comida, guía, lo que sea. Texto libre a
        // propósito, para no quedar atado a una lista.
        [MaxLength(60)]
        [Display(Name = "Qué hace")]
        public string? Tipo { get; set; }

        [MaxLength(140)]
        [Display(Name = "Alias")]
        public string? Alias { get; set; }

        // Donde esta la cuenta: Banco Macro, Mercado Pago, Uala, lo que sea. Texto
        // libre a proposito: Lautaro lo escribe como le salga.
        [MaxLength(80)]
        [Display(Name = "Entidad")]
        public string? Entidad { get; set; }

        // El nombre que figura en la cuenta, cuando no es el mismo que el de arriba.
        [MaxLength(140)]
        [Display(Name = "Titular de la cuenta")]
        public string? Titular { get; set; }

        [MaxLength(60)]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [MaxLength(120)]
        [Display(Name = "Dónde queda")]
        public string? Lugar { get; set; }

        [Display(Name = "Nota")]
        public string? Nota { get; set; }

        public static readonly string[] Regiones = { "Valle", "Quebrada", "Puna", "Yungas", "Otro" };
    }
}

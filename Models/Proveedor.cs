using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Catálogo de proveedores: guías, hospedajes, restaurantes y autos/agencias.
    // Se elige uno en cada salida (o se agrega uno nuevo).
    public class Proveedor
    {
        public int Id { get; set; }

        // "Guía" | "Hospedaje" | "Restaurante" | "Auto"
        [Required, MaxLength(20)]
        [Display(Name = "Tipo")]
        public string Tipo { get; set; } = "Guía";

        [Required(ErrorMessage = "Poné el nombre")]
        [MaxLength(120)]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = "";

        [MaxLength(160)]
        [Display(Name = "Contacto (teléfono / nota)")]
        public string? Contacto { get; set; }

        // Lo que cobra este proveedor (precio de referencia; en el operativo se completa
        // solo y se puede cambiar a mano si esta vez cobra más o menos).
        [Range(0, 999999999)]
        [Display(Name = "Lo que cobra ($)")]
        public decimal Precio { get; set; }

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        // Los tipos disponibles (para menús y validación).
        // "Arriero" son los arrieros de las travesías; los caballos se le contratan a la
        // misma gente, así que salen de esta misma lista.
        public static readonly string[] Tipos = { "Guía", "Hospedaje", "Restaurante", "Auto", "Arriero" };
    }
}

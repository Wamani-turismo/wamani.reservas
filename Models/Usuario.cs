using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Usuario que puede entrar al sistema (un socio).
    public class Usuario
    {
        public int Id { get; set; }

        [Required, MaxLength(80)]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = "";

        // Con esto inicia sesión (se guarda en minúscula, sin espacios)
        [Required, MaxLength(40)]
        [Display(Name = "Usuario (para entrar)")]
        public string NombreUsuario { get; set; } = "";

        public string PasswordHash { get; set; } = "";

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        // ---------- Acceso limitado a UNA excursión ----------
        //
        // Vacío = socio de Wamani, ve todo el sistema (es lo de siempre).
        // Con una excursión elegida = colaborador de afuera: entra SÓLO a esa travesía y
        // no puede ver ni la plata de la empresa ni los precios de las demás excursiones.
        //
        // El candado no está en esta marca sino en Program.cs: para estos usuarios el
        // sistema bloquea TODO y habilita nada más que un puñado de pantallas, siempre
        // atadas a esta excursión. Lo que no está en esa lista, no entra.
        [Display(Name = "Sólo puede ver esta excursión")]
        public int? ExcursionPermitidaId { get; set; }

        public bool EsLimitado => ExcursionPermitidaId is not null;
    }
}

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
    }
}

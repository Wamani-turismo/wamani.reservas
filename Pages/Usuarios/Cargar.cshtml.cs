using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Usuarios;

public class CargarModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<Usuario> _hasher;
    public CargarModel(AppDbContext db, IPasswordHasher<Usuario> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty] public Usuario Usuario { get; set; } = new();
    [BindProperty] public string? Password { get; set; }   // dejar vacío = no cambiar

    public bool EsNuevo => Usuario.Id == 0;
    public string? Error { get; set; }

    public IActionResult OnGet(int? id)
    {
        if (id is null) { Usuario = new Usuario { Activo = true }; return Page(); }
        var u = _db.Usuarios.Find(id);
        if (u is null) return RedirectToPage("/Usuarios/Index");
        Usuario = u;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Usuario.NombreUsuario = (Usuario.NombreUsuario ?? "").Trim().ToLower().Replace(" ", "");

        if (string.IsNullOrWhiteSpace(Usuario.Nombre) || string.IsNullOrWhiteSpace(Usuario.NombreUsuario))
        {
            Error = "Completá el nombre y el usuario.";
            return Page();
        }

        // No repetir el nombre de usuario
        bool repetido = await _db.Usuarios.AnyAsync(u => u.NombreUsuario == Usuario.NombreUsuario && u.Id != Usuario.Id);
        if (repetido) { Error = "Ese usuario ya existe, elegí otro."; return Page(); }

        if (Usuario.Id == 0)
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                Error = "Poné una contraseña para el usuario nuevo.";
                return Page();
            }
            Usuario.PasswordHash = _hasher.HashPassword(Usuario, Password);
            _db.Usuarios.Add(Usuario);
        }
        else
        {
            var u = await _db.Usuarios.FindAsync(Usuario.Id);
            if (u is null) return RedirectToPage("/Usuarios/Index");
            u.Nombre = Usuario.Nombre;
            u.NombreUsuario = Usuario.NombreUsuario;
            u.Activo = Usuario.Activo;
            if (!string.IsNullOrWhiteSpace(Password))
                u.PasswordHash = _hasher.HashPassword(u, Password);   // solo si escribió una nueva
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("/Usuarios/Index", new { Aviso = "Usuario guardado ✔" });
    }
}

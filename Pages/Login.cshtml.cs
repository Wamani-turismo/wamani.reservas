using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages;

public class LoginModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<Usuario> _hasher;
    public LoginModel(AppDbContext db, IPasswordHasher<Usuario> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty] public string NombreUsuario { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; set; }

    public IActionResult OnGet()
    {
        // Si ya está logueado, al inicio
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = (NombreUsuario ?? "").Trim().ToLower();
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.NombreUsuario == user && x.Activo);

        if (u is null || _hasher.VerifyHashedPassword(u, u.PasswordHash, Password ?? "") == PasswordVerificationResult.Failed)
        {
            Error = "Usuario o contraseña incorrectos.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Nombre),
        };
        // Si es un colaborador con acceso a una sola excursión, la marca viaja en la
        // sesión: de ahí la lee el candado de Program.cs y el menú.
        if (u.ExcursionPermitidaId is int excPermitida)
            claims.Add(new Claim("excursion_permitida", excPermitida.ToString()));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        // El colaborador limitado no tiene inicio: va derecho a su operativo
        if (u.ExcursionPermitidaId is not null) return RedirectToPage("/Operativo/Index");

        return RedirectToPage("/Index");
    }
}

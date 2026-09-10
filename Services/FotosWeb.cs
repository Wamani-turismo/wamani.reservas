namespace Wamani.Reservas.Services
{
    // Dónde se guardan las fotos que suben los chicos para la web.
    //  - En tu compu: en wwwroot/web/img (al lado de las que ya vienen).
    //  - En internet (Render): en el disco persistente (UPLOADS_DIR/webfotos), para
    //    que NO se borren al actualizar. Program.cs las sirve en /web/img igual.
    public static class FotosWeb
    {
        public static string Carpeta(IWebHostEnvironment env)
        {
            var disco = Environment.GetEnvironmentVariable("UPLOADS_DIR");
            var carpeta = !string.IsNullOrWhiteSpace(disco)
                ? Path.Combine(disco, "webfotos")
                : Path.Combine(env.WebRootPath, "web", "img");
            Directory.CreateDirectory(carpeta);
            return carpeta;
        }

        // Guarda un archivo subido y devuelve SOLO el nombre (lo que se guarda en la base).
        // Si no se subió nada, devuelve el nombre anterior (para no perder la foto al editar).
        public static async Task<string> Guardar(IFormFile? archivo, string? nombreAnterior, IWebHostEnvironment env)
        {
            if (archivo is null || archivo.Length == 0)
                return nombreAnterior ?? "";

            var ext = Path.GetExtension(archivo.FileName);
            var nombre = $"sube-{Guid.NewGuid():N}{ext}";
            var destino = Path.Combine(Carpeta(env), nombre);
            using (var fs = new FileStream(destino, FileMode.Create))
                await archivo.CopyToAsync(fs);
            return nombre;
        }
    }
}

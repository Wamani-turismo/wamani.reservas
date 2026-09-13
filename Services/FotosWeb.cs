using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

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

        // El lado más largo que va a tener la foto guardada. 2000 px alcanza para que se
        // vea nítida en una pantalla grande y hasta en un celular de alta resolución: la
        // web nunca muestra una foto más ancha que esto. Una foto de celular viene con
        // 4000 px o más, que es resolución para imprimir, no para mirar en pantalla.
        private const int LadoMaximo = 2000;

        // Calidad del WebP. 82 es el punto donde el ojo ya no distingue la diferencia con
        // el original pero el archivo pesa una fracción.
        private const int Calidad = 82;

        // Guarda un archivo subido y devuelve SOLO el nombre (lo que se guarda en la base).
        // Si no se subió nada, devuelve el nombre anterior (para no perder la foto al editar).
        //
        // Las fotos se achican ANTES de guardarlas. Sin esto subían tal cual salían del
        // celular: se midió una de 3,4 MB en la web, cuando una foto bien preparada pesa
        // entre 150 y 300 KB. El visitante con datos móviles se cansaba de esperar, y
        // Google usa la velocidad para decidir a quién muestra primero.
        //
        // Para el que carga la foto no cambia nada: sube la del celular igual que siempre.
        public static async Task<string> Guardar(IFormFile? archivo, string? nombreAnterior, IWebHostEnvironment env)
        {
            if (archivo is null || archivo.Length == 0)
                return nombreAnterior ?? "";

            var nombre = $"sube-{Guid.NewGuid():N}.webp";
            var destino = Path.Combine(Carpeta(env), nombre);

            try
            {
                using var entrada = archivo.OpenReadStream();
                using var imagen = await SixLabors.ImageSharp.Image.LoadAsync(entrada);

                // Las fotos de celular traen adentro un dato que dice para qué lado estaba
                // el teléfono. Sin esto, las verticales se guardan acostadas.
                imagen.Mutate(x => x.AutoOrient());

                // Sólo se achica si hace falta. Una foto que ya venía chica se deja como
                // está: agrandarla la haría ver peor.
                if (imagen.Width > LadoMaximo || imagen.Height > LadoMaximo)
                    imagen.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(LadoMaximo, LadoMaximo),
                        Mode = ResizeMode.Max   // respeta la proporción: no deforma ni recorta
                    }));

                await imagen.SaveAsWebpAsync(destino, new WebpEncoder { Quality = Calidad });
                return nombre;
            }
            catch
            {
                // Si el archivo no es una imagen que se pueda leer (un formato raro, o algo
                // que no era una foto), se guarda tal cual venía. Preferimos una foto pesada
                // antes que perderla.
                var ext = Path.GetExtension(archivo.FileName);
                var crudo = $"sube-{Guid.NewGuid():N}{ext}";
                var destinoCrudo = Path.Combine(Carpeta(env), crudo);
                using var fs = new FileStream(destinoCrudo, FileMode.Create);
                using var origen = archivo.OpenReadStream();
                await origen.CopyToAsync(fs);
                return crudo;
            }
        }
    }
}

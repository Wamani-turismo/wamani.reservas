using SixLabors.ImageSharp;
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

        // Calidad con la que se vuelven a guardar las fotos que YA estaban cargadas.
        // Un poco más apretada que la de arriba: son fotos que ya pasaron por una
        // compresión, y acá lo que se busca es recuperar peso sin que se note.
        private const int CalidadRecompresion = 76;

        // Lo que devuelve la herramienta de Mantenimiento, para poder contarlo.
        public record Recompresion(int Revisadas, int Achicadas, long BytesAntes, long BytesDespues);

        // Vuelve a comprimir las fotos que YA están guardadas en el disco, sin cambiarles
        // el nombre (por eso no hay que tocar nada en la base ni en las excursiones).
        //
        // Para qué: las fotos subidas ANTES del 13/09/2026 entraron sin pasar por ninguna
        // compresión. El 13/11/2026 PageSpeed encontró dos de ellas todavía vivas en la
        // home, de 931 KB y 561 KB, cuando una foto bien preparada pesa 150-250 KB.
        // Volver a subirlas a mano desde el panel también las arregla, pero hay que
        // acordarse de cuál es cuál; esto las encuentra solas.
        //
        // Sólo toca archivos .webp: los videos y cualquier cosa que no sea una imagen se
        // saltean enteros. Y si el resultado no es al menos un 15 % más liviano, deja el
        // original como está — no vale la pena perder calidad por nada.
        public static async Task<Recompresion> RecomprimirGuardadas(IWebHostEnvironment env)
        {
            var carpeta = Carpeta(env);
            int revisadas = 0, achicadas = 0;
            long bytesAntes = 0, bytesDespues = 0;

            foreach (var ruta in Directory.GetFiles(carpeta, "*.webp"))
            {
                revisadas++;
                var pesoOriginal = new FileInfo(ruta).Length;
                bytesAntes += pesoOriginal;

                var temporal = ruta + ".tmp";
                try
                {
                    using (var imagen = await SixLabors.ImageSharp.Image.LoadAsync(ruta))
                    {
                        imagen.Mutate(x => x.AutoOrient());

                        if (imagen.Width > LadoMaximo || imagen.Height > LadoMaximo)
                            imagen.Mutate(x => x.Resize(new ResizeOptions
                            {
                                Size = new SixLabors.ImageSharp.Size(LadoMaximo, LadoMaximo),
                                Mode = ResizeMode.Max
                            }));

                        await imagen.SaveAsWebpAsync(temporal, new WebpEncoder { Quality = CalidadRecompresion });
                    }

                    var pesoNuevo = new FileInfo(temporal).Length;
                    if (pesoNuevo > 0 && pesoNuevo < pesoOriginal * 0.85)
                    {
                        File.Move(temporal, ruta, overwrite: true);
                        achicadas++;
                        bytesDespues += pesoNuevo;
                    }
                    else
                    {
                        File.Delete(temporal);
                        bytesDespues += pesoOriginal;
                    }
                }
                catch
                {
                    // Un archivo que no se puede leer como imagen se deja intacto.
                    if (File.Exists(temporal)) try { File.Delete(temporal); } catch { }
                    bytesDespues += pesoOriginal;
                }
            }

            return new Recompresion(revisadas, achicadas, bytesAntes, bytesDespues);
        }

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

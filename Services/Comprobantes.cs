namespace Wamani.Reservas.Services
{
    // Decide DÓNDE se guardan físicamente los comprobantes que se suben.
    //
    // - En internet (Render): en un DISCO PERSISTENTE (variable de entorno UPLOADS_DIR,
    //   por ejemplo "/var/data/comprobantes"). Así NO se borran cuando se actualiza el sistema.
    // - En tu compu: en la carpeta wwwroot/comprobantes de siempre.
    //
    // La dirección que se guarda en la base ("/comprobantes/archivo.jpg") NO cambia:
    // solo cambia el lugar físico del disco. Program.cs se encarga de que esa dirección
    // apunte a la carpeta correcta.
    public static class Comprobantes
    {
        public static string Carpeta(IWebHostEnvironment env)
        {
            var dir = Environment.GetEnvironmentVariable("UPLOADS_DIR");
            if (!string.IsNullOrWhiteSpace(dir)) return dir;
            return Path.Combine(env.WebRootPath, "comprobantes");
        }
    }
}

namespace Wamani.Reservas.Services
{
    // Dónde se guardan los archivos que la gente adjunta en el formulario de contacto
    // de la web (por ejemplo, una agencia que manda su propuesta en PDF).
    //
    // OJO, no confundir con Services/Adjuntos.cs, que es otra cosa: ése maneja los
    // comprobantes de pago que subimos NOSOTROS desde el panel.
    //
    // Mismo criterio de carpetas que Services/Comprobantes.cs:
    //  - En internet (Render): en el DISCO PERSISTENTE (UPLOADS_DIR/consultas), así no se
    //    borran cuando se actualiza el sistema.
    //  - En tu compu: en wwwroot/adjuntos-consultas.
    //
    // DIFERENCIA IMPORTANTE con los comprobantes: esta carpeta NO se publica en internet.
    // Los comprobantes se sirven como archivos sueltos en /comprobantes, pero acá los sube
    // CUALQUIERA desde la web pública, así que dejarlos a la vista sería regalar un lugar
    // donde subir lo que sea y pasarse el link. Se bajan sólo desde el panel, con la
    // sesión iniciada, por la pantalla de Consultas.
    public static class AdjuntosConsulta
    {
        public static string Carpeta(IWebHostEnvironment env)
        {
            var disco = Environment.GetEnvironmentVariable("UPLOADS_DIR");
            var carpeta = !string.IsNullOrWhiteSpace(disco)
                ? Path.Combine(disco, "consultas")
                : Path.Combine(env.WebRootPath, "adjuntos-consultas");
            Directory.CreateDirectory(carpeta);
            return carpeta;
        }

        // La ruta completa del archivo de una consulta. Devuelve null si el nombre
        // guardado está vacío o si intentara salirse de la carpeta.
        public static string? Ruta(IWebHostEnvironment env, string? nombreGuardado)
        {
            if (string.IsNullOrWhiteSpace(nombreGuardado)) return null;
            var carpeta = Carpeta(env);
            // Path.GetFileName corta cualquier "../" que venga en el nombre
            var completa = Path.GetFullPath(Path.Combine(carpeta, Path.GetFileName(nombreGuardado)));
            return completa.StartsWith(Path.GetFullPath(carpeta), StringComparison.Ordinal) ? completa : null;
        }
    }
}

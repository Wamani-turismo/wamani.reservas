using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Services
{
    // Carga el contenido ACTUAL de la landing la primera vez (si las tablas están vacías).
    // Así al arrancar, la web ya se ve igual que ahora y los chicos pueden editar desde ahí.
    public static class SeedWeb
    {
        public static void Ejecutar(AppDbContext db)
        {
            if (!db.ContenidoWeb.Any())
                db.ContenidoWeb.Add(new ContenidoWeb());   // usa los valores por defecto del modelo

            if (!db.ExcursionesWeb.Any())
            {
                db.ExcursionesWeb.AddRange(
                    Ex("salinas", "Atardecer en las Salinas", "Día completo", false, "122,106,160", "salinas-1.webp", 10,
                        "Purmamarca, la Cuesta de Lipán y el momento mágico: el atardecer reflejado en el blanco infinito del salar.",
                        new[]{"🕐 10 hs","🚌 Salida 9:30 · SSJ","👥 2 a 15 personas","🥾 Dificultad baja"},
                        new[]{"Mirador del Cerro de los 7 Colores en Purmamarca","Paseo de Los Colorados y tiempo libre en el pueblo","Ascenso por la Cuesta de Lipán y Mirador Alto El Morado","Salinas Grandes: recorrido guiado, Ojos del Salar y atardecer con merienda","Regreso en horas de la noche"},
                        new[]{"Traslado ida y vuelta desde S. S. de Jujuy","Guía toda la experiencia","Recorrido guiado en el salar","Merienda en las Salinas","Snacks y botiquín"},
                        "Abrigo para la tarde-noche, anteojos de sol, protector, agua y cámara."),
                    Ex("quebrada", "Recorriendo la Quebrada", "Día completo", false, "112,72,105", "quebrada-1.webp", 20,
                        "La Quebrada de las Señoritas, almuerzo regional en Humahuaca y el Cerro de los 14 Colores a más de 4.000 msnm.",
                        new[]{"🕐 12 hs","🚌 Salida 9:00 · SSJ","👥 2 a 15 personas","🥾 Dificultad moderada"},
                        new[]{"Ruta 9 al norte por la Quebrada de Humahuaca (Patrimonio de la Humanidad)","Trekking guiado en la Quebrada de las Señoritas, Uquía (2-3 hs)","Almuerzo regional en Humahuaca","Serranía del Hornocal: mirador del Cerro de los 14 Colores","Regreso en horas de la noche"},
                        new[]{"Traslado ida y vuelta","Entradas a Hornocal y Señoritas","Guía y trekking guiado","Snacks y refrigerios"},
                        "Calzado cómodo, abrigo, protector solar, gorra y agua. Almuerzo no incluido."),
                    Ex("jordan", "Termas de Jordán", "Día completo", false, "58,110,90", "termas-jordan-1.webp", 30,
                        "Trekking profundo por la selva de las yungas hasta las piscinas naturales de aguas termales color turquesa.",
                        new[]{"🕐 14-16 hs","🚌 Salida 5:00 · SSJ","👥 2 a 15 · Dif. moderada/alta"},
                        new[]{"Traslado a San Francisco, puerta del Parque Nacional Calilegua (3:30 hs)","Trekking de 5-6 hs por selva, ríos y montaña","Tiempo libre en las piscinas termales turquesas","Almuerzo y paseo por San Francisco","Regreso en horas de la noche"},
                        new[]{"Traslado ida y vuelta","Entrada a la excursión","Guía toda la jornada","Snacks y refrigerios","Asistencia permanente"},
                        "Calzado de trekking, traje de baño y toalla, ropa de cambio, repelente y agua. Almuerzo no incluido."),
                    Ex("lagunas", "Ruta de Lagunas y Termas", "Día completo", false, "63,118,131", "lagunas-2.webp", 40,
                        "Lagunas de altura, una cascada escondida con picada gourmet y el cierre perfecto: las Termas de Reyes.",
                        new[]{"🕐 7-8 hs","🚌 Salida 9:00 · SSJ","👥 2 a 15 personas","🥾 Dificultad media/baja"},
                        new[]{"Lagunas de Yala a 2.300 msnm: Comedero, Rodeo y Desaguadero","Cascada La Horqueta (40 m) con picada gourmet al aire libre","Relax en las Termas de Reyes: aguas que brotan a 52°C","Regreso por el camino panorámico"},
                        new[]{"Traslado ida y vuelta","Guía y trekking guiado","Picada gourmet + snacks","Entrada a las Termas de Reyes"},
                        "Traje de baño, calzado cómodo, abrigo ligero y agua."),
                    Ex("santuyoc", "Cascada Santuyoc y Angosto de Jaire", "Día completo", false, "92,125,80", "santuyoc-3.webp", 50,
                        "Un cañón secreto de paredes cubiertas de musgo y una cascada de 80 metros escondida en el verde.",
                        new[]{"🕐 8 hs","🚌 Salida 9:00 · SSJ","👥 2 a 15 · Dif. baja/moderada"},
                        new[]{"Camino de montaña por Yala, Lozano y León (RP 29)","Angosto de Jaire: cañón de helechos, arroyos y cascadas","Caminata a la Cascada Santuyoc (80 m)","Almuerzo regional en Canuto Glamping (incluido)","Regreso 17-18 hs"},
                        new[]{"Traslado ida y vuelta","Guía y caminatas guiadas","Almuerzo (sin bebida)","Snacks y botiquín"},
                        "Calzado de trekking, ropa liviana + abrigo, protector y agua."),
                    Ex("yungas", "Conociendo las Yungas", "3 días", false, "47,95,80", "yungas-1.webp", 60,
                        "Tres días en el corazón de la selva: cuevas, termas turquesas y rincones que casi nadie conoce.",
                        new[]{"🗓️ 3 días / 2 noches","🚌 Salida 8:00 · SSJ","👥 2 a 10 personas","🥾 Dificultad moderada/alta"},
                        new[]{"Día 1 · Llegada a San Francisco y trekking a las Cuevas del Toro","Día 2 · Trekking a las Termas del Jordán (5-6 hs, la joya de la travesía)","Día 3 · Fuente del Jaguar, Refugio del Tapir y Cañón de los Loros"},
                        new[]{"Traslado ida y vuelta","2 noches de hospedaje en San Francisco","Desayunos y cenas (media pensión)","Guía local, entrada a las termas, snacks y botiquín"},
                        "2 calzados de trekking, traje de baño, repelente y mochila chica. Almuerzos y bebidas no incluidos."),
                    Ex("yungas-express", "Yungas Express", "2 días", false, "104,138,74", "yungas-express-1.webp", 70,
                        "La versión intensa de la selva: senderos, cena gourmet y las termas turquesas, todo en un fin de semana.",
                        new[]{"🗓️ 2 días / 1 noche","🚌 Salida 8:00 · SSJ","👥 2 a 10 personas","🥾 Dificultad moderada/alta"},
                        new[]{"Día 1 · Refugio del Tapir, Fuente del Jaguar y Cañón de los Loros + cena gourmet en El Puesto","Día 2 · Trekking a las Termas del Jordán (5-6 hs) y regreso"},
                        new[]{"Traslado ida y vuelta","Hospedaje en San Francisco","Cena gourmet + desayuno","Guía, snacks y asistencia"},
                        "Calzado de trekking, traje de baño, ropa de cambio y repelente."),
                    Ex("humahuaca-yungas", "Humahuaca – Yungas", "Travesía · 5 días", true, "178,94,58", "humahuaca-yungas-1.webp", 80,
                        "De la Quebrada a la selva por el Camino del Inca: pueblos ancestrales, cascadas y termas escondidas.",
                        new[]{"🗓️ 5 días / 4 noches","🚌 Salida · SSJ","👥 4 a 12 personas","🥾 Dificultad moderada/alta"},
                        new[]{"Día 1 · Humahuaca, Hornocal y llegada a Caspalá","Día 2 · Sitio arqueológico El Antiguito y Cascada Casa Mocha → Santa Ana","Día 3 · Trekking por el Qhapaq Ñan (Camino del Inca) → San Francisco","Día 4 · Trekking a las Termas del Jordán (jornada estrella)","Día 5 · Rincones de las yungas y regreso"},
                        new[]{"Todos los traslados","4 noches de hospedaje","Desayunos y cenas","Guía local, entradas, snacks y botiquín"},
                        "Calzado de trekking, abrigo (noches frías) y traje de baño. Almuerzos no incluidos."),
                    Ex("tilcara", "Tilcara – Calilegua", "Travesía · 5 días", true, "86,132,96", "tilcara-2.webp", 90,
                        "En solo 68 km, del árido quebradeño a la nuboselva: el trekking que une dos mundos.",
                        new[]{"🗓️ 5 días / 4 noches","🥾 6-8 hs por día · Dificultad media/alta","👥 6 a 16 · Hasta 4.200 msnm"},
                        new[]{"Día 1 · Aclimatación en Tilcara + cena de bienvenida","Día 2 · Casa Colorada → Wayra Wasi (20 km, hasta 4.200 msnm)","Día 3 · Wayra Wasi → Molúlo (20 km)","Día 4 · Molúlo → San Lucas (22 km)","Día 5 · San Lucas → Peña Alta (San Francisco) y regreso"},
                        new[]{"1 noche en Tilcara + 3 en refugios de montaña","Cenas con gastronomía local","Guía y traslados de inicio y fin"},
                        "Equipo de trekking completo, abrigo de altura y bolsa de dormir. Consultanos el listado detallado."),
                    Ex("iruya", "Iruya – Nazareno", "Travesía · 5 días", true, "88,106,140", "iruya-2.webp", 100,
                        "Caminos ancestrales entre pueblos remotos, durmiendo en casas de familias que te reciben como a uno más.",
                        new[]{"🗓️ 5 días / 4 noches","📍 Inicio: Humahuaca 15 hs","👥 4 a 12 personas","🥾 Jornadas de 9-10 hs","📅 Próxima salida: 5 al 9 de agosto"},
                        new[]{"Día 1 · Colectivo a Iruya y cena de bienvenida","Día 2 · Iruya → Chiyayoc (15 km) — nos recibe Paula y su familia","Día 3 · Chiyayoc → Rodeo Colorado (13 km)","Día 4 · Rodeo Colorado → Cuesta Azul (18 km)","Día 5 · Llegada a Nazareno, almuerzo de despedida y regreso"},
                        new[]{"Todos los traslados","4 noches con familias anfitrionas","Pensión completa de marcha (desayuno, almuerzo, merienda y cena)"},
                        "Calzado de trekking usado (no nuevo), abrigo y linterna. La montaña marca el ritmo."),
                    Ex("ecolodge", "Especial EcoLodge de la Selva", "3 días", false, "134,124,82", "ecolodge-2.webp", 110,
                        "Cascadas, lagunas, termas y tirolesas, durmiendo en cabañas inmersas en la selva a minutos de la ciudad.",
                        new[]{"🗓️ 3 días / 2 noches","🚌 Salida 9:00 · SSJ","👥 2 a 12 · Dif. baja/media"},
                        new[]{"Día 1 · Angosto de Jaire y Cascada Santuyoc + almuerzo regional","Día 2 · Lagunas de Yala, Cascada La Horqueta y Termas de Reyes","Día 3 · Parque Aéreo Los Coatíes: tirolesas y circuitos entre los árboles"},
                        new[]{"Traslados","2 noches en EcoLodge de la Selva","2 desayunos, 2 cenas, almuerzo y picada gourmet","Termas, parque aéreo, guía y seguro"},
                        "Calzado cómodo, traje de baño, protector y muchas ganas de desconectar.")
                );
            }

            if (!db.TestimoniosWeb.Any())
            {
                db.TestimoniosWeb.AddRange(
                    new TestimonioWeb { Texto = "El atardecer en las Salinas fue de lo más lindo que vi en mi vida. Están en cada detalle.", Nombre = "María — ejemplo", Lugar = "Buenos Aires · Atardecer en las Salinas", Orden = 10 },
                    new TestimonioWeb { Texto = "Las Termas de Jordán no parecen reales: selva, ríos y piscinas turquesas. Guías de primera.", Nombre = "Juan — ejemplo", Lugar = "Córdoba · Termas de Jordán", Orden = 20 },
                    new TestimonioWeb { Texto = "Ser grupo chico hizo la diferencia: conocimos un Jujuy que jamás hubiéramos encontrado solos.", Nombre = "Lucía — ejemplo", Lugar = "Rosario · Recorriendo la Quebrada", Orden = 30 }
                );
            }

            if (!db.IntegrantesWeb.Any())
            {
                db.IntegrantesWeb.AddRange(
                    new IntegranteWeb { Nombre = "Luciano", Rol = "Guía y fundador", Bio = "El que empezó todo: conoce cada sendero de estos cerros como la palma de su mano.", Foto = "guia-lucho.webp", Orden = 10 },
                    new IntegranteWeb { Nombre = "Lautaro", Rol = "Cofundador", Bio = "Se sumó a la aventura: el que hace que todo funcione para que vos solo disfrutes.", Foto = "guia-lautaro.webp", Orden = 20 },
                    new IntegranteWeb { Nombre = "Facundo", Rol = "Guía y cofundador", Bio = "Historias, mates y la calidez del norte acompañándote en cada salida.", Foto = "guia-facu.webp", Orden = 30 }
                );
            }

            if (!db.VideosWeb.Any())
            {
                db.VideosWeb.Add(
                    new VideoWeb { Titulo = "Una experiencia inolvidable", Descripcion = "Lo que sintió una viajera al vivir la travesía con nosotros.", Archivo = "video-testimonio.mp4", Poster = "video-testimonio-poster.webp", Vertical = true, Orden = 10 }
                );
            }

            db.SaveChanges();
        }

        // Atajo para crear una excursión con sus listas (se guardan con un ítem por renglón)
        private static ExcursionWeb Ex(string clave, string nombre, string chip, bool travesia, string color, string foto,
            int _dummy, string resumen, string[] datos, string[] itinerario, string[] incluye, string llevar)
        {
            return new ExcursionWeb
            {
                Clave = clave, Nombre = nombre, Chip = chip, EsTravesia = travesia, Color = color, Foto = foto,
                Resumen = resumen, Llevar = llevar, Activa = true, Orden = _dummy,
                Datos = string.Join("\n", datos),
                Itinerario = string.Join("\n", itinerario),
                Incluye = string.Join("\n", incluye)
            };
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Wamani.Reservas.Data;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Pages.Operativo;

// Datos para renderizar una fila de gasto (partial _GastoRow)
public class GastoRowVm
{
    public OperativoGasto G { get; set; } = new();
    public int Mult { get; set; }         // por cuánto se multiplica el unitario
    public int ReservaId { get; set; }    // 0 = compartido de la salida
}

// Datos para renderizar una fila de proveedor (partial _ProvRow)
public class ProvRowVm
{
    public string Tipo { get; set; } = "";
    public OperativoProveedor? Asig { get; set; }
    public List<Proveedor> Cat { get; set; } = new();
    public bool ConPasajero { get; set; }   // hospedaje/restaurante: se pueden agregar varios (por persona)
    public string Key { get; set; } = "";   // clave para asociar el comprobante a la fila (id real o temporal si es nueva)
    public List<Reserva> Reservas { get; set; } = new();   // reservas de la salida (para elegir de quién es el servicio)

    // Guía y Auto: en vez de escribir el total, se pone CUÁNTOS van y lo que cobra cada uno,
    // y el total sale solo. Sirve para las travesías, donde según la gente que se anota se
    // suman guías o vehículos, y no hay una fórmula fija: lo deciden los chicos.
    public bool ConCantidad { get; set; }

    // Hospedaje y restaurante de una salida de VARIOS DÍAS: aparece una cajita para decir
    // cuántas noches (o cuántas comidas) son, y el total se multiplica por eso. En las
    // salidas de un día no aparece: sería siempre 1 y molestaría.
    // NochesSugeridas sale de las fechas de las reservas, así que una excursión nueva de
    // 3 días y 2 noches lo toma sola, sin que haya que cargar nada en el código.
    public bool ConNoches { get; set; }
    public int NochesSugeridas { get; set; } = 1;

    // Lo que hay que mostrar en la cajita: lo que ya se guardó en esta fila y, si es una
    // fila nueva, lo que dura la salida.
    public int NochesFila => Asig?.Noches is int n && n > 0 ? n : (NochesSugeridas > 0 ? NochesSugeridas : 1);

    public string EtiquetaNoches => Tipo == "Restaurante" ? "Comidas" : "Noches";

    public decimal PrecioCatalogo { get; set; }

    // Cuántos van. Las filas cargadas ANTES de que existiera este campo no lo tienen: se
    // muestran como 1, que es lo que no cambia el total que ya tenían.
    public int Cuantos => Asig?.Personas is int n && n > 0 ? n : 1;

    // Lo que cobra cada uno, en este orden:
    //   1) lo que ya se había cargado en esta fila;
    //   2) si la fila es vieja (tiene un total escrito a mano pero no tiene el desglose),
    //      el total entero: así "1 × total = total" y el número NO se mueve al abrir;
    //   3) si la fila es nueva, lo que cobra ese proveedor según el catálogo.
    //
    // El paso 2 es importante: sin él, abrir una salida ya cargada y tocar cualquier cosa
    // recalculaba el total con el precio del catálogo y borraba lo que estaba puesto.
    public decimal PrecioCadaUno
    {
        get
        {
            if (Asig?.PrecioPorPersona is decimal p && p > 0) return p;
            if (Asig is not null && Asig.Total > 0) return Asig.Total;
            return PrecioCatalogo;
        }
    }
}

// Una fila del operativo que se contrata para el GRUPO ENTERO: una noche de hospedaje,
// un traslado, los pasajes del micro, los arrieros o los caballos. Una sola fila
// (cantidad × precio × veces) en vez de una fila por pasajero.
public class EtapaRowVm
{
    // "Hospedaje" | "Traslado" | "Pasaje" | "Arriero" | "Caballo"
    public string Tipo { get; set; } = EtapaExcursion.Hospedaje;
    public bool EsHospedaje => Tipo == EtapaExcursion.Hospedaje;

    public string EtiquetaCantidad => EtapaExcursion.EtiquetaCantidad(Tipo);
    public string EtiquetaPrecio => EtapaExcursion.EtiquetaPrecio(Tipo);
    public string EtiquetaVeces => EtapaExcursion.EtiquetaVeces(Tipo);
    public string Icono => EtapaExcursion.Icono(Tipo);

    public int Noche { get; set; }
    public string Lugar { get; set; } = "";
    public string? Incluye { get; set; }
    public OperativoProveedor? Asig { get; set; }     // lo ya cargado para ese lugar (si existe)
    public List<Proveedor> Cat { get; set; } = new();
    public string Key { get; set; } = "";
    public int ProveedorSugerido { get; set; }         // refugio habitual de la plantilla
    public decimal PrecioSugerido { get; set; }        // precio por persona de la plantilla
    public decimal PrecioCatalogo { get; set; }        // lo que cobra ese refugio según Proveedores
    public int NochesPlantilla { get; set; } = 1;      // cuántas noches se para en este lugar

    // Lo que se muestra: si ya hay algo cargado se respeta; si no, la sugerencia de la
    // plantilla; y si la plantilla no tiene precio, lo que cobra el refugio en Proveedores
    // (así no hay que escribir el precio dos veces).
    public int ProveedorId => Asig?.ProveedorId ?? ProveedorSugerido;
    public decimal PrecioPorPersona =>
        Asig?.PrecioPorPersona ?? (PrecioSugerido > 0 ? PrecioSugerido : PrecioCatalogo);
    // Con cuántos arranca la fila. En hospedaje es la gente de la salida; en los traslados,
    // los autos que hacen falta; en arrieros y caballos NO hay fórmula, arranca en 0 y lo
    // deciden los chicos (por eso también salen con menos del mínimo de gente).
    public int CantidadSugerida { get; set; }

    public int Personas => Asig?.Personas ?? CantidadSugerida;

    // La gente de la salida, para poder avisar cuando el número cargado se queda corto.
    public int Pasajeros { get; set; }
    public int Guias { get; set; }

    // ¿Esta fila tiene una cantidad que se puede calcular sola? Los traslados y los pasajes
    // sí (salen de la gente que viaja); los arrieros y los caballos no, los deciden ellos.
    public bool TieneSugerencia =>
        Tipo == EtapaExcursion.Traslado || Tipo == EtapaExcursion.Pasaje;

    // El aviso para cuando lo cargado no alcanza (o sobra) para la gente que va. En una
    // travesía el chofer es contratado y va aparte, así que los lugares del auto son para
    // los pasajeros MÁS los guías: si se suma gente después de guardar, el número queda
    // viejo y hay que subirlo a mano.
    public string TextoSugerencia()
    {
        var quienes = Pasajeros == 1 ? "1 pasajero" : $"{Pasajeros} pasajeros";
        quienes += Guias == 1 ? " y 1 guía" : $" y {Guias} guías";

        var cosa = Tipo == EtapaExcursion.Pasaje
            ? (CantidadSugerida == 1 ? "1 pasaje" : $"{CantidadSugerida} pasajes")
            : (CantidadSugerida == 1 ? "1 auto" : $"{CantidadSugerida} autos");

        return $"Para {quienes} hacen falta {cosa}.";
    }
    public int Noches => Asig?.Noches ?? (NochesPlantilla > 0 ? NochesPlantilla : 1);
    public decimal Total => Asig?.Total ?? (PrecioPorPersona * Personas * Noches);

    // El título de la fila. Sólo el hospedaje se numera por noche; el resto se muestra
    // con su nombre, que ya dice todo ("Micro de Humahuaca a Iruya").
    public string TituloNoches()
    {
        if (!EsHospedaje) return EtapaExcursion.Seccion(Tipo);
        if (Noches <= 1) return $"Noche {Noche}";
        var hasta = Noche + Noches - 1;
        return Noches == 2 ? $"Noches {Noche} y {hasta}" : $"Noches {Noche} a {hasta}";
    }
}

public class SalidaModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public SalidaModel(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [BindProperty(SupportsGet = true)]
    public int ExcursionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime Fecha { get; set; }

    public string ExcursionNombre { get; set; } = "";
    public List<OperativoGasto> Gastos { get; set; } = new();
    public List<Reserva> Reservas { get; set; } = new();
    public int PasajerosSalida { get; set; }   // total de personas de la salida (para multiplicar)
    public int PersonasPorAuto => Wamani.Reservas.Models.Excursion.PersonasPorAuto;
    public OperativoSalida Salida { get; set; } = new();

    // Proveedores: catálogo por tipo y lo asignado a esta salida (puede haber varios por tipo)
    public Dictionary<string, List<Proveedor>> CatalogoPorTipo { get; set; } = new();
    public Dictionary<string, List<OperativoProveedor>> ProvPorTipo { get; set; } = new();

    // Si la excursión tiene etapas cargadas es una TRAVESÍA: el hospedaje se muestra
    // como una fila por lugar (todo el grupo junto) en vez de una fila por pasajero.
    public List<EtapaRowVm> Etapas { get; set; } = new();
    public bool EsTravesia => Etapas.Count > 0;

    // Si los traslados ya están cargados arriba (como tramos), la sección suelta de "Auto"
    // se esconde: repetirla sería cargar dos veces la misma plata.
    public bool TieneTraslados =>
        Etapas.Any(e => e.Tipo == EtapaExcursion.Traslado || e.Tipo == EtapaExcursion.Pasaje);

    // Igual con los guías: si la excursión ya dice cuánto cobra el guía POR DÍA, la sección
    // suelta de arriba sobra. Ahí el precio salía del catálogo de Proveedores, que es el
    // error que se viene a corregir: el mismo guía cobra distinto según la salida.
    public bool TieneGuias => Etapas.Any(e => e.Tipo == EtapaExcursion.Guia);

    // ¿Esta salida duerme fuera? En una excursión de un día no hay hospedaje que cargar y
    // la sección sólo ensucia la pantalla.
    //
    // No hay una lista de nombres: se decide sola, y alcanza con que se cumpla UNA de estas:
    //   · alguna reserva termina un día distinto del que empieza (una de 3 días va del 6 al 8);
    //   · la excursión es travesía o tiene noches cargadas;
    //   · ya hay un hospedaje cargado en esta salida.
    //
    // Lo último es lo que la vuelve segura: la sección NUNCA se esconde si adentro hay algo
    // cargado. Si se escondiera, al guardar esas filas no volverían y se borrarían.
    public bool DuermeFuera { get; set; }

    // Cuántas noches duerme afuera esta salida (0 = es de un día). Se usa para proponer
    // las noches del hospedaje y las comidas del restaurante.
    public int NochesSalida { get; set; }

    // Gastos que están cargados DOS VECES en esta salida: el mismo ítem como fila del grupo
    // y además por pasajero, o repetido dentro del mismo grupo.
    //
    // Pasa en las salidas viejas: cuando una excursión pasa a manejarse por grupo (porque se
    // le cargaron las noches) el sistema crea la fila del grupo, pero las que ya existían
    // por cada pasajero no se borran solas — podrían tener plata o comprobantes. Quedaban
    // las dos y el gasto se contaba dos veces sin que nadie lo notara.
    public List<string> GastosDuplicados { get; set; } = new();

    private void BuscarDuplicados()
    {
        GastosDuplicados = Gastos
            .GroupBy(g => (g.Nombre ?? "").Trim().ToLowerInvariant())
            .Where(gr => gr.Key.Length > 0)
            .Where(gr => gr.Count() > 1 && (
                // una fila del grupo y además una o más por pasajero
                (gr.Any(x => x.ReservaId == null) && gr.Any(x => x.ReservaId != null))
                // o el mismo ítem repetido en el grupo
                || gr.Count(x => x.ReservaId == null) > 1
                // o repetido dentro de la misma reserva
                || gr.Where(x => x.ReservaId != null)
                     .GroupBy(x => x.ReservaId).Any(r => r.Count() > 1)))
            .Select(gr => gr.First().Nombre.Trim())
            .ToList();
    }

    // Enviados desde el form al guardar
    [BindProperty] public List<int> Ids { get; set; } = new();
    [BindProperty] public List<string> Keys { get; set; } = new();    // clave para asociar el comprobante a la fila (id real, o temporal si es nueva)
    [BindProperty] public List<string> Nombres { get; set; } = new();
    [BindProperty] public List<string> Precios { get; set; } = new();   // unitario (auto) o total (a mano) según el modo
    [BindProperty] public List<string> EsManual { get; set; } = new();  // "1" si el monto se cargó a mano
    [BindProperty] public List<string> Cantidades { get; set; } = new();  // unidades de los ítems tipo "Cantidad"
    [BindProperty] public List<int> GastoReservaIds { get; set; } = new();  // a qué reserva pertenece el gasto (0 = compartido)
    [BindProperty] public List<int> Comprados { get; set; } = new();  // ids tildados
    [BindProperty] public List<string?> FechasPago { get; set; } = new();  // el día en que se pagó cada gasto
    [BindProperty] public List<string?> Comentarios { get; set; } = new();  // la nota de cada gasto (botón 📋)
    [BindProperty] public bool ServiciosPagados { get; set; }
    [BindProperty] public IFormFile? ComprobanteArchivo { get; set; }

    // Proveedores enviados (una fila puede repetirse por tipo; alineadas por índice)
    [BindProperty] public List<int> ProvIds { get; set; } = new();
    [BindProperty] public List<string> ProvKeys { get; set; } = new();   // clave para el comprobante de cada proveedor
    [BindProperty] public List<string> ProvTipos { get; set; } = new();
    [BindProperty] public List<int> ProvProveedorIds { get; set; } = new();
    [BindProperty] public List<string> ProvTotales { get; set; } = new();
    [BindProperty] public List<string> ProvSenas { get; set; } = new();
    [BindProperty] public List<string> ProvSaldos { get; set; } = new();
    // Los días en que se pagaron de verdad la seña y el saldo
    [BindProperty] public List<string?> ProvFechasSena { get; set; } = new();
    [BindProperty] public List<string?> ProvFechasSaldo { get; set; } = new();
    // La nota de cada proveedor (botón 📋)
    [BindProperty] public List<string?> ProvComentarios { get; set; } = new();
    [BindProperty] public List<string?> ProvParaQuien { get; set; } = new();
    [BindProperty] public List<int> ProvReservaIds { get; set; } = new();   // a qué reserva pertenece (hospedaje/restaurante)

    // Nombre escrito a mano cuando se elige "Otro" en vez de un proveedor del catálogo
    [BindProperty] public List<string?> ProvNombresNuevos { get; set; } = new();

    // Travesías: lugar de la ruta + el grupo que duerme ahí (personas × precio × noches).
    // En las filas de Guía y Auto, "Personas" son cuántos van y "PrecioPorPersona" lo que
    // cobra cada uno; la cuenta es la misma.
    [BindProperty] public List<string?> ProvLugares { get; set; } = new();
    [BindProperty] public List<string> ProvPersonas { get; set; } = new();
    [BindProperty] public List<string> ProvPreciosPorPersona { get; set; } = new();
    [BindProperty] public List<string> ProvNoches { get; set; } = new();

    private async Task CargarAsync()
    {
        var exc = await _db.Excursiones.FindAsync(ExcursionId);
        ExcursionNombre = exc?.Nombre ?? "Excursión";

        Reservas = await _db.Reservas
            .Where(r => r.ExcursionId == ExcursionId && r.FechaDesde.Date == Fecha.Date)
            .OrderBy(r => r.NombreCliente)
            .ToListAsync();
        PasajerosSalida = Reservas.Sum(r => r.CantidadPersonas);

        Gastos = await _db.OperativoGastos
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .OrderBy(o => o.Id)
            .ToListAsync();

        Salida = await _db.OperativoSalidas
            .FirstOrDefaultAsync(s => s.ExcursionId == ExcursionId && s.Fecha.Date == Fecha.Date)
            ?? new OperativoSalida { ExcursionId = ExcursionId, Fecha = Fecha.Date };

        ServiciosPagados = Salida.ServiciosPagados;

        // Proveedores: catálogo activo por tipo + lo ya asignado a la salida
        var catalogo = await _db.Proveedores.Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync();
        CatalogoPorTipo = Proveedor.Tipos.ToDictionary(
            t => t, t => catalogo.Where(p => p.Tipo == t).ToList());

        var asignados = await _db.OperativoProveedores
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .OrderBy(o => o.Id)
            .ToListAsync();

        // ---- Travesía: una fila de hospedaje por LUGAR de la ruta ----
        // Si la excursión tiene etapas, el hospedaje sale de ahí. Lo que ya se cargó para
        // cada lugar se busca por el nombre del lugar; lo que falta se muestra con la
        // sugerencia de la plantilla (refugio habitual + precio), SIN guardar nada todavía.
        var etapasPlantilla = await _db.EtapasExcursion
            .Where(e => e.ExcursionId == ExcursionId)
            .OrderBy(e => e.Orden)
            .ToListAsync();

        // ¿Duerme fuera? (ver el comentario de la propiedad). Se calcula con la lista
        // COMPLETA de lo asignado, antes de que más abajo se le saquen las filas de etapa.
        DuermeFuera = exc?.EsTravesia == true
                   || etapasPlantilla.Any(e => e.Tipo == EtapaExcursion.Hospedaje)
                   || Reservas.Any(r => r.FechaHasta.Date > r.FechaDesde.Date)
                   || asignados.Any(o => o.Tipo == "Hospedaje");

        // Cuántas NOCHES dura la salida, sacado de las fechas de las reservas (la que más
        // días se queda). Es lo que se propone en el hospedaje y el restaurante para no
        // tener que multiplicar a mano. En una salida de un día da 0 y la cajita no
        // aparece; en Conociendo las Yungas (06/09 al 08/09) da 2.
        // Sale de los datos, así que una excursión nueva de 3 días y 2 noches lo toma sola.
        NochesSalida = Reservas.Count == 0
            ? 0
            : Math.Max(0, Reservas.Max(r => (r.FechaHasta.Date - r.FechaDesde.Date).Days));

        if (etapasPlantilla.Count > 0)
        {
            static string Clave(string? s) => (s ?? "").Trim().ToLowerInvariant();

            // Cuántos guías se cargaron para esta salida (mínimo 1): entran en los autos,
            // porque el chofer del traslado no hace la travesía pero el guía sí viaja.
            var guias = asignados.Where(o => o.Tipo == "Guía").Sum(o => o.Personas ?? 1);
            if (guias <= 0) guias = 1;

            // 1 auto cada 4, contando a los guías. Si van 3 pasajeros y 1 guía, un auto.
            var autosSugeridos = PasajerosSalida <= 0
                ? 0
                : (int)Math.Ceiling((PasajerosSalida + guias) / (double)Wamani.Reservas.Models.Excursion.PersonasPorAuto);

            // Cada etapa se queda con UNA fila guardada y la saca del montón. Si dos etapas
            // se llaman igual (una travesía que vuelve a dormir en el mismo pueblo), la
            // segunda toma la siguiente fila y no le pisa la plata a la primera.
            var libres = new List<OperativoProveedor>(asignados);
            int primeraNoche = 1;

            Etapas = new List<EtapaRowVm>();
            foreach (var e in etapasPlantilla)
            {
                var tipo = string.IsNullOrWhiteSpace(e.Tipo) ? EtapaExcursion.Hospedaje : e.Tipo;
                var cat = CatalogoPorTipo.GetValueOrDefault(EtapaExcursion.CatalogoDe(tipo)) ?? new();

                var yaCargada = libres.FirstOrDefault(
                    o => o.Tipo == tipo && Clave(o.Lugar) == Clave(e.Lugar));
                if (yaCargada is not null) libres.Remove(yaCargada);

                var noches = e.Noches > 0 ? e.Noches : 1;

                // Arrieros y caballos arrancan en 0 a propósito: cuántos van lo deciden
                // ellos en cada salida, no hay fórmula que lo saque de la cantidad de gente.
                var sugerida = tipo switch
                {
                    // Si la excursión dice cuántos vehículos son (traslado contratado donde
                    // entra todo el grupo), se respeta. Si no, se sugiere 1 auto cada 4.
                    EtapaExcursion.Traslado => e.Cantidad is int v && v > 0 ? v : autosSugeridos,
                    // El micro se paga por boleto: uno por cada cabeza que viaja, guías incluidos
                    EtapaExcursion.Pasaje   => PasajerosSalida <= 0 ? 0 : PasajerosSalida + guias,
                    // Siempre va al menos un guía; si hacen falta más se suman a mano
                    EtapaExcursion.Guia     => 1,
                    EtapaExcursion.Arriero  => 0,
                    EtapaExcursion.Caballo  => 0,
                    _                        => PasajerosSalida,
                };

                Etapas.Add(new EtapaRowVm
                {
                    Tipo = tipo,
                    Noche = primeraNoche,
                    Lugar = e.Lugar,
                    Incluye = e.Incluye,
                    Asig = yaCargada,
                    Cat = cat,
                    Key = "etapa-" + e.Orden,
                    ProveedorSugerido = e.ProveedorId ?? 0,
                    PrecioSugerido = e.PrecioPorPersona,
                    PrecioCatalogo = cat.FirstOrDefault(p => p.Id == e.ProveedorId)?.Precio ?? 0,
                    CantidadSugerida = sugerida,
                    NochesPlantilla = noches,
                    Pasajeros = PasajerosSalida,
                    Guias = guias
                });

                if (tipo == EtapaExcursion.Hospedaje) primeraNoche += noches;
            }

            // Las filas que YA tienen su lugar propio arriba no se repiten abajo.
            var deEtapa = Etapas.Where(x => x.Asig is not null).Select(x => x.Asig!.Id).ToHashSet();
            asignados = asignados.Where(o => !deEtapa.Contains(o.Id)).ToList();
        }

        ProvPorTipo = asignados.GroupBy(o => o.Tipo).ToDictionary(g => g.Key, g => g.ToList());

        BuscarDuplicados();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Los gastos "Por persona" (hospedaje, comidas, entradas…) se materializan POR CADA
        // reserva (× la gente de esa reserva). Los "Por auto"/"Fijo" (nafta, etc.) y los
        // proveedores Auto/Guía son COMPARTIDOS de la salida (una sola vez). Así, cuando se
        // suma una reserva nueva, lo suyo arranca SIN pagar y la salida no figura "lista".
        var reservas = await _db.Reservas
            .Where(r => r.ExcursionId == ExcursionId && r.FechaDesde.Date == Fecha.Date)
            .OrderBy(r => r.NombreCliente)
            .ToListAsync();
        int pasajeros = reservas.Sum(r => r.CantidadPersonas);

        var plantilla = await _db.GastosExcursion
            .Where(g => g.ExcursionId == ExcursionId && !g.EsProveedor)
            .OrderBy(g => g.Id)
            .ToListAsync();

        var yaCargados = await _db.OperativoGastos
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .ToListAsync();

        static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();
        bool cambio = false;

        // Lo que se borró a mano en esta salida NO se vuelve a copiar. Sin esto, borrar un
        // ítem de la plantilla no servía de nada: volvía a aparecer al entrar de nuevo.
        var salidaGuardada = await _db.OperativoSalidas
            .FirstOrDefaultAsync(s => s.ExcursionId == ExcursionId && s.Fecha.Date == Fecha.Date);
        var borrados = salidaGuardada?.BorradosLista() ?? new List<string>();

        // ¿Esta salida se maneja como grupo? (tiene noches/traslados/arrieros cargados).
        // Si es así, los gastos "por persona" NO se abren uno por cliente: va UNA fila para
        // toda la salida, multiplicada por el total de gente. Cuando entra una reserva nueva
        // el número sube solo, y si hace falta un seguro o unos snacks de más se agrega a
        // mano. Abrir seguros y snacks cliente por cliente en una travesía era inmanejable.
        bool porGrupo = await _db.EtapasExcursion.AnyAsync(e => e.ExcursionId == ExcursionId);

        // Limpiar gastos "por persona" del modelo viejo que quedaron COMPARTIDOS (sin reserva).
        // Los que se agregaron a mano (PrecioUnitario null) NO se tocan.
        //
        // OJO: en las salidas por grupo esas filas son las BUENAS (así se cargan ahora), así
        // que ahí no se toca nada. Sin este guard se borraban y se volvían a crear en cada
        // visita, perdiendo lo tildado y los comprobantes.
        var viejos = porGrupo
            ? new List<OperativoGasto>()
            : yaCargados
            .Where(g => g.ReservaId == null && g.TipoCalculo == "Por persona" && g.PrecioUnitario != null)
            .ToList();
        if (viejos.Count > 0)
        {
            _db.OperativoGastos.RemoveRange(viejos);
            yaCargados = yaCargados.Except(viejos).ToList();
            cambio = true;
        }

        foreach (var p in plantilla)
        {
            // Si lo borraron a mano en esta salida, se respeta y no se vuelve a copiar.
            if (borrados.Contains(Norm(p.Nombre))) continue;

            var tipo = string.IsNullOrWhiteSpace(p.TipoCalculo) ? "Por persona" : p.TipoCalculo;

            if (tipo == "Por persona" && !porGrupo)
            {
                // Un gasto por CADA reserva (× la gente de esa reserva)
                foreach (var r in reservas)
                {
                    if (yaCargados.Any(o => o.ReservaId == r.Id && Norm(o.Nombre) == Norm(p.Nombre))) continue;
                    _db.OperativoGastos.Add(new OperativoGasto
                    {
                        ExcursionId = ExcursionId, Fecha = Fecha.Date, ReservaId = r.Id,
                        Nombre = p.Nombre ?? "", TipoCalculo = tipo,
                        PrecioUnitario = p.Precio,
                        Precio = p.Precio * r.CantidadPersonas,
                        Comprado = false
                    });
                    cambio = true;
                }
            }
            else
            {
                // Compartido (por auto / cantidad / fijo): una sola vez para la salida
                if (yaCargados.Any(o => o.ReservaId == null && Norm(o.Nombre) == Norm(p.Nombre))) continue;

                // Si el ítem YA está cargado por pasajero, no se agrega además la fila del
                // grupo: sería la misma plata dos veces.
                //
                // Pasa en las salidas que se abrieron cuando la excursión todavía no tenía
                // noches: ahí cada pasajero tenía su propia fila, y al pasar a manejarse por
                // grupo el sistema le sumaba la fila del grupo encima. Borrar la del grupo no
                // servía porque volvía a aparecer sola en la visita siguiente. Las dos formas
                // dan el mismo total; lo que no puede es haber una de cada una.
                if (porGrupo && tipo == "Por persona"
                    && yaCargados.Any(o => o.ReservaId != null && Norm(o.Nombre) == Norm(p.Nombre)))
                    continue;
                // "Cantidad" (arrieros, caballos, guías, traslados) arranca con la cantidad
                // de referencia de la excursión; después se sube o se baja en la salida.
                var cant = tipo == "Cantidad" ? (p.Cantidad ?? 0) : (int?)null;
                _db.OperativoGastos.Add(new OperativoGasto
                {
                    ExcursionId = ExcursionId, Fecha = Fecha.Date, ReservaId = null,
                    Nombre = p.Nombre ?? "", TipoCalculo = tipo,
                    PrecioUnitario = p.Precio,
                    Cantidad = cant,
                    Precio = p.Precio * OperativoGasto.Multiplicador(tipo, pasajeros, cant),
                    Comprado = false
                });
                cambio = true;
            }
        }

        // Refrescar los totales automáticos por si cambió la cantidad de gente
        var paxPorReserva = reservas.ToDictionary(r => r.Id, r => r.CantidadPersonas);
        foreach (var g in yaCargados)
        {
            if (g.PrecioUnitario is not decimal u) continue;
            int mult = g.ReservaId is int rid
                ? (paxPorReserva.TryGetValue(rid, out var px) ? px : 0)          // por persona de esa reserva
                : g.MultiplicadorPropio(pasajeros);                                // compartido
            var nuevoTotal = u * mult;
            if (g.Precio != nuevoTotal) { g.Precio = nuevoTotal; cambio = true; }
        }
        if (cambio) await _db.SaveChangesAsync();

        await CargarAsync();
        return Page();
    }

    // Cuánta plata ya salió de esta salida: los gastos marcados como comprados más
    // lo que se le pagó a los proveedores (seña + saldo). Se mide antes y después de
    // guardar para anotar en Actividad quién movió cuánto.
    private async Task<decimal> PagadoDeLaSalidaAsync()
    {
        var pax = await _db.Reservas.AsNoTracking()
            .Where(r => r.ExcursionId == ExcursionId && r.FechaDesde.Date == Fecha.Date)
            .SumAsync(r => (int?)r.CantidadPersonas) ?? 0;

        var gs = await _db.OperativoGastos.AsNoTracking()
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date).ToListAsync();
        var ps = await _db.OperativoProveedores.AsNoTracking()
            .Where(p => p.ExcursionId == ExcursionId && p.Fecha.Date == Fecha.Date).ToListAsync();

        return gs.Where(g => g.Comprado).Sum(g => g.TotalPara(pax)) + ps.Sum(p => p.Pagado());
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var pagadoAntes = await PagadoDeLaSalidaAsync();

        var existentes = await _db.OperativoGastos
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .ToListAsync();

        var reservasSalida = await _db.Reservas
            .Where(r => r.ExcursionId == ExcursionId && r.FechaDesde.Date == Fecha.Date)
            .ToListAsync();
        int pasajeros = reservasSalida.Sum(r => r.CantidadPersonas);
        var paxPorReserva = reservasSalida.ToDictionary(r => r.Id, r => r.CantidadPersonas);

        // Total de un gasto automático: unitario × gente (de su reserva, o de la salida si es
        // compartido). En los de tipo "Cantidad" manda la cantidad que cargaron a mano.
        int MultDe(OperativoGasto g) => g.ReservaId is int rid
            ? (paxPorReserva.TryGetValue(rid, out var px) ? px : 0)
            : g.MultiplicadorPropio(pasajeros);

        var idsEnviados = new HashSet<int>();

        for (int i = 0; i < Nombres.Count; i++)
        {
            var nombre = (Nombres[i] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nombre)) continue;

            var id = i < Ids.Count ? Ids[i] : 0;
            var valor = ParsePrecio(i < Precios.Count ? Precios[i] : "0");  // unitario (auto) o total (a mano)
            var esManual = i < EsManual.Count && EsManual[i] == "1";
            var comprado = id != 0 && Comprados.Contains(id);

            // El comprobante viaja como archivo "comp_{clave}". La clave es el id real de la
            // fila, o una temporal (ej "n1") si es una fila recién agregada que todavía no
            // tiene id. Así una fila NUEVA también puede traer su comprobante en el mismo guardado.
            var clave = i < Keys.Count ? Keys[i] : id.ToString();

            var resGasto = i < GastoReservaIds.Count ? GastoReservaIds[i] : 0;

            // El día en que se pagó, tal como quedó escrito en la pantalla.
            var fechaEscrita = ParseFecha(i < FechasPago.Count ? FechasPago[i] : null);

            // La nota del botón 📋. Vacía se guarda como null, no como texto en blanco.
            var notaGasto = Recortar(i < Comentarios.Count ? Comentarios[i] : null);

            if (id == 0)
            {
                // Fila nueva agregada a mano en el operativo → el monto es el total directo.
                var nuevo = new OperativoGasto
                {
                    ExcursionId = ExcursionId,
                    Fecha = Fecha.Date,
                    ReservaId = resGasto == 0 ? null : resGasto,
                    Nombre = nombre,
                    TipoCalculo = "Por persona",
                    PrecioUnitario = null,   // a mano
                    Precio = valor,
                    // Un gasto agregado a mano es plata que ya se gastó de verdad: nace
                    // tildado y con fecha de pago. (La regla es siempre: tilde = pagado.)
                    Comprado = valor > 0,
                    Comentario = notaGasto
                };
                if (valor > 0) nuevo.FechaPago = fechaEscrita ?? Wamani.Reservas.Services.Reloj.HoyJujuy();
                nuevo.Comprobante = await GuardarArchivosAsync($"comp_{clave}", null);
                _db.OperativoGastos.Add(nuevo);
            }
            else
            {
                var g = existentes.FirstOrDefault(x => x.Id == id);
                if (g is not null)
                {
                    g.Nombre = nombre;
                    g.Comprado = comprado;
                    g.Comentario = notaGasto;

                    // Ítems de tipo "Cantidad" (arrieros, caballos, guías, traslados): la
                    // cantidad la deciden los chicos con los botones + y −, así que se toma
                    // tal cual viene de la pantalla ANTES de recalcular el total.
                    if (g.TipoCalculo == "Cantidad")
                    {
                        var cantTxt = i < Cantidades.Count ? Cantidades[i] : null;
                        if (int.TryParse(cantTxt, out var cant) && cant >= 0) g.Cantidad = cant;
                    }

                    if (esManual)
                    {
                        g.PrecioUnitario = null;
                        g.Precio = valor;   // total escrito a mano
                    }
                    else
                    {
                        g.PrecioUnitario = valor;   // precio unitario (por persona / por auto / por unidad)
                        g.Precio = valor * MultDe(g);
                    }

                    // La fecha de pago aparece cuando se TILDA el gasto como listo. Antes se
                    // ponía con sólo tener precio: como la plantilla de la excursión se copia
                    // con los precios ya cargados, alcanzaba con guardar una vez para que TODA
                    // la estimación (guía, viáticos, traslados…) contara como plata pagada ese
                    // día. El tilde "LISTO" sigue siendo el único que confirma el pago.
                    //
                    // Lo que cambia es CUÁL fecha: manda la que se escribió en la pantalla, que
                    // es el día en que se pagó de verdad. Si no escribieron ninguna se respeta
                    // la que ya tenía, y sólo si no había ninguna se usa la de hoy. Sin esto,
                    // un pago hecho el 12 y cargado el 28 quedaba fechado el 28 y el día por
                    // día de Finanzas no cerraba nunca contra el banco.
                    if (comprado && g.Precio > 0)
                        g.FechaPago = fechaEscrita ?? g.FechaPago ?? Wamani.Reservas.Services.Reloj.HoyJujuy();
                    if (!comprado || g.Precio == 0) g.FechaPago = null;

                    g.Comprobante = await GuardarArchivosAsync($"comp_{clave}", g.Comprobante);

                    idsEnviados.Add(id);
                }
            }
        }

        // Borrar los que se quitaron en la pantalla
        var quitados = existentes.Where(g => !idsEnviados.Contains(g.Id)).ToList();
        foreach (var g in quitados)
            _db.OperativoGastos.Remove(g);

        // Estado de la salida: servicios pagados + comprobante
        var salida = await _db.OperativoSalidas
            .FirstOrDefaultAsync(s => s.ExcursionId == ExcursionId && s.Fecha.Date == Fecha.Date);
        if (salida is null)
        {
            salida = new OperativoSalida { ExcursionId = ExcursionId, Fecha = Fecha.Date };
            _db.OperativoSalidas.Add(salida);
        }
        salida.ServiciosPagados = ServiciosPagados;

        // ---- Que borrar signifique BORRAR ----
        // El operativo copia de la excursión lo que falta cada vez que se abre, así que un
        // ítem borrado volvía a aparecer solo. Se anota el nombre de lo que se sacó a mano
        // para no volver a copiarlo; y si lo vuelven a escribir, se saca de la lista.
        static string Clave(string? s) => (s ?? "").Trim().ToLowerInvariant();

        var yaBorrados = salida.BorradosLista();
        var nombresEnPantalla = Nombres
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(Clave)
            .ToHashSet();

        foreach (var g in quitados)
        {
            var k = Clave(g.Nombre);
            // Sólo si NO quedó otra fila con ese mismo nombre: borrar el "Seguro Cliente"
            // de una reserva no significa que la salida se quede sin seguro.
            if (!nombresEnPantalla.Contains(k) && !yaBorrados.Contains(k))
                yaBorrados.Add(k);
        }

        // Lo que volvió a aparecer en la pantalla deja de estar borrado
        yaBorrados.RemoveAll(k => nombresEnPantalla.Contains(k));
        salida.GuardarBorrados(yaBorrados);


        // ---- Proveedores por tipo ----
        var provExistentes = await _db.OperativoProveedores
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date)
            .ToListAsync();
        var catalogo = await _db.Proveedores.ToDictionaryAsync(p => p.Id, p => p.Nombre);

        var provIdsEnviados = new HashSet<int>();
        for (int i = 0; i < ProvTipos.Count; i++)
        {
            var tipo = ProvTipos[i];
            var rowId = i < ProvIds.Count ? ProvIds[i] : 0;
            var provId = i < ProvProveedorIds.Count ? ProvProveedorIds[i] : 0;

            // "Otro": se escribió un proveedor que no está en el catálogo (ej. un hospedaje de
            // una zona donde no hacemos salidas regulares). Se da de alta acá mismo, así queda
            // disponible para la próxima. Si ya existe uno con ese nombre y tipo, se reusa.
            if (provId == -1)
            {
                var nombreNuevo = (i < ProvNombresNuevos.Count ? ProvNombresNuevos[i] : null)?.Trim();
                if (string.IsNullOrWhiteSpace(nombreNuevo))
                {
                    provId = 0;   // eligió "Otro" pero no escribió el nombre
                }
                else
                {
                    var yaExiste = await _db.Proveedores
                        .FirstOrDefaultAsync(p => p.Tipo == tipo && p.Nombre.ToLower() == nombreNuevo.ToLower());
                    if (yaExiste is not null)
                    {
                        provId = yaExiste.Id;
                    }
                    else
                    {
                        var nuevoProv = new Proveedor { Tipo = tipo, Nombre = nombreNuevo, Activo = true };
                        _db.Proveedores.Add(nuevoProv);
                        await _db.SaveChangesAsync();
                        provId = nuevoProv.Id;
                    }
                    catalogo[provId] = nombreNuevo;   // para que la fila guarde bien el nombre
                }
            }

            var total = ParsePrecio(i < ProvTotales.Count ? ProvTotales[i] : "0");
            var sena = ParsePrecio(i < ProvSenas.Count ? ProvSenas[i] : "0");
            var saldo = ParsePrecio(i < ProvSaldos.Count ? ProvSaldos[i] : "0");
            var paraQuien = (i < ProvParaQuien.Count ? ProvParaQuien[i] : null)?.Trim();
            var notaProv = Recortar(i < ProvComentarios.Count ? ProvComentarios[i] : null);

            // Fila "de grupo": todo el grupo en un lugar de la ruta (hospedaje de travesía),
            // o cuántos guías / autos van y cuánto cobra cada uno.
            var lugar = (i < ProvLugares.Count ? ProvLugares[i] : null)?.Trim();
            int.TryParse(i < ProvPersonas.Count ? ProvPersonas[i] : "", out var personas);
            var precioPP = ParsePrecio(i < ProvPreciosPorPersona.Count ? ProvPreciosPorPersona[i] : "0");
            var nochesTexto = (i < ProvNoches.Count ? ProvNoches[i] : "")?.Trim();
            int.TryParse(nochesTexto, out var noches);
            if (noches < 1) noches = 1;
            // ¿La fila TRAJO la cajita de noches? Las que no la tienen mandan vacío. Sirve
            // para saber si hay que guardar el número: en el hospedaje y el restaurante de
            // una salida de varios días hay que guardarlo aunque no haya lugar de la ruta,
            // así al volver a abrir la salida la cajita muestra lo que se había puesto.
            var trajoNoches = !string.IsNullOrWhiteSpace(nochesTexto);

            // Si no se escribió un total a mano, sale solo: cantidad × precio × noches.
            // (En hospedaje son personas × precio por persona × noches; en guía y auto son
            // cuántos × lo que cobra cada uno, con noches = 1.)
            if (total == 0 && personas > 0 && precioPP > 0) total = personas * precioPP * noches;

            // ¿Esta fila usa la cuenta por cantidad? Si sí, se guardan los tres campos para
            // poder rearmarla la próxima vez. Las filas por pasajero no los usan y van en null.
            var esDeGrupo = personas > 0 || precioPP > 0 || !string.IsNullOrWhiteSpace(lugar);

            // Una fila está vacía cuando no tiene NADA: ni proveedor, ni plata, ni para
            // quién… ni una nota escrita. Si alguien anotó algo, la fila se guarda aunque
            // todavía no tenga montos; si no, se perdería lo que acaba de escribir.
            var vacia = provId == 0 && total == 0 && sena == 0 && saldo == 0
                     && string.IsNullOrWhiteSpace(paraQuien) && notaProv is null;

            OperativoProveedor? row;
            if (rowId == 0)
            {
                if (vacia) continue;  // fila nueva vacía → ignorar
                row = new OperativoProveedor { ExcursionId = ExcursionId, Fecha = Fecha.Date, Tipo = tipo };
                _db.OperativoProveedores.Add(row);
            }
            else
            {
                row = provExistentes.FirstOrDefault(x => x.Id == rowId);
                if (row is null) continue;
                provIdsEnviados.Add(rowId);
                if (vacia) { _db.OperativoProveedores.Remove(row); continue; }
            }

            var resId = i < ProvReservaIds.Count ? ProvReservaIds[i] : 0;

            row.Tipo = tipo;
            row.ReservaId = resId == 0 ? null : resId;
            row.ProveedorId = provId == 0 ? null : provId;
            row.ProveedorNombre = provId != 0 && catalogo.TryGetValue(provId, out var n) ? n : "";
            row.Lugar = string.IsNullOrWhiteSpace(lugar) ? null : lugar;
            row.Personas = esDeGrupo ? personas : null;
            row.PrecioPorPersona = esDeGrupo ? precioPP : null;
            row.Noches = (!string.IsNullOrWhiteSpace(lugar) || trajoNoches) ? noches : null;
            row.Total = total;
            row.Sena = sena;
            row.Saldo = saldo;
            row.Comentario = notaProv;
            row.ParaQuien = string.IsNullOrWhiteSpace(paraQuien) ? null : paraQuien;

            // El día en que se pagó la seña y el saldo: manda lo que se escribió en la
            // pantalla. Si no escribieron nada se respeta lo que ya había, y recién si no
            // había nada se usa la de hoy. Si el monto se borra, la fecha se va con él.
            var fSenaEscrita = ParseFecha(i < ProvFechasSena.Count ? ProvFechasSena[i] : null);
            var fSaldoEscrita = ParseFecha(i < ProvFechasSaldo.Count ? ProvFechasSaldo[i] : null);

            if (sena > 0) row.FechaSena = fSenaEscrita ?? row.FechaSena ?? Wamani.Reservas.Services.Reloj.HoyJujuy();
            if (sena == 0) row.FechaSena = null;
            if (saldo > 0) row.FechaSaldo = fSaldoEscrita ?? row.FechaSaldo ?? Wamani.Reservas.Services.Reloj.HoyJujuy();
            if (saldo == 0) row.FechaSaldo = null;

            // Comprobantes por clave de fila (funciona también en filas nuevas y admite varios)
            var provKey = i < ProvKeys.Count ? ProvKeys[i] : rowId.ToString();
            row.ComprobanteSena = await GuardarArchivosAsync($"provcompsena_{provKey}", row.ComprobanteSena);
            row.ComprobanteSaldo = await GuardarArchivosAsync($"provcompsaldo_{provKey}", row.ComprobanteSaldo);
        }

        // Borrar filas que se quitaron en la pantalla (existían y no volvieron)
        foreach (var e in provExistentes)
            if (!provIdsEnviados.Contains(e.Id))
                _db.OperativoProveedores.Remove(e);

        await _db.SaveChangesAsync();

        // Anotar quién pagó y cuánto en este guardado. Va con el signo dado vuelta
        // porque acá la plata SALE: si se pagaron 340.000 más, es un egreso de 340.000.
        var pagadoDespues = await PagadoDeLaSalidaAsync();
        var nombreExc = await _db.Excursiones.AsNoTracking()
            .Where(e => e.Id == ExcursionId).Select(e => e.Nombre).FirstOrDefaultAsync() ?? "Excursión";
        Wamani.Reservas.Services.Registro.Anotar(_db, User, "Operativo",
            $"{nombreExc} · salida del {Fecha:dd/MM/yyyy}",
            ExcursionId, -(pagadoDespues - pagadoAntes));
        await _db.SaveChangesAsync();

        return RedirectToPage("/Operativo/Salida",
            new { ExcursionId, Fecha = Fecha.ToString("yyyy-MM-dd"), guardado = true });
    }

    // Borra los gastos de ESTA salida que no tienen nada encima —ni tilde, ni fecha de
    // pago, ni comprobante— para que se vuelvan a copiar limpios de la plantilla.
    //
    // Hace falta porque el operativo sólo AGREGA los ítems que faltan: si en la excursión
    // se renombra un costo, o se pasa a "proveedor", el ítem viejo queda dando vueltas para
    // siempre y termina duplicado (el guía apareciendo en Proveedores y en Gastos a la vez).
    //
    // Lo que ya está pagado o tiene comprobante NO se toca: eso es plata, no estimación.
    public async Task<IActionResult> OnPostRehacerGastosAsync()
    {
        // Tampoco se toca lo que tenga una NOTA escrita a mano: si alguien se tomó el
        // trabajo de anotar algo ahí, es información suya, igual que un comprobante.
        var borrables = await _db.OperativoGastos
            .Where(o => o.ExcursionId == ExcursionId && o.Fecha.Date == Fecha.Date
                     && !o.Comprado && o.FechaPago == null && o.Comprobante == null
                     && o.Comentario == null)
            .ToListAsync();

        int cuantos = borrables.Count;
        if (cuantos > 0) _db.OperativoGastos.RemoveRange(borrables);

        // "Rehacer" es empezar de nuevo: también se olvida qué ítems se habían sacado a
        // mano, así que la plantilla vuelve entera y de ahí se decide otra vez.
        var salida = await _db.OperativoSalidas
            .FirstOrDefaultAsync(s => s.ExcursionId == ExcursionId && s.Fecha.Date == Fecha.Date);
        if (salida is not null) salida.ItemsBorrados = null;

        await _db.SaveChangesAsync();

        // Al volver por GET, la plantilla se copia de nuevo, ya limpia.
        return RedirectToPage("/Operativo/Salida",
            new { ExcursionId, Fecha = Fecha.ToString("yyyy-MM-dd"), rehechos = cuantos });
    }

    [BindProperty(SupportsGet = true)]
    public int? Rehechos { get; set; }

    // Guarda TODOS los archivos que vinieron en ese campo y los agrega a los que ya había
    // (así una seña puede tener 2 o más comprobantes). Devuelve el valor para la columna.
    private async Task<string?> GuardarArchivosAsync(string campo, string? actual)
    {
        var carpeta = Wamani.Reservas.Services.Comprobantes.Carpeta(_env);
        return await Wamani.Reservas.Services.Adjuntos
            .AgregarAsync(Request.Form.Files.GetFiles(campo), carpeta, actual);
    }

    [BindProperty(SupportsGet = true)]
    public bool Guardado { get; set; }

    // Una nota escrita a mano: sin espacios de más, tope de 2000 letras (lo que entra en la
    // columna) y vacía se guarda como null.
    private static string? Recortar(string? txt)
    {
        var t = txt?.Trim();
        if (string.IsNullOrWhiteSpace(t)) return null;
        return t.Length > 2000 ? t.Substring(0, 2000) : t;
    }

    // Una fecha escrita en la pantalla ("2026-08-28"). Si viene vacía o mal, devuelve null
    // y el que llama decide con qué reemplazarla.
    private static DateTime? ParseFecha(string? txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return null;
        return DateTime.TryParse(txt.Trim(), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var f) ? f.Date : null;
    }

    private static decimal ParsePrecio(string? txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return 0m;
        txt = txt.Trim().Replace(",", ".");
        return decimal.TryParse(txt, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }
}

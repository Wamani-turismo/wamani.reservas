using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Wamani.Reservas.Services;

// CIERRE DE MES: el informe que se baja al terminar cada mes.
//
// Tiene partes que contestan preguntas distintas, y mezclarlas es el error que más
// caro sale:
//
//   1. LA PLATA QUE SE MOVIÓ este mes: lo que entró menos lo que salió. Es la caja.
//   2. EL MES A MES: la misma cuenta para cada mes anterior, con el acumulado al lado,
//      para leer este mes contra lo que viene siendo el año.
//   3. LOS COMPROMISOS de hoy: lo que falta cobrar y lo que falta pagar de TODAS las
//      salidas, las de este mes y las que vienen.
//
// Hace falta ver las dos juntas porque en turismo se cobra la seña de una reserva y se
// paga la seña de los proveedores, y el resto queda para más adelante. Un mes puede
// cerrar "ganando" con plata que en realidad ya está comprometida para pagar los saldos.
//
// Los movimientos del fondo de inversión NO entran acá a propósito: ese fondo vive en
// dólares, aparte, y tiene su propia pantalla. Se menciona al pie sólo como dato.
public static class CierrePdf
{
    private const string VerdeOscuro = "#22332F";
    private const string Crema = "#F5F2EA";
    private const string Dorado = "#D8C096";
    private const string Tinta = "#23332E";
    private const string Gris = "#7C8279";
    private const string Linea = "#E7E0D0";
    private const string Rojo = "#B4472F";
    private const string Verde = "#4C7A4F";

    public class Datos
    {
        public DateTime Mes { get; set; }
        public string MesTexto { get; set; } = "";

        // --- Lo que se movió este mes ---
        public decimal CobradoReservas { get; set; }
        public decimal IngresosExtra { get; set; }
        public decimal PagadoOperativo { get; set; }     // gastos + proveedores de las salidas
        public decimal GastosEmpresa { get; set; }
        public decimal Ganancia => CobradoReservas + IngresosExtra - PagadoOperativo - GastosEmpresa;

        public int Reservas { get; set; }
        public int Personas { get; set; }

        // --- Mes a mes, desde el primer mes con movimiento hasta el mes del informe ---
        //
        // El cierre de un mes solo no dice si vamos para arriba o para abajo. Esta tabla
        // pone los meses anteriores al lado y acumula las ganancias, para leer el mes del
        // informe contra lo que viene siendo el año.
        //
        // No se muestran los meses POSTERIORES al del informe, aunque ya tengan plata
        // cargada (siempre hay señas cobradas de salidas que vienen): el informe es la
        // foto hasta ese mes y meterle un mes a medio empezar haría ruido.
        // Las columnas van separadas igual que en el punto 1 y que en la pantalla Anual:
        // lo pagado de las salidas por un lado y los gastos de la empresa por otro. Si se
        // juntaran en un solo "salió", los números no cuadrarían contra ninguna otra
        // pantalla del sistema.
        public class LineaMes
        {
            public string Nombre { get; set; } = "";
            public decimal Entro { get; set; }
            public decimal Pagado { get; set; }      // proveedores y gastos de las salidas
            public decimal Empresa { get; set; }     // publicidad, suscripciones, botiquín…
            public decimal Ganancia => Entro - Pagado - Empresa;
            public decimal Acumulado { get; set; }   // la suma de las ganancias hasta este mes
            public bool EsEsteMes { get; set; }
        }
        public List<LineaMes> Historia { get; set; } = new();
        public decimal AcumuladoTotal => Historia.Count > 0 ? Historia[^1].Acumulado : Ganancia;
        public decimal AcumuladoAntes => AcumuladoTotal - Ganancia;

        // --- Lo que se llevaron los socios este mes ---
        public List<(string Quien, decimal Monto)> Retiros { get; set; } = new();
        public List<(string Quien, decimal Monto)> Aportes { get; set; } = new();
        public decimal RetirosTotal => Retiros.Sum(r => r.Monto);
        public decimal AportesTotal => Aportes.Sum(a => a.Monto);

        // --- Compromisos, al día de hoy ---
        public decimal CajaHoy { get; set; }
        public decimal FaltaCobrar { get; set; }
        public int CuantosSaldos { get; set; }
        public decimal FaltaPagarGastos { get; set; }
        public decimal FaltaPagarProveedores { get; set; }
        public decimal FaltaPagar => FaltaPagarGastos + FaltaPagarProveedores;
        public decimal Proyectado => CajaHoy + FaltaCobrar - FaltaPagar;

        // --- El fondo, sólo como dato ---
        public decimal FondoDolares { get; set; }

        // --- El control de caja ---
        //
        // La plata contada de verdad en el banco contra lo que decía el sistema. Es el único
        // número del informe que no sale del sistema mismo, así que es el que dice si hay
        // que creerle al resto. Si no se hizo ningún control, no se muestra nada: inventar
        // un "cuadra" que nadie verificó sería peor que no decir nada.
        public bool HayArqueo { get; set; }
        public DateTime ArqueoFecha { get; set; }
        public decimal ArqueoReal { get; set; }
        public decimal ArqueoSistema { get; set; }
        public decimal ArqueoDiferencia { get; set; }
        public string ArqueoMotivo { get; set; } = "";
        public bool ArqueoAjustado { get; set; }
        public string ArqueoEtiqueta =>
            ArqueoDiferencia == 0 ? "cuadra" : (ArqueoDiferencia > 0 ? "sobrante" : "faltante");
    }

    public static byte[] Generar(Datos d, string rutaLogo)
    {
        var ci = CultureInfo.GetCultureInfo("es-AR");
        string Money(decimal m) => (m < 0 ? "− $ " : "$ ") + Math.Abs(m).ToString("N0", ci);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.PageColor(Crema);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Tinta).FontFamily("Helvetica"));

                page.Content().Column(col =>
                {
                    // ───── Encabezado ─────
                    col.Item().Background(VerdeOscuro).Padding(26).Row(fila =>
                    {
                        fila.RelativeItem().Column(c =>
                        {
                            c.Item().Text("CIERRE DE MES").FontSize(9).FontColor(Dorado).Bold().LetterSpacing(0.22f);
                            c.Item().PaddingTop(6).Text(d.MesTexto).FontSize(24).FontColor(Crema).Bold();
                            c.Item().PaddingTop(4).Text("Wamani Turismo").FontSize(10).FontColor("#E9E3D4");
                        });
                        // Mismo modo que el comprobante de reserva, que ya está probado.
                        if (File.Exists(rutaLogo))
                            fila.ConstantItem(130).AlignRight().AlignMiddle().Image(rutaLogo).FitWidth();
                    });

                    col.Item().Padding(28).Column(c =>
                    {
                        // ───── 1. La plata que se movió ─────
                        Titulo(c, "1 · La plata que se movió este mes");
                        c.Item().PaddingTop(4).Text(
                            "Lo que entró y salió de verdad en " + d.MesTexto.ToLower() + ", por fecha de pago.")
                            .FontSize(9).FontColor(Gris);

                        c.Item().PaddingTop(10).Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(120); });
                            Renglon(t, "Cobrado por reservas", Money(d.CobradoReservas), false);
                            if (d.IngresosExtra != 0)
                                Renglon(t, "Ingresos extra (comisiones, alquileres)", Money(d.IngresosExtra), false);
                            Renglon(t, "Pagado de las salidas (proveedores y gastos)", "− " + Money(d.PagadoOperativo), false, Rojo);
                            Renglon(t, "Gastos de la empresa (publicidad, suscripciones…)", "− " + Money(d.GastosEmpresa), false, Rojo);
                            Renglon(t, "GANANCIA DEL MES", Money(d.Ganancia), true,
                                d.Ganancia < 0 ? Rojo : Verde);
                        });

                        c.Item().PaddingTop(8).Text(
                            $"{d.Reservas} reserva(s) · {d.Personas} pasajero(s) movieron plata este mes.")
                            .FontSize(9).FontColor(Gris);

                        // ───── 2. Mes a mes ─────
                        if (d.Historia.Count > 1)
                        {
                            c.Item().PaddingTop(22);
                            Titulo(c, "2 · Mes a mes — cómo viene el año");
                            c.Item().PaddingTop(4).Text(
                                "Cada mes con la misma cuenta del punto 1 — lo que entró, lo pagado de las " +
                                "salidas, los gastos de la empresa — y al final la columna que va acumulando. " +
                                "Así se ve si " + d.MesTexto.ToLower() + " fue mejor o peor que lo que venía.")
                                .FontSize(9).FontColor(Gris);

                            c.Item().PaddingTop(10).Table(t =>
                            {
                                t.ColumnsDefinition(cd =>
                                {
                                    cd.RelativeColumn();        // mes
                                    cd.ConstantColumn(88);      // entró
                                    cd.ConstantColumn(88);      // pagado de las salidas
                                    cd.ConstantColumn(80);      // gastos de empresa
                                    cd.ConstantColumn(88);      // ganancia
                                    cd.ConstantColumn(95);      // acumulado
                                });

                                Encabezado(t, "Mes", "Entró", "Pagado", "Empresa", "Ganancia", "Acumulado");

                                foreach (var m in d.Historia)
                                    FilaMes(t, m.Nombre, Money(m.Entro), Money(m.Pagado), Money(m.Empresa),
                                        Money(m.Ganancia), Money(m.Acumulado),
                                        m.EsEsteMes, m.Ganancia < 0);

                                FilaMes(t, "ACUMULADO",
                                    Money(d.Historia.Sum(x => x.Entro)),
                                    Money(d.Historia.Sum(x => x.Pagado)),
                                    Money(d.Historia.Sum(x => x.Empresa)),
                                    "", Money(d.AcumuladoTotal),
                                    false, d.AcumuladoTotal < 0, total: true);
                            });

                            c.Item().PaddingTop(8).Text(
                                $"Hasta {d.MesTexto.ToLower()} Wamani lleva {Money(d.AcumuladoTotal)} de ganancia " +
                                $"acumulada: {Money(d.AcumuladoAntes)} de los meses anteriores más " +
                                $"{Money(d.Ganancia)} de este mes. El acumulado suma las ganancias de cada mes; " +
                                "no descuenta lo que los socios ya retiraron.")
                                .FontSize(9).FontColor(Gris);
                        }

                        // ───── 3. Lo que se llevaron los socios ─────
                        if (d.Retiros.Count > 0 || d.Aportes.Count > 0)
                        {
                            c.Item().PaddingTop(22);
                            Titulo(c, "3 · Lo que se llevaron los socios");
                            c.Item().PaddingTop(10).Table(t =>
                            {
                                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(120); });
                                foreach (var (quien, monto) in d.Retiros)
                                    Renglon(t, "Retiro · " + (string.IsNullOrWhiteSpace(quien) ? "sin asignar" : quien),
                                        "− " + Money(monto), false, Rojo);
                                foreach (var (quien, monto) in d.Aportes)
                                    Renglon(t, "Aporte · " + (string.IsNullOrWhiteSpace(quien) ? "sin asignar" : quien),
                                        Money(monto), false, Verde);
                                if (d.RetirosTotal > 0)
                                    Renglon(t, "TOTAL RETIRADO", "− " + Money(d.RetirosTotal), true, Rojo);
                            });
                        }

                        // ───── 4. Compromisos ─────
                        c.Item().PaddingTop(22);
                        Titulo(c, "4 · Compromisos — lo que falta cobrar y pagar");
                        c.Item().PaddingTop(4).Text(
                            "Esto no es de este mes: es todo lo pendiente a hoy, de las salidas que ya pasaron y " +
                            "de las que vienen. Se cobra la seña y se paga la seña; el resto, de los dos lados, " +
                            "queda para los meses que siguen.")
                            .FontSize(9).FontColor(Gris);

                        c.Item().PaddingTop(10).Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(120); });
                            Renglon(t, "Plata en caja hoy", Money(d.CajaHoy), false);
                            Renglon(t, $"Falta cobrar ({d.CuantosSaldos} saldo(s) de reservas)", "+ " + Money(d.FaltaCobrar), false, Verde);
                            Renglon(t, "Falta pagar · gastos de las salidas", "− " + Money(d.FaltaPagarGastos), false, Rojo);
                            Renglon(t, "Falta pagar · proveedores", "− " + Money(d.FaltaPagarProveedores), false, Rojo);
                            Renglon(t, "SI SE COBRA Y SE PAGA TODO, QUEDA", Money(d.Proyectado), true,
                                d.Proyectado < 0 ? Rojo : Verde);
                        });

                        // ───── El control de caja ─────
                        //
                        // Va pegado a los compromisos porque desmiente o confirma el primer
                        // renglón de esa tabla: la plata en caja. Todo lo demás del informe
                        // sale del sistema; esto sale de haber contado el banco.
                        if (d.HayArqueo)
                        {
                            var cuadra = d.ArqueoDiferencia == 0;
                            var colorArq = cuadra ? Verde : (d.ArqueoDiferencia > 0 ? Verde : Rojo);
                            c.Item().PaddingTop(14).Background("#F7F2E4").Border(1).BorderColor(Dorado)
                                .Padding(12).Column(a =>
                            {
                                a.Item().Text("Control de caja · " + d.ArqueoFecha.ToString("dd/MM/yyyy"))
                                    .FontSize(10).Bold().FontColor(VerdeOscuro);
                                a.Item().PaddingTop(5).Text(
                                    $"En la cuenta había {Money(d.ArqueoReal)} de verdad. El sistema decía " +
                                    $"{Money(d.ArqueoSistema)}.")
                                    .FontSize(9.5f);
                                if (cuadra)
                                    a.Item().PaddingTop(3).Text("Cuadra exacto.").FontSize(9.5f).Bold().FontColor(Verde);
                                else
                                    a.Item().PaddingTop(3).Text(
                                        $"{char.ToUpper(d.ArqueoEtiqueta[0])}{d.ArqueoEtiqueta[1..]} de " +
                                        $"{Money(Math.Abs(d.ArqueoDiferencia))} · {d.ArqueoMotivo}.")
                                        .FontSize(9.5f).Bold().FontColor(colorArq);
                                if (!cuadra)
                                    a.Item().PaddingTop(3).Text(d.ArqueoAjustado
                                        ? "Ya se acomodó la caja: la diferencia está cargada y los números de este informe la incluyen."
                                        : "Todavía NO se acomodó la caja: los números de este informe no incluyen esta diferencia.")
                                        .FontSize(8.5f).FontColor(Gris);
                            });
                        }

                        // ───── 5. El cierre ─────
                        //
                        // Neutral a propósito: las dos puntas, lo que falta cobrar y lo que falta
                        // pagar, y cómo queda la foto. Lo pendiente de cobrar son reservas con la
                        // seña puesta, no deudas dudosas: descontar sólo lo que se debe e ignorar
                        // lo que se va a cobrar daría una foto falsa y asustaría de más.
                        c.Item().PaddingTop(18).Background("#E8F0E6").Border(1).BorderColor(Verde)
                            .Padding(14).Column(a =>
                        {
                            a.Item().Text("Cómo cerró el mes").FontSize(11).Bold().FontColor(Verde);
                            a.Item().PaddingTop(6).Text(
                                $"{d.MesTexto} dejó {Money(d.Ganancia)} de ganancia.")
                                .FontSize(11).Bold();
                            if (d.Historia.Count > 1)
                                a.Item().PaddingTop(4).Text(
                                    $"Con esto, el acumulado de todos los meses llega a {Money(d.AcumuladoTotal)}.")
                                    .FontSize(10).Bold();
                            a.Item().PaddingTop(6).Text(
                                $"Además, de las salidas ya vendidas quedan {Money(d.FaltaCobrar)} por cobrar y " +
                                $"{Money(d.FaltaPagar)} por pagar, que se van a ir moviendo en los próximos meses. " +
                                $"Contando las dos puntas, la plata de Wamani queda en {Money(d.Proyectado)}.")
                                .FontSize(9.5f).FontColor(Gris);
                        });

                        // ───── Pie ─────
                        c.Item().PaddingTop(20).BorderTop(1).BorderColor(Linea).PaddingTop(10).Column(p =>
                        {
                            if (d.FondoDolares != 0)
                                p.Item().Text($"Aparte, en el fondo de inversión hay US$ {d.FondoDolares.ToString("N2", ci)}. " +
                                              "No entra en este cierre: está en dólares y se maneja aparte.")
                                    .FontSize(8.5f).FontColor(Gris);
                            p.Item().PaddingTop(4).Text(
                                "Informe generado el " + Reloj.HoyJujuy().ToString("dd/MM/yyyy") + " · Wamani Turismo")
                                .FontSize(8).FontColor(Gris);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Titulo(ColumnDescriptor c, string texto)
        => c.Item().Text(texto).FontSize(13).Bold().FontColor(VerdeOscuro);

    // El encabezado de la tabla mes a mes.
    private static void Encabezado(TableDescriptor t, params string[] titulos)
    {
        for (int i = 0; i < titulos.Length; i++)
        {
            var celda = t.Cell().Background(VerdeOscuro).PaddingVertical(6).PaddingHorizontal(6);
            if (i == 0) celda.Text(titulos[i]).FontSize(8.5f).Bold().FontColor(Dorado);
            else celda.AlignRight().Text(titulos[i]).FontSize(8.5f).Bold().FontColor(Dorado);
        }
    }

    // Una fila de la tabla mes a mes. El mes del informe va resaltado para encontrarlo
    // de un saque entre los demás.
    private static void FilaMes(TableDescriptor t, string mes, string entro, string pagado,
        string empresa, string ganancia, string acumulado, bool destacado, bool enRojo,
        bool total = false)
    {
        var fondo = total ? "#EDE7D8" : (destacado ? "#E8F0E6" : Crema);
        var colorGan = enRojo ? Rojo : Verde;

        IContainer Celda(bool derecha)
        {
            var x = t.Cell().Background(fondo).PaddingVertical(6).PaddingHorizontal(6);
            x = total
                ? x.BorderTop(1).BorderColor(VerdeOscuro)
                : x.BorderBottom(1).BorderColor(Linea);
            return derecha ? x.AlignRight() : x;
        }

        // Bold() no acepta un booleano: cada caso va por separado.
        if (total || destacado)
        {
            Celda(false).Text(mes).FontSize(9).Bold();
            Celda(true).Text(entro).FontSize(9).Bold();
            Celda(true).Text(pagado).FontSize(9).Bold().FontColor(Rojo);
            Celda(true).Text(empresa).FontSize(9).Bold().FontColor(Rojo);
            Celda(true).Text(ganancia).FontSize(9).Bold().FontColor(colorGan);
            Celda(true).Text(acumulado).FontSize(9.5f).Bold().FontColor(colorGan);
        }
        else
        {
            Celda(false).Text(mes).FontSize(8.5f);
            Celda(true).Text(entro).FontSize(8.5f);
            Celda(true).Text(pagado).FontSize(8.5f).FontColor(Rojo);
            Celda(true).Text(empresa).FontSize(8.5f).FontColor(Rojo);
            Celda(true).Text(ganancia).FontSize(8.5f).FontColor(colorGan);
            Celda(true).Text(acumulado).FontSize(8.5f).Bold().FontColor(Tinta);
        }
    }

    // Un renglón de la tabla. Los totales van en negrita y con una línea arriba.
    private static void Renglon(TableDescriptor t, string concepto, string monto, bool total, string? color = null)
    {
        var celda1 = t.Cell().PaddingVertical(6);
        var celda2 = t.Cell().PaddingVertical(6).AlignRight();
        if (total)
        {
            celda1 = celda1.BorderTop(1).BorderColor(VerdeOscuro).PaddingTop(8);
            celda2 = celda2.BorderTop(1).BorderColor(VerdeOscuro).PaddingTop(8);
        }
        else
        {
            celda1 = celda1.BorderBottom(1).BorderColor(Linea);
            celda2 = celda2.BorderBottom(1).BorderColor(Linea);
        }

        // Bold() no acepta un booleano: hay que armar cada caso por separado.
        if (total)
        {
            celda1.Text(concepto).FontSize(11).Bold();
            celda2.Text(monto).FontSize(12).Bold().FontColor(color ?? Tinta);
        }
        else
        {
            celda1.Text(concepto).FontSize(10);
            celda2.Text(monto).FontSize(10).FontColor(color ?? Tinta);
        }
    }
}

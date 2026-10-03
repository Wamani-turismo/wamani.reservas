using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Wamani.Reservas.Services;

// CIERRE DE MES: el informe que se baja al terminar cada mes.
//
// Tiene dos partes que contestan preguntas distintas, y mezclarlas es el error que más
// caro sale:
//
//   1. LA PLATA QUE SE MOVIÓ este mes: lo que entró menos lo que salió. Es la caja.
//   2. LOS COMPROMISOS de hoy: lo que falta cobrar y lo que falta pagar de TODAS las
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
        // Lo que de verdad hay libre hoy: la caja menos todo lo que ya se debe.
        public decimal LibreHoy => CajaHoy - FaltaPagar;

        // --- El fondo, sólo como dato ---
        public decimal FondoDolares { get; set; }
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
                        if (File.Exists(rutaLogo))
                            fila.ConstantItem(90).AlignRight().AlignMiddle().Height(44).Image(rutaLogo).FitHeight();
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

                        // ───── 2. Lo que se llevaron los socios ─────
                        if (d.Retiros.Count > 0 || d.Aportes.Count > 0)
                        {
                            c.Item().PaddingTop(22);
                            Titulo(c, "2 · Lo que se llevaron los socios");
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

                        // ───── 3. Compromisos ─────
                        c.Item().PaddingTop(22);
                        Titulo(c, "3 · Compromisos — lo que falta cobrar y pagar");
                        c.Item().PaddingTop(4).Text(
                            "Esto NO es de este mes: es todo lo pendiente a hoy, de las salidas que ya pasaron y " +
                            "de las que vienen. Se cobra la seña y se paga la seña; el resto queda para después.")
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

                        // ───── 4. La advertencia ─────
                        c.Item().PaddingTop(18).Background(d.LibreHoy < 0 ? "#F7E7E2" : "#E8F0E6")
                            .Border(1).BorderColor(d.LibreHoy < 0 ? Rojo : Verde).Padding(14).Column(a =>
                        {
                            a.Item().Text("Cuánto se puede repartir de verdad")
                                .FontSize(11).Bold().FontColor(d.LibreHoy < 0 ? Rojo : Verde);
                            a.Item().PaddingTop(6).Text(
                                $"Caja de hoy {Money(d.CajaHoy)} − todo lo que ya se debe {Money(d.FaltaPagar)} = " +
                                $"{Money(d.LibreHoy)}")
                                .FontSize(10);
                            a.Item().PaddingTop(6).Text(
                                d.LibreHoy < 0
                                ? "La plata que hay HOY no alcanza para cubrir lo que ya se debe: el resto depende de " +
                                  "cobrar los saldos pendientes. No conviene repartir sobre la ganancia del mes."
                                : "Ésta es la plata libre aunque no se cobrara un peso más. Repartir por encima de " +
                                  "este número es usar plata que ya tiene dueño.")
                                .FontSize(9).FontColor(Gris);
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

        celda1.Text(concepto).FontSize(total ? 11 : 10).Bold(total);
        celda2.Text(monto).FontSize(total ? 12 : 10).Bold(total)
            .FontColor(color ?? Tinta);
    }
}

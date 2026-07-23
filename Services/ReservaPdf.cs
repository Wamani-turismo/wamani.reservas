using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Services;

// Comprobante de reserva para mandarle al cliente (por WhatsApp).
// Estilo Wamani: fondo crema, banda verde oscuro con el logo dorado y una
// silueta suave de montañas abajo.
public static class ReservaPdf
{
    // Colores de la marca
    private const string VerdeOscuro = "#22332F";
    private const string Crema = "#F5F2EA";
    private const string Dorado = "#D8C096";
    private const string Coral = "#D2673A";
    private const string Tinta = "#23332E";
    private const string Gris = "#7C8279";
    private const string Linea = "#E7E0D0";

    // Los encargados de Wamani
    private static readonly string[] Encargados = { "Lautaro", "Facundo", "Luciano" };

    // Datos de contacto (si están vacíos, no se muestran)
    private const string Telefono = "";
    private const string Instagram = "";

    public static byte[] Generar(Reserva r, List<Pasajero> pasajeros, Excursion? exc, string rutaLogo, string rutaMontanas)
    {
        var ci = CultureInfo.GetCultureInfo("es-AR");
        string Money(decimal m) => "$ " + m.ToString("N0", ci);

        var total = r.TotalConDescuento();
        var sena = r.SenaMonto ?? 0m;
        var saldo = r.Pendiente();
        var dias = r.DiasAvisoSaldo();               // 4 excursión / 7 travesía
        var fechaLimite = r.FechaAvisoSaldo();       // desde cuándo se cobra el saldo
        var esTravesia = r.EsTravesia;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.PageColor(Crema);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Tinta).FontFamily("Helvetica"));

                // ---------- Fondo: montañas suaves abajo ----------
                page.Background().AlignBottom().Element(e =>
                {
                    if (File.Exists(rutaMontanas)) e.Image(rutaMontanas).FitWidth();
                });

                // ---------- Encabezado: banda verde con el logo ----------
                page.Header().Background(VerdeOscuro).PaddingVertical(22).PaddingHorizontal(40).Row(row =>
                {
                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        if (File.Exists(rutaLogo))
                            c.Item().Width(150).Image(rutaLogo).FitWidth();
                        else
                            c.Item().Text("WAMANI").FontSize(22).Bold().FontColor(Dorado);
                    });
                    row.ConstantItem(200).AlignRight().AlignMiddle().Column(c =>
                    {
                        c.Item().AlignRight().Text("COMPROBANTE").FontSize(13).Bold().FontColor(Dorado).LetterSpacing(0.12f);
                        c.Item().AlignRight().Text("de reserva").FontSize(11).FontColor("#C9D1C4");
                    });
                });

                // ---------- Contenido ----------
                page.Content().PaddingHorizontal(40).PaddingTop(26).Column(col =>
                {
                    col.Spacing(18);

                    // --- Excursión y fechas ---
                    col.Item().Column(c =>
                    {
                        c.Item().Text(r.Excursion).FontSize(19).Bold().FontColor(VerdeOscuro);
                        var fechas = r.FechaDesde.Date == r.FechaHasta.Date
                            ? r.FechaDesde.ToString("dddd d 'de' MMMM 'de' yyyy", ci)
                            : $"{r.FechaDesde:dd/MM/yyyy} al {r.FechaHasta:dd/MM/yyyy}";
                        c.Item().PaddingTop(2).Text(Mayus(fechas)).FontSize(11).FontColor(Gris);
                        c.Item().PaddingTop(6).Text($"A nombre de {r.NombreCliente}  ·  {r.CantidadPersonas} persona(s)")
                            .FontSize(11).FontColor(Tinta);
                    });

                    // --- Plata ---
                    col.Item().Background("#FFFDF7").Border(1).BorderColor(Linea).Padding(20).Column(c =>
                    {
                        c.Item().Text("Detalle del pago").FontSize(12).Bold().FontColor(VerdeOscuro);
                        c.Item().PaddingTop(12).Row(row =>
                        {
                            row.RelativeItem().Column(x =>
                            {
                                x.Item().Text("Total").FontSize(9).FontColor(Gris);
                                x.Item().Text(Money(total)).FontSize(16).Bold().FontColor(Tinta);
                            });
                            row.RelativeItem().Column(x =>
                            {
                                x.Item().Text("Seña abonada").FontSize(9).FontColor(Gris);
                                x.Item().Text(Money(sena)).FontSize(16).Bold().FontColor("#4A7D6B");
                            });
                            row.RelativeItem().Column(x =>
                            {
                                x.Item().Text("Resta abonar").FontSize(9).FontColor(Gris);
                                x.Item().Text(Money(saldo)).FontSize(16).Bold().FontColor(saldo > 0 ? Coral : "#4A7D6B");
                            });
                        });

                        if (saldo > 0)
                        {
                            c.Item().PaddingTop(14).Background("#F8E8DF").Padding(12).Text(t =>
                            {
                                t.Span("El saldo se abona ").FontColor(Tinta);
                                t.Span($"{dias} días antes").Bold().FontColor(Coral);
                                t.Span(esTravesia ? " del inicio (por ser una travesía de varios días)" : " del día de la salida");
                                t.Span($", es decir a partir del {fechaLimite:dd/MM/yyyy}.").FontColor(Tinta);
                            });
                        }
                        else
                        {
                            c.Item().PaddingTop(14).Background("#E8F1EC").Padding(12)
                                .Text("¡Reserva paga por completo! No queda saldo pendiente.").FontColor("#4A7D6B").Bold();
                        }
                    });

                    // --- Pasajeros ---
                    if (pasajeros.Count > 0)
                    {
                        col.Item().Column(c =>
                        {
                            c.Item().Text("Pasajeros").FontSize(12).Bold().FontColor(VerdeOscuro);
                            c.Item().PaddingTop(8).Table(tabla =>
                            {
                                tabla.ColumnsDefinition(cd => { cd.ConstantColumn(26); cd.RelativeColumn(); cd.ConstantColumn(110); });
                                foreach (var (p, i) in pasajeros.Select((p, i) => (p, i)))
                                {
                                    var fondo = i % 2 == 0 ? "#FFFDF7" : Crema;
                                    tabla.Cell().Background(fondo).Padding(7).Text($"{i + 1}").FontColor(Gris);
                                    tabla.Cell().Background(fondo).Padding(7).Text(p.NombreCompleto);
                                    tabla.Cell().Background(fondo).Padding(7).Text(string.IsNullOrWhiteSpace(p.Dni) ? "" : $"DNI {p.Dni}").FontColor(Gris);
                                }
                            });
                        });
                    }

                    // --- Breve guía de la excursión ---
                    var guia = (exc?.GuiaBreve ?? "").Trim();
                    var lugares = (exc?.LugaresVisitar ?? "").Trim();
                    if (guia.Length > 0 || lugares.Length > 0)
                    {
                        col.Item().Column(c =>
                        {
                            c.Item().Text("Sobre la salida").FontSize(12).Bold().FontColor(VerdeOscuro);
                            if (guia.Length > 0)
                                c.Item().PaddingTop(6).Text(guia).FontSize(10).FontColor(Tinta).LineHeight(1.4f);
                            if (lugares.Length > 0)
                            {
                                c.Item().PaddingTop(8).Text("Lugares que visitamos").FontSize(10).Bold().FontColor(Coral);
                                c.Item().PaddingTop(2).Text(lugares).FontSize(10).FontColor(Tinta).LineHeight(1.4f);
                            }
                        });
                    }
                });

                // ---------- Pie: encargados y contacto ----------
                page.Footer().PaddingHorizontal(40).PaddingBottom(24).Column(col =>
                {
                    col.Item().PaddingTop(10).BorderTop(1).BorderColor(Linea).PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Te acompañan").FontSize(9).FontColor(Gris);
                            c.Item().Text(string.Join("  ·  ", Encargados)).FontSize(11).Bold().FontColor(VerdeOscuro);
                        });
                        row.ConstantItem(220).AlignRight().Column(c =>
                        {
                            if (!string.IsNullOrWhiteSpace(Telefono))
                                c.Item().AlignRight().Text(Telefono).FontSize(10).FontColor(Tinta);
                            if (!string.IsNullOrWhiteSpace(Instagram))
                                c.Item().AlignRight().Text(Instagram).FontSize(10).FontColor(Coral);
                            c.Item().AlignRight().Text("Wamani · Jujuy").FontSize(9).FontColor(Gris);
                        });
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static string Mayus(string t)
        => string.IsNullOrEmpty(t) ? t : char.ToUpper(t[0]) + t[1..];
}

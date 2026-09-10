using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Wamani.Reservas.Models;

namespace Wamani.Reservas.Services;

// Arma el PDF con los datos de los pasajeros para mandarle al SEGURO.
// Estilo limpio tipo "factura": barra de color, datos arriba y tabla de pasajeros.
public static class SeguroPdf
{
    // Colores del sistema (mate)
    private const string VerdeOscuro = "#23413A";
    private const string Naranja = "#C67C48";
    private const string Hueso = "#F6F4EF";
    private const string Linea = "#EAE7DF";
    private const string Gris = "#7A837D";

    public static byte[] Generar(Reserva reserva, List<Pasajero> pasajeros)
    {
        var ci = CultureInfo.GetCultureInfo("es-AR");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken4).FontFamily("Helvetica"));

                // ---------- Encabezado ----------
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("SEGURO").FontSize(26).Bold().FontColor(VerdeOscuro).LetterSpacing(0.1f);
                            c.Item().Text("Datos de pasajeros").FontSize(11).FontColor(Gris);
                        });
                        row.ConstantItem(150).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Width(90).Height(10).Background(Naranja);
                            c.Item().PaddingTop(6).AlignRight().Text("WAMANI").FontSize(12).Bold().FontColor(VerdeOscuro);
                            c.Item().AlignRight().Text("Excursiones · Jujuy").FontSize(9).FontColor(Gris);
                        });
                    });
                    col.Item().PaddingTop(14).LineHorizontal(1).LineColor(Linea);
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    // ---------- Datos de la reserva ----------
                    col.Item().Row(row =>
                    {
                        void Dato(RowDescriptor r, string titulo, string valor)
                        {
                            r.RelativeItem().BorderLeft(2).BorderColor(Naranja).PaddingLeft(8).Column(c =>
                            {
                                c.Item().Text(titulo).FontSize(8).FontColor(Gris);
                                c.Item().Text(valor).FontSize(10).Bold();
                            });
                        }
                        Dato(row, "EXCURSIÓN / TRAVESÍA", reserva.Excursion);
                        row.ConstantItem(14);
                        Dato(row, "FECHAS", $"{reserva.FechaDesde:dd/MM/yyyy} al {reserva.FechaHasta:dd/MM/yyyy}");
                        row.ConstantItem(14);
                        Dato(row, "CANTIDAD DE PERSONAS", reserva.CantidadPersonas.ToString());
                        row.ConstantItem(14);
                        Dato(row, "TITULAR", reserva.NombreCliente);
                    });

                    // ---------- Tabla de pasajeros ----------
                    col.Item().PaddingTop(22).Text("Pasajeros").FontSize(13).Bold().FontColor(VerdeOscuro);

                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(22);   // #
                            c.RelativeColumn(3);    // nombre
                            c.RelativeColumn(1.6f); // dni
                            c.RelativeColumn(1.7f); // fecha nac
                            c.RelativeColumn(1.8f); // tel
                            c.RelativeColumn(2.6f); // mail
                        });

                        table.Header(header =>
                        {
                            void Th(string t) => header.Cell().Background(Naranja).Padding(6)
                                .Text(t).FontSize(8).Bold().FontColor(Colors.White);
                            Th("N°"); Th("NOMBRE COMPLETO"); Th("DNI"); Th("FECHA NAC."); Th("TELÉFONO"); Th("MAIL");
                        });

                        int i = 1;
                        foreach (var p in pasajeros)
                        {
                            var fondo = i % 2 == 0 ? Hueso : "#FFFFFF";
                            void Td(string t) => table.Cell().Background(fondo).BorderBottom(1).BorderColor(Linea)
                                .Padding(6).Text(t).FontSize(9);

                            Td(i.ToString());
                            Td(p.NombreCompleto);
                            Td(string.IsNullOrWhiteSpace(p.Dni) ? "—" : p.Dni!);
                            Td(p.FechaNacimiento is null ? "—" : p.FechaNacimiento.Value.ToString("dd/MM/yyyy"));
                            Td(string.IsNullOrWhiteSpace(p.Telefono) ? "—" : p.Telefono!);
                            Td(string.IsNullOrWhiteSpace(p.Email) ? "—" : p.Email!);
                            i++;
                        }
                    });

                    if (pasajeros.Count == 0)
                    {
                        col.Item().PaddingTop(10).Text("No hay pasajeros cargados en esta reserva.")
                            .FontSize(9).FontColor(Gris);
                    }

                    // ---------- Nota ----------
                    col.Item().PaddingTop(26).Background(Hueso).Padding(12).Column(c =>
                    {
                        c.Item().Text("Nota").FontSize(9).Bold().FontColor(VerdeOscuro);
                        c.Item().PaddingTop(3).Text(
                            $"Total de pasajeros a asegurar: {pasajeros.Count}. " +
                            $"Excursión: {reserva.Excursion}. " +
                            $"Salida: {reserva.FechaDesde:dd/MM/yyyy}.")
                            .FontSize(9).FontColor(Gris);
                    });
                });

                // ---------- Pie ----------
                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Linea);
                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text($"Wamani · Excursiones y travesías · Jujuy")
                            .FontSize(8).FontColor(Gris);
                        row.RelativeItem().AlignRight().Text($"Emitido el {DateTime.Today.ToString("dd/MM/yyyy", ci)}")
                            .FontSize(8).FontColor(Gris);
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}

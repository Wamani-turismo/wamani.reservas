using System;
using System.ComponentModel.DataAnnotations;

namespace Wamani.Reservas.Models
{
    // Una reserva de excursión de la agencia Wamani (Jujuy).
    // Todos los montos están en PESOS ARGENTINOS.
    public class Reserva
    {
        public int Id { get; set; }

        // ---------- Excursión elegida ----------
        // Referencia al catálogo (para elegir de la lista)
        [Display(Name = "Excursión")]
        public int? ExcursionId { get; set; }

        // Nombre de la excursión guardado en el momento de la reserva (por si después
        // se cambia el precio o el nombre en el catálogo, esta reserva no se altera).
        [MaxLength(120)]
        public string Excursion { get; set; } = "";

        // Precio por persona congelado al momento de cargar la reserva
        [Range(0, 999999999)]
        [Display(Name = "Precio por persona")]
        public decimal PrecioPorPersona { get; set; }

        // Si es true, el precio por persona lo puso el operador A MANO (ej: 1 sola
        // persona que paga tarifa especial). Si es false, sale del catálogo de la excursión.
        [Display(Name = "Precio manual")]
        public bool PrecioManual { get; set; } = false;

        // Mínimo de personas congelado (para el aviso)
        public int MinimoPersonas { get; set; } = 2;

        // Copiado de la excursión: si es travesía, el saldo se cobra 7 días antes; si no, 4.
        public bool EsTravesia { get; set; } = false;

        // ---------- Datos del pasajero ----------
        [Required(ErrorMessage = "Poné el nombre de la persona")]
        [MaxLength(160)]
        [Display(Name = "Nombre del pasajero")]
        public string NombreCliente { get; set; } = "";

        [Range(1, 200, ErrorMessage = "Tiene que ser al menos 1 persona")]
        [Display(Name = "Cantidad de personas")]
        public int CantidadPersonas { get; set; } = 1;

        [MaxLength(40)]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [MaxLength(160)]
        [EmailAddress(ErrorMessage = "Revisá el mail, no parece válido")]
        [Display(Name = "Mail")]
        public string? Email { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha desde")]
        public DateTime FechaDesde { get; set; } = DateTime.Today;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha hasta")]
        public DateTime FechaHasta { get; set; } = DateTime.Today;

        // ---------- Plata ----------
        [Range(0, 100, ErrorMessage = "El descuento va de 0 a 100")]
        [Display(Name = "Descuento (%)")]
        public decimal DescuentoPct { get; set; } = 0m;

        // ---------- Seña ----------
        [Range(0, 999999999)]
        [Display(Name = "Seña - monto")]
        public decimal? SenaMonto { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Seña - día")]
        public DateTime? SenaFecha { get; set; }

        [MaxLength(80)]
        [Display(Name = "Seña - quién la recibió")]
        public string? SenaRecibioPor { get; set; }

        public string? SenaComprobante { get; set; }

        // ---------- Saldo ----------
        [Range(0, 999999999)]
        [Display(Name = "Saldo - monto")]
        public decimal? SaldoMonto { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Saldo - día")]
        public DateTime? SaldoFecha { get; set; }

        [MaxLength(80)]
        [Display(Name = "Saldo - quién lo recibió")]
        public string? SaldoRecibioPor { get; set; }

        public string? SaldoComprobante { get; set; }

        // ---------- Estado ----------
        // Si es NULL => el estado se calcula solo por los pagos.
        // Si tiene un valor ("Pendiente"/"Señado"/"Pagado") => lo forzó el usuario a mano.
        [MaxLength(20)]
        public string? EstadoManual { get; set; }

        // Cuándo se cargó la reserva
        public DateTime CreadaEl { get; set; } = DateTime.Now;

        // ---------- Cálculos automáticos ----------

        // Precio × personas (antes del descuento)
        public decimal Subtotal()
            => Math.Round(PrecioPorPersona * CantidadPersonas, 2);

        // Total ya con el descuento aplicado
        public decimal TotalConDescuento()
            => Math.Round(Subtotal() * (1 - DescuentoPct / 100m), 2);

        // Cuánto se cobró (seña + saldo)
        public decimal Cobrado()
            => (SenaMonto ?? 0m) + (SaldoMonto ?? 0m);

        // Cuánto falta cobrar
        public decimal Pendiente()
        {
            var falta = TotalConDescuento() - Cobrado();
            return falta <= 0.01m ? 0m : Math.Round(falta, 2);
        }

        public bool EstaPaga() => Pendiente() == 0m;

        // Estado sugerido según los pagos
        public string EstadoCalculado()
        {
            if (Cobrado() <= 0m) return "Pendiente";
            if (Pendiente() == 0m) return "Pagado";
            return "Señado";
        }

        // Estado que se muestra (manual si lo forzaron, si no el calculado)
        public string EstadoActual()
            => string.IsNullOrEmpty(EstadoManual) ? EstadoCalculado() : EstadoManual;

        // Cuántos pasajeros faltan para llegar al mínimo de esta salida (0 si ya llega)
        public int FaltanParaMinimo()
            => Math.Max(0, MinimoPersonas - CantidadPersonas);

        public bool LlegaAlMinimo() => FaltanParaMinimo() == 0;

        // ---------- Cobro del saldo ----------
        // Días antes del inicio en que hay que cobrar el saldo (4 excursión / 7 travesía)
        public int DiasAvisoSaldo() => EsTravesia ? 7 : 4;

        // Fecha a partir de la cual hay que empezar a cobrar el saldo
        public DateTime FechaAvisoSaldo() => FechaDesde.Date.AddDays(-DiasAvisoSaldo());

        // ¿Hay que cobrarle el saldo AHORA? (queda saldo, la salida no pasó y ya entró
        // en la ventana de cobro según sea excursión o travesía)
        public bool HayQueCobrarSaldo(DateTime hoy)
            => Pendiente() > 0m
               && FechaDesde.Date >= hoy.Date
               && hoy.Date >= FechaAvisoSaldo();
    }
}

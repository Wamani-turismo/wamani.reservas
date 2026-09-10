using System;
using System.Collections.Generic;
using System.Linq;

namespace Wamani.Reservas.Models
{
    // Estado a nivel SALIDA (excursión + fecha): si ya se pagaron los servicios/gastos
    // y el comprobante de ese pago.
    public class OperativoSalida
    {
        public int Id { get; set; }
        public int ExcursionId { get; set; }
        public DateTime Fecha { get; set; }
        public bool ServiciosPagados { get; set; }
        public string? Comprobante { get; set; }

        // Los ítems de la plantilla que se BORRARON a mano en esta salida.
        //
        // El operativo copia de la excursión lo que falta cada vez que se abre, así que un
        // ítem borrado volvía a aparecer solo y no había forma de sacarlo ("esta vez no
        // llevamos snacks"). Acá quedan anotados los nombres para no volver a copiarlos.
        //
        // Van separados por "|" y en minúscula, que es como se comparan los nombres en todo
        // el operativo. Si el ítem se vuelve a agregar a mano, se saca de la lista.
        public string? ItemsBorrados { get; set; }

        private const char Separador = '|';

        public List<string> BorradosLista() =>
            string.IsNullOrWhiteSpace(ItemsBorrados)
                ? new List<string>()
                : ItemsBorrados.Split(Separador, StringSplitOptions.RemoveEmptyEntries).ToList();

        public void GuardarBorrados(IEnumerable<string> nombres) =>
            ItemsBorrados = string.Join(Separador, nombres.Distinct());
    }
}

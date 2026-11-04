using System;

namespace DAL.DTOs
{
    public class RelacionPuntosDTO
    {
        public int Id { get; set; }
        public decimal Monto { get; set; }
        public long Puntos { get; set; }
        public DateTime Fecha { get; set; }
        public bool Activo { get; set; }
        public string Imagen { get; set; }
    }

    public class RelacionPuntosListadoDTO
    {
        public int TotalRegistros { get; set; }
        public int PaginaActual { get; set; }
        public int CantidadPorPagina { get; set; }
        public int TotalPaginas { get; set; }
        public object Registros { get; set; }
    }
}
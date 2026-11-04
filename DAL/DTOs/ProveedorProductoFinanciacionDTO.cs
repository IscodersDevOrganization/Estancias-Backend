using System.Collections.Generic;

namespace DAL.DTOs
{
    public class ProveedorProductoFinanciacionCreateDTO
    {
        public int? CantidadCuotasAdd { get; set; }
        // Se conserva la escritura original del DTO del MVC: InteresCoutasAdd.
        public decimal? InteresCoutasAdd { get; set; }
    }

    public class ProveedorProductoFinanciacionResponseDTO
    {
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; }
        public decimal PrecioProducto { get; set; }
        public int? CantidadCuotasAdd { get; set; }
        public decimal? InteresCoutasAdd { get; set; }
        public List<FinanciacionDTO> ListaFinanciacion { get; set; } = new List<FinanciacionDTO>();
    }
}

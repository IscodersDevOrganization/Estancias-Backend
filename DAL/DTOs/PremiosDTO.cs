using System;
using System.Collections.Generic;
using DAL.Models;

namespace DAL.DTOs
{
    public class PremioDTO
    {
        public int Id { get; set; }

        public string Nombre { get; set; }

        public string Descripcion { get; set; }

        public string TerminosCondiciones { get; set; }

        public int Stock { get; set; }

        public int StockActual { get; set; }

        public long Puntos { get; set; }

        public DateTime Fecha { get; set; }

        public bool Activo { get; set; }

        public int CategoriaId { get; set; }

        public string CategoriaNombre { get; set; }

        public DateTime FechaVencimiento { get; set; }

        public int DiasDeVencimiento { get; set; }

        public Marcas Marca { get; set; }
    }

    public class PremioCreateDTO
    {
        public string Nombre { get; set; }

        public string Descripcion { get; set; }

        public string TerminosCondiciones { get; set; }

        public int Stock { get; set; }

        public long Puntos { get; set; }

        public int CategoriaId { get; set; }

        public int MarcaId { get; set; }

        public DateTime FechaVencimiento { get; set; }

        public int DiasDeVencimiento { get; set; }
    }

    public class PremioUpdateDTO
    {
        public string Nombre { get; set; }

        public string Descripcion { get; set; }

        public string TerminosCondiciones { get; set; }

        public int Stock { get; set; }

        public int StockActual { get; set; }

        public long Puntos { get; set; }

        public int CategoriaId { get; set; }

        public int MarcaId { get; set; }

        public DateTime FechaVencimiento { get; set; }

        public int DiasDeVencimiento { get; set; }
    }

    public class PremiosResponseDTO
    {
        public bool Success { get; set; }

        public string Mensaje { get; set; }

        public List<PremioDTO> Premios { get; set; }
    }

    public class PremioResponseDTO
    {
        public bool Success { get; set; }

        public string Mensaje { get; set; }

        public PremioDTO Premio { get; set; }
    }

    public class PremioImagenResponseDTO
    {
        public bool Success { get; set; }

        public string Mensaje { get; set; }

        public PremiosImagenDTO Imagenes { get; set; }
    }
}

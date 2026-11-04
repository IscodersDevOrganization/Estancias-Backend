using System;

namespace DAL.DTOs
{
    public class BannerCrearDTO
    {
        public string Titulo { get; set; }
        public string Subtitulo { get; set; }
        public string Texto { get; set; }
        public string TextoBoton { get; set; }

        public DateTime? Fecha { get; set; }
        public bool Publico { get; set; }
        public string Link { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        public bool LinkExterno { get; set; }
        public bool EsCarrousel { get; set; }
        public int Orden { get; set; }

        public int BannersFijo { get; set; }
        public string TieneFechaVencimiento { get; set; }
        public int? MarcasId { get; set; }
    }

    public class BannerEditarDTO
    {
        public int Id { get; set; }

        public string Titulo { get; set; }
        public string Subtitulo { get; set; }
        public string Texto { get; set; }
        public string TextoBoton { get; set; }

        public DateTime? Fecha { get; set; }
        public bool Publico { get; set; }
        public string Link { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        public bool LinkExterno { get; set; }
        public bool EsCarrousel { get; set; }
        public int Orden { get; set; }

        public int BannerFijo { get; set; }
        public string TieneFechaVencimiento { get; set; }
        public int? MarcasId { get; set; }
    }

    public class BannerOrdenDTO
    {
        public int Id { get; set; }
        public int Orden { get; set; }
    }

    public class BannerListadoDTO
    {
        public int Id { get; set; }

        public string Titulo { get; set; }
        public string Subtitulo { get; set; }
        public string Texto { get; set; }
        public string TextoBoton { get; set; }

        public DateTime? Fecha { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        public bool Publico { get; set; }
        public string Link { get; set; }
        public bool LinkExterno { get; set; }
        public bool EsCarrousel { get; set; }

        public bool BannerFijo { get; set; }
        public bool Vencimiento { get; set; }

        public bool EsVideo { get; set; }
        public string Video { get; set; }
        public string Foto { get; set; }

        public int Orden { get; set; }

        public MarcaBannerDTO Marca { get; set; }
    }

    public class MarcaBannerDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
    }

    public class BannerResponseDTO
    {
        public int Status { get; set; }
        public string Mensaje { get; set; }
    }

    public class BannerItemResponseDTO : BannerResponseDTO
    {
        public BannerListadoDTO Banner { get; set; }
    }

    public class BannerListadoResponseDTO : BannerResponseDTO
    {
        public BannerListadoDataDTO Data { get; set; }
    }

    public class BannerListadoDataDTO
    {
        public int TotalRegistros { get; set; }
        public int PaginaActual { get; set; }
        public int CantidadPorPagina { get; set; }
        public int TotalPaginas { get; set; }
        public object Banners { get; set; }
    }
}

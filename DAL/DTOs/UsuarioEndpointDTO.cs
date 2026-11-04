using System;
using System.Collections.Generic;

namespace DAL.DTOs
{
    public class UsuarioEndpointListadoDTO
    {
        public string Id { get; set; }
        public string Usuario { get; set; }
        public string NroDocumento { get; set; }
        public string NroTarjeta { get; set; }
        public string Nombre { get; set; }
        public string Empresa { get; set; }
        public bool Administrador { get; set; }
        public string AdministradorTexto { get; set; }
        public string Categoria { get; set; }
        public string CodigoColor { get; set; }
        public bool Incobrable { get; set; }
        public string TipoUsuario { get; set; }
    }

    public class UsuarioEndpointListadoResponseDTO
    {
        public List<UsuarioEndpointListadoDTO> Items { get; set; }
        public int TotalRegistros { get; set; }
        public int PaginaActual { get; set; }
        public int CantidadPorPagina { get; set; }
        public int TotalPaginas { get; set; }
        public string Buscar { get; set; }
    }

    public class UsuarioEndpointPersonaDTO
    {
        public int Id { get; set; }
        public int TipoDocumentoId { get; set; }
        public string TipoDocumentoDescripcion { get; set; }
        public string NroDocumento { get; set; }
        public string Apellido { get; set; }
        public string Nombres { get; set; }
        public string Cuil { get; set; }
        public int PaisId { get; set; }
        public string PaisNombre { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public bool TieneFechaNacimiento { get; set; }
        public string NroTarjeta { get; set; }
        public string Email { get; set; }
    }

    public class UsuarioEndpointDTO
    {
        public string UserId { get; set; }
        public string Mail { get; set; }
        public string Password { get; set; }
        public bool Administrador { get; set; }
        public string TarjetaEstancia { get; set; }
        public UsuarioEndpointPersonaDTO Persona { get; set; }
    }

    public class UsuarioEstanciaEndpointDTO
    {
        public string NroDocumento { get; set; }
        public string Mail { get; set; }
        public string Password { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Telefono { get; set; }
        public string TarjetaEstancia { get; set; }
        public bool Error { get; set; }
        public string Mensaje { get; set; }
    }

    public class UsuarioBuscarLoanRequestDTO
    {
        public string NumeroTarjeta { get; set; }
    }

    public class UsuarioBuscarRequestDTO
    {
        public string Valor { get; set; }
        public string TipoDeBusqueda { get; set; }
    }

    public class UsuarioBorrarRequestDTO
    {
        public string Valor { get; set; }
        public string TipoDeBusqueda { get; set; }
    }

    public class UsuarioPasswordRequestDTO
    {
        public string UserId { get; set; }
        public string Password { get; set; }
        public string RepeatPassword { get; set; }
    }

    public class UsuarioCategoriaRequestDTO
    {
        public string UserId { get; set; }
        public int CategoryId { get; set; }
    }

    public class UsuarioCategoriaDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public bool Selected { get; set; }
    }

    public class UsuarioEndpointResponseDTO
    {
        public bool Success { get; set; }
        public bool Respuesta { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}

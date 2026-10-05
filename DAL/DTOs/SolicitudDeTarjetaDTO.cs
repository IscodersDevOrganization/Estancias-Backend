using DAL.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace DAL.DTOs
{
    public class SolicitudDeTarjetaDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El campo Nombre es obligatorio.")]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El campo Apellido es obligatorio.")]
        [Display(Name = "Apellido")]
        public string Apellido { get; set; }

        [Required(ErrorMessage = "El campo DNI es obligatorio.")]
        [Display(Name = "DNI")]
        public string DNI { get; set; }

        [Display(Name = "Teléfono")]
        public string Telefono { get; set; }

        [Required(ErrorMessage = "El campo Email es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "El campo Fecha de Nacimiento es obligatorio.")]
        [Display(Name = "Fecha de Nacimiento")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime FechaNacimiento { get; set; } = DateTime.Today.AddYears(-18);

        [Display(Name = "Domicilio")]
        public string Domicilio { get; set; }

        [Display(Name = "Fecha de Solicitud")]
        public DateTime? FechaSolicitud { get; set; }

        [Display(Name = "Estado ID")]
        public int EstadoId { get; set; } = 1;

        [Display(Name = "Estado")]
        public string Estado { get; set; }

        [Display(Name = "Número de Tarjeta")]
        public string NumeroTarjeta { get; set; }

        [Display(Name = "Fecha Respuesta")]
        public DateTime? FechaRespuesta { get; set; }

        [Display(Name = "Calle")]
        public string Calle { get; set; }

        [Display(Name = "Altura")]
        public string Altura { get; set; }

        [Display(Name = "Piso / Depto")]
        public string PisoDepto { get; set; }

        [Display(Name = "Localidad")]
        public virtual Localidad Localidad { get; set; }

        [Display(Name = "Provincia")]
        public virtual Provincia Provincia { get; set; }

        [Display(Name = "Código Postal")]
        public string CodigoPostal { get; set; }
    }

    public class AprobarSolicitudDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El número de tarjeta es obligatorio.")]
        [Display(Name = "Número de Tarjeta")]
        public string NumeroTarjeta { get; set; }
    }

    public class AdjuntosSolicitudDTO
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; }
        public string DNI { get; set; }
        public string FrenteDNIBase64 { get; set; }
        public string DorsoDNIBase64 { get; set; }
        public string SelfieBase64 { get; set; }
    }
}

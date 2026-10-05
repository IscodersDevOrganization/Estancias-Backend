using System;

namespace DAL.Models
{
    public class SolicitudDeTarjeta
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string DNI { get; set; }
        public string Telefono { get; set; }
        public string NumeroTarjeta { get; set; }
        public string Email { get; set; }
        public byte[] FrenteDNI { get; set; }
        public byte[] DorsoDNI { get; set; }
        public byte[] Selfie { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string Calle { get; set; }
        public string Altura { get; set; }
        public string PisoDepto { get; set; }
        public virtual Localidad Localidad { get; set; }
        public virtual Provincia Provincia { get; set; }
        public string CodigoPostal { get; set; }
        public DateTime FechaSolicitud { get; set; } = DateTime.Now;
        public DateTime FechaDeRechazoAprobacion { get; set; } = DateTime.Now;
        public virtual EstadoSolicitudDeTarjeta Estado{ get; set; }
    }

    public class EstadoSolicitudDeTarjeta
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public bool Activo { get; set; } = true;
    }
}

using System;

namespace DAL.Models
{
    public class Morosos
    {
        public int Id { get; set; }
        public string DNI { get; set; }
        public string NombreCompleto { get; set; }
        public DateTime FechaCarga { get; set; } = DateTime.Now;
    }

    public class HistoricoDeCargaMorosos
    {
        public int Id { get; set; }
        public int RegistrosTotales { get; set; }
        public int Cargados { get; set; }
        public int Fallidos { get; set; }
        public int Omitidos { get; set; }
        public string Observacion { get; set; }
        public virtual Usuario Usuario { get; set; }
        public DateTime FechaCarga { get; set; } = DateTime.Now;
    }
}

using Commons.Identity;
using DAL.Models.Core;
using System.Collections.Generic;

namespace DAL.Models
{
    public class PreRegistroCategorias
    {
        public int Id { get; set; }
        public string DNI { get; set; }
        public string NombreCompleto { get; set; }
        public int? CategoriaId { get; set; }
        public virtual UsuariosCategorias Categoria { get; set; }
    }

}
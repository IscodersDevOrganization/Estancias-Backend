namespace DAL.DTOs
{
    public class UsuariosCategoriasDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Codigo { get; set; }
        public string Color { get; set; }
        public string CodigoColor { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; }
        public bool TieneImagenTarjeta { get; set; }
    }

    public class UsuarioCategoriaCreateRequest
    {
        public string Nombre { get; set; }
        public string Codigo { get; set; }
        public string Color { get; set; }
        public string CodigoColor { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; }
    }

    public class UsuarioCategoriaUpdateRequest
    {
        public string Nombre { get; set; }
        public string Codigo { get; set; }
        public string Color { get; set; }
        public string CodigoColor { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; }
    }

    public class UsuarioCategoriaOrdenRequest
    {
        public int Orden { get; set; }
    }
}
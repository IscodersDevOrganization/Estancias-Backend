namespace EstanciasCore.Areas.Administracion.ViewModels
{
    public class UserDTViewModel
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
        public string IncobrableText { get; set; }
    }
}

namespace GoJob.Domain
{
    public class Administrador : Usuario
    {
        public Administrador()
        {
            TipoUsuario = "Administrador";
        }

        public string NivelAcesso { get; set; } = "Total";
    }
}

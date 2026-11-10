namespace GoJob.Domain
{
    public class Administrador : Usuario
    {
        public Administrador()
        {
            TipoUsuario = "Administrador";
        }

        public string NivelAcesso { get; set; } = "Total";

        // Implementação polimórfica obrigatória do método abstrato da classe Usuario
        public override string ObterResumoPerfil()
        {
            return $"Administrador: {Nome} | Nível: {NivelAcesso}";
        }
    }
}
namespace GoJob.Domain
{
    public class Cliente:Usuario
    {
        public Cliente()
        {
            TipoUsuario = "Cliente";
        }

        // Lista de avaliações que este cliente fez para profissionais
        public ICollection<Avaliacao> AvaliacoesRealizadas { get; set; } = new List<Avaliacao>();
    }
}

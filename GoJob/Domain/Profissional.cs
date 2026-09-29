namespace GoJob.Domain
{
    public class Profissional : Usuario
    {
        public Profissional()
        {
            TipoUsuario = "Profissional";
        }

        public float ReputacaoMedia { get; set; } = 0;
        public string DescricaoServico { get; set; } = string.Empty;

        // Relacionamentos
        public ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();
        public ICollection<Avaliacao> AvaliacoesRecebidas { get; set; } = new List<Avaliacao>();

        public void RecalcularReputacao()
        {
            if (AvaliacoesRecebidas == null || !AvaliacoesRecebidas.Any())
            {
                ReputacaoMedia = 0;
                return;
            }

            ReputacaoMedia = (float)AvaliacoesRecebidas.Average(a => a.Nota);
        }
    }
}

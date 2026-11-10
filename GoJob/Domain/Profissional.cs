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

        // Encapsulamento: o setter privado impede a manipulação direta da lista fora da classe
        public ICollection<Categoria> Categorias { get; private set; } = new List<Categoria>();
        public ICollection<Avaliacao> AvaliacoesRecebidas { get; set; } = new List<Avaliacao>();

        // Método de negócio para encapsular a validação do limite de 3 categorias (RN05)
        public bool AdicionarCategoria(Categoria categoria)
        {
            if (Categorias.Count >= 3)
            {
                return false; // Ou lançar uma InvalidOperationException se preferires tratar como exceção
            }

            if (!Categorias.Contains(categoria))
            {
                Categorias.Add(categoria);
            }

            return true;
        }

        public void RecalcularReputacao()
        {
            if (AvaliacoesRecebidas == null || !AvaliacoesRecebidas.Any())
            {
                ReputacaoMedia = 0;
                return;
            }

            ReputacaoMedia = (float)AvaliacoesRecebidas.Average(a => a.Nota);
        }

        // Implementação polimórfica do método abstrato da classe base Usuario
        public override string ObterResumoPerfil()
        {
            return $"Profissional: {Nome} | Nota: {ReputacaoMedia:F1} | Categorias: {Categorias.Count}";
        }
    }
}
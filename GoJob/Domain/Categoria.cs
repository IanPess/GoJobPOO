namespace GoJob.Domain
{
    public class Categoria
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public bool Ativa { get; set; } = true;

        // Chave opcional para alinhar com a coluna existente no Supabase
        public int? ProfissionalId { get; set; }

        public void Ativar() => Ativa = true;
        public void Inativar() => Ativa = false;
        public bool EstaAtiva() => Ativa;
    }
}
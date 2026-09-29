namespace GoJob.Domain
{
    public class Avaliacao
    {
        public int Id { get; set; }
        public int Nota { get; set; } // Nota de 1 a 5
        public string Comentario { get; set; } = string.Empty;
        public DateTime DataHora { get; set; } = DateTime.UtcNow;

        // Relacionamento com o Cliente que fez a avaliação
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }

        // Relacionamento com o Profissional que recebeu a avaliação
        public int ProfissionalId { get; set; }
        public Profissional? Profissional { get; set; }

        public bool ValidarNota()
        {
            return Nota >= 1 && Nota <= 5;
        }
    }
}

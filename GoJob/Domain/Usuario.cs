namespace GoJob.Domain
{
    public abstract class Usuario
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SenhaHash { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string TipoUsuario { get; set; } = string.Empty; // "Cliente", "Profissional" ou "Administrador"
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public int? EnderecoId { get; set; }
        public Endereco? Endereco { get; set; }

        public bool ValidarLogin(string emailDigitado, string senhaHashDigitada)
        {
            return Email.Equals(emailDigitado, StringComparison.OrdinalIgnoreCase) &&
                   SenhaHash == senhaHashDigitada;
        }

        // Método abstrato exigido para garantir Abstração e Polimorfismo
        public abstract string ObterResumoPerfil();
    }
}
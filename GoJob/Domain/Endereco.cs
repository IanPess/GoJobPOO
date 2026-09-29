namespace GoJob.Domain
{
    public class Endereco
    {
        public int Id { get; set; }
        public string Logradouro { get; set; } = string.Empty;
        public string Bairro {  get; set; } = string.Empty;
        public string Cidade {  get; set; } = string.Empty;
        public string Estado { get;set; } = string.Empty;

        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        public bool ValidarCamposObrigatorios()
        {
            return !string.IsNullOrWhiteSpace(Logradouro) &&
                   !string.IsNullOrWhiteSpace(Cidade) &&
                   !string.IsNullOrWhiteSpace(Estado);
        }
    }
}

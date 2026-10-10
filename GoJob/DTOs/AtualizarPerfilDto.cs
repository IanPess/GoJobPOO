namespace GoJob.DTOs;

public class AtualizarPerfilDto
{
    public List<int> CategoriasIds { get; set; } = new();
    public string Logradouro { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}
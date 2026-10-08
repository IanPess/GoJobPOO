namespace GoJob.DTOs;

public class ProfissionalPerfilDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Profissao { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string? NomeCategoria { get; set; }

    // Front-end panel-er jonno eikhane Missing Properties add kora holo:
    public List<int> CategoriasIds { get; set; } = new();
    public string Cidade { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;

    public double AvaliacaoMedia { get; set; }
    public int TotalAvaliacoes { get; set; }
}
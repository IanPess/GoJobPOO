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
    public double AvaliacaoMedia { get; set; }
    public int TotalAvaliacoes { get; set; }
}
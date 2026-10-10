namespace GoJob.DTOs;

public class AvaliacaoCriarDto
{
    public int ClienteId { get; set; }
    public int ProfissionalId { get; set; }
    public int Nota { get; set; }
    public string Comentario { get; set; } = string.Empty;
}
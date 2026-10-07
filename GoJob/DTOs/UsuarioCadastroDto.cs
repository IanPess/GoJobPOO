namespace GoJob.DTOs;

public class UsuarioCadastroDto
{
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;

    // Tipo de utilizador: "Cliente" ou "Profissional"
    public string TipoUsuario { get; set; } = string.Empty;

    // Campos específicos para Profissional (opcionais no cadastro inicial)
    public string? Profissao { get; set; }
    public string? Descricao { get; set; }
    public int? CategoriaId { get; set; }
}
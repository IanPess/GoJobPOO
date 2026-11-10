using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;
using GoJob.DTOs;

namespace GoJob.Controllers;

// Define que esta classe é um Controller de API REST e aceita requisições JSON
[ApiController]
// Define a rota base da API como '/api/auth' (pega o nome do controller sem o sufixo)
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    // Injeção de dependência do contexto do banco de dados (EF Core)
    private readonly AppDbContext _context;

    // Construtor que recebe o DbContext configurado na Startup/Program.cs
    public AuthController(AppDbContext context)
    {
        _context = context;
    }

    // Endpoint HTTP POST para cadastro de novos usuários (/api/auth/cadastrar)
    [HttpPost("cadastrar")]
    public async Task<IActionResult> Cadastrar([FromBody] UsuarioCadastroDto dto)
    {
        // Normaliza o e-mail removendo espaços e convertendo para minúsculas
        var emailClean = dto.Email?.Trim().ToLower();

        // Validação de entrada: impede e-mail vazio ou nulo
        if (string.IsNullOrEmpty(emailClean))
        {
            return BadRequest(new { mensagem = "E-mail inválido!" });
        }

        // RN01 (Unicidade de E-mail): Consulta assíncrona no Supabase para verificar se o e-mail já existe
        var emailExiste = await _context.Usuarios
            .AnyAsync(u => u.Email.ToLower() == emailClean);

        // Se o e-mail já estiver cadastrado, lança erro HTTP 400 (Bad Request)
        if (emailExiste)
        {
            return BadRequest(new { mensagem = "E-mail já cadastrado!" });
        }

        dto.Email = emailClean;

        // Declara a referência genérica da classe base abstrata Usuario (Polimorfismo/Herança)
        Usuario novoUsuario;

        // Verifica se a conta solicitada é do tipo Profissional
        if (dto.TipoUsuario.Equals("Profissional", StringComparison.OrdinalIgnoreCase))
        {
            // Instancia a subclasse concreta Profissional (herda de Usuario)
            var prof = new Profissional
            {
                Nome = dto.Nome,
                Email = dto.Email,
                SenhaHash = dto.Senha,
                Telefone = dto.Telefone,
                DescricaoServico = dto.Descricao ?? string.Empty
            };

            // Se uma categoria inicial foi selecionada no cadastro, faz a busca no banco
            if (dto.CategoriaId.HasValue)
            {
                var cat = await _context.Categorias.FindAsync(dto.CategoriaId.Value);
                if (cat != null)
                {
                    // RN05: Adiciona a categoria à lista encapsulada do profissional
                    prof.Categorias.Add(cat);
                }
            }

            // Atribui a instância do Profissional à variável da classe pai Usuario
            novoUsuario = prof;
        }
        else
        {
            // Instancia a subclasse concreta Cliente (herda de Usuario)
            novoUsuario = new Cliente
            {
                Nome = dto.Nome,
                Email = dto.Email,
                SenhaHash = dto.Senha,
                Telefone = dto.Telefone
            };
        }

        // Adiciona o novo objeto (Cliente ou Profissional) ao ChangeTracker do EF Core
        _context.Usuarios.Add(novoUsuario);
        // Persiste as alterações no banco de dados PostgreSQL do Supabase (estratégia TPH)
        await _context.SaveChangesAsync();

        // Retorna HTTP 200 OK com mensagem de sucesso e o ID gerado no banco
        return Ok(new { mensagem = "Usuário cadastrado com sucesso!", usuarioId = novoUsuario.Id });
    }

    // Endpoint HTTP POST para autenticação de usuários (/api/auth/login)
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        // Normaliza o e-mail informado no login
        var emailClean = dto.Email?.Trim().ToLower();

        // RF02: Consulta o usuário no banco comparando e-mail normalizado e o hash da senha
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailClean && u.SenhaHash == dto.Senha);

        // Se não encontrar nenhum registro correspondente, lança erro HTTP 401 (Unauthorized)
        if (usuario == null)
        {
            return Unauthorized(new { mensagem = "E-mail ou senha inválidos!" });
        }

        // Mapeamento dinâmico de papéis (roles) para controle de acesso no front-end
        string role;

        // Verifica o tipo real da classe C# usando operador 'is' ou a propriedade TipoUsuario
        if (usuario is Administrador || usuario.TipoUsuario?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true)
        {
            role = "ADMIN";
        }
        else if (usuario is Profissional || usuario.TipoUsuario?.Equals("profissional", StringComparison.OrdinalIgnoreCase) == true)
        {
            role = "PROFISSIONAL";
        }
        else
        {
            role = "CLIENTE";
        }

        // Retorna HTTP 200 OK com o token de sessão e o DTO seguro do usuário autenticado (RN07)
        return Ok(new
        {
            token = "demo-jwt-token",
            usuario = new
            {
                id = usuario.Id,
                nome = usuario.Nome,
                email = usuario.Email,
                tipo = role,
                tipoUsuario = role,
                role = role
            }
        });
    }
}
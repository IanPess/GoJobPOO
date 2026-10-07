using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;
using GoJob.DTOs;

namespace GoJob.Controllers;

[ApiController] // Define que esta classe é um Controller de API e ativa validações automáticas
[Route("api/[controller]")] // Define a rota base como "api/auth"
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context; // Variável para armazenar a conexão com a base de dados

    // Construtor: Recebe o AppDbContext via Injeção de Dependência configurada no Program.cs
    public AuthController(AppDbContext context)
    {
        _context = context; // Guarda a instância da conexão para usar nos métodos
    }

    // Endpoint POST para cadastro: responde em "POST api/auth/cadastrar"
    [HttpPost("cadastrar")]
    public async Task<IActionResult> Cadastrar([FromBody] UsuarioCadastroDto dto) // Recebe o JSON do front-end e converte no DTO
    {
        // 1. Consulta o banco para ver se já existe algum utilizador com o e-mail informado
        if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
        {
            // Se encontrar, retorna um erro HTTP 400 (Bad Request) com uma mensagem em JSON
            return BadRequest(new { mensagem = "E-mail já cadastrado!" });
        }

        Usuario novoUsuario; // Declara a variável genérica da entidade pai (Usuario)

        // 2. Verifica se o tipo enviado no DTO é "Profissional" (ignorando maiúsculas/minúsculas)
        if (dto.TipoUsuario.Equals("Profissional", StringComparison.OrdinalIgnoreCase))
        {
            // Cria a instância específica da classe Profissional ajustada ao modelo do Domain
            var prof = new Profissional
            {
                Nome = dto.Nome,
                Email = dto.Email,
                Senha = dto.Senha, // Atribui a senha recebida
                Telefone = dto.Telefone,
                DescricaoServico = dto.Descricao ?? string.Empty // Atribui a descrição ao campo DescricaoServico da entidade
            };

            // Se o front-end enviou o ID de uma Categoria no cadastro
            if (dto.CategoriaId.HasValue)
            {
                // Procura a categoria correspondente na tabela de Categorias
                var cat = await _context.Categorias.FindAsync(dto.CategoriaId.Value);
                if (cat != null)
                {
                    // Adiciona a categoria encontrada à coleção N:N do profissional
                    prof.Categorias.Add(cat);
                }
            }

            novoUsuario = prof; // Atribui a instância do profissional à variável pai
        }
        else
        {
            // Se não for profissional, cria uma instância específica da classe herdeira Cliente
            novoUsuario = new Cliente
            {
                Nome = dto.Nome,
                Email = dto.Email,
                Senha = dto.Senha,
                Telefone = dto.Telefone
            };
        }

        _context.Usuarios.Add(novoUsuario); // Prepara o objeto para ser inserido na tabela 'Usuarios'
        await _context.SaveChangesAsync(); // Executa o comando INSERT no banco de dados SQLite de forma assíncrona

        // Retorna HTTP 200 (OK) com a mensagem e o ID que o banco acabou de gerar automaticamente
        return Ok(new { mensagem = "Usuário cadastrado com sucesso!", usuarioId = novoUsuario.Id });
    }

    // Endpoint POST para login: responde em "POST api/auth/login"
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto) // Recebe email e senha do front-end
    {
        // Procura no banco o primeiro utilizador que tenha exatamente o mesmo e-mail E a mesma senha
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.Senha == dto.Senha);

        // Se não encontrar nenhum registo correspondente
        if (usuario == null)
        {
            // Retorna HTTP 401 (Unauthorized - Não Autorizado)
            return Unauthorized(new { mensagem = "E-mail ou senha inválidos!" });
        }

        // Se encontrou, retorna HTTP 200 (OK) enviando os dados essenciais para o front-end salvar no navegador
        return Ok(new
        {
            id = usuario.Id,
            nome = usuario.Nome,
            email = usuario.Email,
            tipo = usuario.GetType().Name // Descobre o nome da classe concreta ("Cliente" ou "Profissional")
        });
    }
}
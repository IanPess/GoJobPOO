using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;

namespace GoJob.Controllers;

// Define a classe como um Controller de API REST (converte dados automaticamente via JSON)
[ApiController]
// Define a rota base HTTP como '/api/avaliacoes'
[Route("api/[controller]")]
public class AvaliacoesController : ControllerBase
{
    // Injeção de dependência do contexto do banco de dados (EF Core)
    private readonly AppDbContext _context;

    // Construtor que recebe o DbContext configurado no ASP.NET Core
    public AvaliacoesController(AppDbContext context)
    {
        _context = context;
    }

    // Endpoint HTTP GET para listar as avaliações de um profissional (/api/avaliacoes/profissional/{profissionalId})
    [HttpGet("profissional/{profissionalId}")]
    public async Task<IActionResult> ObterPorProfissional(int profissionalId)
    {
        // Consulta LINQ otimizada no banco PostgreSQL do Supabase
        var avaliacoes = await _context.Avaliacoes
            // AsNoTracking: melhora a performance pois é apenas uma consulta de leitura
            .AsNoTracking()
            // Carrega o relacionamento com o Cliente que fez a avaliação
            .Include(a => a.Cliente)
            // Filtra as avaliações destinadas ao ID do profissional informado
            .Where(a => a.ProfissionalId == profissionalId)
            // Ordena das mais recentes para as mais antigas pelo ID
            .OrderByDescending(a => a.Id)
            // Projeção DTO (RN07): seleciona e expõe apenas dados públicos, ocultando dados sensíveis
            .Select(a => new
            {
                id = a.Id,
                nota = a.Nota,
                comentario = a.Comentario,
                // Trata o nome do cliente de forma segura caso a avaliação seja antiga ou sem cliente direto
                nomeCliente = a.Cliente != null ? a.Cliente.Nome : "Cliente",
                dataCriacao = a.DataHora.ToString("dd/MM/yyyy")
            })
            .ToListAsync();

        // Retorna HTTP 200 OK com a lista de avaliações formatada
        return Ok(avaliacoes);
    }

    // Endpoint HTTP POST para cadastrar uma nova avaliação (/api/avaliacoes)
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] AvaliacaoInputDto dto)
    {
        // Validação de entrada: garante que os IDs de cliente e profissional foram informados
        if (dto.ClienteId <= 0 || dto.ProfissionalId <= 0)
        {
            return BadRequest(new { mensagem = "Cliente e Profissional válidos são obrigatórios." });
        }

        // RN09 (Proibição de Autoavaliação): Impede que o cliente avalie a si mesmo
        if (dto.ClienteId == dto.ProfissionalId)
        {
            return BadRequest(new { mensagem = "Não é possível avaliar a si mesmo." });
        }

        // Instancia a entidade de domínio Avaliacao com os dados recebidos do DTO
        var avaliacao = new Avaliacao
        {
            ClienteId = dto.ClienteId,
            ProfissionalId = dto.ProfissionalId,
            Nota = dto.Nota,
            Comentario = dto.Comentario ?? string.Empty
        };

        // RN06 / ENCAPSULAMENTO: A validação da nota (1 a 5) é feita chamando um método interno da própria entidade Avaliacao
        if (!avaliacao.ValidarNota())
        {
            return BadRequest(new { mensagem = "A nota deve ser entre 1 e 5 estrelas." });
        }

        // RF08 / RN08: Impede avaliações duplicadas (verifica se o mesmo cliente já avaliou este profissional)
        var jaAvaliou = await _context.Avaliacoes
            .AsNoTracking()
            .AnyAsync(a => a.ClienteId == dto.ClienteId && a.ProfissionalId == dto.ProfissionalId);

        if (jaAvaliou)
        {
            return BadRequest(new { mensagem = "Você já avaliou este profissional anteriormente." });
        }

        // RN03: Busca as entidades no banco para garantir que ambos existem e estão ativos
        var cliente = await _context.Usuarios.FindAsync(dto.ClienteId);
        var profissional = await _context.Usuarios
            .OfType<Profissional>()
            // Carrega o histórico de avaliações já recebidas pelo profissional
            .Include(p => p.AvaliacoesRecebidas)
            .FirstOrDefaultAsync(p => p.Id == dto.ProfissionalId);

        // Se o cliente ou o profissional não existirem no Supabase, nega o registro
        if (cliente == null || profissional == null)
        {
            return BadRequest(new { mensagem = "Cliente ou profissional não encontrado." });
        }

        // 1. Persistência: Adiciona a nova avaliação à tabela do banco
        _context.Avaliacoes.Add(avaliacao);
        await _context.SaveChangesAsync();

        // 2. RN04 / ENCAPSULAMENTO: Recalcula a reputação média invocando o método de domínio do objeto Profissional
        profissional.RecalcularReputacao();
        // Salva a nova média (ReputacaoMedia) do profissional no banco de dados
        await _context.SaveChangesAsync();

        // Retorna HTTP 200 OK confirmando a gravação e informando o ID da avaliação gerada
        return Ok(new { mensagem = "Avaliação registrada e reputação atualizada com sucesso!", id = avaliacao.Id });
    }
}

// DTO (Data Transfer Object): Objeto seguro de transporte de dados vindo do front-end/JavaScript
public class AvaliacaoInputDto
{
    public int ClienteId { get; set; }
    public int ProfissionalId { get; set; }
    public int Nota { get; set; }
    public string? Comentario { get; set; }
}
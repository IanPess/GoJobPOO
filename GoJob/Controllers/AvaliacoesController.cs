using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;

namespace GoJob.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AvaliacoesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AvaliacoesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("profissional/{profissionalId}")]
    public async Task<IActionResult> ObterPorProfissional(int profissionalId)
    {
        var avaliacoes = await _context.Avaliacoes
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Where(a => a.ProfissionalId == profissionalId)
            .OrderByDescending(a => a.Id)
            .Select(a => new
            {
                id = a.Id,
                nota = a.Nota,
                comentario = a.Comentario,
                nomeCliente = a.Cliente != null ? a.Cliente.Nome : "Cliente",
                dataCriacao = a.DataHora.ToString("dd/MM/yyyy")
            })
            .ToListAsync();

        return Ok(avaliacoes);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] AvaliacaoInputDto dto)
    {
        if (dto.ClienteId <= 0 || dto.ProfissionalId <= 0)
        {
            return BadRequest(new { mensagem = "Cliente e Profissional válidos são obrigatórios." });
        }

        // RN09: Proibição de autoavaliação
        if (dto.ClienteId == dto.ProfissionalId)
        {
            return BadRequest(new { mensagem = "Não é possível avaliar a si mesmo." });
        }

        // Instancia a entidade primeiro para usar o encapsulamento
        var avaliacao = new Avaliacao
        {
            ClienteId = dto.ClienteId,
            ProfissionalId = dto.ProfissionalId,
            Nota = dto.Nota,
            Comentario = dto.Comentario ?? string.Empty
        };

        // RN06: Validação encapsulada dentro da própria entidade Avaliacao
        if (!avaliacao.ValidarNota())
        {
            return BadRequest(new { mensagem = "A nota deve ser entre 1 e 5 estrelas." });
        }

        // RF08 / RN08: Cliente não pode avaliar o mesmo profissional mais de uma vez
        var jaAvaliou = await _context.Avaliacoes
            .AsNoTracking()
            .AnyAsync(a => a.ClienteId == dto.ClienteId && a.ProfissionalId == dto.ProfissionalId);

        if (jaAvaliou)
        {
            return BadRequest(new { mensagem = "Você já avaliou este profissional anteriormente." });
        }

        var cliente = await _context.Usuarios.FindAsync(dto.ClienteId);
        var profissional = await _context.Usuarios
            .OfType<Profissional>()
            .Include(p => p.AvaliacoesRecebidas)
            .FirstOrDefaultAsync(p => p.Id == dto.ProfissionalId);

        if (cliente == null || profissional == null)
        {
            return BadRequest(new { mensagem = "Cliente ou profissional não encontrado." });
        }

        // 1. Cria e grava a avaliação no banco
        _context.Avaliacoes.Add(avaliacao);
        await _context.SaveChangesAsync();

        // 2. RN04: Recalcula a Reputação Média usando o método de domínio da entidade Profissional
        profissional.RecalcularReputacao();
        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "Avaliação registrada e reputação atualizada com sucesso!", id = avaliacao.Id });
    }
}

public class AvaliacaoInputDto
{
    public int ClienteId { get; set; }
    public int ProfissionalId { get; set; }
    public int Nota { get; set; }
    public string? Comentario { get; set; }
}
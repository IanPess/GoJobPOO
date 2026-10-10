using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;
using GoJob.DTOs;

namespace GoJob.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfissionaisController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProfissionaisController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> ObterTodos([FromQuery] string? busca, [FromQuery] int? categoriaId)
    {
        var query = _context.Usuarios
            .AsNoTracking()
            .OfType<Profissional>()
            .Include(p => p.Categorias)
            .Include(p => p.AvaliacoesRecebidas)
            .Include(p => p.Endereco)
            .AsQueryable();

        if (!string.IsNullOrEmpty(busca))
        {
            var termo = busca.Trim().ToLower();
            query = query.Where(p => p.Nome.ToLower().Contains(termo) ||
                                     (p.DescricaoServico != null && p.DescricaoServico.ToLower().Contains(termo)) ||
                                     (p.Endereco != null && p.Endereco.Cidade.ToLower().Contains(termo)));
        }

        if (categoriaId.HasValue)
        {
            query = query.Where(p => p.Categorias.Any(c => c.Id == categoriaId.Value));
        }

        var lista = await query.Select(p => new ProfissionalPerfilDto
        {
            Id = p.Id,
            Nome = p.Nome,
            Email = p.Email,
            Telefone = p.Telefone ?? string.Empty,
            Profissao = p.Categorias.FirstOrDefault() != null ? p.Categorias.FirstOrDefault()!.Nome : "Geral",
            Descricao = p.DescricaoServico ?? string.Empty,
            NomeCategoria = string.Join(", ", p.Categorias.Select(c => c.Nome)),
            CategoriasIds = p.Categorias.Select(c => c.Id).ToList(),
            Cidade = p.Endereco != null ? p.Endereco.Cidade : string.Empty,
            Bairro = p.Endereco != null ? p.Endereco.Bairro : string.Empty,
            Estado = p.Endereco != null ? p.Endereco.Estado : string.Empty,
            AvaliacaoMedia = p.ReputacaoMedia,
            TotalAvaliacoes = p.AvaliacoesRecebidas != null ? p.AvaliacoesRecebidas.Count : 0
        }).ToListAsync();

        return Ok(lista);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(int id)
    {
        var prof = await _context.Usuarios
            .AsNoTracking()
            .OfType<Profissional>()
            .Include(p => p.Categorias)
            .Include(p => p.Endereco)
            .Include(p => p.AvaliacoesRecebidas)
                .ThenInclude(a => a.Cliente)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prof == null)
        {
            return NotFound(new { mensagem = "Profissional não encontrado." });
        }

        var avaliacoesLista = prof.AvaliacoesRecebidas?.Select(a => new
        {
            id = a.Id,
            nota = a.Nota,
            comentario = a.Comentario,
            nomeCliente = a.Cliente != null ? a.Cliente.Nome : "Cliente",
            dataCriacao = DateTime.Now.ToString("dd/MM/yyyy")
        }).ToList() ?? new();

        var dto = new
        {
            id = prof.Id,
            nome = prof.Nome,
            email = prof.Email,
            telefone = prof.Telefone ?? string.Empty,
            profissao = prof.Categorias.FirstOrDefault() != null ? prof.Categorias.FirstOrDefault()!.Nome : "Geral",
            descricao = prof.DescricaoServico ?? string.Empty,
            nomeCategoria = string.Join(", ", prof.Categorias.Select(c => c.Nome)),
            categoriasIds = prof.Categorias.Select(c => c.Id).ToList(),
            cidade = prof.Endereco?.Cidade ?? string.Empty,
            bairro = prof.Endereco?.Bairro ?? string.Empty,
            estado = prof.Endereco?.Estado ?? string.Empty,
            avaliacaoMedia = prof.ReputacaoMedia,
            totalAvaliacoes = avaliacoesLista.Count,
            avaliacoes = avaliacoesLista
        };

        return Ok(dto);
    }

    [HttpPut("{id}")]
    [HttpPut("{id}/perfil")]
    public async Task<IActionResult> AtualizarPerfil(int id, [FromBody] ProfissionalPerfilAtualizarDto dto)
    {
        var profissional = await _context.Usuarios
            .OfType<Profissional>()
            .Include(p => p.Categorias)
            .Include(p => p.Endereco)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (profissional == null)
        {
            return NotFound(new { mensagem = "Profissional não encontrado." });
        }

        if (profissional.Endereco == null)
        {
            profissional.Endereco = new Endereco
            {
                UsuarioId = profissional.Id,
                Logradouro = dto.Logradouro ?? string.Empty,
                Bairro = dto.Bairro ?? string.Empty,
                Cidade = dto.Cidade ?? string.Empty,
                Estado = dto.Estado ?? string.Empty
            };
            _context.Enderecos.Add(profissional.Endereco);
        }
        else
        {
            profissional.Endereco.Logradouro = dto.Logradouro ?? string.Empty;
            profissional.Endereco.Bairro = dto.Bairro ?? string.Empty;
            profissional.Endereco.Cidade = dto.Cidade ?? string.Empty;
            profissional.Endereco.Estado = dto.Estado ?? string.Empty;
        }

        profissional.Categorias.Clear();

        if (dto.CategoriasIds != null && dto.CategoriasIds.Any())
        {
            var categorias = await _context.Categorias
                .Where(c => dto.CategoriasIds.Contains(c.Id))
                .ToListAsync();

            foreach (var cat in categorias)
            {
                profissional.Categorias.Add(cat);
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "Perfil atualizado com sucesso!" });
    }
}
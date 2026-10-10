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
                                     (p.Endereco != null && p.Endereco.Cidade.ToLower().Contains(termo)));// adciona a busca por cidade
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
        // Consulta o utilizador base no banco e verifica a conversão para Profissional
        var usuario = await _context.Usuarios
            .Include(u => (u as Profissional)!.Categorias)
            .Include(u => (u as Profissional)!.AvaliacoesRecebidas)
            .Include(u => u.Endereco)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario is not Profissional p)
        {
            return NotFound(new { mensagem = "Profissional não encontrado." });
        }

        var dto = new ProfissionalPerfilDto
        {
            Id = p.Id,
            Nome = p.Nome,
            Email = p.Email,
            Telefone = p.Telefone ?? string.Empty,
            Profissao = p.Categorias.FirstOrDefault() != null ? p.Categorias.FirstOrDefault()!.Nome : "Geral",
            Descricao = p.DescricaoServico ?? string.Empty,
            NomeCategoria = string.Join(", ", p.Categorias.Select(c => c.Nome)),
            CategoriasIds = p.Categorias.Select(c => c.Id).ToList(),
            Cidade = p.Endereco?.Cidade ?? string.Empty,
            Bairro = p.Endereco?.Bairro ?? string.Empty,
            Estado = p.Endereco?.Estado ?? string.Empty,
            AvaliacaoMedia = p.ReputacaoMedia,
            TotalAvaliacoes = p.AvaliacoesRecebidas != null ? p.AvaliacoesRecebidas.Count : 0
        };

        return Ok(dto);
    }
    // Atualizar por id
    [HttpPut("{id}/perfil")]
    public async Task<IActionResult> AtualizarPerfil(int id, [FromBody] AtualizarPerfilDto dto)
    {
        // 1. Procura o profissional na base de dados, incluindo as tabelas relacionadas
        var profissional = await _context.Usuarios
            .OfType<Profissional>()
            .Include(p => p.Categorias)
            .Include(p => p.Endereco)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (profissional == null)
        {
            return NotFound(new { mensagem = "Profissional não encontrado." });
        }

        // 2. Atualiza ou cria o endereço
        if (profissional.Endereco == null)
        {
            profissional.Endereco = new Endereco { UsuarioId = id };
        }
        profissional.Endereco.Logradouro = dto.Logradouro;
        profissional.Endereco.Bairro = dto.Bairro;
        profissional.Endereco.Cidade = dto.Cidade;
        profissional.Endereco.Estado = dto.Estado;

        // 3. Atualiza as categorias (limpa as antigas e adiciona as novas)
        profissional.Categorias.Clear();
        if (dto.CategoriasIds != null && dto.CategoriasIds.Any())
        {
            var novasCategorias = await _context.Categorias
                .Where(c => dto.CategoriasIds.Contains(c.Id))
                .ToListAsync();

            foreach (var cat in novasCategorias)
            {
                profissional.Categorias.Add(cat);
            }
        }

        // 4. Guarda as alterações no banco de dados
        await _context.SaveChangesAsync();

        return NoContent(); // Retorna sucesso (204) sem conteúdo
    }
}
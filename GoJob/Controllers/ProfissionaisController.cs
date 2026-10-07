using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.DTOs;

namespace GoJob.Controllers;

[ApiController] // Ativa recursos de API Controller, como validações automáticas de modelo
[Route("api/[controller]")] // Mapeia a rota base para "api/profissionais"
public class ProfissionaisController : ControllerBase
{
    private readonly AppDbContext _context; // Variável para manter a instância do banco de dados

    // Construtor: Recebe a conexão do banco por Injeção de Dependência
    public ProfissionaisController(AppDbContext context)
    {
        _context = context; // Armazena a conexão para uso nos métodos
    }

    // Endpoint GET com filtros opcionais: responde em "GET api/profissionais?busca=texto&categoriaId=1"
    [HttpGet]
    public async Task<IActionResult> ObterTodos([FromQuery] string? busca, [FromQuery] int? categoriaId)
    {
        // Monta a consulta inicial na tabela de Profissionais sem executá-la no banco ainda (AsQueryable)
        var query = _context.Profissionais
            .Include(p => p.Categorias) // Faz o JOIN para trazer a lista de Categorias (Muitos-para-Muitos)
            .Include(p => p.AvaliacoesRecebidas) // Faz o JOIN para trazer as avaliações recebidas pelo prestador
            .AsQueryable();

        // Se o front-end enviou algum termo de busca na URL
        if (!string.IsNullOrEmpty(busca))
        {
            // Adiciona o filtro WHERE buscando o termo no Nome OU na Descrição do Serviço
            query = query.Where(p => p.Nome.Contains(busca) || p.DescricaoServico.Contains(busca));
        }

        // Se o front-end enviou um ID de categoria na URL
        if (categoriaId.HasValue)
        {
            // Filtra mantendo apenas os profissionais que possuem essa Categoria dentro de sua lista N:N
            query = query.Where(p => p.Categorias.Any(c => c.Id == categoriaId));
        }

        // Converte as entidades do banco para DTOs (Data Transfer Objects) antes de responder ao cliente
        var lista = await query.Select(p => new ProfissionalPerfilDto
        {
            Id = p.Id,
            Nome = p.Nome,
            Email = p.Email,
            Telefone = p.Telefone,
            // Pega o nome da primeira categoria como profissão principal ou define "Geral" se estiver sem categoria
            Profissao = p.Categorias.FirstOrDefault() != null ? p.Categorias.FirstOrDefault()!.Nome : "Geral",
            Descricao = p.DescricaoServico, // Mapeia o campo DescricaoServico da entidade para o DTO
            NomeCategoria = string.Join(", ", p.Categorias.Select(c => c.Nome)), // Concatena os nomes de todas as categorias em uma string separada por vírgula
            AvaliacaoMedia = p.ReputacaoMedia, // Pega a nota média calculada diretamente da entidade Profissional
            TotalAvaliacoes = p.AvaliacoesRecebidas.Count // Conta quantas avaliações o profissional possui
        }).ToListAsync(); // Executa a consulta SELECT no banco de dados SQLite e converte o resultado em lista de forma assíncrona

        return Ok(lista); // Retorna HTTP 200 (OK) enviando a lista de profissionais formatada em JSON
    }

    // Endpoint GET por ID: responde em "GET api/profissionais/5"
    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(int id) // Recebe o ID diretamente a partir do caminho da URL
    {
        // Procura no banco o primeiro profissional correspondente ao ID informado
        var p = await _context.Profissionais
            .Include(prof => prof.Categorias) // Carrega as categorias vinculadas
            .Include(prof => prof.AvaliacoesRecebidas) // Carrega as avaliações vinculadas
            .FirstOrDefaultAsync(prof => prof.Id == id); // Retorna o objeto ou nulo se não encontrar

        // Se nenhum profissional for encontrado com o ID informado
        if (p == null) return NotFound(new { mensagem = "Profissional não encontrado." }); // Retorna HTTP 404 (Not Found)

        // Prepara o objeto DTO com os detalhes completos do perfil para exibição na tela
        var dto = new ProfissionalPerfilDto
        {
            Id = p.Id,
            Nome = p.Nome,
            Email = p.Email,
            Telefone = p.Telefone,
            Profissao = p.Categorias.FirstOrDefault() != null ? p.Categorias.FirstOrDefault()!.Nome : "Geral",
            Descricao = p.DescricaoServico,
            NomeCategoria = string.Join(", ", p.Categorias.Select(c => c.Nome)),
            AvaliacaoMedia = p.ReputacaoMedia,
            TotalAvaliacoes = p.AvaliacoesRecebidas.Count
        };

        return Ok(dto); // Retorna HTTP 200 (OK) com o DTO do profissional no formato JSON
    }
}
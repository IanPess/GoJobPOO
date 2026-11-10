using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;
using GoJob.DTOs;

namespace GoJob.Controllers
{
    // Define a classe como Controller de API REST (serializa objetos C# em JSON automaticamente)
    [ApiController]
    // Permite acesso público temporário/irrestrito às rotas para facilitar a demonstração na defesa
    [AllowAnonymous]
    // Define a rota base HTTP como '/api/categorias'
    [Route("api/[controller]")]
    public class CategoriasController : ControllerBase
    {
        // Injeção de dependência do contexto do banco de dados (EF Core)[cite: 22]
        private readonly AppDbContext _context;

        // Construtor que recebe a instância do DbContext
        public CategoriasController(AppDbContext context)
        {
            _context = context;
        }

        // Endpoint HTTP GET para listar todas as categorias (/api/categorias)[cite: 17, 22]
        [HttpGet]
        public async Task<IActionResult> ObterTodas()
        {
            try
            {
                // Consulta assíncrona otimizada no banco Supabase
                var categorias = await _context.Categorias
                    // AsNoTracking: melhora o desempenho pois não rastreia alterações (apenas leitura)[cite: 31]
                    .AsNoTracking()
                    // Projeção DTO (RN07): formata a resposta JSON limpa para o front-end[cite: 22, 27]
                    .Select(c => new
                    {
                        id = c.Id,
                        nome = c.Nome,
                        descricao = c.Descricao ?? string.Empty,
                        ativa = c.Ativa
                    })
                    .ToListAsync();

                // Retorna HTTP 200 OK com a lista de categorias
                return Ok(categorias);
            }
            catch (Exception ex)
            {
                // Captura exceção interna e retorna status HTTP 500 em caso de erro no banco[cite: 28]
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { mensagem = $"Erro ao buscar categorias: {msg}" });
            }
        }

        // Endpoint HTTP POST para cadastrar uma nova categoria (RF03 / RN08) (/api/categorias)[cite: 7, 12, 27]
        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] CategoriaInputDto dto)
        {
            // Validação de entrada: nome da categoria é obrigatório
            if (dto == null || string.IsNullOrWhiteSpace(dto.Nome))
            {
                return BadRequest(new { mensagem = "O nome da categoria é obrigatório." });
            }

            try
            {
                // Instancia a classe de domínio Categoria com os dados recebidos do DTO[cite: 22]
                var categoria = new Categoria
                {
                    Nome = dto.Nome.Trim(),
                    Descricao = dto.Descricao?.Trim() ?? string.Empty,
                    Ativa = true // Por padrão, nova categoria nasce ativa no catálogo[cite: 7, 15]
                };

                // Adiciona ao contexto e persiste no banco PostgreSQL no Supabase[cite: 22]
                _context.Categorias.Add(categoria);
                await _context.SaveChangesAsync();

                // Retorna HTTP 200 OK com o ID gerado pelo banco para atualização da interface gráfica
                return Ok(new
                {
                    id = categoria.Id,
                    nome = categoria.Nome,
                    descricao = categoria.Descricao,
                    ativa = categoria.Ativa,
                    mensagem = "Categoria criada com sucesso!"
                });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { mensagem = $"Erro ao criar: {msg}" });
            }
        }

        // Endpoint HTTP PUT para editar nome e descrição de uma categoria existente (RF04) (/api/categorias/{id})[cite: 7, 12]
        [HttpPut("{id}")]
        public async Task<IActionResult> Editar(int id, [FromBody] CategoriaInputDto dto)
        {
            // Validação de entrada
            if (dto == null || string.IsNullOrWhiteSpace(dto.Nome))
            {
                return BadRequest(new { mensagem = "O nome da categoria não pode ser vazio." });
            }

            try
            {
                // Localiza a categoria no Supabase pelo ID[cite: 7, 17]
                var categoria = await _context.Categorias.FindAsync(id);
                if (categoria == null)
                {
                    // Se a categoria não existir, lança HTTP 404 (NotFound)[cite: 7, 28]
                    return NotFound(new { mensagem = "Categoria não encontrada." });
                }

                // Atualiza os atributos da entidade[cite: 7, 14]
                categoria.Nome = dto.Nome.Trim();
                categoria.Descricao = dto.Descricao?.Trim() ?? string.Empty;

                // Persiste a edição no banco de dados[cite: 22]
                await _context.SaveChangesAsync();
                return Ok(new { mensagem = "Categoria atualizada com sucesso!" });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { mensagem = $"Erro ao atualizar: {msg}" });
            }
        }

        // Endpoint para inativação/ativação (RF05 / RN02) (/api/categorias/{id}/inativar)[cite: 7, 12, 27]
        // Suporta Múltiplos Verbos HTTP (DELETE, PATCH) para flexibilidade e compatibilidade com rotas web
        [HttpDelete("{id}")]
        [HttpDelete("{id}/inativar")]
        [HttpPatch("{id}/inativar")]
        public async Task<IActionResult> Excluir(int id)
        {
            try
            {
                // Busca a categoria a ser alterada[cite: 7, 17]
                var categoria = await _context.Categorias.FindAsync(id);
                if (categoria == null)
                {
                    return NotFound(new { mensagem = "Categoria não encontrada." });
                }

                // ENCAPSULAMENTO DE POO: Em vez de alterar o atributo booleano diretamente (categoria.Ativa = false),
                // o controller invoca os métodos de domínio encapsulados da própria classe Categoria[cite: 15, 20]
                if (categoria.Ativa)
                {
                    categoria.Inativar(); // Executa o método categoria.Inativar()[cite: 15]
                }
                else
                {
                    categoria.Ativar(); // Executa o método categoria.Ativar()[cite: 15]
                }

                // Persiste o novo status preservando o histórico de vínculos existentes (RNF03)
                await _context.SaveChangesAsync();
                return Ok(new { mensagem = "Status alterado com sucesso!", ativa = categoria.Ativa });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { mensagem = $"Erro ao alterar status: {msg}" });
            }
        }
    }
}
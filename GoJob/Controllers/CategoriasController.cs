using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;
using GoJob.DTOs;

namespace GoJob.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class CategoriasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriasController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> ObterTodas()
        {
            try
            {
                var categorias = await _context.Categorias
                    .AsNoTracking()
                    .Select(c => new
                    {
                        id = c.Id,
                        nome = c.Nome,
                        descricao = c.Descricao ?? string.Empty,
                        ativa = c.Ativa
                    })
                    .ToListAsync();

                return Ok(categorias);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { mensagem = $"Erro ao buscar categorias: {msg}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] CategoriaInputDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Nome))
            {
                return BadRequest(new { mensagem = "O nome da categoria é obrigatório." });
            }

            try
            {
                var categoria = new Categoria
                {
                    Nome = dto.Nome.Trim(),
                    Descricao = dto.Descricao?.Trim() ?? string.Empty,
                    Ativa = true
                };

                _context.Categorias.Add(categoria);
                await _context.SaveChangesAsync();

                // Retorna o objeto com o ID gerado pelo banco para o front-end renderizar os botões
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

        [HttpPut("{id}")]
        public async Task<IActionResult> Editar(int id, [FromBody] CategoriaInputDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Nome))
            {
                return BadRequest(new { mensagem = "O nome da categoria não pode ser vazio." });
            }

            try
            {
                var categoria = await _context.Categorias.FindAsync(id);
                if (categoria == null)
                {
                    return NotFound(new { mensagem = "Categoria não encontrada." });
                }

                categoria.Nome = dto.Nome.Trim();
                categoria.Descricao = dto.Descricao?.Trim() ?? string.Empty;

                await _context.SaveChangesAsync();
                return Ok(new { mensagem = "Categoria atualizada com sucesso!" });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { mensagem = $"Erro ao atualizar: {msg}" });
            }
        }

        [HttpDelete("{id}")]
        [HttpDelete("{id}/inativar")]
        [HttpPatch("{id}/inativar")]
        public async Task<IActionResult> Excluir(int id)
        {
            try
            {
                var categoria = await _context.Categorias.FindAsync(id);
                if (categoria == null)
                {
                    return NotFound(new { mensagem = "Categoria não encontrada." });
                }

                // Utiliza os métodos encapsulados da entidade de domínio Categoria
                if (categoria.Ativa)
                {
                    categoria.Inativar();
                }
                else
                {
                    categoria.Ativar();
                }

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
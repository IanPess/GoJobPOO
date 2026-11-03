using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;
using GoJob.DTOs;

namespace GoJob.Controllers;

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
            var nomeClean = dto.Nome.Trim();
            var descClean = dto.Descricao?.Trim() ?? string.Empty;

            var affected = await _context.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Categorias\" (\"Nome\", \"Descricao\", \"Ativa\") VALUES ({0}, {1}, true)",
                nomeClean, descClean);

            if (affected == 0)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO categorias (nome, descricao, ativa) VALUES ({0}, {1}, true)",
                    nomeClean, descClean);
            }

            return Ok(new { mensagem = "Categoria criada com sucesso!" });
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
            var nomeClean = dto.Nome.Trim();
            var descClean = dto.Descricao?.Trim() ?? string.Empty;

            var affected = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE \"Categorias\" SET \"Nome\" = {0}, \"Descricao\" = {1} WHERE \"Id\" = {2}",
                nomeClean, descClean, id);

            if (affected == 0)
            {
                affected = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE categorias SET nome = {0}, descricao = {1} WHERE id = {2}",
                    nomeClean, descClean, id);
            }

            return Ok(new { mensagem = "Categoria atualizada com sucesso!" });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { mensagem = $"Erro ao atualizar: {msg}" });
        }
    }

    [HttpDelete("{id}")]
    [HttpPatch("{id}/inativar")]
    public async Task<IActionResult> Excluir(int id)
    {
        try
        {
            var affected = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE \"Categorias\" SET \"Ativa\" = NOT \"Ativa\" WHERE \"Id\" = {0}", id);

            if (affected == 0)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE categorias SET ativa = NOT ativa WHERE id = {0}", id);
            }

            return Ok(new { mensagem = "Status alterado com sucesso!" });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { mensagem = $"Erro ao alterar status: {msg}" });
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;

namespace GoJob.Controllers;

[ApiController]
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
        var categorias = await _context.Categorias.AsNoTracking().ToListAsync();
        return Ok(categorias);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Criar([FromBody] CategoriaInputDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
        {
            return BadRequest(new { mensagem = "O nome da categoria é obrigatório." });
        }

        var existe = await _context.Categorias.AnyAsync(c => c.Nome.ToLower() == dto.Nome.Trim().ToLower());
        if (existe)
        {
            return BadRequest(new { mensagem = "Já existe uma categoria com este nome." });
        }

        var categoria = new Categoria { Nome = dto.Nome.Trim() };
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "Categoria criada com sucesso!", id = categoria.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Editar(int id, [FromBody] CategoriaInputDto dto)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null)
        {
            return NotFound(new { mensagem = "Categoria não encontrada." });
        }

        if (string.IsNullOrWhiteSpace(dto.Nome))
        {
            return BadRequest(new { mensagem = "O nome da categoria não pode ser vazio." });
        }

        categoria.Nome = dto.Nome.Trim();
        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "Categoria atualizada com sucesso!" });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Excluir(int id)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null)
        {
            return NotFound(new { mensagem = "Categoria não encontrada." });
        }

        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "Categoria removida com sucesso!" });
    }
}

public class CategoriaInputDto
{
    public string Nome { get; set; } = string.Empty;
}
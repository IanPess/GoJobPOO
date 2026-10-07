using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;

namespace GoJob.Controllers;

[ApiController]
[Route("api/[controller]")] // Define a rota base como "api/categorias"
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _context; // Instância do banco de dados

    public CategoriasController(AppDbContext context)
    {
        _context = context; // Recebe o contexto via injeção de dependência
    }

    // Endpoint GET: responde em "GET api/categorias"
    [HttpGet]
    public async Task<IActionResult> ObterTodas()
    {
        // Faz um SELECT * FROM Categorias no SQLite de forma assíncrona
        var categorias = await _context.Categorias.ToListAsync();

        // Retorna HTTP 200 (OK) com a lista de categorias em JSON para preencher o menu do front-end
        return Ok(categorias);
    }
}
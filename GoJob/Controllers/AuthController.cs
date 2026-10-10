using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;
using GoJob.DTOs;

namespace GoJob.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuthController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("cadastrar")]
    public async Task<IActionResult> Cadastrar([FromBody] UsuarioCadastroDto dto)
    {
        var emailClean = dto.Email?.Trim().ToLower();

        if (string.IsNullOrEmpty(emailClean))
        {
            return BadRequest(new { mensagem = "E-mail inválido!" });
        }

        var emailExiste = await _context.Usuarios
            .AnyAsync(u => u.Email.ToLower() == emailClean);

        if (emailExiste)
        {
            return BadRequest(new { mensagem = "E-mail já cadastrado!" });
        }

        dto.Email = emailClean;

        Usuario novoUsuario;

        if (dto.TipoUsuario.Equals("Profissional", StringComparison.OrdinalIgnoreCase))
        {
            var prof = new Profissional
            {
                Nome = dto.Nome,
                Email = dto.Email,
                SenhaHash = dto.Senha,
                Telefone = dto.Telefone,
                DescricaoServico = dto.Descricao ?? string.Empty
            };

            if (dto.CategoriaId.HasValue)
            {
                var cat = await _context.Categorias.FindAsync(dto.CategoriaId.Value);
                if (cat != null)
                {
                    prof.Categorias.Add(cat);
                }
            }

            novoUsuario = prof;
        }
        else
        {
            novoUsuario = new Cliente
            {
                Nome = dto.Nome,
                Email = dto.Email,
                SenhaHash = dto.Senha,
                Telefone = dto.Telefone
            };
        }

        _context.Usuarios.Add(novoUsuario);
        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "Usuário cadastrado com sucesso!", usuarioId = novoUsuario.Id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var emailClean = dto.Email?.Trim().ToLower();

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailClean && u.SenhaHash == dto.Senha);

        if (usuario == null)
        {
            return Unauthorized(new { mensagem = "E-mail ou senha inválidos!" });
        }

        var tipoClasse = usuario.GetType().Name;

        return Ok(new
        {
            token = "demo-jwt-token",
            usuario = new
            {
                id = usuario.Id,
                nome = usuario.Nome,
                email = usuario.Email,
                tipo = tipoClasse.ToUpper(),
                tipoUsuario = tipoClasse.ToUpper()
            }
        });
    }
}
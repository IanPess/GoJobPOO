using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;
using GoJob.DTOs;

namespace GoJob.Controllers
{
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

            var listaBruta = await query.ToListAsync();

            var lista = listaBruta.Select(p => new
            {
                id = p.Id,
                nome = p.Nome,
                email = p.Email,
                telefone = p.Telefone ?? string.Empty,
                profissao = p.Categorias.FirstOrDefault() != null ? p.Categorias.FirstOrDefault()!.Nome : "Geral",
                descricao = p.DescricaoServico ?? string.Empty,
                nomeCategoria = string.Join(", ", p.Categorias.Select(c => c.Nome)),
                categoriasIds = p.Categorias.Select(c => c.Id).ToList(),
                cidade = p.Endereco != null ? p.Endereco.Cidade : string.Empty,
                bairro = p.Endereco != null ? p.Endereco.Bairro : string.Empty,
                estado = p.Endereco != null ? p.Endereco.Estado : string.Empty,
                avaliacaoMedia = p.ReputacaoMedia,
                totalAvaliacoes = p.AvaliacoesRecebidas != null ? p.AvaliacoesRecebidas.Count : 0,
                ativo = !p.Email.StartsWith("inativo_") && p.Nome != "Usuário Inativo"
            }).ToList();

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
                reputacaoMedia = prof.ReputacaoMedia,
                totalAvaliacoes = avaliacoesLista.Count,
                avaliacoes = avaliacoesLista,
                ativo = !prof.Email.StartsWith("inativo_") && prof.Nome != "Usuário Inativo"
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

            // Limpa as categorias via lista interna para reassociar
            profissional.Categorias.Clear();

            if (dto.CategoriasIds != null && dto.CategoriasIds.Any())
            {
                var categorias = await _context.Categorias
                    .Where(c => dto.CategoriasIds.Contains(c.Id))
                    .ToListAsync();

                foreach (var cat in categorias)
                {
                    // Usa o método encapsulado da entidade Profissional (Valida a regra RN05 de no máximo 3 categorias)
                    bool adicionou = profissional.AdicionarCategoria(cat);
                    if (!adicionou)
                    {
                        return BadRequest(new { mensagem = "Um profissional pode ter no máximo 3 categorias associadas." });
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { mensagem = "Perfil atualizado com sucesso!" });
        }

        [HttpDelete("{id}")]
        [HttpDelete("{id}/inativar")]
        [HttpPatch("{id}/inativar")]
        [HttpPut("{id}/inativar")]
        public async Task<IActionResult> Inativar(int id)
        {
            var profissional = await _context.Usuarios
                .OfType<Profissional>()
                .Include(p => p.Categorias)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profissional == null)
            {
                return NotFound(new { mensagem = "Profissional não encontrado." });
            }

            // Anonimiza os dados do perfil
            profissional.Nome = "Usuário Inativo";
            profissional.Email = $"inativo_{profissional.Id}_{Guid.NewGuid().ToString()[..4]}@gojob.local";
            profissional.Telefone = string.Empty;
            profissional.DescricaoServico = "Conta desativada.";

            profissional.Categorias.Clear();

            await _context.SaveChangesAsync();

            return Ok(new { mensagem = "Perfil inativado e dados anonimizados com sucesso!" });
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoJob.Data;
using GoJob.Domain;
using GoJob.DTOs;

namespace GoJob.Controllers
{
    // Define a classe como um Controller de API REST (serializa objetos C# em JSON automaticamente)[cite: 22]
    [ApiController]
    // Define a rota base HTTP como '/api/profissionais'[cite: 22]
    [Route("api/[controller]")]
    public class ProfissionaisController : ControllerBase
    {
        // Injeção de dependência do contexto do banco de dados (EF Core)[cite: 22]
        private readonly AppDbContext _context;

        // Construtor que recebe a instância do DbContext[cite: 22]
        public ProfissionaisController(AppDbContext context)
        {
            _context = context;
        }

        // Endpoint HTTP GET para busca e filtragem dinâmica de profissionais (RF07 / Sobrecarga de Filtros)[cite: 8, 20, 31]
        [HttpGet]
        public async Task<IActionResult> ObterTodos([FromQuery] string? busca, [FromQuery] int? categoriaId)
        {
            // POLIMORFISMO / EF CORE TPH: Filtra a tabela 'Usuarios' trazendo exclusivamente instâncias da subclasse 'Profissional'
            var query = _context.Usuarios
                .AsNoTracking() // Melhora a performance pois é uma leitura pura (sem Change Tracker)
                .OfType<Profissional>() // Garante que só traga objetos do tipo Profissional[cite: 31]
                .Include(p => p.Categorias) // Carregamento adiantado (Eager Loading) das categorias associadas[cite: 31]
                .Include(p => p.AvaliacoesRecebidas) // Traz as avaliações para contabilizar o total[cite: 31]
                .Include(p => p.Endereco) // Traz o endereço para consulta de cidade/bairro[cite: 31]
                .AsQueryable();

            // Refinamento de busca: Texto (Nome, Descrição do Serviço ou Cidade)[cite: 31]
            if (!string.IsNullOrEmpty(busca))
            {
                var termo = busca.Trim().ToLower();
                query = query.Where(p => p.Nome.ToLower().Contains(termo) ||
                                         (p.DescricaoServico != null && p.DescricaoServico.ToLower().Contains(termo)) ||
                                         (p.Endereco != null && p.Endereco.Cidade.ToLower().Contains(termo)));
            }

            // Refinamento de busca por Categoria Específica[cite: 31]
            if (categoriaId.HasValue)
            {
                query = query.Where(p => p.Categorias.Any(c => c.Id == categoriaId.Value));
            }

            // Executa a consulta no PostgreSQL do Supabase[cite: 22, 31]
            var listaBruta = await query.ToListAsync();

            // PROJEÇÃO DTO / RN07: Converte as entidades em objetos anônimos seguros, omitindo dados sensíveis como 'SenhaHash'[cite: 9, 11, 31]
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

            // Retorna HTTP 200 OK com a lista filtrada[cite: 31]
            return Ok(lista);
        }

        // Endpoint HTTP GET para exibir o perfil público detalhado de um profissional por ID (RF10)[cite: 9, 10]
        [HttpGet("{id}")]
        public async Task<IActionResult> ObterPorId(int id)
        {
            // Busca o profissional por ID incluindo os relacionamentos profundos (Avaliações e Clientes que avaliaram)[cite: 9]
            var prof = await _context.Usuarios
                .AsNoTracking()
                .OfType<Profissional>()
                .Include(p => p.Categorias)
                .Include(p => p.Endereco)
                .Include(p => p.AvaliacoesRecebidas)
                    .ThenInclude(a => a.Cliente)
                .FirstOrDefaultAsync(p => p.Id == id);

            // Caso o profissional não exista no banco, lança HTTP 404 (NotFound)[cite: 10, 24]
            if (prof == null)
            {
                return NotFound(new { mensagem = "Profissional não encontrado." });
            }

            // Formata o histórico de comentários e avaliações recebidas do profissional[cite: 9]
            var avaliacoesLista = prof.AvaliacoesRecebidas?.Select(a => new
            {
                id = a.Id,
                nota = a.Nota,
                comentario = a.Comentario,
                nomeCliente = a.Cliente != null ? a.Cliente.Nome : "Cliente",
                dataCriacao = DateTime.Now.ToString("dd/MM/yyyy")
            }).ToList() ?? new();

            // DTO de resposta limpo com a reputação média recalculada e dados de contato (RN07)[cite: 9, 11]
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
                estado = prof.Estado ?? string.Empty,
                avaliacaoMedia = prof.ReputacaoMedia,
                reputacaoMedia = prof.ReputacaoMedia,
                totalAvaliacoes = avaliacoesLista.Count,
                avaliacoes = avaliacoesLista,
                ativo = !prof.Email.StartsWith("inativo_") && prof.Nome != "Usuário Inativo"
            };

            return Ok(dto);
        }

        // Endpoint HTTP PUT para atualização do perfil profissional e suas categorias (RF06)[cite: 8, 27]
        [HttpPut("{id}")]
        [HttpPut("{id}/perfil")]
        public async Task<IActionResult> AtualizarPerfil(int id, [FromBody] ProfissionalPerfilAtualizarDto dto)
        {
            // Carrega o profissional do banco com seu Endereço e lista de Categorias[cite: 8]
            var profissional = await _context.Usuarios
                .OfType<Profissional>()
                .Include(p => p.Categorias)
                .Include(p => p.Endereco)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profissional == null)
            {
                return NotFound(new { mensagem = "Profissional não encontrado." });
            }

            // COMPOSIÇÃO: Cria o objeto Endereco vinculado ao profissional caso ainda não possua[cite: 19]
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
                // Atualiza os dados de localização existentes[cite: 8]
                profissional.Endereco.Logradouro = dto.Logradouro ?? string.Empty;
                profissional.Endereco.Bairro = dto.Bairro ?? string.Empty;
                profissional.Endereco.Cidade = dto.Cidade ?? string.Empty;
                profissional.Endereco.Estado = dto.Estado ?? string.Empty;
            }

            // Limpa as categorias vinculadas atualmente para reassociar as novas enviadas pelo DTO[cite: 8]
            profissional.Categorias.Clear();

            // Vínculo e validação de Categorias[cite: 8]
            if (dto.CategoriasIds != null && dto.CategoriasIds.Any())
            {
                var categorias = await _context.Categorias
                    .Where(c => dto.CategoriasIds.Contains(c.Id))
                    .ToListAsync();

                foreach (var cat in categorias)
                {
                    // ENCAPSULAMENTO DE POO / RN05: Chama o método da própria entidade Profissional
                    // O método 'profissional.AdicionarCategoria(cat)' valida internamente o limite de no máximo 3 categorias
                    bool adicionou = profissional.AdicionarCategoria(cat);
                    if (!adicionou)
                    {
                        // Se ultrapassar 3 categorias, interrompe e retorna erro HTTP 400 (Bad Request)[cite: 8, 24]
                        return BadRequest(new { mensagem = "Um profissional pode ter no máximo 3 categorias associadas." });
                    }
                }
            }

            // Persiste as alterações no PostgreSQL no Supabase[cite: 22]
            await _context.SaveChangesAsync();

            return Ok(new { mensagem = "Perfil atualizado com sucesso!" });
        }

        // Endpoint para Inativação e Anonimização de Conta de Profissional (RF12 / RN10)
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

            // RN10 / RNF03 (Anonimização de Dados): Em vez de deletar fisicamente o registro do banco,
            // o sistema descaracteriza os dados pessoais para preservar as estatísticas e histórico de avaliações recebidas[cite: 10, 11]
            profissional.Nome = "Usuário Inativo";
            profissional.Email = $"inativo_{profissional.Id}_{Guid.NewGuid().ToString()[..4]}@gojob.local";
            profissional.Telefone = string.Empty;
            profissional.DescricaoServico = "Conta desativada.";

            // Remove os vínculos de categoria para que ele não apareça em buscas futuras
            profissional.Categorias.Clear();

            // Salva as alterações no banco de dados[cite: 22]
            await _context.SaveChangesAsync();

            return Ok(new { mensagem = "Perfil inativado e dados anonimizados com sucesso!" });
        }
    }
}
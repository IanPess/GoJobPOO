/* =====================================================================
   GOJOB — Frontend (HTML5 + CSS3 + JavaScript ES6+)
   Estrutura orientada a objetos client-side, pronta para consumir a
   API REST em ASP.NET Core. Os métodos de serviço trazem a chamada
   fetch() comentada ao lado da simulação local usada nesta prévia,
   para facilitar a troca por uma integração real.
   ===================================================================== */

const API_BASE_URL = "https://localhost:5001/api"; // ajuste para a URL da sua API ASP.NET Core
const STORAGE_TOKEN_KEY = "gojob_token";
const STORAGE_USER_KEY = "gojob_user";

/* ---------------------------------------------------------------------
   1. EXCEÇÕES DE DOMÍNIO (hierarquia única, capturada pelos Controllers)
--------------------------------------------------------------------- */
class GojobException extends Error {
    constructor(mensagem) {
        super(mensagem);
        this.name = this.constructor.name;
    }
}
class EmailDuplicadoException extends GojobException {
    constructor() { super("Este e-mail já está cadastrado na plataforma."); }
}
class UsuarioNaoEncontradoException extends GojobException {
    constructor() { super("E-mail ou senha inválidos."); }
}
class ProfissionalNaoEncontradoException extends GojobException {
    constructor() { super("Profissional não encontrado."); }
}
class CategoriaInativaException extends GojobException {
    constructor(nome) { super(`A categoria "${nome}" está inativa.`); }
}
class LimiteCategoriasException extends GojobException {
    constructor() { super("Você já atingiu o limite de 3 categorias."); }
}
class AvaliacaoInvalidaException extends GojobException {
    constructor() { super("Informe uma nota de 1 a 5 e um comentário."); }
}
class AutoAvaliacaoException extends GojobException {
    constructor() { super("Não é possível avaliar a si mesmo."); }
}
class OperacaoNaoPermitidaException extends GojobException {
    constructor() { super("Você não tem permissão para executar esta ação."); }
}
class DadosObrigatoriosException extends GojobException {
    constructor(campo) { super(`Preencha o campo obrigatório: ${campo}.`); }
}

/* ---------------------------------------------------------------------
   2. CLASSES DE DOMÍNIO (encapsulamento, herança, abstração, polimorfismo)
--------------------------------------------------------------------- */
class Endereco {
    constructor({ logradouro = "", bairro = "", cidade = "", estado = "" } = {}) {
        this.logradouro = logradouro;
        this.bairro = bairro;
        this.cidade = cidade;
        this.estado = estado;
    }
    validarCamposObrigatorios() {
        return Boolean(this.logradouro && this.cidade && this.estado);
    }
}

class Categoria {
    constructor(id, nome, descricao, ativa = true) {
        this.id = id;
        this.nome = nome;
        this.descricao = descricao;
        this.ativa = ativa;
    }
    ativar() { this.ativa = true; }
    inativar() { this.ativa = false; }
    estaAtiva() { return this.ativa; }
}

class Avaliacao {
    constructor({ id, idCliente, idProfissional, nota, comentario, dataHora }) {
        this.id = id;
        this.idCliente = idCliente; // null quando anonimizada (RN10)
        this.idProfissional = idProfissional;
        this.nota = nota;
        this.comentario = comentario;
        this.dataHora = dataHora;
    }
    validarNota() { return this.nota >= 1 && this.nota <= 5 && Boolean(this.comentario); }
    anonimizar() { this.idCliente = null; }
}

class Usuario {
    #email; #senhaHash;
    constructor(id, nome, email, senhaHash, telefone) {
        if (new.target === Usuario) {
            throw new Error("Usuario é abstrata e não pode ser instanciada diretamente.");
        }
        this.id = id;
        this.nome = nome;
        this.#email = email;
        this.#senhaHash = senhaHash;
        this.telefone = telefone;
        this.ativo = true;
    }
    autenticar(senha) { return senha === this.#senhaHash; } // demo: comparação direta (backend usa bcrypt)
    atualizarDados(nome, telefone) {
        if (!nome || !telefone) throw new DadosObrigatoriosException("nome/telefone");
        this.nome = nome;
        this.telefone = telefone;
    }
    // Método abstrato — cada subclasse fornece sua implementação (polimorfismo)
    obterResumoPerfil() { throw new Error("obterResumoPerfil() deve ser implementado pela subclasse."); }
    getId() { return this.id; }
    getNome() { return this.nome; }
    getEmail() { return this.#email; }
}

class Cliente extends Usuario {
    constructor(id, nome, email, senhaHash, telefone) {
        super(id, nome, email, senhaHash, telefone);
        this.tipo = "CLIENTE";
    }
    obterResumoPerfil() {
        return { tipo: this.tipo, nome: this.nome };
    }
}

class Profissional extends Usuario {
    #categorias = [];
    constructor(id, nome, email, senhaHash, telefone) {
        super(id, nome, email, senhaHash, telefone);
        this.tipo = "PROFISSIONAL";
        this.reputacaoMedia = 0;
        this.endereco = null;
    }
    get categorias() { return this.#categorias; }
    set categorias(lista) { this.#categorias = lista; }

    configurarEndereco(endereco) {
        if (!endereco.validarCamposObrigatorios()) {
            throw new DadosObrigatoriosException("logradouro/cidade/estado");
        }
        this.endereco = endereco;
    }
    adicionarCategoria(categoria) {
        if (!categoria.estaAtiva()) throw new CategoriaInativaException(categoria.nome);
        if (this.#categorias.includes(categoria.id)) return;
        if (this.#categorias.length >= 3) throw new LimiteCategoriasException();
        this.#categorias.push(categoria.id);
    }
    removerCategoria(categoriaId) {
        this.#categorias = this.#categorias.filter((c) => c !== categoriaId);
    }
    calcularReputacao(avaliacoes) {
        if (avaliacoes.length === 0) { this.reputacaoMedia = 0; return 0; }
        const soma = avaliacoes.reduce((acc, av) => acc + av.nota, 0);
        this.reputacaoMedia = soma / avaliacoes.length;
        return this.reputacaoMedia;
    }
    obterResumoPerfil() {
        // RN07 — nunca expõe e-mail, hash de senha ou tipo de usuário sensível
        return {
            nome: this.nome,
            categorias: [...this.#categorias],
            cidade: this.endereco?.cidade,
            reputacaoMedia: this.reputacaoMedia,
        };
    }
}

class Administrador extends Usuario {
    constructor(id, nome, email, senhaHash, telefone) {
        super(id, nome, email, senhaHash, telefone);
        this.tipo = "ADMIN";
    }
    obterResumoPerfil() { return { tipo: this.tipo, nome: this.nome }; }
}

/* ---------------------------------------------------------------------
   3. DADOS-BASE PARA A PRÉVIA (substituídos pelas tabelas reais via API)
--------------------------------------------------------------------- */
const seedCategorias = [
    new Categoria(1, "Elétrica", "Instalações, reparos e manutenção elétrica residencial e comercial."),
    new Categoria(2, "Limpeza", "Faxina residencial, comercial e pós-obra."),
    new Categoria(3, "Pintura", "Pintura de paredes, fachadas e retoques."),
    new Categoria(4, "Encanamento", "Reparos hidráulicos, vazamentos e instalações."),
    new Categoria(5, "Jardinagem", "Manutenção de jardins, poda e paisagismo."),
    new Categoria(6, "Montagem de Móveis", "Montagem e pequenos reparos de móveis planejados."),
    new Categoria(7, "Aulas Particulares", "Reforço escolar e aulas de idiomas."),
    new Categoria(8, "Manutenção de TI", "Suporte técnico e manutenção de computadores.", false),
];

const seedProfissionais = [
    { id: 101, nome: "Marcos Andrade", email: "marcos@exemplo.com", senha: "123456", telefone: "18999990001", categorias: [1, 6], endereco: { logradouro: "Rua das Palmeiras, 120", bairro: "Centro", cidade: "Birigui", estado: "SP" } },
    { id: 102, nome: "Fernanda Lima", email: "fernanda@exemplo.com", senha: "123456", telefone: "18999990002", categorias: [2], endereco: { logradouro: "Av. Brasil, 800", bairro: "Vila Iracema", cidade: "Birigui", estado: "SP" } },
    { id: 103, nome: "Carlos Eduardo", email: "carlos@exemplo.com", senha: "123456", telefone: "18999990003", categorias: [3, 4], endereco: { logradouro: "Rua Sete de Setembro, 45", bairro: "Jardim Planalto", cidade: "Birigui", estado: "SP" } },
    { id: 104, nome: "Juliana Ferraz", email: "juliana@exemplo.com", senha: "123456", telefone: "18999990004", categorias: [7], endereco: { logradouro: "Rua Amazonas, 300", bairro: "Centro", cidade: "Araçatuba", estado: "SP" } },
    { id: 105, nome: "Roberto Nunes", email: "roberto@exemplo.com", senha: "123456", telefone: "18999990005", categorias: [5], endereco: { logradouro: "Rua das Flores, 15", bairro: "Jardim Europa", cidade: "Birigui", estado: "SP" } },
    { id: 106, nome: "Patrícia Souza", email: "patricia@exemplo.com", senha: "123456", telefone: "18999990006", categorias: [2, 6], endereco: { logradouro: "Av. Portugal, 512", bairro: "Vila Mendonça", cidade: "Araçatuba", estado: "SP" } },
];

const seedClientes = [
    { id: 201, nome: "Ana Beatriz", email: "ana@exemplo.com", senha: "123456", telefone: "18988880001" },
    { id: 202, nome: "Pedro Henrique", email: "pedro@exemplo.com", senha: "123456", telefone: "18988880002" },
];

const seedAdmin = { id: 901, nome: "Administrador GOJOB", email: "admin@gojob.com", senha: "admin123", telefone: "1800000000" };

const seedAvaliacoes = [
    { id: 1, idCliente: 201, idProfissional: 101, nota: 5, comentario: "Excelente serviço, super pontual e organizado.", dataHora: "2026-08-02" },
    { id: 2, idCliente: 202, idProfissional: 101, nota: 4, comentario: "Bom trabalho, recomendo.", dataHora: "2026-08-14" },
    { id: 3, idCliente: 201, idProfissional: 102, nota: 5, comentario: "Deixou a casa impecável!", dataHora: "2026-08-20" },
    { id: 4, idCliente: 202, idProfissional: 103, nota: 3, comentario: "Serviço ok, mas atrasou um pouco.", dataHora: "2026-08-25" },
];

/* ---------------------------------------------------------------------
   4. "REPOSITORIES" EM MEMÓRIA (troque por chamadas HTTP ao IRepository<T> real)
--------------------------------------------------------------------- */
class MockDatabase {
    constructor() {
        this.categorias = [...seedCategorias];
        this.profissionais = seedProfissionais.map((p) => {
            const prof = new Profissional(p.id, p.nome, p.email, p.senha, p.telefone);
            prof.categorias = [...p.categorias];
            prof.configurarEndereco(new Endereco(p.endereco));
            return prof;
        });
        this.clientes = seedClientes.map((c) => new Cliente(c.id, c.nome, c.email, c.senha, c.telefone));
        this.admins = [new Administrador(seedAdmin.id, seedAdmin.nome, seedAdmin.email, seedAdmin.senha, seedAdmin.telefone)];
        this.avaliacoes = seedAvaliacoes.map((a) => new Avaliacao(a));
        this.proximoId = { usuario: 300, categoria: 100, avaliacao: 100 };
    }
    todosUsuarios() { return [...this.clientes, ...this.profissionais, ...this.admins]; }
}
const db = new MockDatabase();

/* ---------------------------------------------------------------------
   5. SERVICES — orquestram regras de negócio (RN01–RN10)
   Cada método comenta a chamada fetch() equivalente para a API real.
--------------------------------------------------------------------- */
class UsuarioService {
    async cadastrarUsuario({ nome, email, telefone, senha, tipo, cidade, estado }) {
        if (!nome || !email || !telefone || !senha || !cidade || !estado) {
            throw new DadosObrigatoriosException("nome/e-mail/telefone/senha/cidade/estado");
        }
        if (db.todosUsuarios().some((u) => u.getEmail() === email)) throw new EmailDuplicadoException(); // RN01

        /* fetch() real:
        const res = await fetch(`${API_BASE_URL}/auth/cadastro`, {
          method: "POST", headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ nome, email, telefone, senha, tipo, cidade, estado }),
        });
        if (!res.ok) throw new GojobException((await res.json()).mensagem);
        return res.json();
        */

        const id = db.proximoId.usuario++;
        if (tipo === "PROFISSIONAL") {
            const novo = new Profissional(id, nome, email, senha, telefone);
            // Endereço inicial com apenas cidade/estado, para que a busca (RF07) já encontre o
            // profissional; logradouro e bairro são completados depois no painel (RF06).
            novo.endereco = new Endereco({ logradouro: "", bairro: "", cidade, estado: estado.toUpperCase() });
            db.profissionais.push(novo);
            return novo;
        }
        const novo = new Cliente(id, nome, email, senha, telefone);
        novo.cidade = cidade; // referência de localização do cliente (não usada nas regras de busca)
        novo.estado = estado.toUpperCase();
        db.clientes.push(novo);
        return novo;
    }

    async autenticar(email, senha) {
        /* fetch() real:
        const res = await fetch(`${API_BASE_URL}/auth/login`, {
          method: "POST", headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ email, senha }),
        });
        if (!res.ok) throw new UsuarioNaoEncontradoException();
        return res.json(); // { token, usuario }
        */

        const usuario = db.todosUsuarios().find((u) => u.getEmail() === email);
        if (!usuario || !usuario.autenticar(senha)) throw new UsuarioNaoEncontradoException(); // RF02
        return { token: "demo-jwt-token", usuario };
    }
}

class CategoriaService {
    listarAtivas() {
        /* fetch(`${API_BASE_URL}/categorias?ativa=true`) */
        return db.categorias.filter((c) => c.estaAtiva());
    }
    listarTodas() { return db.categorias; }

    criarCategoria(nome, descricao, solicitante) {
        if (solicitante?.tipo !== "ADMIN") throw new OperacaoNaoPermitidaException(); // RN08
        if (!nome) throw new DadosObrigatoriosException("nome");
        const nova = new Categoria(db.proximoId.categoria++, nome, descricao, true);
        db.categorias.push(nova);
        /* fetch(`${API_BASE_URL}/categorias`, { method: "POST", body: JSON.stringify({ nome, descricao }) }) */
        return nova;
    }
    editarCategoria(id, nome, descricao, solicitante) {
        if (solicitante?.tipo !== "ADMIN") throw new OperacaoNaoPermitidaException();
        const cat = db.categorias.find((c) => c.id === id);
        if (!cat) throw new GojobException("Categoria não encontrada.");
        cat.nome = nome;
        cat.descricao = descricao;
        /* fetch(`${API_BASE_URL}/categorias/${id}`, { method: "PUT", body: JSON.stringify({ nome, descricao }) }) */
        return cat;
    }
    inativarCategoria(id, solicitante) {
        if (solicitante?.tipo !== "ADMIN") throw new OperacaoNaoPermitidaException();
        const cat = db.categorias.find((c) => c.id === id);
        if (!cat) throw new GojobException("Categoria não encontrada.");
        cat.inativar(); // RF05
        /* fetch(`${API_BASE_URL}/categorias/${id}/inativar`, { method: "PATCH" }) */
    }
}

class ProfissionalService {
    // Sobrecarga simulada via parâmetros opcionais (Parte 6.5 da documentação)
    buscarProfissionais(cidade, categoriaId = null, bairro = null) {
        if (!cidade) throw new DadosObrigatoriosException("cidade");
        if (categoriaId) {
            const cat = db.categorias.find((c) => c.id === Number(categoriaId));
            if (cat && !cat.estaAtiva()) throw new CategoriaInativaException(cat.nome); // RN02
        }
        /* fetch(`${API_BASE_URL}/profissionais?cidade=${cidade}&categoriaId=${categoriaId ?? ""}&bairro=${bairro ?? ""}`) */
        return db.profissionais.filter((p) => {
            if (!p.ativo) return false;
            const okCidade = p.endereco.cidade.toLowerCase().includes(cidade.toLowerCase());
            const okCategoria = !categoriaId || p.categorias.includes(Number(categoriaId));
            const okBairro = !bairro || p.endereco.bairro.toLowerCase().includes(bairro.toLowerCase());
            return okCidade && okCategoria && okBairro;
        }).map((p) => {
            p.calcularReputacao(db.avaliacoes.filter((a) => a.idProfissional === p.id));
            return p;
        });
    }

    buscarPorId(id) {
        /* fetch(`${API_BASE_URL}/profissionais/${id}`) */
        const p = db.profissionais.find((pr) => pr.id === id);
        if (!p) throw new ProfissionalNaoEncontradoException();
        p.calcularReputacao(db.avaliacoes.filter((a) => a.idProfissional === id));
        return p;
    }

    configurarPerfil(idProfissional, { categoriaIds, endereco }) {
        const profissional = this.buscarPorId(idProfissional);
        if (categoriaIds.length > 3) throw new LimiteCategoriasException(); // RN05
        profissional.categorias = [];
        categoriaIds.forEach((cid) => {
            const categoria = db.categorias.find((c) => c.id === cid);
            profissional.adicionarCategoria(categoria);
        });
        profissional.configurarEndereco(new Endereco(endereco));
        /* fetch(`${API_BASE_URL}/profissionais/${idProfissional}/perfil`, { method: "PUT", body: JSON.stringify({ categoriaIds, endereco }) }) */
        return profissional;
    }

    inativarConta(idProfissional, solicitante) {
        if (solicitante?.tipo !== "ADMIN") throw new OperacaoNaoPermitidaException();
        const profissional = this.buscarPorId(idProfissional);
        profissional.ativo = false;
        db.avaliacoes.filter((a) => a.idProfissional === idProfissional).forEach((a) => a.anonimizar()); // RN10
        /* fetch(`${API_BASE_URL}/profissionais/${idProfissional}/inativar`, { method: "PATCH" }) */
    }
}

class AvaliacaoService {
    constructor(profissionalService) { this.profissionalService = profissionalService; }

    listarPorProfissional(idProfissional) {
        /* fetch(`${API_BASE_URL}/profissionais/${idProfissional}/avaliacoes`) */
        return db.avaliacoes.filter((a) => a.idProfissional === idProfissional).sort((a, b) => new Date(b.dataHora) - new Date(a.dataHora));
    }

    registrarAvaliacao({ idCliente, idProfissional, nota, comentario }) {
        if (idCliente === idProfissional) throw new AutoAvaliacaoException(); // RN09
        if (!nota || nota < 1 || nota > 5 || !comentario) throw new AvaliacaoInvalidaException(); // RN06
        this.profissionalService.buscarPorId(idProfissional); // valida existência (RN03)

        const avaliacao = new Avaliacao({
            id: db.proximoId.avaliacao++,
            idCliente, idProfissional, nota, comentario,
            dataHora: new Date().toISOString(),
        });
        db.avaliacoes.push(avaliacao);
        /* fetch(`${API_BASE_URL}/avaliacoes`, { method: "POST", body: JSON.stringify({ idProfissional, nota, comentario }) }) */
        return avaliacao;
    }

    excluirAvaliacao(idAvaliacao, idCliente) {
        const avaliacao = db.avaliacoes.find((a) => a.id === idAvaliacao);
        if (!avaliacao || avaliacao.idCliente !== idCliente) throw new OperacaoNaoPermitidaException(); // RN06
        db.avaliacoes = db.avaliacoes.filter((a) => a.id !== idAvaliacao);
        /* fetch(`${API_BASE_URL}/avaliacoes/${idAvaliacao}`, { method: "DELETE" }) */
    }
}

/* Instâncias únicas dos serviços, injetadas na camada de apresentação */
const usuarioService = new UsuarioService();
const categoriaService = new CategoriaService();
const profissionalService = new ProfissionalService();
const avaliacaoService = new AvaliacaoService(profissionalService);

/* ---------------------------------------------------------------------
   6. HELPERS DE UI (toasts, animações GSAP)
--------------------------------------------------------------------- */
function toastSucesso(mensagem) {
    Swal.fire({
        toast: true, position: "top-end", timer: 2800, showConfirmButton: false,
        icon: "success", title: mensagem, background: "#191C1A", color: "#F5F4F0",
        iconColor: "#4FC189",
    });
}
function toastErro(mensagem) {
    Swal.fire({
        toast: true, position: "top-end", timer: 3200, showConfirmButton: false,
        icon: "error", title: mensagem, background: "#191C1A", color: "#F5F4F0",
        iconColor: "#CE9A3E",
    });
}
function confirmarAcao(titulo, texto) {
    return Swal.fire({
        title: titulo, text: texto, icon: "warning", showCancelButton: true,
        confirmButtonText: "Confirmar", cancelButtonText: "Cancelar",
        background: "#191C1A", color: "#F5F4F0",
        confirmButtonColor: "#CE9A3E", cancelButtonColor: "#2A2E2B",
    }).then((r) => r.isConfirmed);
}

/* ---------------------------------------------------------------------
   7. APP ALPINE — estado de tela e ligação com os serviços
--------------------------------------------------------------------- */
function gojobApp() {
    return {
        /* estado geral */
        view: "home",
        mobileMenu: false,
        categorias: db.categorias,
        profissionais: db.profissionais,
        resultados: [],
        search: { categoriaId: "", cidade: "", bairro: "" },

        session: { logado: false, id: null, role: null, nome: null },

        authModal: { open: false, tab: "login" },
        loginForm: { email: "", senha: "" },
        cadastroForm: { nome: "", email: "", telefone: "", senha: "", cidade: "", estado: "", tipo: "CLIENTE" },

        perfilAberto: false,
        perfilAtual: null,
        novaAvaliacao: { nota: 0, comentario: "" },

        dash: { categorias: [], endereco: { logradouro: "", bairro: "", cidade: "", estado: "" } },
        dashCategoriaAviso: "",

        categoriaModal: { open: false, id: null, nome: "", descricao: "" },

        /* ---------------- ciclo de vida ---------------- */
        init() {
            this.atualizarSessaoUI();
            this.$nextTick(() => {
                lucide.createIcons();
                this.animarEntradaHero();
            });
            this.$watch("view", () => this.$nextTick(() => lucide.createIcons()));
            this.$watch("perfilAberto", () => this.$nextTick(() => lucide.createIcons()));
            this.$watch("authModal.open", () => this.$nextTick(() => lucide.createIcons()));
            this.$watch("cadastroForm.tipo", () => this.$nextTick(() => lucide.createIcons()));
            this.$watch("session.logado", () => this.$nextTick(() => lucide.createIcons()));
            this.$watch("resultados", () => this.$nextTick(() => { lucide.createIcons(); this.animarCards(); }));
        },

        animarEntradaHero() {
            if (!window.gsap) return;
            gsap.from(".hero-headline", { y: 18, opacity: 0, duration: 0.7, ease: "power2.out" });
            gsap.from(".hero-search", { y: 14, opacity: 0, duration: 0.7, delay: 0.12, ease: "power2.out" });
            gsap.utils.toArray(".how-card").forEach((el, i) => {
                gsap.to(el, {
                    opacity: 1, y: 0, duration: 0.6, delay: i * 0.08, ease: "power2.out",
                    scrollTrigger: { trigger: el, start: "top 88%" },
                    onStart: () => el.classList.add("revealed"),
                });
            });
        },
        animarCards() {
            if (!window.gsap) return;
            const cards = document.querySelectorAll("#cards-grid .prof-card");
            gsap.to(cards, {
                opacity: 1, y: 0, duration: 0.45, stagger: 0.06, ease: "power2.out",
                onStart: () => cards.forEach((c) => c.classList.add("revealed")),
            });
        },

        /* ---------------- navegação ---------------- */
        setView(v) { this.view = v; window.scrollTo({ top: 0, behavior: "smooth" }); },
        goHome() { this.view = "home"; this.mobileMenu = false; window.scrollTo({ top: 0, behavior: "smooth" }); },
        scrollToHow() {
            this.view = "home";
            this.$nextTick(() => document.getElementById("how-it-works")?.scrollIntoView({ behavior: "smooth" }));
        },
        roleLabel(role) { return { CLIENTE: "Cliente", PROFISSIONAL: "Profissional", ADMIN: "Administrador" }[role] || ""; },

        /* ---------------- categorias (helpers) ---------------- */
        categoriasAtivas() { return this.categorias.filter((c) => c.ativa); },
        nomeCategoria(id) { return this.categorias.find((c) => c.id === id)?.nome || ""; },

        /* ---------------- busca (RF07) ---------------- */
        buscarProfissionais() {
            try {
                this.resultados = profissionalService.buscarProfissionais(this.search.cidade, this.search.categoriaId || null, this.search.bairro || null);
                this.view = "resultados";
                window.scrollTo({ top: 0, behavior: "smooth" });
            } catch (e) {
                toastErro(e.message);
            }
        },
        resultadosTitulo() {
            const catNome = this.search.categoriaId ? this.nomeCategoria(Number(this.search.categoriaId)) : "Profissionais";
            return `${catNome} em ${this.search.cidade}`;
        },
        iniciais(nome) { return nome.split(" ").filter(Boolean).slice(0, 2).map((n) => n[0]).join("").toUpperCase(); },

        /* ---------------- perfil público (RF10, RF08, RF09) ---------------- */
        abrirPerfil(id) {
            try {
                this.perfilAtual = profissionalService.buscarPorId(id);
                this.novaAvaliacao = { nota: 0, comentario: "" };
                this.perfilAberto = true;
            } catch (e) { toastErro(e.message); }
        },
        fecharPerfil() { this.perfilAberto = false; },
        avaliacoesDoAtual() { return this.perfilAtual ? avaliacaoService.listarPorProfissional(this.perfilAtual.id) : []; },
        nomeCliente(id) { return db.clientes.find((c) => c.getId() === id)?.getNome() || "Cliente"; },
        formatarData(iso) { return new Date(iso).toLocaleDateString("pt-BR"); },

        registrarAvaliacao() {
            try {
                avaliacaoService.registrarAvaliacao({
                    idCliente: this.session.id,
                    idProfissional: this.perfilAtual.id,
                    nota: this.novaAvaliacao.nota,
                    comentario: this.novaAvaliacao.comentario.trim(),
                });
                this.perfilAtual = profissionalService.buscarPorId(this.perfilAtual.id); // recalcula reputação (RF11)
                this.novaAvaliacao = { nota: 0, comentario: "" };
                toastSucesso("Avaliação registrada com sucesso.");
            } catch (e) { toastErro(e.message); }
        },
        async excluirAvaliacao(idAvaliacao) {
            const ok = await confirmarAcao("Excluir avaliação?", "Esta ação não pode ser desfeita.");
            if (!ok) return;
            try {
                avaliacaoService.excluirAvaliacao(idAvaliacao, this.session.id);
                this.perfilAtual = profissionalService.buscarPorId(this.perfilAtual.id);
                toastSucesso("Avaliação excluída.");
            } catch (e) { toastErro(e.message); }
        },

        /* ---------------- autenticação (RF01, RF02) ---------------- */
        openAuth(tab) { this.authModal = { open: true, tab }; },
        closeAuth() { this.authModal.open = false; },

        async autenticar() {
            try {
                const { token, usuario } = await usuarioService.autenticar(this.loginForm.email, this.loginForm.senha);
                this.salvarSessao(usuario, token);
                this.atualizarSessaoUI();
                this.closeAuth();
                this.loginForm = { email: "", senha: "" };
                toastSucesso(`Bem-vindo(a), ${this.primeiroNome(usuario.getNome())}!`);
            } catch (e) { toastErro(e.message); }
        },

        async cadastrar() {
            try {
                const usuario = await usuarioService.cadastrarUsuario(this.cadastroForm);
                this.salvarSessao(usuario, "demo-jwt-token");
                this.atualizarSessaoUI();
                if (usuario.tipo === "PROFISSIONAL") this.profissionais = db.profissionais;
                this.closeAuth();
                const tipoCadastrado = this.cadastroForm.tipo;
                this.cadastroForm = { nome: "", email: "", telefone: "", senha: "", cidade: "", estado: "", tipo: "CLIENTE" };

                if (tipoCadastrado === "PROFISSIONAL") {
                    Swal.fire({
                        title: "Conta criada com sucesso!",
                        text: "Agora configure até 3 categorias de atuação no seu painel para começar a aparecer nas buscas de clientes.",
                        icon: "success", background: "#191C1A", color: "#F5F4F0",
                        iconColor: "#4FC189", confirmButtonColor: "#2F9E6E",
                        confirmButtonText: "Ir para meu painel",
                    }).then(() => this.setView("dashboard"));
                } else {
                    toastSucesso("Conta criada com sucesso! Bem-vindo(a) ao GOJOB.");
                }
            } catch (e) { toastErro(e.message); }
        },

        /* ---------------- gerenciamento de sessão (localStorage) ---------------- */
        salvarSessao(usuario, token) {
            const dadosUsuario = { id: usuario.getId(), nome: usuario.getNome(), role: usuario.tipo };
            localStorage.setItem(STORAGE_TOKEN_KEY, token);
            localStorage.setItem(STORAGE_USER_KEY, JSON.stringify(dadosUsuario));
        },

        atualizarSessaoUI() {
            const token = localStorage.getItem(STORAGE_TOKEN_KEY);
            const dadosUsuario = localStorage.getItem(STORAGE_USER_KEY);

            if (!token || !dadosUsuario) {
                this.session = { logado: false, id: null, role: null, nome: null };
                return;
            }

            const { id, nome, role } = JSON.parse(dadosUsuario);
            this.session = { logado: true, id, role, nome };

            if (role === "PROFISSIONAL") {
                try {
                    const profissional = profissionalService.buscarPorId(id);
                    this.dash.categorias = [...profissional.categorias];
                    this.dash.endereco = { ...profissional.endereco };
                } catch {
                    // Conta não encontrada na base local (ex.: após recarregar a página em modo prévia);
                    // o cabeçalho continua refletindo a sessão salva e o painel parte de campos em branco.
                    this.dash.categorias = [];
                    this.dash.endereco = { logradouro: "", bairro: "", cidade: "", estado: "" };
                }
            }
        },

        fazerLogout() {
            localStorage.removeItem(STORAGE_TOKEN_KEY);
            localStorage.removeItem(STORAGE_USER_KEY);
            this.atualizarSessaoUI();
            this.mobileMenu = false;
            this.goHome();
            toastSucesso("Sessão encerrada com sucesso!");
        },

        primeiroNome(nome) { return (nome || "").split(" ")[0]; },
        temPainel() { return this.session.role === "PROFISSIONAL" || this.session.role === "ADMIN"; },
        irParaPainel() {
            if (this.session.role === "PROFISSIONAL") this.setView("dashboard");
            else if (this.session.role === "ADMIN") this.setView("admin");
        },

        /* ---------------- dashboard do profissional (RF06) ---------------- */
        toggleCategoriaDashboard(id) {
            this.dashCategoriaAviso = "";
            if (this.dash.categorias.includes(id)) {
                this.dash.categorias = this.dash.categorias.filter((c) => c !== id);
                return;
            }
            if (this.dash.categorias.length >= 3) {
                this.dashCategoriaAviso = "Limite de 3 categorias atingido. Remova uma para adicionar outra.";
                return;
            }
            this.dash.categorias.push(id);
        },
        salvarPerfilProfissional() {
            try {
                profissionalService.configurarPerfil(this.session.id, { categoriaIds: this.dash.categorias, endereco: this.dash.endereco });
                toastSucesso("Perfil profissional atualizado.");
            } catch (e) { toastErro(e.message); }
        },
        minhasAvaliacoes() { return this.session.id ? avaliacaoService.listarPorProfissional(this.session.id) : []; },
        minhaReputacao() {
            const avs = this.minhasAvaliacoes();
            if (avs.length === 0) return 0;
            return avs.reduce((acc, a) => acc + a.nota, 0) / avs.length;
        },

        /* ---------------- painel administrador (RF03–RF05, RF12) ---------------- */
        abrirModalCategoria(id = null) {
            if (id) {
                const cat = this.categorias.find((c) => c.id === id);
                this.categoriaModal = { open: true, id, nome: cat.nome, descricao: cat.descricao };
            } else {
                this.categoriaModal = { open: true, id: null, nome: "", descricao: "" };
            }
        },
        salvarCategoria() {
            try {
                const admin = { tipo: this.session.role };
                if (this.categoriaModal.id) {
                    categoriaService.editarCategoria(this.categoriaModal.id, this.categoriaModal.nome, this.categoriaModal.descricao, admin);
                    toastSucesso("Categoria atualizada.");
                } else {
                    categoriaService.criarCategoria(this.categoriaModal.nome, this.categoriaModal.descricao, admin);
                    toastSucesso("Categoria criada.");
                }
                this.categoriaModal.open = false;
            } catch (e) { toastErro(e.message); }
        },
        async inativarCategoria(id) {
            const ok = await confirmarAcao("Inativar categoria?", "Ela deixará de aparecer em novas buscas e vínculos.");
            if (!ok) return;
            try {
                categoriaService.inativarCategoria(id, { tipo: this.session.role });
                toastSucesso("Categoria inativada.");
            } catch (e) { toastErro(e.message); }
        },
        async inativarProfissional(id) {
            const ok = await confirmarAcao("Inativar conta do profissional?", "As avaliações vinculadas serão anonimizadas.");
            if (!ok) return;
            try {
                profissionalService.inativarConta(id, { tipo: this.session.role });
                toastSucesso("Conta inativada e avaliações anonimizadas.");
            } catch (e) { toastErro(e.message); }
        },
    };
}
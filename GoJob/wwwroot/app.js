/* =========================================================
   GOJOB — app.js
   Camada de UI + camada de API preparada para o back-end C#.
   Organização:
     1. Configuração e estado
     2. Camada de API (fetch real + fallback de demonstração)
     3. Utilitários (toast, estrelas, formatação)
     4. Dados de demonstração (usados só se a API não responder)
     5. Renderização (cards, categorias, perfil, avaliações)
     6. Modais e formulários
     7. Busca
     8. Animações (Lenis + GSAP)
     9. Inicialização
   ========================================================= */

/* =========================================================
   1. CONFIGURAÇÃO E ESTADO
   ========================================================= */
const CONFIG = {
  // Ajuste para a URL real da API C# (ex.: https://api.gojob.com.br/api)
  API_BASE_URL: "https://localhost:5001/api",
  HEALTHCHECK_TIMEOUT_MS: 2500,
};

const state = {
  apiOnline: false,      // true quando a API C# responde de fato
  usingMockData: false,  // true quando os dados vêm do fallback local
  categorias: [],
  profissionais: [],
  currentUser: null,     // { id, nome, tipoUsuario }
  activeProfileId: null, // profissional aberto no modal de perfil
};

/* =========================================================
   2. CAMADA DE API
   Toda função tenta a chamada real primeiro. Se a rede falhar
   (back-end C# fora do ar, CORS, etc.) cai no fallback de
   demonstração — para produção, basta remover os blocos
   marcados com "// DEV FALLBACK".
   ========================================================= */
const API = {
  async _request(path, options = {}) {
    const res = await fetch(`${CONFIG.API_BASE_URL}${path}`, {
      headers: { "Content-Type": "application/json", ...(options.headers || {}) },
      ...options,
    });

    let body = null;
    try { body = await res.json(); } catch (_) { /* corpo vazio */ }

    if (!res.ok) {
      // Espera-se do back-end C#: { exception: "EmailDuplicadoException", message: "..." }
      const error = new Error(body?.message || "Erro inesperado na API.");
      error.exception = body?.exception || null;
      error.status = res.status;
      throw error;
    }
    return body;
  },

  async healthcheck() {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), CONFIG.HEALTHCHECK_TIMEOUT_MS);
    try {
      await fetch(`${CONFIG.API_BASE_URL}/health`, { signal: controller.signal });
      clearTimeout(timer);
      return true;
    } catch (_) {
      clearTimeout(timer);
      return false;
    }
  },

  // RF01 — Cadastro Unificado de Usuários
  async cadastrarUsuario(dto) {
    if (state.apiOnline) {
      return this._request("/usuarios", { method: "POST", body: JSON.stringify(dto) });
    }
    return mockCadastrarUsuario(dto); // DEV FALLBACK
  },

  // RF02 — Autenticação
  async autenticar(email, senha) {
    if (state.apiOnline) {
      return this._request("/auth/login", { method: "POST", body: JSON.stringify({ email, senha }) });
    }
    return mockAutenticar(email, senha); // DEV FALLBACK
  },

  // Categorias ativas (suporta RF06 / RF07)
  async listarCategorias() {
    if (state.apiOnline) {
      return this._request("/categorias?ativa=true");
    }
    return mockListarCategorias(); // DEV FALLBACK
  },

  // RF07 — Busca e Filtragem de Profissionais
  async buscarProfissionais({ cidade, categoria, bairro }) {
    if (state.apiOnline) {
      const params = new URLSearchParams({ cidade });
      if (categoria) params.set("categoria", categoria);
      if (bairro) params.set("bairro", bairro);
      return this._request(`/profissionais?${params.toString()}`);
    }
    return mockBuscarProfissionais({ cidade, categoria, bairro }); // DEV FALLBACK
  },

  // RF10 — Exibição do Perfil Profissional
  async obterPerfilProfissional(id) {
    if (state.apiOnline) {
      return this._request(`/profissionais/${id}`);
    }
    return mockObterPerfil(id); // DEV FALLBACK
  },

  // RF08 — Registro de Avaliação
  async registrarAvaliacao(dto) {
    if (state.apiOnline) {
      return this._request("/avaliacoes", { method: "POST", body: JSON.stringify(dto) });
    }
    return mockRegistrarAvaliacao(dto); // DEV FALLBACK
  },

  // RF09 — Exclusão de Avaliação
  async excluirAvaliacao(idAvaliacao, idCliente) {
    if (state.apiOnline) {
      return this._request(`/avaliacoes/${idAvaliacao}`, {
        method: "DELETE",
        body: JSON.stringify({ idCliente }),
      });
    }
    return mockExcluirAvaliacao(idAvaliacao, idCliente); // DEV FALLBACK
  },
};

/* =========================================================
   3. UTILITÁRIOS
   ========================================================= */
function toast({ type = "success", title, message, duration = 5000 }) {
  const stack = document.getElementById("toast-stack");
  const el = document.createElement("div");
  el.className = `toast toast--${type}`;
  el.innerHTML = `
    <span class="toast__icon"><i data-lucide="${type === "error" ? "alert-circle" : "check-circle-2"}"></i></span>
    <div class="toast__body">
      <p class="toast__title">${title}</p>
      ${message ? `<p class="toast__message">${message}</p>` : ""}
    </div>
    <button type="button" class="toast__close" aria-label="Fechar notificação"><i data-lucide="x"></i></button>
  `;
  stack.appendChild(el);
  if (window.lucide) lucide.createIcons();

  const remove = () => {
    el.style.transition = "opacity 200ms ease, transform 200ms ease";
    el.style.opacity = "0";
    el.style.transform = "translateX(12px)";
    setTimeout(() => el.remove(), 200);
  };
  el.querySelector(".toast__close").addEventListener("click", remove);
  const timer = setTimeout(remove, duration);
  el.addEventListener("mouseenter", () => clearTimeout(timer));
}

// Traduz exceções de domínio do back-end (Parte 8 do documento GOJOB)
// em mensagens compreensíveis para o usuário final.
const EXCEPTION_MESSAGES = {
  EmailDuplicadoException: "Este e-mail já está cadastrado na plataforma.",
  UsuarioNaoEncontradoException: "E-mail ou senha inválidos.",
  ProfissionalNaoEncontradoException: "Este profissional não foi encontrado ou está inativo.",
  CategoriaNaoEncontradaException: "Categoria não encontrada.",
  CategoriaInativaException: "Esta categoria não está mais ativa.",
  LimiteCategoriasException: "É permitido escolher no máximo 3 categorias.",
  AvaliacaoInvalidaException: "Informe uma nota de 1 a 5 e um comentário.",
  AutoAvaliacaoException: "Não é possível avaliar o próprio perfil.",
  OperacaoNaoPermitidaException: "Você não tem permissão para executar esta ação.",
  DadosObrigatoriosException: "Preencha todos os campos obrigatórios.",
};

function handleApiError(error, fallbackTitle = "Não foi possível concluir a ação") {
  const message = EXCEPTION_MESSAGES[error.exception] || error.message || "Tente novamente em instantes.";
  toast({ type: "error", title: fallbackTitle, message });
}

function renderStars(container, media, count) {
  container.innerHTML = "";
  for (let i = 1; i <= 5; i++) {
    const icon = document.createElement("i");
    icon.dataset.lucide = "star";
    if (i <= Math.round(media)) icon.classList.add("is-filled");
    container.appendChild(icon);
  }
  if (typeof count === "number") {
    const span = document.createElement("span");
    span.className = "stars__count";
    span.textContent = media > 0 ? `${media.toFixed(1)} · ${count} avaliação${count === 1 ? "" : "es"}` : "Sem avaliações";
    container.appendChild(span);
  }
  if (window.lucide) lucide.createIcons();
}

function initials(nome) {
  return nome.split(" ").filter(Boolean).slice(0, 2).map((p) => p[0]).join("").toUpperCase();
}

function formatDate(iso) {
  return new Date(iso).toLocaleDateString("pt-BR", { day: "2-digit", month: "short", year: "numeric" });
}

/* =========================================================
   4. DADOS DE DEMONSTRAÇÃO
   Usados apenas quando a API C# real não está acessível,
   para que a interface permaneça navegável e demonstrável.
   ========================================================= */
const MOCK_CATEGORIES = [
  { id: 1, nome: "Elétrica", descricao: "Instalações e reparos elétricos residenciais", ativa: true },
  { id: 2, nome: "Hidráulica", descricao: "Encanamento, vazamentos e reparos", ativa: true },
  { id: 3, nome: "Limpeza", descricao: "Limpeza residencial e pós-obra", ativa: true },
  { id: 4, nome: "Pintura", descricao: "Pintura residencial e comercial", ativa: true },
  { id: 5, nome: "Jardinagem", descricao: "Manutenção de jardins e paisagismo", ativa: true },
  { id: 6, nome: "Marcenaria", descricao: "Móveis sob medida e reparos em madeira", ativa: true },
  { id: 7, nome: "Informática", descricao: "Manutenção de computadores e redes", ativa: true },
  { id: 8, nome: "Beleza", descricao: "Serviços de estética a domicílio", ativa: true },
];

const MOCK_PROFESSIONALS = [
  {
    id: 101, nome: "Marcos Aurélio Ferreira", cidade: "Birigui", bairro: "Centro",
    telefone: "(18) 99123-4567", categorias: [1, 7],
    avaliacoes: [
      { id: 1, cliente: "Renata C.", nota: 5, comentario: "Resolveu o curto-circuito no mesmo dia. Muito atencioso.", dataHora: "2026-08-02" },
      { id: 2, cliente: "João P.", nota: 4, comentario: "Bom serviço, chegou um pouco atrasado.", dataHora: "2026-07-14" },
    ],
  },
  {
    id: 102, nome: "Cláudia Regina Souza", cidade: "Birigui", bairro: "Jardim Cristina",
    telefone: "(18) 99876-5432", categorias: [3],
    avaliacoes: [
      { id: 3, cliente: "Marina T.", nota: 5, comentario: "Apartamento ficou impecável. Recomendo muito.", dataHora: "2026-09-01" },
      { id: 4, cliente: "Felipe A.", nota: 5, comentario: "Pontual e cuidadosa com os detalhes.", dataHora: "2026-08-20" },
      { id: 5, cliente: "Bianca R.", nota: 4, comentario: "Ótimo custo-benefício.", dataHora: "2026-07-30" },
    ],
  },
  {
    id: 103, nome: "Eduardo Lima Santos", cidade: "Birigui", bairro: "Vila Mendonça",
    telefone: "(18) 99222-1188", categorias: [2, 6],
    avaliacoes: [
      { id: 6, cliente: "Patrícia G.", nota: 3, comentario: "Resolveu o vazamento, mas demorou para retornar mensagens.", dataHora: "2026-06-18" },
    ],
  },
  {
    id: 104, nome: "Ana Beatriz Moraes", cidade: "Araçatuba", bairro: "Jardim Europa",
    telefone: "(18) 99345-7788", categorias: [4, 5],
    avaliacoes: [],
  },
  {
    id: 105, nome: "Ricardo Nogueira Alves", cidade: "Araçatuba", bairro: "Vila Mendonça",
    telefone: "(18) 99555-3321", categorias: [1],
    avaliacoes: [
      { id: 7, cliente: "Vanessa L.", nota: 5, comentario: "Excelente profissional, super indico.", dataHora: "2026-08-27" },
    ],
  },
  {
    id: 106, nome: "Fernanda Costa Ribeiro", cidade: "Birigui", bairro: "Centro",
    telefone: "(18) 99777-4455", categorias: [8],
    avaliacoes: [
      { id: 8, cliente: "Camila S.", nota: 5, comentario: "Atendimento a domicílio impecável.", dataHora: "2026-09-05" },
      { id: 9, cliente: "Letícia F.", nota: 4, comentario: "Muito boa, voltarei a chamar.", dataHora: "2026-08-11" },
    ],
  },
];

const mockUsers = []; // usuários criados durante a sessão de demonstração
let mockAvaliacaoSeq = 100;

function mockReputacao(prof) {
  if (prof.avaliacoes.length === 0) return 0;
  const soma = prof.avaliacoes.reduce((acc, a) => acc + a.nota, 0);
  return soma / prof.avaliacoes.length;
}

async function mockDelay(ms = 350) { return new Promise((r) => setTimeout(r, ms)); }

async function mockCadastrarUsuario(dto) {
  await mockDelay();
  const existente = mockUsers.find((u) => u.email === dto.email) ||
    MOCK_PROFESSIONALS.some((p) => p.nome === dto.nome);
  if (mockUsers.some((u) => u.email === dto.email)) {
    const err = new Error("E-mail já cadastrado.");
    err.exception = "EmailDuplicadoException";
    throw err;
  }
  const usuario = { id: Date.now(), nome: dto.nome, email: dto.email, tipoUsuario: dto.tipoUsuario };
  mockUsers.push(usuario);
  return usuario;
}

async function mockAutenticar(email, senha) {
  await mockDelay();
  const usuario = mockUsers.find((u) => u.email === email);
  if (!usuario) {
    const err = new Error("Credenciais inválidas.");
    err.exception = "UsuarioNaoEncontradoException";
    throw err;
  }
  return { id: usuario.id, nome: usuario.nome, tipoUsuario: usuario.tipoUsuario, token: "mock-token" };
}

async function mockListarCategorias() {
  await mockDelay(200);
  return MOCK_CATEGORIES.filter((c) => c.ativa);
}

async function mockBuscarProfissionais({ cidade, categoria, bairro }) {
  await mockDelay();
  const cidadeNorm = (cidade || "").trim().toLowerCase();
  return MOCK_PROFESSIONALS.filter((p) => {
    const matchCidade = !cidadeNorm || p.cidade.toLowerCase().includes(cidadeNorm);
    const matchCategoria = !categoria || p.categorias.includes(Number(categoria));
    const matchBairro = !bairro || p.bairro.toLowerCase().includes(bairro.trim().toLowerCase());
    return matchCidade && matchCategoria && matchBairro;
  }).map((p) => ({
    id: p.id,
    nome: p.nome,
    cidade: p.cidade,
    bairro: p.bairro,
    categorias: p.categorias,
    reputacaoMedia: mockReputacao(p),
    totalAvaliacoes: p.avaliacoes.length,
  }));
}

async function mockObterPerfil(id) {
  await mockDelay(250);
  const prof = MOCK_PROFESSIONALS.find((p) => p.id === Number(id));
  if (!prof) {
    const err = new Error("Profissional não encontrado.");
    err.exception = "ProfissionalNaoEncontradoException";
    throw err;
  }
  return {
    id: prof.id,
    nome: prof.nome,
    cidade: prof.cidade,
    bairro: prof.bairro,
    telefone: prof.telefone,
    categorias: prof.categorias,
    reputacaoMedia: mockReputacao(prof),
    avaliacoes: [...prof.avaliacoes].sort((a, b) => new Date(b.dataHora) - new Date(a.dataHora)),
  };
}

async function mockRegistrarAvaliacao({ idProfissional, idCliente, nota, comentario }) {
  await mockDelay();
  if (nota < 1 || nota > 5 || !comentario?.trim()) {
    const err = new Error("Avaliação inválida.");
    err.exception = "AvaliacaoInvalidaException";
    throw err;
  }
  const prof = MOCK_PROFESSIONALS.find((p) => p.id === Number(idProfissional));
  if (!prof) {
    const err = new Error("Profissional não encontrado.");
    err.exception = "ProfissionalNaoEncontradoException";
    throw err;
  }
  if (idCliente && prof.id === Number(idCliente)) {
    const err = new Error("Autoavaliação não permitida.");
    err.exception = "AutoAvaliacaoException";
    throw err;
  }
  const avaliacao = {
    id: mockAvaliacaoSeq++,
    cliente: state.currentUser?.nome || "Cliente demonstração",
    nota,
    comentario,
    dataHora: new Date().toISOString(),
  };
  prof.avaliacoes.push(avaliacao);
  return avaliacao;
}

async function mockExcluirAvaliacao(idAvaliacao) {
  await mockDelay();
  for (const prof of MOCK_PROFESSIONALS) {
    const idx = prof.avaliacoes.findIndex((a) => a.id === Number(idAvaliacao));
    if (idx >= 0) {
      prof.avaliacoes.splice(idx, 1);
      return { ok: true };
    }
  }
  const err = new Error("Avaliação não encontrada.");
  err.exception = "OperacaoNaoPermitidaException";
  throw err;
}

/* =========================================================
   5. RENDERIZAÇÃO
   ========================================================= */
function categoriaNome(id) {
  const cat = state.categorias.find((c) => c.id === Number(id));
  return cat ? cat.nome : "—";
}

function renderCategoriesInSelect() {
  const select = document.getElementById("select-categoria");
  state.categorias.forEach((cat) => {
    const opt = document.createElement("option");
    opt.value = cat.id;
    opt.textContent = cat.nome;
    select.appendChild(opt);
  });
}

function renderCategoriesList() {
  const list = document.getElementById("categories-list");
  list.innerHTML = "";
  state.categorias.forEach((cat) => {
    const count = state.profissionais.filter((p) => p.categorias?.includes(cat.id)).length;
    const pill = document.createElement("button");
    pill.type = "button";
    pill.className = "category-pill";
    pill.innerHTML = `<span>${cat.nome}</span><span class="category-pill__count">${count}</span>`;
    pill.addEventListener("click", () => {
      document.getElementById("select-categoria").value = cat.id;
      document.getElementById("search-form").requestSubmit();
      document.getElementById("buscar").scrollIntoView({ behavior: "smooth" });
    });
    list.appendChild(pill);
  });
}

function renderCategoriesCheckboxes() {
  const grid = document.getElementById("pro-categorias-grid");
  grid.innerHTML = "";
  state.categorias.forEach((cat) => {
    const label = document.createElement("label");
    label.className = "checkbox-item";
    label.innerHTML = `<input type="checkbox" name="categorias" value="${cat.id}"><span>${cat.nome}</span>`;
    grid.appendChild(label);
  });

  // RN05 — no máximo 3 categorias selecionáveis
  grid.addEventListener("change", () => {
    const checked = grid.querySelectorAll("input:checked");
    const all = grid.querySelectorAll("input");
    const erro = document.getElementById("pro-categorias-erro");
    const atingiuLimite = checked.length >= 3;
    all.forEach((input) => {
      const item = input.closest(".checkbox-item");
      if (!input.checked) {
        input.disabled = atingiuLimite;
        item.classList.toggle("is-disabled", atingiuLimite);
      }
    });
    erro.classList.toggle("is-hidden", checked.length <= 3);
  });
}

function renderProfessionalCard(prof) {
  const template = document.getElementById("template-card-profissional");
  const node = template.content.cloneNode(true);

  node.querySelector(".pro-card__avatar").textContent = initials(prof.nome);
  node.querySelector(".pro-card__name").textContent = prof.nome;
  node.querySelector(".pro-card__location-text").textContent = `${prof.bairro}, ${prof.cidade}`;

  const catsContainer = node.querySelector(".pro-card__categories");
  (prof.categorias || []).slice(0, 3).forEach((catId) => {
    const chip = document.createElement("span");
    chip.className = "chip";
    chip.textContent = categoriaNome(catId);
    catsContainer.appendChild(chip);
  });

  renderStars(node.querySelector(".pro-card__stars"), prof.reputacaoMedia || 0, prof.totalAvaliacoes || 0);

  node.querySelector(".pro-card__view").addEventListener("click", () => openProfileModal(prof.id));

  return node;
}

function renderResults(lista) {
  const grid = document.getElementById("results-grid");
  const empty = document.getElementById("empty-state");
  const meta = document.getElementById("results-meta");

  grid.innerHTML = "";

  if (lista.length === 0) {
    empty.classList.remove("is-hidden");
    meta.textContent = "";
    return;
  }
  empty.classList.add("is-hidden");
  meta.textContent = `${lista.length} profissional${lista.length === 1 ? "" : "is"} encontrado${lista.length === 1 ? "" : "s"}`;

  lista.forEach((prof) => grid.appendChild(renderProfessionalCard(prof)));
  if (window.lucide) lucide.createIcons();

  if (window.gsap) {
    gsap.from(grid.children, {
      opacity: 0, y: 14, duration: 0.45, ease: "power2.out", stagger: 0.04,
    });
  }
}

function updateStatsBar() {
  const cidades = new Set(state.profissionais.map((p) => p.cidade)).size;
  const totalReviews = state.profissionais.reduce((acc, p) => acc + (p.totalAvaliacoes || 0), 0);
  animateStatValue("stat-professionals", state.profissionais.length);
  animateStatValue("stat-categories", state.categorias.length);
  animateStatValue("stat-cities", cidades);
  animateStatValue("stat-reviews", totalReviews);
}

function animateStatValue(id, target) {
  const el = document.getElementById(id);
  const obj = { val: 0 };
  if (window.gsap) {
    gsap.to(obj, {
      val: target, duration: 0.8, ease: "power1.out",
      onUpdate: () => { el.textContent = Math.round(obj.val); },
    });
  } else {
    el.textContent = target;
  }
}

/* =========================================================
   6. MODAIS E FORMULÁRIOS
   ========================================================= */
function openModal(id) {
  const modal = document.getElementById(id);
  modal.classList.remove("is-hidden");
  document.body.style.overflow = "hidden";
  const firstField = modal.querySelector("input, select, textarea, button");
  if (firstField) firstField.focus({ preventScroll: true });
}
function closeModal(modal) {
  modal.classList.add("is-hidden");
  document.body.style.overflow = "";
}
document.querySelectorAll("[data-close-modal]").forEach((btn) => {
  btn.addEventListener("click", (e) => closeModal(e.target.closest(".modal-overlay")));
});
document.querySelectorAll(".modal-overlay").forEach((overlay) => {
  overlay.addEventListener("click", (e) => { if (e.target === overlay) closeModal(overlay); });
});
document.addEventListener("keydown", (e) => {
  if (e.key === "Escape") {
    document.querySelectorAll(".modal-overlay:not(.is-hidden)").forEach(closeModal);
  }
});

/* ---- Autenticação (RF01 / RF02) ---- */
function setAuthTab(tab) {
  document.querySelectorAll(".modal__tab").forEach((t) => t.classList.toggle("is-active", t.dataset.tab === tab));
  document.querySelectorAll(".auth-panel").forEach((p) => p.classList.toggle("is-active", p.dataset.panel === tab));
  const titles = { login: "Entrar na sua conta", cliente: "Criar conta de cliente", profissional: "Criar conta de profissional" };
  document.getElementById("auth-panel-title").textContent = titles[tab];
}
document.querySelectorAll("[data-auth-tab]").forEach((btn) => {
  btn.addEventListener("click", () => {
    openModal("modal-auth");
    setAuthTab(btn.dataset.authTab);
  });
});
document.querySelectorAll(".modal__tab").forEach((tab) => {
  tab.addEventListener("click", () => setAuthTab(tab.dataset.tab));
});

document.getElementById("form-login").addEventListener("submit", async (e) => {
  e.preventDefault();
  const form = e.target;
  try {
    const usuario = await API.autenticar(form.email.value.trim(), form.senha.value);
    onLoginSuccess(usuario);
    closeModal(document.getElementById("modal-auth"));
    form.reset();
  } catch (err) {
    handleApiError(err, "Não foi possível entrar");
  }
});

document.getElementById("form-cadastro-cliente").addEventListener("submit", async (e) => {
  e.preventDefault();
  await submitCadastro(e.target, "CLIENTE");
});
document.getElementById("form-cadastro-profissional").addEventListener("submit", async (e) => {
  e.preventDefault();

  const categoriasSelecionadas = Array.from(
    e.target.querySelectorAll('input[name="categorias"]:checked')
  ).map((i) => Number(i.value));

  if (categoriasSelecionadas.length === 0) {
    toast({ type: "error", title: "Selecione ao menos uma categoria", message: "Escolha até 3 categorias de atuação." });
    return;
  }

  await submitCadastro(e.target, "PROFISSIONAL", {
    categorias: categoriasSelecionadas,
    endereco: { cidade: e.target.cidade.value.trim(), bairro: e.target.bairro.value.trim() },
  });
});

async function submitCadastro(form, tipoUsuario, extra = {}) {
  const dto = {
    nome: form.nome.value.trim(),
    email: form.email.value.trim(),
    telefone: form.telefone.value.trim(),
    senha: form.senha.value,
    tipoUsuario,
    ...extra,
  };
  try {
    const usuario = await API.cadastrarUsuario(dto);
    toast({ type: "success", title: "Conta criada com sucesso", message: "Você já pode entrar com seu e-mail e senha." });
    closeModal(document.getElementById("modal-auth"));
    form.reset();
    onLoginSuccess({ ...usuario, nome: dto.nome, tipoUsuario });
  } catch (err) {
    handleApiError(err, "Não foi possível criar a conta");
  }
}

function onLoginSuccess(usuario) {
  state.currentUser = usuario;
  document.getElementById("btn-open-login").classList.add("is-hidden");
  document.getElementById("btn-open-signup").classList.add("is-hidden");
  const chip = document.getElementById("user-chip");
  chip.classList.remove("is-hidden");
  document.getElementById("user-chip-initial").textContent = initials(usuario.nome);
  document.getElementById("user-chip-name").textContent = usuario.nome.split(" ")[0];
  toast({ type: "success", title: `Bem-vindo(a), ${usuario.nome.split(" ")[0]}` });
}

document.getElementById("btn-logout").addEventListener("click", () => {
  state.currentUser = null;
  document.getElementById("btn-open-login").classList.remove("is-hidden");
  document.getElementById("btn-open-signup").classList.remove("is-hidden");
  document.getElementById("user-chip").classList.add("is-hidden");
  toast({ type: "success", title: "Sessão encerrada" });
});

/* ---- Perfil público (RF10) ---- */
async function openProfileModal(id) {
  try {
    const perfil = await API.obterPerfilProfissional(id);
    state.activeProfileId = perfil.id;

    document.getElementById("profile-avatar").textContent = initials(perfil.nome);
    document.getElementById("profile-title").textContent = perfil.nome;
    document.getElementById("profile-location").textContent = `${perfil.bairro}, ${perfil.cidade}`;
    renderStars(document.getElementById("profile-stars"), perfil.reputacaoMedia, perfil.avaliacoes.length);

    const catsEl = document.getElementById("profile-categories");
    catsEl.innerHTML = "";
    perfil.categorias.forEach((catId) => {
      const chip = document.createElement("span");
      chip.className = "chip";
      chip.textContent = categoriaNome(catId);
      catsEl.appendChild(chip);
    });

    document.getElementById("profile-contact").classList.add("is-hidden");
    document.getElementById("profile-contact").textContent = `Telefone: ${perfil.telefone}`;

    const list = document.getElementById("reviews-list");
    const emptyMsg = document.getElementById("reviews-empty");
    list.innerHTML = "";
    document.getElementById("profile-reviews-count").textContent = `(${perfil.avaliacoes.length})`;

    if (perfil.avaliacoes.length === 0) {
      emptyMsg.classList.remove("is-hidden");
    } else {
      emptyMsg.classList.add("is-hidden");
      perfil.avaliacoes.forEach((av) => {
        const li = document.createElement("li");
        li.className = "review-item";
        li.innerHTML = `
          <div class="review-item__head">
            <span class="review-item__author">${av.cliente || "Cliente anônimo"}</span>
            <span class="review-item__date">${formatDate(av.dataHora)}</span>
          </div>
          <div class="stars" data-mini-stars></div>
          <p class="review-item__comment">${av.comentario}</p>
        `;
        list.appendChild(li);
        renderStars(li.querySelector("[data-mini-stars]"), av.nota);
      });
    }

    document.getElementById("review-subtitle").textContent = `Avaliando: ${perfil.nome}`;
    openModal("modal-profile");
    if (window.lucide) lucide.createIcons();
  } catch (err) {
    handleApiError(err, "Não foi possível abrir o perfil");
  }
}

document.getElementById("btn-show-contact").addEventListener("click", () => {
  document.getElementById("profile-contact").classList.remove("is-hidden");
});

document.getElementById("btn-open-review").addEventListener("click", () => {
  if (!state.currentUser) {
    toast({ type: "error", title: "Entre na sua conta", message: "É preciso estar autenticado como cliente para avaliar." });
    openModal("modal-auth");
    setAuthTab("login");
    return;
  }
  resetStarPicker();
  openModal("modal-review");
});

/* ---- Avaliação (RF08) ---- */
const starPicker = document.getElementById("star-picker");
function resetStarPicker() {
  document.getElementById("review-nota").value = "0";
  document.getElementById("form-review").reset();
  starPicker.querySelectorAll(".star-picker__btn").forEach((b) => b.classList.remove("is-active"));
  if (window.lucide) lucide.createIcons();
}
starPicker.addEventListener("click", (e) => {
  const btn = e.target.closest(".star-picker__btn");
  if (!btn) return;
  const value = Number(btn.dataset.value);
  document.getElementById("review-nota").value = value;
  starPicker.querySelectorAll(".star-picker__btn").forEach((b) => {
    b.classList.toggle("is-active", Number(b.dataset.value) <= value);
  });
});

document.getElementById("form-review").addEventListener("submit", async (e) => {
  e.preventDefault();
  const nota = Number(document.getElementById("review-nota").value);
  const comentario = document.getElementById("review-comentario").value.trim();

  if (nota < 1 || nota > 5) {
    toast({ type: "error", title: "Selecione uma nota", message: "Escolha de 1 a 5 estrelas antes de enviar." });
    return;
  }
  if (!comentario) {
    toast({ type: "error", title: "Comentário obrigatório", message: "Conte brevemente como foi o serviço." });
    return;
  }

  try {
    await API.registrarAvaliacao({
      idProfissional: state.activeProfileId,
      idCliente: state.currentUser?.id,
      nota,
      comentario,
    });
    toast({ type: "success", title: "Avaliação registrada", message: "A reputação do profissional foi atualizada." });
    closeModal(document.getElementById("modal-review"));
    openProfileModal(state.activeProfileId); // recarrega com reputação recalculada (RF11)
    runSearch(); // atualiza a lista de resultados em segundo plano
  } catch (err) {
    handleApiError(err, "Não foi possível registrar a avaliação");
  }
});

/* =========================================================
   7. BUSCA (RF07)
   ========================================================= */
async function runSearch() {
  const cidade = document.getElementById("input-cidade").value.trim();
  const categoria = document.getElementById("select-categoria").value;
  const bairro = document.getElementById("input-bairro").value.trim();

  const meta = document.getElementById("results-meta");
  meta.textContent = "Buscando...";

  try {
    const resultado = await API.buscarProfissionais({ cidade, categoria, bairro });
    state.profissionais = resultado;
    renderResults(resultado);
  } catch (err) {
    handleApiError(err, "Não foi possível concluir a busca");
    renderResults([]);
  }
}

document.getElementById("search-form").addEventListener("submit", (e) => {
  e.preventDefault();
  runSearch();
});

/* =========================================================
   8. ANIMAÇÕES — Lenis (scroll suave) + GSAP (revelações)
   ========================================================= */
function initSmoothScrollAndReveals() {
  if (window.Lenis) {
    const lenis = new Lenis({ duration: 1.1, smoothWheel: true });
    function raf(time) { lenis.raf(time); requestAnimationFrame(raf); }
    requestAnimationFrame(raf);
    if (window.gsap && window.ScrollTrigger) {
      lenis.on("scroll", ScrollTrigger.update);
      gsap.ticker.add((time) => lenis.raf(time * 1000));
      gsap.ticker.lagSmoothing(0);
    }
  }

  if (window.gsap && window.ScrollTrigger) {
    gsap.registerPlugin(ScrollTrigger);
    document.querySelectorAll("[data-reveal]").forEach((el) => {
      gsap.from(el, {
        opacity: 0,
        y: 24,
        duration: 0.7,
        ease: "power2.out",
        scrollTrigger: { trigger: el, start: "top 88%", once: true },
      });
    });
  }
}

/* =========================================================
   9. INICIALIZAÇÃO
   ========================================================= */
async function checkApiStatus() {
  const dot = document.getElementById("api-status-dot");
  const label = document.getElementById("api-status-label");
  const wrapper = document.getElementById("api-status");

  const online = await API.healthcheck();
  state.apiOnline = online;
  state.usingMockData = !online;

  wrapper.classList.toggle("is-online", online);
  wrapper.classList.toggle("is-offline", false);
  label.textContent = online ? "API conectada" : "modo demonstração";
  wrapper.title = online
    ? "Conectado à API C# em " + CONFIG.API_BASE_URL
    : "API C# não encontrada — exibindo dados de demonstração locais";
}

async function bootstrap() {
  if (window.lucide) lucide.createIcons();

  await checkApiStatus();

  try {
    state.categorias = await API.listarCategorias();
    renderCategoriesInSelect();
    renderCategoriesCheckboxes();
  } catch (err) {
    handleApiError(err, "Não foi possível carregar as categorias");
  }

  try {
    state.profissionais = await API.buscarProfissionais({ cidade: "" });
  } catch (_) {
    state.profissionais = [];
  }

  renderCategoriesList();
  updateStatsBar();
  renderResults(state.profissionais);

  initSmoothScrollAndReveals();

  if (state.usingMockData) {
    toast({
      type: "success",
      title: "Modo demonstração ativo",
      message: "A API C# não foi encontrada em " + CONFIG.API_BASE_URL + ". Exibindo dados de exemplo.",
      duration: 7000,
    });
  }
}

document.addEventListener("DOMContentLoaded", bootstrap);

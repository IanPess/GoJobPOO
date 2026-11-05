using Microsoft.EntityFrameworkCore;
using GoJob.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Conexão com o Banco de Dados no Supabase via PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Registra os Controllers e OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Comentado para evitar o aviso de porta HTTPS em ambientes de container (Render)
// app.UseHttpsRedirection();

// 3. Procura automaticamente arquivos padrão (como index.html) na pasta wwwroot
app.UseDefaultFiles();

// 4. Serve os arquivos estáticos (HTML, CSS, JS) da pasta wwwroot
app.UseStaticFiles();

// Comentado enquanto a autenticação por esquema não estiver ativa
// app.UseAuthorization();

// 5. Mapeia os endpoints dos Controllers
app.MapControllers();

app.Run();
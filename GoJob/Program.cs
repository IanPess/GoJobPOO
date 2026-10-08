using Microsoft.EntityFrameworkCore;
using GoJob.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Conexão com o Banco de Dados no Supabase via PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Registra o suporte a Controllers
builder.Services.AddControllers();

// Suporte para documentação OpenAPI/Swagger
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 3. Permite servir arquivos do front-end (HTML, CSS, JS) da pasta wwwroot
app.UseStaticFiles();

app.UseAuthorization();

// 4. Mapeia os Controllers para responderem aos endpoints
app.MapControllers();

app.Run();
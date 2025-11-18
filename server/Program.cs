using EncryptionApp.Api.Global.Exceptions;
using EncryptionApp.Config;

var builder = WebApplication.CreateBuilder(args);

builder.AddDbContext();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.AddRateLimiting();

builder.AddSecurityConfig();
builder.AddIdentityConfig();
builder.AddAuthConfig();
builder.Services.AddAuthorization();

builder.AddServicesConfig();
builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
} else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseRouting();
app.UseRateLimiter();
app.UseCors("AllowSpecificOrigin");
app.UseAuthorization();
app.MapControllers();

app.Run();

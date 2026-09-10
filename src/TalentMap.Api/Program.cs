using TalentMap.Api.Extensions;
using TalentMap.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMongoDb(builder.Configuration);
builder.Services.AddSingleton<IMoldovaBorderValidator, MoldovaBorderValidator>();

var app = builder.Build();

app.LogMongoRegistration();
await app.CheckMongoConnectivityAsync();
await app.EnsureMapPointIndexesAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

using ElectionProcessing.Api.Application;
using ElectionProcessing.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElectionProcessingCore(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapElectionProcessingEndpoints();

app.Run();

public partial class Program;

public partial class Program;

using ElectionAuthorization.Api.Application;
using ElectionAuthorization.Api.Data;
using ElectionAuthorization.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddElectionAuthorizationCore(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
    // Local scope: create schema automatically for component bring-up.
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapElectionAuthorizationEndpoints();

app.Run();

public partial class Program;

using WatcherAudit.Api.Application;
using WatcherAudit.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "CHAINBALLOT_");
builder.Services.AddWatcherAuditApi(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapWatcherEndpoints();

app.Run();

public partial class Program;

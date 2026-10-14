using TransparencyAudit.Api.Application;
using TransparencyAudit.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "CHAINBALLOT_");
builder.Services.AddTransparencyAuditApi(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapTransparencyEndpoints();

app.Run();

public partial class Program;

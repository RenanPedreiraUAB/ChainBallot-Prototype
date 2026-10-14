using ThresholdDecryption.Api.Application;
using ThresholdDecryption.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "CHAINBALLOT_");
builder.Services.AddThresholdDecryptionApi(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapThresholdEndpoints();

app.Run();

public partial class Program;

using DynamicCube.Hosting;

// Internal API: employees, builders and Apps Control. Reachable only from the entity's internal network.
var builder = WebApplication.CreateBuilder(args);

builder.AddDynamicCubeApi(new ApiHostOptions
{
    AppCode = "INTERNAL",
    AuthenticationSection = "Authentication:Internal",
});

var app = builder.Build();
app.MapDynamicCubeDefaults();

app.Run();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;

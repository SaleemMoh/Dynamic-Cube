using DynamicCube.Hosting;

// External API: applicant-facing endpoints only (apply, track, pay, verify). Internet-facing, deployed in the DMZ.
var builder = WebApplication.CreateBuilder(args);

builder.AddDynamicCubeApi(new ApiHostOptions
{
    AppCode = "EXTERNAL",
    AuthenticationSection = "Authentication:External",
});

var app = builder.Build();
app.MapDynamicCubeDefaults();

app.Run();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;

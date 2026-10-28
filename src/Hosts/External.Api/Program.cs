using DynamicCube.Hosting;
using DynamicCube.Modules.AppsControl;
using DynamicCube.Modules.Decisions;
using DynamicCube.Modules.Documents;
using DynamicCube.Modules.Integration;
using DynamicCube.Modules.Parties;
using DynamicCube.Modules.Services;
using DynamicCube.Modules.Workflow;

// External API: applicant-facing endpoints only (apply, track, pay, verify). Internet-facing, deployed in the DMZ.
var builder = WebApplication.CreateBuilder(args);

builder.AddDynamicCubeApi(new ApiHostOptions
{
    AppCode = "EXTERNAL",
    AuthenticationSection = "Authentication:External",
});
builder.Services.AddAppsControl(builder.Configuration, managesUsers: false);
builder.Services.AddIntegration(builder.Configuration);
builder.Services.AddParties(builder.Configuration);
builder.Services.AddServices(builder.Configuration);
builder.Services.AddWorkflow(builder.Configuration);
builder.Services.AddDocuments(builder.Configuration);
builder.Services.AddDecisions(builder.Configuration);

var app = builder.Build();
app.MapDynamicCubeDefaults();
app.MapAppsControlExternal();
app.MapPartiesExternal();
app.MapServicesExternal();
app.MapDocumentsExternal();
app.MapDecisionsExternal();

app.Run();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;

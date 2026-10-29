using DynamicCube.Hosting;
using DynamicCube.Modules.AppsControl;
using DynamicCube.Modules.Communication;
using DynamicCube.Modules.Content;
using DynamicCube.Modules.Decisions;
using DynamicCube.Modules.Documents;
using DynamicCube.Modules.Integration;
using DynamicCube.Modules.Parties;
using DynamicCube.Modules.Payments;
using DynamicCube.Modules.Services;
using DynamicCube.Modules.Workflow;

// Internal API: employees, builders and Apps Control. Reachable only from the entity's internal network.
var builder = WebApplication.CreateBuilder(args);

builder.AddDynamicCubeApi(new ApiHostOptions
{
    AppCode = "INTERNAL",
    AuthenticationSection = "Authentication:Internal",
});
builder.Services.AddAppsControl(builder.Configuration, managesUsers: true);
builder.Services.AddIntegration(builder.Configuration);
builder.Services.AddParties(builder.Configuration);
builder.Services.AddServices(builder.Configuration);
builder.Services.AddWorkflow(builder.Configuration);
builder.Services.AddDocuments(builder.Configuration);
builder.Services.AddDecisions(builder.Configuration);
builder.Services.AddCommunication(builder.Configuration);
builder.Services.AddPayments(builder.Configuration);
builder.Services.AddContent(builder.Configuration);

var app = builder.Build();
app.MapDynamicCubeDefaults();
app.MapAppsControlInternal();
app.MapIntegrationInternal();
app.MapPartiesInternal();
app.MapServicesInternal();
app.MapWorkflowInternal();
app.MapDocumentsInternal();
app.MapDecisionsInternal();
app.MapCommunicationInternal();
app.MapPaymentsInternal();

app.Run();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;

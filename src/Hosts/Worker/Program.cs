using DynamicCube.Hosting;
using DynamicCube.Modules.AppsControl;
using DynamicCube.Modules.Communication;
using DynamicCube.Modules.Decisions;
using DynamicCube.Modules.Documents;
using DynamicCube.Modules.Integration;
using DynamicCube.Modules.Parties;
using DynamicCube.Modules.Payments;
using DynamicCube.Modules.Visits;
using DynamicCube.Modules.Services;
using DynamicCube.Modules.Workflow;
using DynamicCube.Platform.Security;
using DynamicCube.Worker;

// Worker: outbox delivery (PDF printing, notifications, integration pushes), SLA timers and scheduled jobs.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.AddDynamicCubeDefaults("WORKER");
builder.Services.AddSingleton<ICurrentUser>(new SystemUser("WORKER"));
builder.Services.AddHostedService<OutboxBackgroundService>();
builder.Services.AddAppsControl(builder.Configuration, managesUsers: false);
builder.Services.AddIntegration(builder.Configuration);
builder.Services.AddParties(builder.Configuration);
builder.Services.AddServices(builder.Configuration);
builder.Services.AddWorkflow(builder.Configuration);
builder.Services.AddDocuments(builder.Configuration);
builder.Services.AddDecisions(builder.Configuration);
builder.Services.AddCommunication(builder.Configuration);
builder.Services.AddPayments(builder.Configuration);
builder.Services.AddVisits(builder.Configuration);
builder.Services.AddHostedService<SlaBackgroundService>();
builder.Services.AddHostedService<DocumentDatesBackgroundService>();
builder.Services.AddHostedService<BillExpiryBackgroundService>();
builder.Services.AddHostedService<ExternalVisitsBackgroundService>();

builder.Build().Run();

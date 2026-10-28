using DynamicCube.Hosting;
using DynamicCube.Modules.AppsControl;
using DynamicCube.Modules.Workflow;
using DynamicCube.Platform.Security;
using DynamicCube.Worker;

// Worker: outbox delivery, notifications, integration pushes, SLA timers and scheduled jobs.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.AddDynamicCubeDefaults("WORKER");
builder.Services.AddSingleton<ICurrentUser>(new SystemUser("WORKER"));
builder.Services.AddHostedService<OutboxBackgroundService>();
builder.Services.AddAppsControl(builder.Configuration, managesUsers: false);
builder.Services.AddWorkflow(builder.Configuration);
builder.Services.AddHostedService<SlaBackgroundService>();

builder.Build().Run();

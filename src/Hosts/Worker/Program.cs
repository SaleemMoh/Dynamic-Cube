using DynamicCube.Hosting;
using DynamicCube.Platform.Security;
using DynamicCube.Worker;

// Worker: outbox delivery, notifications, integration pushes, SLA timers and scheduled jobs.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.AddDynamicCubeDefaults("WORKER");
builder.Services.AddSingleton<ICurrentUser>(new SystemUser("WORKER"));
builder.Services.AddHostedService<OutboxBackgroundService>();

builder.Build().Run();

using DynamicCube.Platform;
using DynamicCube.Platform.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.AppsControl.Tests.Infrastructure;

public static class TestServices
{
    public static ServiceProvider Build(string connectionString, TestUser user, IDictionary<string, string?>? settings = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DynamicCube"] = connectionString,
            ["ChangeSets:RequireApprovalByDefault"] = "true",
            ["AppsControl:BootstrapUsers:admin:0"] = "SYSTEM_ADMIN",
            ["AppsControl:BootstrapUsers:maker:0"] = "CONFIG_MAKER",
            ["AppsControl:BootstrapUsers:checker:0"] = "CONFIG_CHECKER",
            ["AppsControl:AccessCacheSeconds"] = "1",
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(user);
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<TestUser>());
        services.AddPlatform(configuration);
        services.AddAppsControl(configuration, managesUsers: true);
        return services.BuildServiceProvider(validateScopes: true);
    }
}

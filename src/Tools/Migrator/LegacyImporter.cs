using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.AppsControl.Accounts;
using DynamicCube.Modules.AppsControl.Seeding;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DynamicCube.Migrator;

/// <summary>
/// Imports the CSV export of the legacy Apps Control (Oracle) into Apps Control (docs/handover/legacy-data-import-plan.md):
/// instances, companies, apps, classes and values, lists of values, menus, roles with their menus and list grants, and
/// users with their roles, keyed like the legacy tables. The data goes through change sets like any package. The first
/// import into an installation replaces what is there (the demo); later runs update and add. Passwords, tokens and
/// connection settings of the export are never read. Users get a sign-in account with a one-time password, written to
/// a file in the folder for the administrator to hand out.
/// </summary>
internal sealed partial class LegacyImporter(IServiceProvider services, PackageLoader loader, Action<string?> actAs, ILogger logger)
{
    /// <summary>The legacy app that is the legacy Apps Control itself, replaced by Dynamic Cube: not imported.</summary>
    private const string ConfigurationCenter = "5";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string[] All = ["*"];

    private static readonly string[] ViewOnly = ["VIEW"];

    /// <summary>What was left out, and why; written to the folder with the result.</summary>
    private readonly List<string> _skipped = [];

    private string _folder = string.Empty;

    /// <summary>CREATE on an import (rows already there are left alone); UPSERT when the export is applied again.</summary>
    private string _operation = "CREATE";

    /// <summary>
    /// Imports the export the first time (replacing the installation's data); later runs only add missing sign-in
    /// accounts, unless <paramref name="refresh"/> asks to apply the export again over the data.
    /// </summary>
    public async Task RunAsync(string folder, bool refresh, CancellationToken cancellationToken)
    {
        _folder = folder;
        _operation = refresh ? "UPSERT" : "CREATE";
        var data = new Legacy(folder);
        var packages = Build(data);
        var all = packages.SelectMany(p => p.Items).ToList();
        var first = all.First(i => i.EntityType == "apps.company");
        // Imported before when its first company exists; finished when its last row exists too (the loader checks rows to create).
        Task<bool> Exists(ConfigPackageItem item) =>
            loader.IsLoadedAsync([new ConfigPackage("check", [item with { Operation = "CREATE" }])], a => actAs(a), cancellationToken);
        var started = await Exists(first);
        if (!started)
        {
            logger.LogWarning("Legacy import: first import into this installation, replacing its data");
            await ReplaceAsync(cancellationToken);
        }

        if (!started || refresh || !await Exists(all[^1]))
        {
            // A run stopped part way carries on: rows already created are left alone.
            await loader.LoadAsync(packages, a => actAs(a), cancellationToken);
        }
        else
        {
            logger.LogInformation("Legacy import: imported before; only missing sign-in accounts are created (LegacyImport:Refresh=true applies the export again)");
        }

        actAs(null);
        var accounts = new List<(string UserName, string? Email, string Password)>();
        try
        {
            await AccountsAsync(accounts, cancellationToken);
        }
        finally
        {
            // Written even when a run stops part way, so no one-time password is lost.
            await WriteReportAsync(packages, accounts, CancellationToken.None);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Legacy import: {Items} item(s) loaded, {Accounts} sign-in account(s) created, {Skipped} line(s) skipped (see {Folder}/import-report.txt)",
                packages.Sum(p => p.Items.Count), accounts.Count, _skipped.Count, folder);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Replace: every module's data goes; what the product itself needs is seeded again.

    private async Task ReplaceAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DynamicCube.Modules.AppsControl.Persistence.AppsControlDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """
            DO $replace$
            DECLARE t record;
            BEGIN
              FOR t IN
                SELECT table_schema, table_name FROM information_schema.tables
                WHERE table_type = 'BASE TABLE'
                  AND table_schema IN ('apps', 'audit', 'cfg', 'cms', 'com', 'dec', 'doc', 'integ', 'party', 'pay', 'rpt', 'svc', 'vis', 'wf', 'platform')
                  AND table_name NOT IN ('__ef_migrations_history', 'data_protection_keys')
              LOOP
                EXECUTE format('TRUNCATE TABLE %I.%I RESTART IDENTITY CASCADE', t.table_schema, t.table_name);
              END LOOP;
            END $replace$;
            """,
            cancellationToken);
        await scope.ServiceProvider.GetRequiredService<AppsControlSeeder>().SeedAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The export, read once.

    private sealed class Legacy(string folder)
    {
        public IReadOnlyList<IReadOnlyDictionary<string, string>> this[string table] =>
            _tables.TryGetValue(table, out var rows) ? rows : _tables[table] = LegacyCsv.Read(folder, table);

        private readonly Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> _tables = new(StringComparer.Ordinal);
    }

    private static string V(IReadOnlyDictionary<string, string> row, string column) =>
        row.TryGetValue(column, out var value) ? value.Trim() : string.Empty;

    private static bool Flag(string value, bool empty = true) => value.Length == 0 ? empty : value is "1" or "Y" or "y";

    private static int Number(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? (int)Math.Clamp(number, int.MinValue, int.MaxValue) : 0;

    /// <summary>Both names are required: one falls back on the other, then on the code.</summary>
    private static (string Ar, string En) Names(string ar, string en, string code)
    {
        ar = Clip(ar, 500);
        en = Clip(en, 500);
        return (ar.Length > 0 ? ar : en.Length > 0 ? en : code, en.Length > 0 ? en : ar.Length > 0 ? ar : code);
    }

    private static string Clip(string value, int length) => value.Length > length ? value[..length] : value;

    private ConfigPackageItem Item(string type, string code, object payload) =>
        new(type, code, _operation, JsonSerializer.SerializeToElement(payload, Json));

    // ---------------------------------------------------------------------------------------------------------------
    // The packages, in the order they depend on each other.

    private List<ConfigPackage> Build(Legacy data)
    {
        var instances = data["SYS_INSTANCEPROFILE"];
        var instanceCodes = instances.Select(r => V(r, "INSTID")).Where(c => c.Length > 0).ToHashSet(StringComparer.Ordinal);
        var packages = new List<ConfigPackage>
        {
            new("Legacy Apps Control: instances", [.. instances.Where(r => V(r, "INSTID").Length > 0).Select(r =>
            {
                var code = V(r, "INSTID");
                var (ar, en) = Names(V(r, "INSTNAME_NTV"), V(r, "INSTNAME_FRN"), code);
                return Item("apps.entity", code, new { nameAr = ar, nameEn = en, isActive = Flag(V(r, "ACTIVEFLAG")) });
            })]),
        };

        // Companies: instance and company number.
        var companies = new HashSet<string>(StringComparer.Ordinal);
        var companyItems = new List<ConfigPackageItem>();
        foreach (var r in data["SYS_COMPANY_PROFILE"])
        {
            var (instid, number) = (V(r, "INSTID"), V(r, "CMP_NUM"));
            if (!instanceCodes.Contains(instid))
            {
                Skip("SYS_COMPANY_PROFILE", $"{instid}/{number}", "its instance is not in SYS_INSTANCEPROFILE");
                continue;
            }

            var key = $"{instid}/{number}";
            if (companies.Add(key))
            {
                var (ar, en) = Names(V(r, "CMP_DES_NTV"), V(r, "CMP_DES_FRN"), key);
                companyItems.Add(Item("apps.company", key, new { nameAr = ar, nameEn = en, isActive = Flag(V(r, "ACTIVE_FLAG")) }));
            }
        }

        packages.Add(new("Legacy Apps Control: companies", companyItems));

        // Apps: the legacy apps run on their own screens (apps managed by Dynamic Cube), in the companies SYS_APPS_COMPANY lists.
        var apps = new HashSet<string>(StringComparer.Ordinal);
        var appItems = new List<ConfigPackageItem>();
        foreach (var r in data["SYS_APPS_PROFILE"])
        {
            var code = V(r, "APPSID");
            if (code == ConfigurationCenter)
            {
                Skip("SYS_APPS_PROFILE", code, "the legacy Configuration Center is replaced by Dynamic Cube (owner's decision)");
                continue;
            }

            var appCompanies = data["SYS_APPS_COMPANY"].Where(c => V(c, "APPSID") == code)
                .Select(c => $"{V(c, "INSTID")}/{V(c, "CMP_NUM")}").Where(companies.Contains).Distinct().Order(StringComparer.Ordinal).ToList();
            var (ar, en) = Names(V(r, "APPS_DES_NTV"), V(r, "APPS_DES_FRN"), code);
            apps.Add(code);
            appItems.Add(Item("apps.business-app", code, new
            {
                nameAr = ar,
                nameEn = en,
                isActive = Flag(V(r, "ACTIVE_FLAG")),
                entityCode = string.Empty,
                kind = "CONNECTED",
                companies = appCompanies.Count > 0 ? appCompanies : [.. All],
                branches = All,
            }));
        }

        packages.Add(new("Legacy Apps Control: apps", appItems));
        var (classItems, valueItems, classes, values) = Dictionary(data, companies);
        packages.Add(new("Legacy Apps Control: classes", classItems));
        packages.Add(new("Legacy Apps Control: class values", valueItems));
        var (lovItems, lovs) = Lovs(data, classes);
        packages.Add(new("Legacy Apps Control: lists of values", lovItems));
        var (screenItems, menuItems, menus) = Menus(data, apps);
        packages.Add(new("Legacy Apps Control: screens", screenItems));
        packages.Add(new("Legacy Apps Control: menus", menuItems));
        var (roleItems, roles) = Roles(data, companies, menus, lovs, values);
        packages.Add(new("Legacy Apps Control: roles", roleItems));
        packages.Add(new("Legacy Apps Control: users", Users(data, instanceCodes, roles)));
        return packages;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // DD_CLASSES, DD_DOMAIN, DD_COMPANY_DOMAIN.

    private (List<ConfigPackageItem> Classes, List<ConfigPackageItem> Values, HashSet<string> ClassCodes, Dictionary<string, HashSet<string>> ValueCodes) Dictionary(
        Legacy data, HashSet<string> companies)
    {
        var classRows = data["DD_CLASSES"].Where(r => V(r, "CLASS_CODE").Length > 0).ToList();

        // Two classes whose codes differ only in case would share one report view: the one with values is kept.
        var valueCount = data["DD_DOMAIN"].Concat(data["DD_COMPANY_DOMAIN"]).GroupBy(r => V(r, "CLASS_CODE"), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var clash in classRows.GroupBy(r => V(r, "CLASS_CODE").ToLowerInvariant().Replace('-', '_'), StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            foreach (var dropped in clash.OrderByDescending(r => valueCount.GetValueOrDefault(V(r, "CLASS_CODE"))).Skip(1).ToList())
            {
                Skip("DD_CLASSES", V(dropped, "CLASS_CODE"), $"its code differs only in case from {V(clash.OrderByDescending(r => valueCount.GetValueOrDefault(V(r, "CLASS_CODE"))).First(), "CLASS_CODE")}, which has the values");
                classRows.Remove(dropped);
            }
        }

        var classCodes = classRows.Select(r => V(r, "CLASS_CODE")).ToHashSet(StringComparer.Ordinal);
        var system = data["DD_DOMAIN"].Where(r => classCodes.Contains(V(r, "CLASS_CODE"))).ToList();
        var company = new List<IReadOnlyDictionary<string, string>>();
        foreach (var r in data["DD_COMPANY_DOMAIN"])
        {
            var key = $"{V(r, "INSTID")}/{V(r, "CMP_NUM")}";
            if (!classCodes.Contains(V(r, "CLASS_CODE")))
            {
                Skip("DD_COMPANY_DOMAIN", $"{V(r, "CLASS_CODE")}/{key}/{V(r, "VALUE")}", "its class is not in DD_CLASSES");
            }
            else if (!companies.Contains(key))
            {
                Skip("DD_COMPANY_DOMAIN", $"{V(r, "CLASS_CODE")}/{key}/{V(r, "VALUE")}", $"company {key} is not in SYS_COMPANY_PROFILE");
            }
            else
            {
                company.Add(r);
            }
        }

        foreach (var r in data["DD_COMPANY_DOMAIN_VIRTUAL_VALUES"])
        {
            Skip("DD_COMPANY_DOMAIN_VIRTUAL_VALUES", $"{V(r, "CLASS_CODE")}/{V(r, "VALUE")}", "virtual list values are not carried (2 rows)");
        }

        var valueCodes = classCodes.ToDictionary(c => c, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var r in system.Concat(company))
        {
            valueCodes[V(r, "CLASS_CODE")].Add(V(r, "VALUE"));
        }

        // The flex fields of a class become typed attributes (FLEX_VALUE_n): those with a label, and those values use.
        var attributes = new Dictionary<string, List<(string Code, int Slot, string Ar, string En, string Type, string? RefClass, bool Multiple)>>(StringComparer.Ordinal);
        var classItems = new List<ConfigPackageItem>();
        foreach (var r in classRows)
        {
            var code = V(r, "CLASS_CODE");
            var rows = system.Concat(company).Where(v => V(v, "CLASS_CODE") == code).ToList();
            var definitions = new List<(string, int, string, string, string, string?, bool)>();
            for (var slot = 1; slot <= 10; slot++)
            {
                var ar = FirstOf(V(r, $"DOMAIN_FLEX_VALUE_{slot}_LABEL_NTV"), V(r, $"CLASS_FLEX_VALUE_{slot}_DESC_NTV"));
                var en = FirstOf(V(r, $"DOMAIN_FLEX_VALUE_{slot}_LABEL_FRN"), V(r, $"CLASS_FLEX_VALUE_{slot}_DESC_FRN"));
                var used = rows.Select(v => V(v, $"FLEX_VALUE_{slot}")).Where(v => v.Length > 0).ToList();
                if (ar.Length == 0 && en.Length == 0 && used.Count == 0)
                {
                    continue;
                }

                var kind = V(r, $"DOMAIN_FLEX_VALUE_{slot}_TYPE").ToLowerInvariant();
                var refClass = V(r, $"DOMAIN_FLEX_VALUE_{slot}_CLASS_CODE");
                var multiple = kind == "select-multiple";
                var parts = used.SelectMany(v => multiple ? v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [v]).ToList();

                // A pick from another class when every value used is one of its values; plain text otherwise (also for a pick
                // from the class itself, whose values arrive together).
                var isRef = kind.StartsWith("select", StringComparison.Ordinal) && refClass.Length > 0 && refClass != code && valueCodes.TryGetValue(refClass, out var known)
                    && parts.All(known.Contains);
                var type = isRef ? "DOMAIN_REF" : kind == "textarea" ? "TEXTAREA" : "TEXT";
                definitions.Add(($"FLEX_VALUE_{slot}", slot, ar.Length > 0 ? ar : en.Length > 0 ? en : $"قيمة إضافية {slot}", en.Length > 0 ? en : ar.Length > 0 ? ar : $"Flex value {slot}",
                    type, isRef ? refClass : null, isRef && multiple));
            }

            attributes[code] = definitions;
            var (nameAr, nameEn) = Names(V(r, "CLASS_DESC_NTV"), V(r, "CLASS_DESC_FRN"), code);
            var parent = V(r, "REFERENCE_CLASS_CODE");
            classItems.Add(Item("apps.class", code, new
            {
                nameAr,
                nameEn,
                parentClassCode = parent.Length > 0 && parent != code && classCodes.Contains(parent) ? parent : null,
                allowMultiAssign = Flag(V(r, "VIRTUAL_ENABLE_MULTIPLE_ASSIGN"), empty: false),
                valuesPerCompany = rows.Any(v => v.ContainsKey("CMP_NUM")),
                attributes = definitions.Select(d => new
                {
                    code = d.Item1,
                    nameAr = d.Item3,
                    nameEn = d.Item4,
                    dataType = d.Item5,
                    refClassCode = d.Item6,
                    isMultiple = d.Item7,
                    attributeGroup = "ATTRIBUTE",
                    displayOrder = d.Item2,
                    isReportable = true,
                }).ToList(),
            }));
        }

        // A class after the classes it refers to (parent class, picks from another class).
        var byCode = classItems.ToDictionary(i => i.EntityCode, StringComparer.Ordinal);
        var ordered = new List<ConfigPackageItem>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        void Place(string code, HashSet<string> path)
        {
            if (placed.Contains(code) || !byCode.TryGetValue(code, out var item) || !path.Add(code))
            {
                return;
            }

            var body = item.Payload!.Value;
            var refs = body.GetProperty("attributes").EnumerateArray()
                .Select(a => a.GetProperty("refClassCode")).Where(r => r.ValueKind == JsonValueKind.String).Select(r => r.GetString()!)
                .Append(body.GetProperty("parentClassCode") is { ValueKind: JsonValueKind.String } parent ? parent.GetString()! : string.Empty);
            foreach (var other in refs.Where(r => r.Length > 0 && r != code))
            {
                Place(other, path);
            }

            placed.Add(code);
            ordered.Add(item);
        }

        foreach (var item in classItems)
        {
            Place(item.EntityCode, new HashSet<string>(StringComparer.Ordinal));
        }

        classItems = ordered;

        // Values of every company first, so references between classes find them, then each company's.
        var items = new Dictionary<string, ConfigPackageItem>(StringComparer.Ordinal);
        foreach (var r in system.Concat(company))
        {
            var cls = V(r, "CLASS_CODE");
            var value = V(r, "VALUE");
            var companyKey = r.ContainsKey("CMP_NUM") ? $"{V(r, "INSTID")}/{V(r, "CMP_NUM")}" : null;
            var key = companyKey is null ? $"{cls}/{value}" : $"{cls}/{companyKey}/{value}";
            var values = new JsonObject();
            foreach (var definition in attributes[cls])
            {
                var raw = V(r, $"FLEX_VALUE_{definition.Slot}");
                if (raw.Length == 0)
                {
                    continue;
                }

                values[definition.Code] = definition.Multiple
                    ? new JsonArray([.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Select(v => (JsonNode)JsonValue.Create(v))])
                    : JsonValue.Create(raw);
            }

            var (ar, en) = Names(V(r, "DOMAIN_DESC_NTV"), V(r, "DOMAIN_DESC_FRN"), value);
            items[key] = Item("apps.domain", key, new
            {
                nameAr = ar,
                nameEn = en,
                isActive = !r.ContainsKey("ACTIVE_FLAG") || Flag(V(r, "ACTIVE_FLAG")),
                detailsAr = NullIfEmpty(V(r, "DOMAIN_DESC_DETAIL_NTV")),
                detailsEn = NullIfEmpty(V(r, "DOMAIN_DESC_DETAIL_FRN")),
                displayOrder = Number(V(r, "ORDERBY")),
                attributes = values,
            });
        }

        // Values in the order of their classes (a class after the ones it picks from), every company's before each company's.
        var rank = classItems.Select((item, i) => (item.EntityCode, i)).ToDictionary(x => x.EntityCode, x => x.i, StringComparer.Ordinal);
        var sortedValues = items.Values
            .OrderBy(i => rank.GetValueOrDefault(i.EntityCode[..i.EntityCode.IndexOf('/', StringComparison.Ordinal)], int.MaxValue))
            .ThenBy(i => i.EntityCode.Count(c => c == '/'))
            .ToList();
        return (classItems, sortedValues, classCodes, valueCodes);
    }

    private static string FirstOf(string a, string b) => a.Length > 0 ? a : b;

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    // ---------------------------------------------------------------------------------------------------------------
    // DD_LOVS: a list that reads a class becomes a class list; the others ran Oracle SQL on tables that are not here.

    private (List<ConfigPackageItem> Items, HashSet<string> Codes) Lovs(Legacy data, HashSet<string> classes)
    {
        var items = new List<ConfigPackageItem>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in data["DD_LOVS"])
        {
            var code = V(r, "LOV_CODE");
            var cls = V(r, "REF_CLASS_CODE");
            if (cls.Length == 0 || !classes.Contains(cls))
            {
                Skip("DD_LOVS", code, cls.Length == 0 || cls.Any(char.IsWhiteSpace)
                    ? "it runs Oracle SQL on tables that are not in Dynamic Cube (SQL lists are off); rebuild it with the query builder"
                    : $"its class {cls} is not in DD_CLASSES");
                continue;
            }

            var (ar, en) = Names(V(r, "LOV_DESC_NTV"), V(r, "LOV_DESC_FRN"), code);
            codes.Add(code);
            items.Add(Item("apps.lov", code, new
            {
                nameAr = ar,
                nameEn = en,
                sourceType = "CLASS",
                classCode = cls,
                searchMode = "FULL",
                maxRows = 500,
                defaultSelectionMode = V(r, "VALUE_FIELD") == "select-multiple" ? "MULTI" : "SINGLE",
                requiresGrant = false,
                isPublic = false,
            }));
        }

        return (items, codes);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // SYS_MENU_GROUPS, SYS_MENU: each app's own menu tree; every item that opens something is a screen of the app.

    private (List<ConfigPackageItem> Screens, List<ConfigPackageItem> Menus, Dictionary<string, string?> MenuScreens) Menus(Legacy data, HashSet<string> apps)
    {
        var screens = new List<ConfigPackageItem>();
        var menus = new List<ConfigPackageItem>();
        var menuScreens = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var r in data["SYS_MENU_GROUPS"])
        {
            var app = V(r, "APPSID");
            if (!apps.Contains(app))
            {
                Skip("SYS_MENU_GROUPS", $"{app}/{V(r, "MENU_GROUP_ID")}", "its app is not imported");
                continue;
            }

            var (ar, en) = Names(V(r, "MENU_GROUP_DES_NTV"), V(r, "MENU_GROUP_DES_FRN"), V(r, "MENU_GROUP_ID"));
            menus.Add(Item("apps.menu-group", $"{app}/{V(r, "MENU_GROUP_ID")}", new
            {
                nameAr = ar,
                nameEn = en,
                businessApp = app,
                displayOrder = Number(V(r, "MENU_GROUP_ORDER")),
                icon = NullIfEmpty(Clip(V(r, "MENU_ICON"), 100)),
            }));
        }

        var groups = data["SYS_MENU_GROUPS"].Select(r => $"{V(r, "APPSID")}/{V(r, "MENU_GROUP_ID")}").ToHashSet(StringComparer.Ordinal);
        var rows = data["SYS_MENU"].Where(r => apps.Contains(V(r, "APPSID"))).ToList();
        foreach (var r in data["SYS_MENU"].Where(r => !apps.Contains(V(r, "APPSID"))))
        {
            Skip("SYS_MENU", $"{V(r, "APPSID")}/{V(r, "MENU_CODE")}", "its app is not imported");
        }

        var keys = rows.Select(r => $"{V(r, "APPSID")}/{V(r, "MENU_CODE")}").ToHashSet(StringComparer.Ordinal);

        // Parents before children, by the real depth of each item (the legacy MENU_LEVEL_NUM is not always right).
        var parentOf = rows.ToDictionary(r => $"{V(r, "APPSID")}/{V(r, "MENU_CODE")}", r => V(r, "PARENT_MENU_CODE") is { Length: > 0 } p && p != V(r, "MENU_CODE") && keys.Contains($"{V(r, "APPSID")}/{p}") ? $"{V(r, "APPSID")}/{p}" : null, StringComparer.Ordinal);
        int Depth(string key)
        {
            var depth = 0;
            for (var at = parentOf[key]; at is not null && depth <= parentOf.Count; at = parentOf[at])
            {
                depth++;
            }

            return depth;
        }

        foreach (var r in rows.OrderBy(r => Depth($"{V(r, "APPSID")}/{V(r, "MENU_CODE")}")).ThenBy(r => V(r, "MENU_CODE"), StringComparer.Ordinal))
        {
            var app = V(r, "APPSID");
            var code = V(r, "MENU_CODE");
            var key = $"{app}/{code}";
            var (ar, en) = Names(V(r, "MENU_DESC_NTV"), V(r, "MENU_DESC_FRN"), code);
            // A heading (GENERAL_DETAIL_FLAG 1), or an item that opens nothing, is a group of the tree.
            var isGroup = V(r, "GENERAL_DETAIL_FLAG") == "1" || V(r, "OBJECT_NAME").Length == 0;
            string? screen = null;
            if (!isGroup)
            {
                screen = $"{app}.{code}";
                screens.Add(Item("apps.resource", screen, new { nameAr = ar, nameEn = en, businessApp = app, resourceType = "SCREEN", availableActions = ViewOnly }));
            }

            menuScreens[key] = screen;
            var parent = V(r, "PARENT_MENU_CODE");
            var group = V(r, "MENU_GROUP_ID");
            var icon = V(r, "MENU_ICON");
            menus.Add(Item("apps.menu", key, new
            {
                nameAr = ar,
                nameEn = en,
                businessApp = app,
                groupCode = group.Length > 0 && groups.Contains($"{app}/{group}") ? $"{app}/{group}" : null,
                parentCode = parent.Length > 0 && parent != code && keys.Contains($"{app}/{parent}") ? $"{app}/{parent}" : null,
                itemType = isGroup ? "GROUP" : "SCREEN",
                target = isGroup ? null : NullIfEmpty(Clip(V(r, "OBJECT_NAME"), 1000)),
                resourceCode = screen,
                descriptionAr = NullIfEmpty(V(r, "MENU_LONG_DESC_NTV")),
                descriptionEn = NullIfEmpty(V(r, "MENU_LONG_DESC_FRN")),
                icon = icon.Length is > 0 and <= 4000 ? icon : null,
                displayOrder = Number(V(r, "ORDER_NUM")),
                isVisible = Flag(V(r, "SHOW_FLAG")),
            }));
        }

        return (screens, menus, menuScreens);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // SYS_ROLES with SYS_ROLE_MENUS and SYS_ROLE_MENU_LOVS (one grant per role, screen and list with its values).

    private (List<ConfigPackageItem> Items, HashSet<string> Keys) Roles(
        Legacy data, HashSet<string> companies, Dictionary<string, string?> menuScreens, HashSet<string> lovs, Dictionary<string, HashSet<string>> values)
    {
        var lovClass = data["DD_LOVS"].ToDictionary(r => V(r, "LOV_CODE"), r => V(r, "REF_CLASS_CODE"), StringComparer.Ordinal);
        var items = new List<ConfigPackageItem>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in data["SYS_ROLES"])
        {
            var (instid, number, code) = (V(r, "INSTID"), V(r, "CMP_NUM"), V(r, "ROLE_CODE"));
            var key = $"{instid}/{number}/{code}";
            if (!companies.Contains($"{instid}/{number}"))
            {
                Skip("SYS_ROLES", key, $"company {instid}/{number} is not in SYS_COMPANY_PROFILE");
                continue;
            }

            bool Mine(IReadOnlyDictionary<string, string> x) => V(x, "INSTID") == instid && V(x, "CMP_NUM") == number && V(x, "ROLE_CODE") == code;
            var menus = new List<string>();
            foreach (var m in data["SYS_ROLE_MENUS"].Where(Mine))
            {
                var menu = $"{V(m, "APPSID")}/{V(m, "MENU_CODE")}";
                if (menuScreens.ContainsKey(menu))
                {
                    menus.Add(menu);
                }
                else
                {
                    Skip("SYS_ROLE_MENUS", $"{key} → {menu}", "the menu item is not imported");
                }
            }

            var grants = new List<object>();
            foreach (var g in data["SYS_ROLE_MENU_LOVS"].Where(Mine).GroupBy(x => (Menu: $"{V(x, "APPSID")}/{V(x, "MENU_CODE")}", Lov: V(x, "LOV_CODE"))))
            {
                if (!lovs.Contains(g.Key.Lov))
                {
                    Skip("SYS_ROLE_MENU_LOVS", $"{key} → {g.Key.Menu} → {g.Key.Lov}", $"list {g.Key.Lov} is not imported ({g.Count()} value(s))");
                    continue;
                }

                if (!menuScreens.TryGetValue(g.Key.Menu, out var screen) || screen is null)
                {
                    Skip("SYS_ROLE_MENU_LOVS", $"{key} → {g.Key.Menu} → {g.Key.Lov}", "the menu item is not an imported screen");
                    continue;
                }

                var known = values.GetValueOrDefault(lovClass[g.Key.Lov]) ?? [];
                var granted = g.Select(x => V(x, "VALUE_CODE")).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToList();
                var unknown = granted.Where(v => !known.Contains(v)).ToList();
                if (unknown.Count > 0)
                {
                    Skip("SYS_ROLE_MENU_LOVS", $"{key} → {g.Key.Menu} → {g.Key.Lov}", $"value(s) {string.Join(", ", unknown)} are not values of class {lovClass[g.Key.Lov]}");
                }

                granted = [.. granted.Where(known.Contains)];
                var defaultValue = g.Where(x => V(x, "DEFAULT_VALUE_FLAG") == "1").Select(x => V(x, "VALUE_CODE")).FirstOrDefault(granted.Contains);
                grants.Add(new { lovCode = g.Key.Lov, resourceCode = screen, grantType = granted.Count > 0 ? "VALUES" : "NONE", grantedValues = granted, defaultValue });
            }

            var (ar, en) = Names(V(r, "ROLE_DESC_NTV"), V(r, "ROLE_DESC_FRN"), code);
            keys.Add(key);
            items.Add(Item("apps.role", key, new
            {
                nameAr = ar,
                nameEn = en,
                isActive = Flag(V(r, "IS_ACTIVE")),
                referenceCode = NullIfEmpty(V(r, "REF_CODE")),
                menus = menus.Distinct(StringComparer.Ordinal).ToList(),
                grants,
            }));
        }

        return (items, keys);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // SYS_USERS with their instance (SYS_USERINSTANCES) and roles (SYS_USER_ROLES). Passwords are not read.

    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.@\\-]{0,255}$")]
    private static partial Regex UserNamePattern();

    /// <summary>Legacy user ids as Dynamic Cube user names (a space becomes a dot).</summary>
    private readonly Dictionary<string, string> _userNames = new(StringComparer.Ordinal);

    private readonly Dictionary<string, (string NameEn, string? Email)> _accounts = new(StringComparer.Ordinal);

    private List<ConfigPackageItem> Users(Legacy data, HashSet<string> instances, HashSet<string> roles)
    {
        var items = new List<ConfigPackageItem>();
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in data["SYS_USERS"])
        {
            var id = V(r, "USERID");
            var name = Regex.Replace(id, "\\s+", ".");
            if (!UserNamePattern().IsMatch(name))
            {
                Skip("SYS_USERS", id, "the user id is not a valid user name");
                continue;
            }

            if (name != id)
            {
                Skip("SYS_USERS", id, $"imported as user name {name} (a user name has no spaces)");
            }

            _userNames[id] = name;
            var email = V(r, "USEREMAIL");
            if (email.Length > 0 && !EmailPattern().IsMatch(email))
            {
                Skip("SYS_USERS", id, "its e-mail is not a valid address; left empty");
                email = string.Empty;
            }
            else if (email.Length > 0 && !emails.Add(email))
            {
                Skip("SYS_USERS", id, $"its e-mail is already another user's; left empty");
                email = string.Empty;
            }

            var userInstances = data["SYS_USERINSTANCES"].Where(i => V(i, "USERID") == id && instances.Contains(V(i, "INSTID"))).ToList();
            var instance = userInstances.FirstOrDefault(i => V(i, "DEFAULTINSTFLAG") == "1") ?? userInstances.FirstOrDefault();
            var userRoles = new List<object>();
            foreach (var ur in data["SYS_USER_ROLES"].Where(u => V(u, "USERID") == id))
            {
                var role = $"{V(ur, "INSTID")}/{V(ur, "CMP_NUM")}/{V(ur, "ROLE_CODE")}";
                if (roles.Contains(role))
                {
                    userRoles.Add(new { roleCode = role, companyCode = $"{V(ur, "INSTID")}/{V(ur, "CMP_NUM")}", branches = All });
                }
                else
                {
                    Skip("SYS_USER_ROLES", $"{id} → {role}", "the role is not imported");
                }
            }

            var (ar, en) = Names(V(r, "USERNAMENTV"), V(r, "USERNAMEFRN"), name);
            var mobile = V(r, "USERPHONENO");
            _accounts[name] = (en, email.Length > 0 ? email : null);
            items.Add(Item("apps.user", name, new
            {
                nameAr = ar,
                nameEn = en,
                entityCode = instance is null ? string.Empty : V(instance, "INSTID"),
                kind = "PERSON",
                status = "ACTIVE",
                email = email.Length > 0 ? email : null,
                mobile = mobile.Length is > 0 and <= 32 ? mobile : null,
                roles = userRoles,
            }));
        }

        return items;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Sign-in accounts: one per imported user that has none, with a one-time password changed at first sign-in.

    private async Task AccountsAsync(List<(string UserName, string? Email, string Password)> created, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountDirectory>();
        AccountCapabilities capabilities;
        try
        {
            capabilities = await RetryAsync(() => accounts.CapabilitiesAsync(cancellationToken), cancellationToken);
        }
#pragma warning disable CA1031 // An unreachable identity provider is reported; the data import stands.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Legacy import: sign-in accounts not created (the identity provider did not answer); they are created on the next run");
            return;
        }

        if (!capabilities.CanCreate)
        {
            logger.LogWarning("Legacy import: sign-in accounts are not managed here ({Provider}); create them in the identity provider", capabilities.Provider);
            return;
        }

        foreach (var (name, (nameEn, email)) in _accounts)
        {
            if ((await accounts.GetAsync(name, cancellationToken)).Exists)
            {
                continue;
            }

            var password = Password();
            var (first, last) = PersonName(nameEn, name);
            try
            {
                await accounts.CreateAsync(new NewAccountRequest(name, email, first, last, password, SendSetupEmail: false), cancellationToken);
                created.Add((name, email, password));
                continue;
            }
            catch (AccountException ex) when (email is not null)
            {
                // E-mail already used by another account: the account is created without it.
                Skip("SYS_USERS", name, $"sign-in account created without e-mail ({ex.Message})");
            }
            catch (AccountException ex)
            {
                Skip("SYS_USERS", name, Refused(ex));
                continue;
            }

            try
            {
                await accounts.CreateAsync(new NewAccountRequest(name, null, first, last, password, SendSetupEmail: false), cancellationToken);
                created.Add((name, null, password));
            }
            catch (AccountException ex)
            {
                Skip("SYS_USERS", name, Refused(ex));
            }
        }
    }

    private static string Refused(AccountException ex) =>
        ex.Message.Contains("same username", StringComparison.OrdinalIgnoreCase)
            ? "no sign-in account: its user name is the e-mail of another account (sign-in by e-mail is on), so that account is the one to use"
            : ex.Message.Contains("invalid-length", StringComparison.OrdinalIgnoreCase)
                ? "no sign-in account: the user name is shorter than 3 characters, which the identity provider does not take"
                : $"no sign-in account: the identity provider refused it ({ex.Message})";

    /// <summary>First and last name as the identity provider takes them: no special characters, at most 255 each.</summary>
    private static (string First, string Last) PersonName(string name, string fallback)
    {
        var clean = new string([.. name.Select(c => "<>&\"$%!#?§;*~/\\,^(){}[]|@:=+".Contains(c, StringComparison.Ordinal) ? ' ' : c)]).Trim();
        clean = string.Join(' ', clean.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length == 0)
        {
            clean = fallback;
        }

        var space = clean.IndexOf(' ', StringComparison.Ordinal);
        var (first, last) = space > 0 ? (clean[..space], clean[(space + 1)..]) : (clean, clean);
        return (Clip(first, 255), Clip(last, 255));
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await call();
            }
            catch (HttpRequestException) when (attempt < 24)
            {
                logger.LogInformation("Legacy import: waiting for the identity provider…");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    /// <summary>A random one-time password with upper and lower case letters, digits and a sign.</summary>
    private static string Password()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnpqrstuvwxyz", digits = "23456789", signs = "@#%+=";
        var all = upper + lower + digits + signs;
        var chars = new List<char> { Pick(upper), Pick(lower), Pick(digits), Pick(signs) };
        while (chars.Count < 14)
        {
            chars.Add(Pick(all));
        }

        return new string([.. chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))]);

        static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
    }

    private void Skip(string table, string row, string reason) => _skipped.Add($"{table}\t{row}\t{reason}");

    private async Task WriteReportAsync(List<ConfigPackage> packages, List<(string UserName, string? Email, string Password)> accounts, CancellationToken cancellationToken)
    {
        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"Legacy Apps Control import — {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
        report.AppendLine();
        foreach (var package in packages)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"{package.Title}: {package.Items.Count}");
        }

        report.AppendLine(CultureInfo.InvariantCulture, $"Sign-in accounts created: {accounts.Count}");
        report.AppendLine();
        report.AppendLine(CultureInfo.InvariantCulture, $"Not imported or changed ({_skipped.Count}):");
        report.AppendLine("table\trow\treason");
        foreach (var line in _skipped)
        {
            report.AppendLine(line);
        }

        try
        {
            await File.WriteAllTextAsync(Path.Combine(_folder, "import-report.txt"), report.ToString(), cancellationToken);
            if (accounts.Count > 0)
            {
                // One-time passwords: for the administrator to hand out; each is changed at first sign-in.
                var file = Path.Combine(_folder, $"one-time-passwords-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv");
                var lines = new List<string> { "user_name,email,one_time_password" };
                lines.AddRange(accounts.Select(a => $"{a.UserName},{a.Email},{a.Password}"));
                await File.WriteAllLinesAsync(file, lines, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The data stands; the passwords are not written anywhere else (an administrator sets them in Users).
            logger.LogError(ex, "Legacy import: the report could not be written to {Folder}; {Accounts} new account(s) need a password set in Apps Control → Users", _folder, accounts.Count);
        }
    }
}

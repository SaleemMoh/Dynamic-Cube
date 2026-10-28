namespace DynamicCube.Modules.AppsControl.Domain;

public enum SettingDataType
{
    Text,
    Number,
    Bool,
    Json,
}

/// <summary>A configuration value, global or per app. Code is the setting key.</summary>
public sealed class Setting : ConfigEntity
{
    public Guid? AppId { get; set; }

    public App? App { get; set; }

    public SettingDataType DataType { get; set; }

    /// <summary>JSON value.</summary>
    public string Value { get; set; } = "null";

    public string? Description { get; set; }

    /// <summary>Secret values are never returned by read APIs.</summary>
    public bool IsSecret { get; set; }

    /// <summary>Readable by the external app.</summary>
    public bool IsPublic { get; set; }
}

/// <summary>Overrides a UI label or message without a release. Code is the text key.</summary>
public sealed class UiText : ConfigEntity
{
    public Guid? AppId { get; set; }

    public App? App { get; set; }
}

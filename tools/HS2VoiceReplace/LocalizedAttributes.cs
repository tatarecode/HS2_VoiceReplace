using System.ComponentModel;

namespace HS2VoiceReplace;

// Resolves CategoryAttribute values through the shared localization catalog so PropertyGrid
// categories can switch languages without hardcoding text in the settings model.
internal sealed class LocalizedCategoryAttribute : CategoryAttribute
{
    public LocalizedCategoryAttribute(string key) : base(key)
    {
    }

    protected override string GetLocalizedString(string value) => UiTextCatalog.Get(value);
}

// Resolves DisplayNameAttribute values through the shared localization catalog.
internal sealed class LocalizedDisplayNameAttribute : DisplayNameAttribute
{
    private readonly string _key;

    public LocalizedDisplayNameAttribute(string key)
    {
        _key = key;
    }

    public override string DisplayName => UiTextCatalog.Get(_key);
}

// Resolves DescriptionAttribute values through the shared localization catalog.
internal sealed class LocalizedDescriptionAttribute : DescriptionAttribute
{
    private readonly string _key;

    public LocalizedDescriptionAttribute(string key)
    {
        _key = key;
    }

    public override string Description => UiTextCatalog.Get(_key);
}


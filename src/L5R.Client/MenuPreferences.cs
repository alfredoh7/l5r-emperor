using Godot;
namespace L5R.Client;

/// <summary>Presentation-only preferences. No match state or player credentials.</summary>
public sealed class MenuPreferences
{
    public bool Fullscreen, ReducedMotion;
    public bool VSync = true;
    public float Atmosphere = .65f;
    public string Path { get; }
    public MenuPreferences(string profile) => Path = $"user://presentation-{profile}.cfg";
    public void Load()
    {
        var config = new ConfigFile();
        if (config.Load(Path) != Error.Ok) return;
        Fullscreen = config.GetValue("display", "fullscreen", false).AsBool();
        ReducedMotion = config.GetValue("display", "reduced_motion", false).AsBool();
        VSync = config.GetValue("display", "vsync", true).AsBool();
        Atmosphere = Mathf.Clamp((float)config.GetValue("display", "atmosphere", .65f).AsDouble(), 0, 1);
        if (!float.IsFinite(Atmosphere)) Atmosphere = .65f;
    }
    public Error Save()
    {
        var config = new ConfigFile();
        config.SetValue("display", "fullscreen", Fullscreen);
        config.SetValue("display", "reduced_motion", ReducedMotion);
        config.SetValue("display", "vsync", VSync);
        config.SetValue("display", "atmosphere", Atmosphere);
        return config.Save(Path);
    }
}

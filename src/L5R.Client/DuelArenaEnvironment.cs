using Godot;

namespace L5R.Client;

// Presentation-only environment. The original mat/pine/lantern meshes and card
// anchors remain in v5; a separate GLB adds editable garden and architecture.
public partial class DuelArenaReview
{
    private int _gardenPhase = 3;
    private bool _gardenReducedMotion;
    private float _gardenTime;
    private Node3D _gardenEffects = null!;
    private TextureRect _gardenBackdrop = null!;
    private ShaderMaterial _gardenSky = null!;
    private ShaderMaterial? _gardenWater;
    private readonly List<ShaderMaterial> _gardenMist = [];
    private readonly List<OmniLight3D> _gardenPracticals = [];
    private DirectionalLight3D _gardenRim = null!;

    private void BuildGardenPresentation()
    {
        _gardenEffects = new Node3D(); _world.AddChild(_gardenEffects);
        _gardenSky = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/DuelArena/garden-distance.gdshader") };
        _gardenBackdrop = new TextureRect {
            Texture = GD.Load<Texture2D>("res://Art/DuelArena/approved-garden-distance.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore, Material = _gardenSky
        };
        AddChild(_gardenBackdrop);
        _gardenRim = new DirectionalLight3D { RotationDegrees = new Vector3(-32, 158, 0),
            LightColor = new Color("8caadc"), LightEnergy = .45f, ShadowEnabled = false };
        _gardenEffects.AddChild(_gardenRim);
        _environment.Environment.Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial {
            SkyTopColor = new Color("101d35"), SkyHorizonColor = new Color("526782"),
            GroundBottomColor = new Color("0a0c12"), GroundHorizonColor = new Color("263043"),
            SkyEnergyMultiplier = .55f, GroundEnergyMultiplier = .35f } };
        _environment.Environment.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
        if (_pineBefore) return;
        var keep = new HashSet<string> { "ApprovedDuelMat", "PineHero", "StoneLanternLeft", "StoneLanternRight", "Thin silk backing" };
        foreach (var mesh in _model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray())
            if (!keep.Contains(mesh.Name)) { mesh.GetParent().RemoveChild(mesh); mesh.Free(); }
        var environment = GD.Load<PackedScene>("res://Art/DuelArena/duel-arena-v6-environment.glb").Instantiate<Node3D>();
        _model.AddChild(environment);
        var graded = new HashSet<ulong>();
        foreach (var mesh in environment.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D material || !graded.Add(material.GetInstanceId())) continue;
                var name = material.ResourceName;
                if (name.Contains("fired slate")) { material.AlbedoColor = new Color(.04f, .065f, .10f); material.Roughness = .72f; }
                if (name.Contains("weathered cedar")) material.AlbedoColor = new Color(.52f, .38f, .27f);
                if (name.Contains("wet granite")) { material.AlbedoColor = new Color(.30f, .36f, .45f); material.Roughness = .38f; }
                if (name.Contains("warm shoji")) material.EmissionEnergyMultiplier = .48f;
                if (name.Contains("moss and earth"))
                {
                    var bank = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/DuelArena/garden-bank.gdshader") };
                    bank.SetShaderParameter("albedo", material.AlbedoTexture);
                    // The bank and small moss stones share this original material.
                    foreach (var other in environment.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                        for (var slot = 0; slot < other.Mesh.GetSurfaceCount(); slot++)
                            if (other.GetActiveMaterial(slot) == material) other.SetSurfaceOverrideMaterial(slot, bank);
                }
            }
        foreach (var mesh in environment.FindChildren("*Approved*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var original = (BaseMaterial3D)mesh.GetActiveMaterial(0);
            var ornament = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/DuelArena/approved-ornament.gdshader") };
            ornament.SetShaderParameter("artwork", original.AlbedoTexture);
            mesh.MaterialOverride = ornament;
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        var stream = environment.FindChildren("*stream*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Single();
        _gardenWater = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/DuelArena/garden-water.gdshader") };
        stream.MaterialOverride = _gardenWater;
        foreach (var name in new[] { "V6ShojiLeft", "V6ShojiRight" })
        {
            var anchor = (Node3D)_model.FindChild(name, true, false);
            var light = new OmniLight3D { Position = anchor.GlobalPosition, LightColor = new Color("ffaf65"),
                LightEnergy = 1.8f, OmniRange = 7.5f, OmniAttenuation = 1.2f, ShadowEnabled = true };
            _gardenEffects.AddChild(light); _gardenPracticals.Add(light);
        }
        // Low mist is confined behind the railing, in front of the distant artwork.
        for (var i = 0; i < 3; i++)
        {
            var shader = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/DuelArena/garden-mist.gdshader") };
            shader.SetShaderParameter("offset", i * 3.1f); _gardenMist.Add(shader);
            var mist = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(35, 1.7f + i * .4f) },
                Position = new Vector3(0, .2f + i * .22f, -12.8f - i * 3), MaterialOverride = shader,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _gardenEffects.AddChild(mist);
        }
        // Small emissive cores sit inside the genuinely open lantern chambers.
        foreach (var name in new[] { "StoneLanternLeftGlow", "StoneLanternRightGlow" })
        {
            var anchor = (Node3D)_model.FindChild(name, true, false);
            var flame = new MeshInstance3D { Position = anchor.GlobalPosition,
                Mesh = new SphereMesh { Radius = .07f, Height = .25f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = new Color("ffe3a3"), EmissionEnabled = true, Emission = new Color("ffad48"), EmissionEnergyMultiplier = 2 },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _gardenEffects.AddChild(flame);
            _gardenEffects.AddChild(new MeshInstance3D {
                Position = anchor.GlobalPosition + new Vector3(0, .08f, .23f),
                Mesh = new QuadMesh { Size = new Vector2(.74f, .74f) },
                MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/DuelArena/lantern-halo.gdshader") },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
    }

    private void LayoutGardenPresentation(bool detail)
    {
        var active = !detail && !_showReference && !_pineBefore;
        var atmosphere = active && _gardenPhase >= 3;
        _gardenEffects.Visible = atmosphere;
        _gardenBackdrop.Visible = atmosphere;
        _gardenBackdrop.Position = _play.Position; _gardenBackdrop.Size = _play.Size;
        var horizon = _camera.UnprojectPosition(new Vector3(0, -.4f, -20)).Y;
        _gardenSky.SetShaderParameter("horizon", Mathf.Max(100, horizon));
        _gardenSky.SetShaderParameter("screen_size", _play.Size);
        _world.TransparentBg = atmosphere;
        _environment.Environment.ReflectedLightSource = atmosphere ? Godot.Environment.ReflectionSource.Sky : Godot.Environment.ReflectionSource.Bg;
        if (!active) return;
        _environment.Environment.AmbientLightEnergy = atmosphere ? .25f : .38f;
        _environment.Environment.AmbientLightColor = new Color("9eacc6");
        _keyLight.LightEnergy = atmosphere ? .50f : .8f;
        _keyLight.LightColor = new Color("c2d4ff");
        _keyLight.RotationDegrees = new Vector3(-48, -24, 0);
        foreach (var light in _lanternLights) { light.LightEnergy = atmosphere ? 2.8f : 1; light.OmniRange = atmosphere ? 5 : 2.3f; }
    }

    private void AnimateGardenPresentation(double delta)
    {
        if (_gardenSky is null) return;
        if (!_gardenReducedMotion) _gardenTime += (float)delta;
        _gardenSky.SetShaderParameter("clock", _gardenTime);
        _gardenWater?.SetShaderParameter("clock", _gardenTime);
        foreach (var mist in _gardenMist) mist.SetShaderParameter("clock", _gardenTime);
        for (var i = 0; i < _gardenPracticals.Count; i++)
            _gardenPracticals[i].LightEnergy = 1.8f * (1 + .025f * Mathf.Sin(_gardenTime * 2.1f + i));
    }
}

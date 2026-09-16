using Godot;

namespace L5R.Client;

public partial class FortressHall
{
    private void AddBrazierFire(Vector3 position, Practical practical)
    {
        // The iron and coal bed are separate Blender exports. The live fire
        // roots sit inside the pan, behind its rim, not above the basket.
        var root = position + new Vector3(0, -.045f, .034f);
        for (var layer = 0; layer < 2; layer++)
        {
            var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/brazier-fire.gdshader") };
            material.SetShaderParameter("fire_art", GD.Load<Texture2D>("res://Art/WarRoom/brazier-fire.png"));
            material.SetShaderParameter("seed", layer * 3.47f);
            material.SetShaderParameter("strength", layer == 0 ? .66f : .22f);
            practical.Flames.Add(material);
            AddChild(new MeshInstance3D {
                Name = "BrazierFireLayer", Mesh = new QuadMesh { Size = new Vector2(.22f, layer == 0 ? .21f : .16f) },
                Position = root + new Vector3(layer == 0 ? -.005f : .014f, .073f, layer == 0 ? 0 : -.025f),
                MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
        var halo = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/practical-glow.gdshader") };
        halo.SetShaderParameter("glow_colour", new Color(1f, .22f, .025f, .13f));
        practical.Halos.Add(halo);
        AddChild(new MeshInstance3D { Name = "BrazierWarmHalo", Mesh = new QuadMesh { Size = new Vector2(.44f,.42f) },
            Position = root + new Vector3(0,.04f,0), MaterialOverride = halo,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var coalArt = "res://Art/WarRoom/brazier-coals.glb";
        if (ResourceLoader.Exists(coalArt))
        {
            var coals = GD.Load<PackedScene>(coalArt).Instantiate<Node3D>();
            coals.Position = root + new Vector3(0,-.024f,.01f);
            var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/brazier-coals.gdshader") };
            practical.Flames.Add(material);
            foreach (var mesh in coals.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>())
                mesh.MaterialOverride = material;
            AddChild(coals);
        }
        for (var i = 0; i < 7; i++)
        {
            var spark = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/brazier-spark.gdshader") };
            spark.SetShaderParameter("seed", i*1.71f);
            practical.Flames.Add(spark);
            AddChild(new MeshInstance3D { Name = "BrazierRisingEmber", Mesh = new QuadMesh { Size = new Vector2(.0018f,.0035f) },
                Position = root, MaterialOverride = spark, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
    }
}

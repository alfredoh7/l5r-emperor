using Godot;
namespace L5R.Client;

/// <summary>Presentation-only instanced vegetation; both duel cameras share its wind clock.</summary>
public partial class DuelWindField : Node3D
{
    private readonly List<ShaderMaterial> _materials=[];
    public int Instances {get;private set;}
    public void Build(bool portraitStage=false)
    {
        var random=new Random(2050916);
        float R(float a,float b)=>a+(float)random.NextDouble()*(b-a);
        var shader=GD.Load<Shader>("res://Art/CrimsonDuel/atmosphere-grass.gdshader");
        // Segmented photographic seed heads and narrow geometry stems have separate stiffness.
        for(var layer=0;layer<2;layer++)
        {
            var art=layer==0;
            var material=new ShaderMaterial{Shader=shader};
            material.SetShaderParameter("use_art",art);
            material.SetShaderParameter("foliage_art",GD.Load<Texture2D>("res://Art/CrimsonDuel/golden-grass-tuft.png"));
            _materials.Add(material);
            var count=art?38:150;
            var mesh=Ribbon(art);
            var multi=new MultiMesh{TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,UseCustomData=true,Mesh=mesh,InstanceCount=count};
            for(var i=0;i<count;i++)
            {
                var x=R(-8.8f,8.8f);var depth=R(-1,1);
                var height=art?R(.28f,.60f):R(.22f,.57f);
                var width=art?height*R(.8f,1.25f):R(.010f,.022f);
                var y=6.12f+depth*.16f+Mathf.Sin(x*1.7f)*.06f;
                var z=depth;
                if(portraitStage){x*=1.8f;y=-.12f;z=R(-17f,-5.0f);height*=1.8f;width*=1.8f;}
                var basis=Basis.FromEuler(new Vector3(R(-.09f,.09f),R(-.52f,.52f),R(-.15f,.15f)))*Basis.FromScale(new Vector3(width,height,1));
                multi.SetInstanceTransform(i,new Transform3D(basis,new Vector3(x,y,z)));
                multi.SetInstanceCustomData(i,new Color(R(0,1),R(0,1),R(0,1),1));
            }
            AddChild(new MultiMeshInstance3D{Multimesh=multi,MaterialOverride=material,
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,ExtraCullMargin=2});
            Instances+=count;
        }
    }
    private static ArrayMesh Ribbon(bool art)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
        const int segments=10;
        for(var row=0;row<=segments;row++)
        {
            var h=row/(float)segments;var width=art?1:Mathf.Lerp(1,.05f,h*h);
            var curve=art?0:.16f*h*h;
            vertices.Add(new Vector3(-width/2+curve,h,0));vertices.Add(new Vector3(width/2+curve,h,0));
            uv.Add(new Vector2(0,1-h));uv.Add(new Vector2(1,1-h));
            if(row<segments){var a=row*2;indices.AddRange([a,a+2,a+1,a+1,a+2,a+3]);}
        }
        var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]=vertices.ToArray();arrays[(int)Mesh.ArrayType.Normal]=Enumerable.Repeat(Vector3.Back,vertices.Count).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV]=uv.ToArray();arrays[(int)Mesh.ArrayType.Index]=indices.ToArray();
        var mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);return mesh;
    }
    public void Present(double time,float strength)
    {
        foreach(var material in _materials){material.SetShaderParameter("wind_time",(float)time);material.SetShaderParameter("wind_strength",strength);}
    }
}

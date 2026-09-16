using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Public presentation copies only. The camera never moves gameplay cards or reads a deck.</summary>
public partial class DuelIntroStage : Control
{
    private SubViewport _view=null!;
    private Camera3D _camera=null!;
    private TextureRect _background=null!;
    private readonly List<(int Owner,ShaderMaterial Material)> _faces=[];
    private int _viewer;
    private ShaderMaterial _vignette=null!;
    public Vector2 NameAnchor(int owner)=>_camera.UnprojectPosition(new Vector3(owner==_viewer?-3.15f:3.15f,5.35f,0));
    private Vector2I _lastSize;
    public void Build(DuelRecord record,int viewer)
    {
        _viewer=viewer;MouseFilter=MouseFilterEnum.Ignore;
        _background=new TextureRect{Texture=GD.Load<Texture2D>("res://Art/CrimsonDuel/landscape-clean-plate.png"),
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,
            Material=new ShaderMaterial{Shader=GD.Load<Shader>("res://Art/CrimsonDuel/intro-background.gdshader")},MouseFilter=MouseFilterEnum.Ignore};AddChild(_background);
        _view=new SubViewport{OwnWorld3D=true,TransparentBg=true,HandleInputLocally=false,
            RenderTargetUpdateMode=SubViewport.UpdateMode.Always,Msaa3D=Viewport.Msaa.Msaa4X};AddChild(_view);
        _view.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.ClearColor,
            TonemapMode=Godot.Environment.ToneMapper.Linear,AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=1}});
        _camera=new Camera3D{Fov=48,Near=.05f,Far=100,Current=true};_view.AddChild(_camera);
        var output=new TextureRect{Texture=_view.GetTexture(),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter=MouseFilterEnum.Ignore};AddChild(output);
        output.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _vignette=new ShaderMaterial{Shader=GD.Load<Shader>("res://Art/CrimsonDuel/intro-vignette.gdshader")};
        var shade=new ColorRect{Color=Colors.White,Material=_vignette,MouseFilter=MouseFilterEnum.Ignore};AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddPlane(GD.Load<Texture2D>("res://Art/CrimsonDuel/campaign-mat-filigree.png"),new Vector2(16,9),new Vector3(0,-.04f,1.4f));
        foreach(var hero in new[]{record.Challenger,record.Challenged})
        {
            var side=hero.OwnerId==viewer?-1:1;var x=side*3.15f;
            var attached=record.Attachments.Where(c=>c.AttachedTo==hero.Id).ToArray();
            var step=Math.Min(.68f,1.15f/Math.Max(1,attached.Length));
            for(var i=attached.Length-1;i>=0;i--)AddCard(attached[i],new Vector3(x-.1f*(i+1),step*(i+1),-.17f*(i+1)),3.7f);
            AddCard(hero,new Vector3(x,0,.04f),3.8f);
            for(var i=0;i<6;i++)AddPlane(TablePile.FateBack,new Vector2(1.28f,1.8f),new Vector3(side*6,.018f+i*.011f,2.5f));
        }
    }
    private void AddCard(CardView card,Vector3 foot,float height)
    {
        // Render the shared face component so printed artwork, current stats and fixtures match play.
        var portrait=new SubViewport{Size=new Vector2I(392,548),TransparentBg=true,Disable3D=true,HandleInputLocally=false,
            RenderTargetUpdateMode=SubViewport.UpdateMode.Once};AddChild(portrait);
        portrait.AddChild(new CardTile(card with{Bowed=false},true,_=>{},_=>{}){Size=Vector2.One*560,Position=new Vector2(-84,-6),
            FaceHeight=548,ReducedMotion=true,ShowTokenRail=false,Material=CardPaper.Reading,MouseFilter=MouseFilterEnum.Ignore,FocusMode=FocusModeEnum.None});
        var material=new ShaderMaterial{Shader=GD.Load<Shader>("res://Art/CrimsonDuel/intro-card.gdshader")};
        material.SetShaderParameter("face",portrait.GetTexture());_faces.Add((card.OwnerId,material));
        _view.AddChild(new MeshInstance3D{Mesh=new QuadMesh{Size=new Vector2(height*392/548,height)},MaterialOverride=material,Position=foot+Vector3.Up*height/2});
    }
    private void AddPlane(Texture2D texture,Vector2 size,Vector3 position)
    {
        _view.AddChild(new MeshInstance3D{Mesh=new PlaneMesh{Size=size},Position=position,
            MaterialOverride=new StandardMaterial3D{AlbedoTexture=texture,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode=BaseMaterial3D.CullModeEnum.Disabled,TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps}});
    }
    public void Advance(double time,Vector2 size,int challenger,int challenged)
    {
        Size=size;_background.Size=size;
        var pixels=new Vector2I(Math.Max(1,(int)size.X),Math.Max(1,(int)size.Y));
        if(pixels!=_lastSize){_view.Size=pixels;_lastSize=pixels;}
        var wide=new Vector3(0,2.15f,10.4f);var wideAim=new Vector3(0,2,0);
        Vector3 Eye(int owner)=>new(owner==_viewer?-4.2f:4.2f,2.9f,7.5f);
        Vector3 Aim(int owner)=>new(owner==_viewer?-.95f:.95f,2.9f,0);
        float Smooth(double v){var p=Math.Clamp((float)v,0,1);return p*p*(3-2*p);}
        Vector3 eye,aim;int active;
        if(time<.7){eye=wide+Vector3.Back*(1-Smooth(time/.7))*.3f;aim=wideAim;active=-1;}
        else if(time<1.6){var p=Smooth((time-.7)/.26);eye=wide.Lerp(Eye(challenger),p);aim=wideAim.Lerp(Aim(challenger),p);active=challenger;}
        else if(time<2.5){var p=Smooth((time-1.6)/.26);eye=Eye(challenger).Lerp(Eye(challenged),p);aim=Aim(challenger).Lerp(Aim(challenged),p);active=challenged;}
        else {eye=wide;aim=wideAim;active=-1;}
        var amount=active<0?0:Smooth((time-.7)/.26);
        var direction=time<1.6?(challenger==_viewer?1f:0f):Mathf.Lerp(challenger==_viewer?1f:0f,challenged==_viewer?1f:0f,Smooth((time-1.6)/.26));
        _vignette.SetShaderParameter("opponent_on_right",direction);
        _vignette.SetShaderParameter("strength",amount);
        _camera.Position=eye;_camera.LookAt(aim);
        foreach(var face in _faces)face.Material.SetShaderParameter("softness",active<0||face.Owner==active?0f:1f);
        var blur=(ShaderMaterial)_background.Material;blur.SetShaderParameter("softness",active<0?.2f:1f);

    }
}

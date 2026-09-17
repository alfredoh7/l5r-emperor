using Godot;
namespace L5R.Client;

// Source-to-game: keep the approved wax pixels, remove only the navy matte.
public static class RemovedPileArt
{
    private static Texture2D? _seal;
    public static Texture2D Seal
    {
        get
        {
            if(_seal is not null)return _seal;
            using var source=GD.Load<Texture2D>("res://Art/removed-pile-approved.png").GetImage();
            if(source.IsCompressed())source.Decompress();
            source.Convert(Image.Format.Rgba8);
            using var region=source.GetRegion(new Rect2I(370,538,29,31));
            var data=region.GetData();
            for(var i=0;i<data.Length;i+=4)
                data[i+3]=(byte)(255*Math.Clamp((data[i]-Math.Max(data[i+1],data[i+2])-5f)/22f,0,1));
            using var image=Image.CreateFromData(29,31,false,Image.Format.Rgba8,data);
            image.GenerateMipmaps();return _seal=ImageTexture.CreateFromImage(image);
        }
    }
}

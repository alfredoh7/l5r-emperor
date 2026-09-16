using Godot;
namespace L5R.Client;

/// <summary>Deterministic prototype audio, synthesized here; no external recordings or game authority.</summary>
internal static class DuelAudio
{
    private static AudioStreamWav? _wind,_rise,_cut;
    private static readonly Dictionary<int,AudioStreamWav> Papers=[];
    public static AudioStreamWav Wind=>_wind??=BuildWind();
    public static AudioStreamWav Accent(bool cut)=>cut?(_cut??=BuildAccent(true,0)):(_rise??=BuildAccent(false,0));
    public static AudioStreamWav Paper(int side)
    {if(!Papers.TryGetValue(side,out var clip))Papers[side]=clip=BuildAccent(false,side);return clip;}
    private static AudioStreamWav BuildWind()
    {
        const int rate=22050,count=rate*18,cross=rate/2;
        var random=new Random(2050916);var samples=new double[count*2];
        double left=0,right=0,midLeft=0,midRight=0;
        for(var i=0;i<count;i++)
        {
            var t=i/(double)rate;var a=random.NextDouble()*2-1;var b=random.NextDouble()*2-1;
            left=left*.991+a*.009;right=right*.991+b*.009;
            midLeft=midLeft*.74+a*.26;midRight=midRight*.74+b*.26;
            var swell=.65+.2*Math.Sin(t*.7)+.15*Math.Sin(t*1.73);
            var rustle=.018+.035*Math.Pow(.5+.5*Math.Sin(t*2.1+Math.Sin(t*.63)),3);
            samples[i*2]=left*2.2*swell+(a-midLeft)*rustle;
            samples[i*2+1]=right*2.2*swell+(b-midRight)*rustle;
        }
        // Crossfade end to the loop start instead of fading to silence every cycle.
        for(var i=0;i<cross;i++)for(var c=0;c<2;c++)
        {var u=i/(double)cross;samples[(count-cross+i)*2+c]=samples[(count-cross+i)*2+c]*(1-u)+samples[i*2+c]*u;}
        var wav=Pcm(samples,rate);wav.LoopMode=AudioStreamWav.LoopModeEnum.Forward;wav.LoopBegin=cross;wav.LoopEnd=count;return wav;
    }
    private static AudioStreamWav BuildAccent(bool cut,int side)
    {
        const int rate=22050;var paper=side!=0;var seconds=paper?.22:cut?.35:.65;var count=(int)(rate*seconds);
        var values=new double[count*2];var random=new Random(205+(cut?9:4)+side);double low=0;
        for(var i=0;i<count;i++)
        {
            var t=i/(double)rate;var u=i/(double)count;var n=random.NextDouble()*2-1;low=low*.86+n*.14;
            var env=Math.Sin(Math.PI*u)*Math.Exp(-t*(paper?13:cut?9:3));
            var rasp=paper?(n-low)*.26:cut?n*.18+low*.8:low*.65;
            var body=cut?Math.Sin(Math.Tau*(170*t-110*t*t))*Math.Exp(-t*24)*.18:paper?Math.Sin(Math.Tau*105*t)*Math.Exp(-t*35)*.12:0;
            var value=rasp*env+body;
            values[i*2]=value*(side>0?.5:1);values[i*2+1]=value*(side<0?.5:1);
        }
        return Pcm(values,rate);
    }
    private static AudioStreamWav Pcm(double[] samples,int rate)
    {
        var bytes=new byte[samples.Length*2];
        for(var i=0;i<samples.Length;i++){var value=(short)(Math.Clamp(samples[i],-.95,.95)*32767);bytes[i*2]=(byte)(value&255);bytes[i*2+1]=(byte)((value>>8)&255);}
        return new AudioStreamWav{Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=rate,Stereo=true,Data=bytes};
    }
}

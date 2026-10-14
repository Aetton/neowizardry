using Godot;
using NeoWizardry.Core;

// Original-layout scaffolding with procedural placeholders; no original artwork is bundled.
public partial class HeroPortrait : Control
{
    public Hero? Hero;
    public bool Selected;
    public Action<string>? Pick;
    public override void _GuiInput(InputEvent e)
    {
        if(e is InputEventMouseButton { Pressed:true,ButtonIndex:MouseButton.Left } && Hero is not null)
        { Pick?.Invoke(Hero.Id);AcceptEvent(); }
    }
    public override void _Draw()
    {
        var gold=new Color(.55f,.46f,.29f);var dim=new Color(.23f,.24f,.23f);
        DrawStyleBox(Frame(Selected?new Color(.025f,.055f,.065f):new Color(.055f,.058f,.055f),Selected?new Color(.85f,.68f,.35f):dim),new Rect2(Vector2.Zero,Size));
        if(Hero is not { } h)return;
        float bottom=Size.Y-17, top=17, height=Math.Max(28,bottom-top-3), faceWidth=Size.X-42;
        var face=new Rect2(new Vector2(7,top),new Vector2(faceWidth,height));
        DrawRect(face,new Color(.025f,.031f,.035f));
        var center=face.GetCenter();float r=Math.Min(faceWidth*.22f,height*.25f);
        var skin=h.Hp==0 ? new Color(.15f,.15f,.15f) : h.Captive ? new Color(.28f,.25f,.23f) : new Color(.36f,.43f,.44f);
        DrawCircle(center+new Vector2(0,r*.7f),r*1.65f,new Color(skin,.55f));
        DrawCircle(center-new Vector2(0,r*.65f),r,skin);
        if(h.Race=="Faerie")
        {
            DrawColoredPolygon([center+new Vector2(-r*.7f,-r),center+new Vector2(-r*1.7f,-r*1.4f),center+new Vector2(-r*.7f,0)],skin);
            DrawColoredPolygon([center+new Vector2(r*.7f,-r),center+new Vector2(r*1.7f,-r*1.4f),center+new Vector2(r*.7f,0)],skin);
        }
        DrawLine(center+new Vector2(-r*.65f,-r*.8f),center+new Vector2(-r*.25f,-r*.8f),gold,2);
        DrawLine(center+new Vector2(r*.25f,-r*.8f),center+new Vector2(r*.65f,-r*.8f),gold,2);
        Bar(Size.X-27,top,9,height,h.Hp,h.MaxHp,new Color(.72f,.12f,.10f));
        int current=(h.Mana ?? []).Sum(p=>p.Current),max=(h.Mana ?? []).Sum(p=>p.Max);
        Bar(Size.X-14,top,9,height,current,max,new Color(.17f,.36f,.80f));
        var font=GetThemeDefaultFont();
        DrawString(font,new(7,13),h.Profession[..Math.Min(3,h.Profession.Length)].ToUpperInvariant(),HorizontalAlignment.Left,Size.X-35,10,gold);
        DrawString(font,new(5,Size.Y-4),h.Name,HorizontalAlignment.Center,Size.X-10,12,new Color(.86f,.82f,.70f));
        if(h.Hp==0 || h.Captive)DrawString(font,new(8,top+height*.55f),h.Hp==0?"FALLEN":"CAPTIVE",HorizontalAlignment.Center,faceWidth-2,11,new Color(.90f,.36f,.25f));
        TooltipText=$"{h.Name} · {h.Race} {h.Profession}\nHP {h.Hp}/{h.MaxHp} · XP {h.Xp}\n"+string.Join(" · ",(h.Mana ?? []).Select(p=>$"{p.Realm} {p.Current}/{p.Max}"))+"\n"+string.Join(", ",(h.Effects ?? []).Select(e=>$"{e.Kind} {e.Rounds} rounds"));
    }
    private void Bar(float x,float y,float w,float height,int value,int max,Color color)
    {
        DrawRect(new Rect2(x,y,w,height),new Color(.015f,.018f,.018f));
        float fill=max>0 ? height*value/max : 0;
        DrawRect(new Rect2(x,y+height-fill,w,fill),color);
        DrawRect(new Rect2(x,y,w,height),new Color(.38f,.35f,.25f),false,1);
    }
    internal static StyleBoxFlat Frame(Color fill,Color border)
    {
        var s=new StyleBoxFlat { BgColor=fill,BorderColor=border,BorderBlend=false };
        s.SetBorderWidthAll(2);s.SetContentMarginAll(7);return s;
    }
}

public partial class ArenaCompass : Control
{
    public Vector3 PlayerPosition;
    public float Yaw;
    public bool EnemyAlive;
    public override void _Draw()
    {
        var c=Size/2;float r=Math.Min(Size.X,Size.Y)*.44f;
        DrawCircle(c,r,new Color(.025f,.035f,.032f));
        DrawArc(c,r,0,Mathf.Tau,64,new Color(.58f,.46f,.28f),3,true);
        var map=new Rect2(c.X-r*.30f,c.Y-r*.68f,r*.60f,r*1.34f);
        DrawRect(map,new Color(.25f,.28f,.24f),false,1);
        Vector2 Point(Vector3 p)=>new(map.Position.X+(p.X+6)/12*map.Size.X,map.Position.Y+(p.Z+17)/22*map.Size.Y);
        if(EnemyAlive)DrawCircle(Point(new(0,0,-7)),3,new Color(.83f,.19f,.10f));
        var player=Point(PlayerPosition);var forward=new Vector2(-Mathf.Sin(Yaw),-Mathf.Cos(Yaw));
        DrawCircle(player,3,new Color(.80f,.75f,.45f));DrawLine(player,player+forward*13,new Color(.86f,.82f,.55f),2,true);
        DrawString(GetThemeDefaultFont(),new(c.X-5,12),"N",HorizontalAlignment.Left,-1,12,new Color(.8f,.72f,.51f));
        TooltipText="Training room · party position and facing\nRed: training sentinel";
    }
}

public partial class RealmDial : Control
{
    public string[] Available=[];
    public Action<string>? Pick;
    private static readonly string[] Realms=["Fire","Water","Air","Earth","Mental","Divine"];
    private static readonly Color[] Colors=[new(.85f,.23f,.10f),new(.18f,.43f,.85f),new(.65f,.73f,.77f),new(.36f,.61f,.22f),new(.68f,.30f,.68f),new(.90f,.76f,.30f)];
    private Vector2 Point(int i)=>Size/2+Vector2.FromAngle(-Mathf.Pi/2+i*Mathf.Tau/6)*(Math.Min(Size.X,Size.Y)*.31f);
    public override void _Draw()
    {
        var c=Size/2;float r=Math.Min(Size.X,Size.Y)*.44f;
        DrawCircle(c,r,new Color(.025f,.032f,.032f));DrawArc(c,r,0,Mathf.Tau,64,new Color(.58f,.46f,.28f),3,true);
        for(int i=0;i<6;i++)
        {
            bool enabled=Available.Contains(Realms[i]);var p=Point(i);
            DrawCircle(p,12,enabled ? Colors[i].Darkened(.45f) : new Color(.09f,.10f,.10f));
            DrawArc(p,12,0,Mathf.Tau,24,enabled?Colors[i]:new Color(.22f,.24f,.23f),1,true);
            DrawString(GetThemeDefaultFont(),p+new Vector2(-5,5),Realms[i][..1],HorizontalAlignment.Left,-1,12,enabled?new Color(.95f,.88f,.72f):new Color(.32f,.33f,.31f));
        }
        DrawString(GetThemeDefaultFont(),c+new Vector2(-17,4),"MAGIC",HorizontalAlignment.Left,-1,10,new Color(.65f,.59f,.41f));
        TooltipText="Spell realms for selected hero\nFire / Water / Air / Earth / Mental / Divine\nOnly learned fixture realms are enabled.";
    }
    public override void _GuiInput(InputEvent e)
    {
        if(e is not InputEventMouseButton { Pressed:true,ButtonIndex:MouseButton.Left } click)return;
        for(int i=0;i<6;i++)if(click.Position.DistanceTo(Point(i))<16 && Available.Contains(Realms[i]))
        { Pick?.Invoke(Realms[i]);AcceptEvent();break; }
    }
}

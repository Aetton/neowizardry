using Godot;

public partial class TouchPad : Control
{
    public Vector2 Stick;
    public override void _Draw()
    {
        var center=Size/2;
        DrawCircle(center,60,new Color(0.03f,0.04f,0.05f,0.65f));
        DrawArc(center,60,0,Mathf.Tau,64,new Color(0.65f,0.52f,0.3f),3,true);
        DrawCircle(center+Stick*42,21,new Color(0.65f,0.52f,0.3f,0.85f));
    }
}

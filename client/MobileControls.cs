using System.Numerics;

namespace NeoWizardry.Input;

// Gesture ownership is independent of Godot and of game rules.
public sealed class MobileControls
{
    public int? MovePointer { get; private set; }
    public int? LookPointer { get; private set; }
    public Vector2 Movement { get; private set; }
    private Vector2 _origin;
    private Vector2 _look;
    public float Radius { get; } = 60;

    public bool Press(int pointer, Vector2 position, bool moveZone, bool lookZone, Vector2 moveCenter)
    {
        if(pointer<0 || pointer==MovePointer || pointer==LookPointer) return false;
        if(moveZone && MovePointer is null) { MovePointer=pointer;_origin=moveCenter;Move(position);return true; }
        if(!moveZone && lookZone && LookPointer is null) { LookPointer=pointer;return true; }
        return false;
    }
    public void Drag(int pointer, Vector2 position, Vector2 relative)
    {
        if(pointer==MovePointer) Move(position);
        if(pointer==LookPointer) _look+=relative;
    }
    private void Move(Vector2 position)
    {
        var direction=(position-_origin)/Radius;
        float length=direction.Length();
        Movement=length<=0.12f ? Vector2.Zero : direction/length*Math.Min(1,(length-0.12f)/0.88f);
    }
    public void Release(int pointer)
    {
        if(pointer==MovePointer) { MovePointer=null;Movement=Vector2.Zero; }
        if(pointer==LookPointer) { LookPointer=null;_look=Vector2.Zero; }
    }
    public Vector2 ConsumeLook() { var result=_look;_look=Vector2.Zero;return result; }
    public void Reset() { MovePointer=null;LookPointer=null;Movement=Vector2.Zero;_look=Vector2.Zero; }
}

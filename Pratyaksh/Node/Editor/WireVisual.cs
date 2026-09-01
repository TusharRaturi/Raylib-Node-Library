using System.Numerics;
using Pratyaksh.Core;
using Raylib_cs;

namespace Pratyaksh.Node.Editor;

public class ObjectOrPosition(bool isPosition, Vector2 position, EditorObject? editorObject)
{
    private bool isPosition = isPosition;
    private Vector2 position = position;
    private EditorObject? editorObject = editorObject;

    public bool IsPosition { get => isPosition; }
    public EditorObject? EditorObject { get => editorObject; }

    public Vector2 GetPos()
    {
        if (isPosition)
            return position;
        else return editorObject == null ? Vector2.Zero : editorObject.Position;
    }

    public void SetPos(EditorObject editorObject)
    {
        this.editorObject = editorObject;
        isPosition = false;
    }

    public void SetPos(Vector2 position)
    {
        this.position = position;
        isPosition = true;
    }
}

public class WireVisual : Actor
{
    private ObjectOrPosition wireStart;
    private ObjectOrPosition wireEnd;

    private float wireThickness = 1.5f;

    private Color wireColor = Color.White;

    public void SetColor(Color color)
    {
        wireColor = color;
    }

    public void SetThickness(float thickness)
    {
        wireThickness = thickness;
    }

    public Vector2 WireStart { get => wireStart.GetPos(); }
    public Vector2 WireEnd { get => wireEnd.GetPos(); }

    public WireVisual(Drawable parent) : base(parent)
    {
        ResetWire();
    }

    public void ResetWire()
    {
        wireStart = new ObjectOrPosition(true, Vector2.Zero, null);
        wireEnd = new ObjectOrPosition(true, Vector2.Zero, null);
        Hide();
    }

    public void SetStartPos(Vector2 newStartPos)
    {
        wireStart.SetPos(newStartPos);
    }

    public void SetEndPos(Vector2 newEndPos)
    {
        wireEnd.SetPos(newEndPos);
    }

    public void SetStartPos(EditorObject newStartObj)
    {
        wireStart.SetPos(newStartObj);
    }

    public void SetEndPos(EditorObject newEndObj)
    {
        wireEnd.SetPos(newEndObj);
    }

    protected override void OnDraw()
    {
        Vector2 p0 = wireStart.GetPos();
        Vector2 p3 = wireEnd.GetPos();
        float dist = Vector2.Distance(p0, p3);
        float offset = Math.Max(dist * 0.5f, 35.0f);

        Vector2 t0;
        Vector2 t3;

        PortVisual? sp = wireStart.EditorObject as PortVisual;
        PortVisual? ep = wireEnd.EditorObject as PortVisual;

        if (sp != null && ep != null)
        {
            t0 = sp.GetBezierTangent(offset);
            t3 = ep.GetBezierTangent(offset);
        }
        else if (sp != null)
        {
            t0 = sp.GetBezierTangent(offset);
            t3 = -t0;
        }
        else if (ep != null)
        {
            t3 = ep.GetBezierTangent(offset);
            t0 = -t3;
        }
        else
        {
            t0 = new Vector2(offset, 0);
            t3 = new Vector2(-offset, 0);
        }

        Vector2 p1 = p0 + t0;
        Vector2 p2 = p3 + t3;

        Raylib.DrawSplineSegmentBezierCubic(p0, p1, p2, p3, wireThickness, wireColor);
    }

    public void NotifyDeleted(PortVisual portUI)
    {
        bool flag = false;

        if (!wireStart.IsPosition && (wireStart.EditorObject == portUI || wireEnd.EditorObject == portUI))
            flag = true;

        if (flag) ResetWire();
    }
}

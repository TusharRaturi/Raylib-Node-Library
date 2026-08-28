using System.Numerics;

namespace Pratyaksh.Core;

public enum ParentBasis
{
    TopLeft, TopCenter, TopRight, Left, Center, Right, BottomLeft, BottomCenter, BottomRight
}

public abstract class UIBase : EditorObject, IPointerVisitable
{
    private ParentBasis parentBasis;
    private UIBase? uibParent;
    private Vector2 size;

    protected bool hovered;

    public event Action? OnResize;

    public override Vector2 Position
    {
        get
        {
            if (parent == null)
                return RelativePosition;

            Vector2 parentBasisPos = CalcParentBasisPos(parentBasis, uibParent, parent);
            return parentBasisPos + RelativePosition;
        }
        set
        {
            Vector2 required = value;

            if (parent == null)
                RelativePosition = required;
            else
            {
                Vector2 diff = required - CalcParentBasisPos(parentBasis, uibParent, parent);
                RelativePosition = diff;
            }
        }
    }

    public Vector2 Size
    {
        get => size;
        set
        {
            if (size != value)
            {
                size = value;
                SetBasisInfo(parentBasis); // Refresh basis after chaning size.

                OnResize?.Invoke();
            }
        }
    }

    public int Width { get => (int)Size.X; }
    public int Height { get => (int)Size.Y; }

    public override Rectangle InteractionRect
    {
        get => new(Position.X, Position.Y, Size.X, Size.Y);
    }

    protected UIBase(int relativePosX, int relativePosY, int width, int height, Drawable? parent, ParentBasis? parentBasis = null) : base(null)
    {
        Size = new Vector2(width, height);

        this.parentBasis = parentBasis ?? ParentBasis.TopLeft;
        SetParent(parent, false);

        RelativePosition = new Vector2(relativePosX, relativePosY);

        hovered = false;
    }

    private Vector2 CalcParentBasisPos(ParentBasis parentBasis, UIBase? uibParent, Drawable parent)
    {
        if (uibParent == null)
        {
            return parent!.Position;
        }

        return parentBasis switch
        {
            ParentBasis.TopLeft => uibParent.Position,
            ParentBasis.TopCenter => uibParent.Position + new Vector2(uibParent.Size.X / 2 - Size.X / 2, 0),
            ParentBasis.TopRight => uibParent.Position + new Vector2(uibParent.Size.X - Size.X, 0),
            ParentBasis.Left => uibParent.Position + new Vector2(0, uibParent.Size.Y / 2 - Size.Y / 2),
            ParentBasis.Center => uibParent.Position + new Vector2(uibParent.Size.X / 2 - Size.X / 2, uibParent.Size.Y / 2 - Size.Y / 2),
            ParentBasis.Right => uibParent.Position + new Vector2(uibParent.Size.X - Size.X, uibParent.Size.Y / 2 - Size.Y / 2),
            ParentBasis.BottomLeft => uibParent.Position + new Vector2(0, uibParent.Size.Y - Size.Y),
            ParentBasis.BottomCenter => uibParent.Position + new Vector2(uibParent.Size.X / 2 - Size.X / 2, uibParent.Size.Y - Size.Y),
            ParentBasis.BottomRight => uibParent.Position + new Vector2(uibParent.Size.X - Size.X, uibParent.Size.Y - Size.Y),
            _ => throw new ArgumentOutOfRangeException(nameof(parentBasis), parentBasis, "Invalid parent basis in CalcParentBasisPos!"),
        };
    }

    public void SetBasisInfo(ParentBasis parentBasis)
    {
        this.parentBasis = parentBasis;
    }

    protected override void OnSetParent(Drawable? newParent, Drawable? oldParent, bool preservePosition)
    {
        Vector2 currAbsPos = Position;

        if (newParent is UIBase uib) uibParent = uib;
        else uibParent = null;

        parent = newParent;

        if (preservePosition) Position = currAbsPos;    // Recalculate relative position based on new parent
        else RelativePosition = Vector2.Zero;           // Reset relative position if not preserving
    }

    public override bool InteractionUseWorldPos()
    {
        return false;
    }

    public void OnMouseEnter(PointerVisitEventData evt)
    {
        hovered = true;
        OnMouseEnter();
    }

    public void OnMouseExit(PointerVisitEventData evt)
    {
        hovered = false;
        OnMouseExit();
    }

    protected virtual void OnMouseEnter() { }

    protected virtual void OnMouseExit() { }
}

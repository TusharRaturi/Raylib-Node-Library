using System.Numerics;
using Pratyaksh.Core;

namespace Pratyaksh.UI.UIElements;

public class Panel : UIBase, IClippable
{
    public Panel(int width, int height, Drawable? parent = null, ParentBasis? parentBasis = null) 
        : base(0, 0, width, height, parent, parentBasis)
    {
    }

    public Rectangle GetScissorRect(IWorldToScreenTransformer transformer)
    {
        Rectangle rect = new(Position.X, Position.Y, Size.X, Size.Y);
        bool worldSpace = InteractionUseWorldPos() || CheckAncestorsForInteractWorldPos();

        if (worldSpace)
            rect = transformer.WorldToScreen(rect);

        return rect;
    }

    protected override void OnDraw()
    {
    }
}

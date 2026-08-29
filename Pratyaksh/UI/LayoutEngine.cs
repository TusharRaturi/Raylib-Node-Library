using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using Pratyaksh.Core;
using Pratyaksh.Core.DataBinding;
using Pratyaksh.UI.DataBinding;
using Pratyaksh.UI.UIElements;

namespace Pratyaksh.UI;

public enum LayoutOpType
{
    Horizontal, Vertical
}

public struct LayoutOperation
{
    public int CurrentPos { get; set; }
    public int PosModifier { get; set; }
    public LayoutOpType OpType { get; set; }
    public int PosModifiedCount { get; set; }
}

public struct ElementInfo(UIBase uiElement, BinderBase? dataBinder)
{
    public UIBase UIElement { get; set; } = uiElement;
    public BinderBase? DataBinder { get; set; } = dataBinder;
    public bool IsActiveThisFrame { get; set; }

    public readonly bool Is<T>() where T : UIBase
    {
        return UIElement is T;
    }

    public readonly T Get<T>() where T : UIBase
    {
        return (T)UIElement;
    }

    public ElementInfo Activate()
    {
        IsActiveThisFrame = true;
        return this;
    }

    public ElementInfo Deactivate()
    {
        IsActiveThisFrame = false;
        return this;
    }
}

public class LayoutEngine
{
    private LayoutOperation[] layoutOps = new LayoutOperation[20];

    private int layoutOpsIdx = -1;
    private int lastHorizontalIdx = -1;
    private int lastVerticalIdx = -1;

    private static Raylib_cs.Font textFont;

    private Dictionary<int, ElementInfo> layoutElements = []; //NOTE: Layering support is not there after refactor. Previously was broken (probably). This is intended. Will be re-added later.

    private Stack<EditorObject?> parentStack = new();
    private Stack<int> activeScrollViews = new();
    private Stack<(int id, LayoutOpType opType, bool hasExtraLayout)> activePanels = new();
    private Stack<Core.Rectangle> scissorStack = new();

    private EditorObject? defaultParent = null;

    private static void ActivateElements(Dictionary<int, ElementInfo> targetDict)
    {
        foreach (var kvp in targetDict)
            targetDict[kvp.Key] = targetDict[kvp.Key].Activate();
    }

    private static void DeactivateElements(Dictionary<int, ElementInfo> targetDict)
    {
        foreach (var kvp in targetDict)
            targetDict[kvp.Key] = targetDict[kvp.Key].Deactivate();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeleteElement(int id, Dictionary<int, ElementInfo> targetDict)
    {
        if (targetDict.TryGetValue(id, out var info))
        {
            info.DataBinder?.Unbind();
            info.UIElement.Delete();
            targetDict.Remove(id);
        }
    }

    private static void DeleteElements(Dictionary<int, ElementInfo> targetDict)
    {
        foreach (var kvp in targetDict)
        {
            targetDict[kvp.Key].DataBinder?.Unbind();
            targetDict[kvp.Key].UIElement.Delete();
        }

        targetDict.Clear();
    }

    private static void UpdateActiveElements(Dictionary<int, ElementInfo> targetDict)
    {
        foreach (var kvp in targetDict)
        {
            if (targetDict[kvp.Key].IsActiveThisFrame)
                targetDict[kvp.Key].UIElement.Update();
        }
    }

    private static Drawable? HitTestActiveElements(Dictionary<int, ElementInfo> targetDict, IWorldToScreenTransformer transformer, Vector2 mouseScreenPosition, Vector2 mouseWorldPosition)
    {
        var keys = targetDict.Keys.ToArray();
        for (int i = keys.Length - 1; i >= 0; i--)
        {
            int key = keys[i];
            if (targetDict[key].IsActiveThisFrame)
            {
                var hit = targetDict[key].UIElement.HitTest(transformer, mouseScreenPosition, mouseWorldPosition);
                if (hit != null) return hit;
            }
        }
        return null;
    }

    public void BeginFrame()
    {
        DeactivateElements(layoutElements);
    }

    public void EndFrame()
    {
        foreach (KeyValuePair<int, ElementInfo> kvp in layoutElements)
        {
            if (!kvp.Value.IsActiveThisFrame && kvp.Value.Is<InputField>() && kvp.Value.Get<InputField>().IsFocused)
                Engine.Instance.InteractionManager.ReleaseFocus();
        }
    }

    public static void InitSLEDefaultFont(Raylib_cs.Font font)
    {
        textFont = font;
    }

    public LayoutEngine(EditorObject? defaultParent)
    {
        this.defaultParent = defaultParent;
    }

    public void ResetLayout()
    {
        layoutOpsIdx = -1;
        lastHorizontalIdx = -1;
        lastVerticalIdx = -1;

        activePanels.Clear();
        activeScrollViews.Clear();
        scissorStack.Clear();
        parentStack.Clear();

        DeleteElements(layoutElements);
    }

    public void UpdateLayoutElements()
    {
        UpdateActiveElements(layoutElements);
    }

    public void RemoveLayoutElement(int id) => DeleteElement(id, layoutElements);

    public Drawable? HitTestElements(IWorldToScreenTransformer transformer, Vector2 mouseScreenPosition, Vector2 mouseWorldPosition)
    {
        Drawable? hit = HitTestActiveElements(layoutElements, transformer, mouseScreenPosition, mouseWorldPosition);
        if (hit != null) return hit;

        return null;
    }

    public int LYOGetPos(int idx)
    {
        return layoutOps[idx].CurrentPos + layoutOps[idx].PosModifier * layoutOps[idx].PosModifiedCount;
    }

    public void LYOAddPos(int idx, int addAmt)
    {
        layoutOps[idx].CurrentPos += addAmt;
    }

    public int LYOGetPosAndUpdate(int idx)
    {
        return layoutOps[idx].CurrentPos + layoutOps[idx].PosModifier * layoutOps[idx].PosModifiedCount++;
    }

    public void LYOUpdate(int idx)
    {
        layoutOps[idx].PosModifiedCount++;
    }

    public int PosXAbsolute()
    {
        if (lastHorizontalIdx == -1)
            return 0;
        else
            return LYOGetPos(lastHorizontalIdx);
    }

    public int PosXRelative()
    {
        return PosXAbsolute() - (int)(defaultParent?.Position.X ?? 0);
    }

    public int PosYAbsolute()
    {
        if (lastVerticalIdx == -1)
            return 0;
        else
            return LYOGetPos(lastVerticalIdx);
    }

    public int PosYRelative()
    {
        return PosYAbsolute() - (int)(defaultParent?.Position.Y ?? 0);
    }

    public int CurrentWidth()
    {
        int idx = -1;
        for (int i = 0; i < layoutOps.Length; i++)
        {
            if (layoutOps[i].OpType == LayoutOpType.Horizontal)
            {
                idx = i;
                break;
            }
        }

        if (idx == -1)
            return 0;

        return PosXAbsolute() - LYOGetPos(idx);
    }

    private int PosXAbs_Dynamic()
    {
        if (lastHorizontalIdx == -1)
            return 0;
        else
        {
            if (lastHorizontalIdx == layoutOpsIdx)
                return LYOGetPosAndUpdate(lastHorizontalIdx);
            else return LYOGetPos(lastHorizontalIdx);
        }
    }

    private int PosYAbs_Dynamic()
    {
        if (lastVerticalIdx == -1)
            return 0;
        else
        {
            if (lastVerticalIdx == layoutOpsIdx)
                return LYOGetPosAndUpdate(lastVerticalIdx);
            else return LYOGetPos(lastVerticalIdx);
        }
    }

    private void AddNewLayoutOp(int pos, int dist, LayoutOpType type)
    {
        layoutOps[++layoutOpsIdx] = new LayoutOperation() { CurrentPos = pos, PosModifier = dist, OpType = type, PosModifiedCount = 0 };

        if (type == LayoutOpType.Horizontal)
            lastHorizontalIdx = layoutOpsIdx;
        else lastVerticalIdx = layoutOpsIdx;
    }

    private void RemoveLastLayoutOp()
    {
        if (layoutOpsIdx <= 0)
        {
            layoutOpsIdx = lastHorizontalIdx = lastVerticalIdx = -1;
        }
        else
        {
            if (lastHorizontalIdx == layoutOpsIdx)
            {
                lastHorizontalIdx = -1;

                for (int i = layoutOpsIdx - 1; i >= 0; i--)
                {
                    if (layoutOps[i].OpType == LayoutOpType.Horizontal)
                    {
                        lastHorizontalIdx = i;
                        break;
                    }
                }
            }
            else if (lastVerticalIdx == layoutOpsIdx)
            {
                lastVerticalIdx = -1;

                for (int i = layoutOpsIdx - 1; i >= 0; i--)
                {
                    if (layoutOps[i].OpType == LayoutOpType.Vertical)
                    {
                        lastVerticalIdx = i;
                        break;
                    }
                }
            }
            else
            {
                Console.WriteLine("Last horizontal and vertical indices were equal! This should never happen!");
                return;
            }

            layoutOpsIdx--;

            if (layoutOpsIdx != -1)
                layoutOps[layoutOpsIdx].PosModifiedCount++;
        }
    }

    public void NotifyDraw(int width, int height)
    {
        if (lastHorizontalIdx == lastVerticalIdx)
            return;
        else
        {
            if (lastHorizontalIdx < lastVerticalIdx)
                layoutOps[lastVerticalIdx].CurrentPos += height;
            else
                layoutOps[lastHorizontalIdx].CurrentPos += width;
        }
    }

    public static int MeasureTextW(string text, float fontSize)
    {
        return (int)Raylib_cs.Raylib.MeasureTextEx(textFont, text, fontSize, 0.5f).X;
    }

    public static int MeasureTextH(string text, float fontSize)
    {
        return (int)Raylib_cs.Raylib.MeasureTextEx(textFont, text, fontSize, 0.5f).Y;
    }

    public static Vector2 MeasureText(string text, float fontSize)
    {
        return Raylib_cs.Raylib.MeasureTextEx(textFont, text, fontSize, 0.5f);
    }

    public static void DrawTextAbsolute(string text, int posX, int posY, Raylib_cs.Color fontColor, float fontSize, Vector2 offset)
    {
        Raylib_cs.Raylib.DrawTextPro(textFont, text, new Vector2(posX + (int)offset.X, posY + (int)offset.Y), Vector2.Zero, 0, fontSize, 0.5f, fontColor);
    }

    public static void DrawTextAbsolute(string text, int posX, int posY, Raylib_cs.Color fontColor, Vector2 offset)
    {
        Raylib_cs.Raylib.DrawTextEx(textFont, text, new Vector2(posX + (int)offset.X, posY + (int)offset.Y), 15, 0.5f, fontColor);
    }

    public static void DrawPanelAbsolute(int posX, int posY, int width, int height, Raylib_cs.Color panelColor)
    {
        Raylib_cs.Raylib.DrawRectangle(posX, posY, width, height, panelColor);
    }

    public static void DrawTextPanelAbsolute(string text, int posX, int posY, int panelWidth, int panelHeight, int textSize, Raylib_cs.Color panelColor, Raylib_cs.Color textColor, Vector2 textOffset)
    {
        DrawPanelAbsolute(posX, posY, panelWidth, panelHeight, panelColor);
        DrawTextAbsolute(text, posX, posY, textColor, textSize, textOffset);
    }

    public static void DrawSectionAbsolute(string heading, int posX, int posY, int width, int heigth, float headingPercent, int headingSize, Raylib_cs.Color headingBgColor, Raylib_cs.Color bodyBgColor, Raylib_cs.Color fontColor)
    {
        int headingHeight = (int)(heigth * headingPercent);

        DrawPanelAbsolute(posX, posY + headingHeight, width, heigth - headingHeight, bodyBgColor);

        if (headingHeight != 0)
            DrawTextPanelAbsolute(heading, posX, posY, width, headingHeight, headingSize, headingBgColor, fontColor, new Vector2(5, 0));
        else
            Raylib_cs.Raylib.DrawTextEx(textFont, heading, new Vector2(posX + 5, posY), headingSize, 0.5f, fontColor);
    }

    public void Text(string text, float fontSize = 15, Raylib_cs.Color? fontColor = null, Vector2? offset = null, bool updateLayout = true)
    {
        DrawTextAbsolute(text, PosXAbs_Dynamic(), PosYAbs_Dynamic(), fontColor ?? Raylib_cs.Color.DarkGray, fontSize, offset ?? Vector2.Zero);
        Vector2 textSize = MeasureText(text, fontSize);
        if (updateLayout) NotifyDraw((int)textSize.X, (int)textSize.Y);
    }

    public void TextTruncated(string text, int maxWidth, Raylib_cs.Color fontColor, int fontSize = 15, bool updateLayout = true)
    {
        string truncatedStr = text;
        int measuredW = MeasureTextW(text, fontSize);
        if (measuredW > maxWidth)
        {
            const string ellipsis = "...";
            int ellipsisW = MeasureTextW(ellipsis, fontSize);
            int availableW = maxWidth - ellipsisW;

            if (availableW > 0)
            {
                int len = text.Length;
                while (len > 0 && MeasureTextW(text.Substring(0, len), fontSize) > availableW)
                {
                    len--;
                }
                truncatedStr = string.Concat(text.AsSpan(0, len), ellipsis);
            }
            else
            {
                truncatedStr = ellipsis;
            }
        }

        DrawTextAbsolute(truncatedStr, PosXAbs_Dynamic(), PosYAbs_Dynamic(), fontColor, fontSize, Vector2.Zero);
        if (updateLayout) NotifyDraw(Math.Min(measuredW, maxWidth), fontSize + 4);
    }

    public void Panel(int width, int height, Raylib_cs.Color panelColor, bool updateLayout = true)
    {
        DrawPanelAbsolute(PosXAbs_Dynamic(), PosYAbs_Dynamic(), width, height, panelColor);
        if (updateLayout) NotifyDraw(width, height);
    }

    public void TextPanelFixed(string text, int x, int y, int panelWidth, int panelHeight, Raylib_cs.Color panelColor, Raylib_cs.Color textColor, bool updateLayout = true)
    {
        PosXAbs_Dynamic();
        PosYAbs_Dynamic();

        DrawTextPanelAbsolute(text, x, y, panelWidth, panelHeight, 15, panelColor, textColor, new Vector2(5, 0));
        if (updateLayout) NotifyDraw(panelWidth, panelHeight);
    }

    public void TextPanelPro(string text, int panelWidth, int panelHeight, Raylib_cs.Color panelColor, Raylib_cs.Color textColor, bool updateLayout = true)
    {
        DrawTextPanelAbsolute(text, PosXAbs_Dynamic(), PosYAbs_Dynamic(), panelWidth, panelHeight, 15, panelColor, textColor, new Vector2(5, 0));
        if (updateLayout) NotifyDraw(panelWidth, panelHeight);
    }

    public void TextPanelEx(string text, int panelWidth, int panelHeight, Vector2 panelOffset, bool updateLayout = true)
    {
        DrawTextPanelAbsolute(text, PosXAbs_Dynamic(), PosYAbs_Dynamic(), panelWidth, panelHeight, 15, Raylib_cs.Color.Gray, Raylib_cs.Color.LightGray, panelOffset);
        if (updateLayout) NotifyDraw(panelWidth, panelHeight);
    }

    public void TextPanel(string text, int panelWidth, int panelHeight, bool updateLayout = true)
    {
        DrawTextPanelAbsolute(text, PosXAbs_Dynamic(), PosYAbs_Dynamic(), panelWidth, panelHeight, 15, Raylib_cs.Color.LightGray, Raylib_cs.Color.DarkGray, new Vector2(5, 0));
        if (updateLayout) NotifyDraw(panelWidth, panelHeight);
    }

    public void SectionEx(string heading, int width, int height, Raylib_cs.Color headingBgColor, Raylib_cs.Color bodyBgColor, Raylib_cs.Color fontColor, float headerPerc, bool updateLayout = true)
    {
        DrawSectionAbsolute(heading, PosXAbs_Dynamic(), PosYAbs_Dynamic(), width, height, headerPerc, 20, headingBgColor, bodyBgColor, fontColor);
        if (updateLayout) NotifyDraw(width, height);
    }

    public void Section(string heading, int width, int height, float headerPerc, bool updateLayout = true)
    {
        DrawSectionAbsolute(heading, PosXAbs_Dynamic(), PosYAbs_Dynamic(), width, height, headerPerc, 20, Raylib_cs.Color.DarkGray, Raylib_cs.Color.Gray, Raylib_cs.Color.LightGray);
        if (updateLayout) NotifyDraw(width, height);
    }

    public (int heightOffset, int drawStopOffset) DrawParentBG(Raylib_cs.Color bodyColor, float headerPerc = 0, string panelHeading = "", Raylib_cs.Color? headingColor = null, Raylib_cs.Color? textColor = null, bool updateLayoutAccHeader = true, int spaceAfterHeader = 10, int negativeDrawStopY = 0)
    {
        int x, y, width, height, heightOffset = 0;
        if (defaultParent is UIBase uib)
        {
            x = (int)uib.Position.X;
            y = (int)uib.Position.Y;
            width = uib.Width;
            height = uib.Height;

            int modifiedW = width;
            int modifiedH = height + negativeDrawStopY;
            float modifiedHeaderPer;

            if (modifiedH != height) modifiedHeaderPer = (headerPerc / height) * (modifiedH);
            else modifiedHeaderPer = headerPerc;

            DrawSectionAbsolute(panelHeading, x, y, modifiedW, modifiedH, modifiedHeaderPer, 20, headingColor ?? Raylib_cs.Color.DarkGray, bodyColor, textColor ?? Raylib_cs.Color.White);

            if (updateLayoutAccHeader)
            {
                if (modifiedHeaderPer > 0)
                {
                    heightOffset = (int)(modifiedH * modifiedHeaderPer) + spaceAfterHeader;
                    NotifyDraw(width, heightOffset);
                }
            }
        }
        else Console.WriteLine("WARN: Cannot draw background of non UI parent. Skipping.");

        return (heightOffset, -negativeDrawStopY); // This is total vertical 'excess space'.
    }

    public T DrawElementAbsolute<T>(int id, Func<(T, BinderBase?)> factory, Action<(T, BinderBase?)> storedReflect, int posX, int posY) where T : UIBase
    {
        bool found = layoutElements.ContainsKey(id);

        if (!found)
        {
            (T newElem, BinderBase? binder) = factory.Invoke();
            ElementInfo elem = new(newElem, binder);
            layoutElements.Add(id, elem);
        }
        else storedReflect.Invoke((layoutElements[id].Get<T>(), layoutElements[id].DataBinder));

        layoutElements[id] = layoutElements[id].Activate();

        layoutElements[id].UIElement.Position = new Vector2(posX, posY);
        layoutElements[id].UIElement.Render();

        return layoutElements[id].Get<T>();
    }

    public T DrawElementAbsolute<T>(int id, Func<T> factory, Action<T> storedReflect, int posX, int posY) where T : UIBase
    {
        return DrawElementAbsolute(id, () =>
        {
            T elem = factory.Invoke();
            BinderBase? binder = null;
            return (elem, binder);
        }, (expanded) =>
        {
            storedReflect.Invoke(expanded.Item1);
        },
        posX, posY);
    }

    public ElemType DrawBindableElementAbsolute<ElemType, ValType, RLUIType>(int id, BindableValueBase<ValType> dataModel, int posX, int posY, Func<(ElemType, RLUIType)> factory, Action<ElemType> storedReflect = null) where ElemType : UIBase where RLUIType : BindableUIBase<ValType>
    {
        return DrawElementAbsolute(id, () =>
        {
            (ElemType newElem, RLUIType uiBindable) = factory.Invoke();

            Binder<BindableValueBase<ValType>, RLUIType, ValType> binder = new();
            binder.Bind(dataModel, uiBindable);

            return (newElem, binder);
        }, ((ElemType stored, BinderBase? binder) expanded) =>
        {
            storedReflect?.Invoke(expanded.stored);

            if (expanded.binder is Binder<BindableValueBase<ValType>, RLUIType, ValType> binder)
            {
                if (binder.GetBoundValObject() != dataModel)
                {
                    binder.Unbind();

                    if (binder.GetBoundUIObject() is RLUIType uiTarget)
                        binder.Bind(dataModel, uiTarget);
                }
            }
        }, posX, posY);
    }

    public T DrawElement<T>(T element, bool updateLayout = true) where T : UIBase
    {
        Vector2 pos = new(PosXAbs_Dynamic(), PosYAbs_Dynamic());

        T drawnElem = DrawElementAbsolute(element.Id, () => element, (stored) => { }, (int)pos.X, (int)pos.Y);
        if (updateLayout) NotifyDraw((int)drawnElem.Width, (int)drawnElem.Height);
        return drawnElem;
    }

    public T DrawElement<T>(Func<Vector2, T> drawAbsoluteCaller, bool updateLayout = true) where T : UIBase
    {
        Vector2 pos = new(PosXAbs_Dynamic(), PosYAbs_Dynamic());

        T drawnElem = drawAbsoluteCaller.Invoke(pos);
        if (updateLayout) NotifyDraw(drawnElem.Width, drawnElem.Height);
        return drawnElem;
    }

    public Label Label(Label label, bool updateLayout = true) => DrawElement(label, updateLayout);

    public Label Label(int id, string label, int fontSize = 15, Raylib_cs.Color? textColor = null, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new Label((int)pos.X, (int)pos.Y, label, fontSize, textColor, defaultParent);
            }, (stored) =>
            {
                stored.Text = label;
                if (textColor.HasValue) stored.TextColor = (Raylib_cs.Color)textColor;
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public Label BindableLabel(int id, BindableValueBase<string> dataModel, int fontSize = 15, Raylib_cs.Color? textColor = null, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawBindableElementAbsolute(id, dataModel, (int)pos.X, (int)pos.Y, () =>
            {
                Label label = new((int)pos.X, (int)pos.Y, dataModel.Get(), fontSize, textColor, defaultParent);
                RLLabelUI newUIBindable = new(label);
                return (label, newUIBindable);
            });
        }, updateLayout);
    }

    public Button Button(Button button, bool updateLayout = true) => DrawElement(button, updateLayout);

    public Button Button(int id, string buttonText, int buttonWidth, int buttonHeight, Action<Button> onButtonPressed, object payload, int fontSize = 15, bool hasBorder = true, Raylib_cs.Color? fillColor = null, Raylib_cs.Color? borderColor = null, Raylib_cs.Color? textColor = null, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new Button((int)pos.X, (int)pos.Y, buttonWidth, buttonHeight, buttonText, onButtonPressed, payload, fontSize, hasBorder, fillColor, borderColor, textColor, defaultParent);
            }, (stored) =>
            {
                stored.ButtonText = buttonText;

                if (fillColor.HasValue) stored.FillColor = fillColor;
                if (borderColor.HasValue) stored.BorderColor = borderColor;
                if (textColor.HasValue) stored.TextColor = textColor;
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public Button Button(int id, ButtonDesc buttonDesc, bool updateLayout = true)
    {
        return Button(id, buttonDesc.text, buttonDesc.width ?? 200, buttonDesc.height ?? 25, buttonDesc.onClick, 0, buttonDesc.fontSize, buttonDesc.hasBorder, buttonDesc.fillColor, buttonDesc.borderColor, buttonDesc.textColor, updateLayout);
    }

    public Selectable Selectable(Selectable selectable, bool updateLayout = true) => DrawElement(selectable, updateLayout);

    public Selectable Selectable(int id, bool isSelected, string selectableText, int selectableWidth, int selectableHeight, Action<Selectable> onSelectableSelect, object? payload, int fontSize = 15, Raylib_cs.Color? bgColor = null, Raylib_cs.Color? bgSelectionColor = null, Raylib_cs.Color? textColor = null, bool updateLayout = true)
    {
        bgColor ??= new Raylib_cs.Color((byte)38, (byte)38, (byte)38, (byte)255);
        bgSelectionColor ??= new Raylib_cs.Color((byte)28, (byte)50, (byte)88, (byte)255);
        textColor ??= new Raylib_cs.Color((byte)200, (byte)200, (byte)200, (byte)255);

        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new Selectable(selectableText, isSelected, (int)pos.X, (int)pos.Y, selectableWidth, selectableHeight, onSelectableSelect, payload, fontSize, bgColor, bgSelectionColor, textColor, defaultParent);
            }, (stored) =>
            {
                if (isSelected != stored.IsSelected)
                {
                    if (isSelected) stored.Select(false);
                    else stored.Deselect(false);
                }

                stored.SelectableText = selectableText;
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public Selectable BindableSelectable(int id, BindableValueBase<bool> dataModel, string selectableText, int width, int height, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawBindableElementAbsolute(id, dataModel, (int)pos.X, (int)pos.Y, () =>
            {
                Selectable selectable = new(selectableText, dataModel.Get(), (int)pos.X, (int)pos.Y, width, height, (sel) => { }, id, 15, Raylib_cs.Color.Gray, Raylib_cs.Color.Blue, Raylib_cs.Color.White, defaultParent);
                RLSelectableUI newUIBindable = new(selectable);
                return (selectable, newUIBindable);
            });
        }, updateLayout);
    }

    public InputField InputField(InputField inputField, bool updateLayout = true) => DrawElement(inputField, updateLayout);

    public InputField InputField(int id, string placeholderText, string fieldText, int inputFieldWidth, int inputFieldHeight, Action<InputField>? onTextEdited = null, Action<InputField>? onFocusEnd = null, int fontSize = 15, bool isMasked = false, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new InputField(placeholderText, fieldText, (int)pos.X, (int)pos.Y, inputFieldWidth, inputFieldHeight, onTextEdited, onFocusEnd, fontSize, isMasked, defaultParent);
            }, (stored) =>
            {
                if (!stored.IsFocused)
                {
                    stored.InputFieldText = fieldText;
                }

                stored.IsMasked = isMasked;
                stored.OnTextChanged = onTextEdited;
                stored.OnFocusEnd = onFocusEnd;
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public InputField BindableInputFieldString(int id, string placeholderText, BindableValueBase<string> dataModel, int width, int height, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawBindableElementAbsolute(id, dataModel, (int)pos.X, (int)pos.Y, () =>
            {
                InputField newInputField = new(placeholderText, dataModel.Get(), (int)pos.X, (int)pos.Y, width, height, null, null, 15, false, defaultParent);
                RLInputFieldUI_String newUIBindable = new(newInputField);
                return (newInputField, newUIBindable);
            });
        }, updateLayout);
    }

    public InputField BindableInputFieldInt(int id, string placeholderText, BindableValueBase<int> dataModel, int width, int height, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawBindableElementAbsolute(id, dataModel, (int)pos.X, (int)pos.Y, () =>
            {
                InputField newInputField = new(placeholderText, dataModel.Get().ToString(), (int)pos.X, (int)pos.Y, width, height, null, null, 15, false, defaultParent);
                RLInputFieldUI_Int newUIBindable = new(newInputField);
                return (newInputField, newUIBindable);
            });
        }, updateLayout);
    }

    public InputField BindableInputFieldFloat(int id, string placeholderText, BindableValueBase<float> dataModel, int width, int height, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawBindableElementAbsolute(id, dataModel, (int)pos.X, (int)pos.Y, () =>
            {
                InputField newInputField = new(placeholderText, dataModel.Get().ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture), (int)pos.X, (int)pos.Y, width, height, null, null, 15, false, defaultParent);
                RLInputFieldUI_Float newUIBindable = new(newInputField);
                return (newInputField, newUIBindable);
            });
        }, updateLayout);
    }

    public Toggle Toggle(Toggle toggle, bool updateLayout = true) => DrawElement(toggle, updateLayout);

    public Toggle Toggle(int id, bool toggleValue, int toggleWidth, int toggleHeight, Action<Toggle>? onToggleChanged, object? payload, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new Toggle((int)pos.X, (int)pos.Y, toggleValue, toggleWidth, toggleHeight, onToggleChanged, payload, 15, defaultParent);
            }, (stored) =>
            {
                stored.Value = toggleValue;
                stored.SetOnToggleChanged(onToggleChanged);
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public Toggle BindableToggle(int id, BindableValueBase<bool> dataModel, int width, int height, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawBindableElementAbsolute(id, dataModel, (int)pos.X, (int)pos.Y, () =>
            {
                Toggle newToggle = new((int)pos.X, (int)pos.Y, dataModel.Get(), width, height, null, id, 15, defaultParent);
                RLToggleUI newUIBindable = new(newToggle);
                return (newToggle, newUIBindable);
            });
        }, updateLayout);
    }

    public Dropdown Dropdown(Dropdown dropdown, bool updateLayout = true) => DrawElement(dropdown, updateLayout);

    public Dropdown Dropdown(int id, string[] options, int selectedIndex, int width, int itemHeight, Action<Dropdown>? onSelectionChanged, object? payload, int fontSize = 15, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new Dropdown(options, selectedIndex, (int)pos.X, (int)pos.Y, width, itemHeight, onSelectionChanged, payload, fontSize, defaultParent);
            }, (stored) =>
            {
                if (stored.Options.Length != options.Length)
                    stored.SetOptions(options, selectedIndex);
                else
                {
                    stored.SelectedIndex = selectedIndex;
                    stored.SetOnSelectionChanged(onSelectionChanged);
                }
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public Dropdown BindableDropdown(int id, string[] options, BindableValueBase<int> dataModel, int width, int height, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawBindableElementAbsolute(id, dataModel, (int)pos.X, (int)pos.Y, () =>
            {
                Dropdown dropdown = new(options, dataModel.Get(), (int)pos.X, (int)pos.Y, width, height, null, id, 15, defaultParent);
                RLDropdownUI newUIBindable = new(dropdown);
                return (dropdown, newUIBindable);
            });
        }, updateLayout);
    }

    public CycleSelector CycleSelector(CycleSelector cycleSelector, bool updateLayout = true) => DrawElement(cycleSelector, updateLayout);

    public CycleSelector CycleSelector(int id, string[] options, int selectedIndex, int width, int height, Action<CycleSelector>? onSelectionChanged, object? payload = null, int fontSize = 15, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new CycleSelector(options, selectedIndex, (int)pos.X, (int)pos.Y, width, height, onSelectionChanged, payload, fontSize, defaultParent);
            }, (stored) =>
            {
                stored.SelectedIndex = selectedIndex;
                stored.Options = options;
                stored.SetOnSelectionChanged(onSelectionChanged);
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public LinkButton LinkButton(LinkButton linkButton, bool updateLayout = true) => DrawElement(linkButton, updateLayout);

    public LinkButton LinkButton(int id, string text, string url, Action<LinkButton>? onClick = null, int fontSize = 14, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new LinkButton((int)pos.X, (int)pos.Y, text, url, onClick, fontSize, defaultParent);
            }, (stored) =>
            {
                stored.Text = text;
                stored.Url = url;
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public StatusBadge StatusBadge(StatusBadge statusBadge, bool updateLayout = true) => DrawElement(statusBadge, updateLayout);

    public StatusBadge StatusBadge(int id, string text, StatusType statusType = StatusType.Idle, Raylib_cs.Color? customColor = null, int fontSize = 13, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new StatusBadge((int)pos.X, (int)pos.Y, text, statusType, customColor, fontSize, defaultParent);
            }, (stored) =>
            {
                stored.Text = text;
                stored.Type = statusType;

                if (customColor.HasValue) stored.CustomColor = customColor.Value;
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public AlertBanner AlertBanner(AlertBanner alertBanner, bool updateLayout = true) => DrawElement(alertBanner, updateLayout);

    public AlertBanner AlertBanner(int id, string message, AlertType alertType = AlertType.Error, int width = 360, int height = 32, int fontSize = 13, bool isDismissible = true, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new AlertBanner((int)pos.X, (int)pos.Y, message, alertType, width, height, isDismissible, fontSize, defaultParent);
            }, (stored) =>
            {
                stored.Message = message;
                stored.Type = alertType;
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public Slider Slider(Slider slider, bool updateLayout = true) => DrawElement(slider, updateLayout);

    public Slider Slider(int id, float value, float minValue, float maxValue, int width, int height, Action<Slider>? onValueChanged = null, object? payload = null, bool showValue = true, string? format = null, float? step = null, int fontSize = 13, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawElementAbsolute(id, () =>
            {
                return new Slider((int)pos.X, (int)pos.Y, value, minValue, maxValue, width, height, onValueChanged, payload, showValue, format, fontSize, step, defaultParent);
            }, (stored) =>
            {
                stored.MinValue = minValue;
                stored.MaxValue = maxValue;
                stored.Step = step;
                stored.ShowValue = showValue;
                stored.Format = format;
                stored.FontSize = fontSize;

                if (!stored.IsDragging) stored.SetValueWithoutNotify(value);

                stored.SetOnValueChanged(onValueChanged);
            }, (int)pos.X, (int)pos.Y);
        }, updateLayout);
    }

    public Slider BindableSlider(int id, BindableValueBase<float> dataModel, float minValue, float maxValue, int width, int height, bool showValue = true, string? format = null, float? step = null, int fontSize = 13, bool updateLayout = true)
    {
        return DrawElement((pos) =>
        {
            return DrawBindableElementAbsolute(id, dataModel, (int)pos.X, (int)pos.Y, () =>
            {
                Slider slider = new((int)pos.X, (int)pos.Y, dataModel.Get(), minValue, maxValue, width, height, null, id, showValue, format, fontSize, step, defaultParent);
                RLSliderUI newUIBindable = new(slider);
                return (slider, newUIBindable);
            }, (stored) =>
            {
                stored.MinValue = minValue;
                stored.MaxValue = maxValue;
                stored.Step = step;
                stored.ShowValue = showValue;
                stored.Format = format;
                stored.FontSize = fontSize;
            });
        }, updateLayout);
    }

    private string GetParentStackStr()
    {
        return string.Join("->", parentStack) + (parentStack.Count > 0 ? "->" : "") + defaultParent;
    }

    public void BeginHorizontal(int spacingDist)
    {
        if (lastHorizontalIdx > lastVerticalIdx)
            Console.WriteLine("WARN: Adding horizontal layout inside horizontal layout. No need. (and it's not supported): " + GetParentStackStr());

        AddNewLayoutOp(PosXAbs_Dynamic(), spacingDist, LayoutOpType.Horizontal);
    }

    public void BeginHorizontalEx(int spacingDist, int posXOverride)
    {
        if (lastHorizontalIdx > lastVerticalIdx)
            Console.WriteLine("WARN: Adding horizontal layout inside horizontal layout. No need. (and it's not supported): " + GetParentStackStr());

        AddNewLayoutOp(posXOverride, spacingDist, LayoutOpType.Horizontal);
    }

    public void EndHorizontal(int endHeight)
    {
        RemoveLastLayoutOp();
        NotifyDraw(0, endHeight);
    }

    public void BeginVertical(int spacingDist)
    {
        if (lastVerticalIdx > lastHorizontalIdx)
            Console.WriteLine("WARN: Adding vertical layout inside vertical layout. No need. (and it's not supported): " + GetParentStackStr());

        AddNewLayoutOp(PosYAbs_Dynamic(), spacingDist, LayoutOpType.Vertical);
    }

    public void BeginVerticalEx(int spacingDist, int posYOverride)
    {
        if (lastVerticalIdx > lastHorizontalIdx)
            Console.WriteLine("WARN: Adding horizontal layout inside horizontal layout. No need. (and it's not supported): " + GetParentStackStr());

        AddNewLayoutOp(posYOverride, spacingDist, LayoutOpType.Vertical);
    }

    public void EndVertical(int endWidth)
    {
        RemoveLastLayoutOp();
        NotifyDraw(endWidth, 0);
    }

    private void PushScissor(Core.Rectangle rect)
    {
        Core.Rectangle clippedRect = rect;
        if (scissorStack.Count > 0)
        {
            Core.Rectangle current = scissorStack.Peek();
            float left = Math.Max(current.X, rect.X);
            float top = Math.Max(current.Y, rect.Y);
            float right = Math.Min(current.X + current.Width, rect.X + rect.Width);
            float bottom = Math.Min(current.Y + current.Height, rect.Y + rect.Height);
            float width = Math.Max(0, right - left);
            float height = Math.Max(0, bottom - top);
            clippedRect = new Core.Rectangle(left, top, width, height);
        }

        scissorStack.Push(clippedRect);
        Raylib_cs.Raylib.BeginScissorMode((int)clippedRect.X, (int)clippedRect.Y, (int)clippedRect.Width, (int)clippedRect.Height);
    }

    private void PopScissor()
    {
        if (scissorStack.Count == 0)
            return;

        scissorStack.Pop();
        Raylib_cs.Raylib.EndScissorMode();

        if (scissorStack.Count > 0)
        {
            Core.Rectangle prev = scissorStack.Peek();
            Raylib_cs.Raylib.BeginScissorMode((int)prev.X, (int)prev.Y, (int)prev.Width, (int)prev.Height);
        }
    }

    public Panel BeginPanel(int id, int width, int height, int spacing, LayoutOpType layoutFlowDirection)
    {
        bool found = layoutElements.ContainsKey(id);
        if (!found)
        {
            Panel newPanel = new(width, height, defaultParent);
            ElementInfo elem = new(newPanel, null);
            layoutElements.Add(id, elem);
        }

        layoutElements[id] = layoutElements[id].Activate();
        Panel pnl = layoutElements[id].Get<Panel>();

        pnl.Size = new Vector2(width, height);
        pnl.Position = new Vector2(PosXAbs_Dynamic(), PosYAbs_Dynamic());

        Core.Rectangle scissorRect = pnl.GetScissorRect(Engine.Instance.InteractionManager.WorldToScreenTransformer);
        PushScissor(scissorRect);

        int startX = (int)pnl.Position.X;
        int startY = (int)pnl.Position.Y;

        bool hasExtraLayout = (layoutOpsIdx == -1) || (layoutOps[layoutOpsIdx].OpType == layoutFlowDirection);

        if (layoutFlowDirection == LayoutOpType.Vertical)
        {
            if (hasExtraLayout)
                BeginHorizontalEx(0, startX);

            BeginVerticalEx(spacing, startY);
        }
        else
        {
            if (hasExtraLayout)
                BeginVerticalEx(0, startY);

            BeginHorizontalEx(spacing, startX);
        }

        parentStack.Push(defaultParent);
        defaultParent = pnl;
        activePanels.Push((id, layoutFlowDirection, hasExtraLayout));

        return pnl;
    }

    public void EndPanel()
    {
        if (activePanels.Count == 0)
            return;

        (int pId, LayoutOpType opType, bool hasExtraLayout) = activePanels.Pop();
        Panel pnl = layoutElements[pId].Get<Panel>();

        if (hasExtraLayout)
        {
            if (opType == LayoutOpType.Vertical)
            {
                EndVertical(pnl.Width);
                EndHorizontal(pnl.Height);
            }
            else
            {
                EndHorizontal(pnl.Height);
                EndVertical(pnl.Width);
            }
        }
        else
        {
            if (opType == LayoutOpType.Vertical)
            {
                EndVertical(pnl.Width);
            }
            else
            {
                EndHorizontal(pnl.Height);
            }
        }

        PopScissor();

        defaultParent = parentStack.Pop();

        pnl.Render();
    }

    // Add the spacing parameter to the method signature
    public ScrollView BeginScrollView(int id, int viewWidth, int viewHeight, int verticalSpacing = 0, int startXOffset = 0, int startYOffset = 0)
    {
        bool found = layoutElements.ContainsKey(id);
        if (!found)
        {
            ScrollView newSvc = new(viewWidth, viewHeight, defaultParent);
            ElementInfo elem = new(newSvc, null);
            layoutElements.Add(id, elem);
        }

        layoutElements[id] = layoutElements[id].Activate();
        ScrollView svc = layoutElements[id].Get<ScrollView>();

        svc.Size = new Vector2(viewWidth, viewHeight);
        svc.Position = new Vector2(PosXAbs_Dynamic(), PosYAbs_Dynamic());

        Core.Rectangle scissorRect = svc.GetScissorRect(Engine.Instance.InteractionManager.WorldToScreenTransformer);
        PushScissor(scissorRect);

        int startX = (int)svc.Position.X;
        int startY = (int)svc.Position.Y;

        // Pass the spacing to the internal vertical layout!
        BeginHorizontalEx(0, startX + (int)svc.ScrollOffset.X);
        AddSpace(startXOffset + 1);
        BeginVerticalEx(verticalSpacing, startY + (int)svc.ScrollOffset.Y);
        AddSpace(startYOffset);

        parentStack.Push(defaultParent);
        defaultParent = svc;
        activeScrollViews.Push(id);

        return svc;
    }

    public void EndScrollView()
    {
        if (activeScrollViews.Count == 0)
            return;

        int svId = activeScrollViews.Pop();
        ScrollView svc = layoutElements[svId].Get<ScrollView>();

        int contentWidth = PosXAbsolute() - ((int)svc.Position.X + (int)svc.ScrollOffset.X);
        int contentHeight = PosYAbsolute() - ((int)svc.Position.Y + (int)svc.ScrollOffset.Y);

        svc.SetContentSize(new Vector2(Math.Max(svc.Size.X, contentWidth), Math.Max(svc.Size.Y, contentHeight)));

        EndVertical(svc.Width);
        EndHorizontal(svc.Height);

        PopScissor();

        defaultParent = parentStack.Pop();

        svc.Render();
    }

    public void AddSpace(int space)
    {
        NotifyDraw(space, space);
    }

    public void DrawOverlays()
    {
        foreach (KeyValuePair<int, ElementInfo> kvp in layoutElements)
        {
            if (kvp.Value.UIElement is IOverlayable overlay && kvp.Value.IsActiveThisFrame)
                overlay.DrawOverlay();
        }
    }
}
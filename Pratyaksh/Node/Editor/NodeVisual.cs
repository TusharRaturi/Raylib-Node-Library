namespace Pratyaksh.Node.Editor;

using Pratyaksh.Core;
using Pratyaksh.UI;
using Pratyaksh.Node.Core.DataModel;
using System.Numerics;

public enum NodeFlow
{
    Horizontal,
    Vertical
}

public class NodeVisual : Actor, IPointerInteractable, IDragable
{
    private readonly int nodeId;

    private Raylib_cs.Rectangle rect;
    private Raylib_cs.Rectangle headerRect;
    private string title;

    private float bgRectRoundness = 0.1f;
    private float bgRectSegments = 0.0f;
    private float bgRectOutlineThickness = 1.0f;

    private float hdRectRoundness = 0.2f;
    private float hdRectSegments = 0.0f;
    private float hdRectOutlineThickness = 1.0f;

    private Raylib_cs.Color titleColor = Raylib_cs.Color.White;

    private Raylib_cs.Color bgRectFillColor = Raylib_cs.Raylib.Fade(Raylib_cs.Color.Black, 0.65f);
    private Raylib_cs.Color bgRectBorderColor = Raylib_cs.Raylib.Fade(Raylib_cs.Color.DarkBlue, 0.4f);

    private Raylib_cs.Color hdRectFillColor = new((byte)125, (byte)50, (byte)50, (byte)255);
    private Raylib_cs.Color hdRectBorderColor = Raylib_cs.Raylib.Fade(Raylib_cs.Color.DarkBlue, 0.4f);

    private NodeFlow nodeExecFlow;
    private bool showHeader = true;

    private bool isDragging;
    private Vector2 dragOffset;

    private List<PortVisual> inputPorts;
    private List<PortVisual> outputPorts;

    private PortVisual? potConnectionStartPortUI;
    private WireVisual? potConnectionWireUI;

    public int NodeId { get => nodeId; }

    private ChildLayout nodeBodyLayout;

    private List<(int id, string name)> inputPortIdNames;
    private List<(int id, string name)> outputPortIdNames;

    private List<(UIElementType elemType, UIElementDescription elemDesc)> bodyUIElements;

    public override Rectangle InteractionRect => new(rect.X, rect.Y, rect.Width, rect.Height);

    public NodeFlow Flow { get => nodeExecFlow; }

    public bool ShowHeader
    {
        get => showHeader;
        set
        {
            if (showHeader != value)
            {
                showHeader = value;
                UpdateNodeVisual();
            }
        }
    }

    public NodeVisual(int nodeId,
                      List<(UIElementType elemType, UIElementDescription elemDesc)> bodyUIElements,
                      string title, float posX, float posY,
                      NodeFlow nodeExecFlow = NodeFlow.Horizontal,
                      bool showHeader = true,
                      Drawable? parent = null) : base(parent)
    {
        this.nodeId = nodeId;
        this.nodeExecFlow = nodeExecFlow;
        this.showHeader = showHeader;

        NodeEditorEngine.NotifyConnectNodeAndUI(nodeId, this);

        selfInteractable = true;

        this.title = title;

        RelativePosition = new Vector2(posX, posY);
        
        this.bodyUIElements = bodyUIElements;

        UpdateNodeVisual();
    }

    public void UpdateTitle(string newTitle)
    {
        title = newTitle;
    }

    public void UpdateNodeVisual()
    {
        Node? n = NodeEditorEngine.Graph.GetNode(nodeId);

        if (n == null)
        {
            Console.WriteLine($"Error: While trying to update node visual, the connected node (id: {nodeId}) was not valid!");
            return;
        }

        inputPortIdNames = n.InputPortIdNames;
        outputPortIdNames = n.OutputPortIdNames;

        float defaultWidth = 200;
        float defaultHeight = 75;
        float headerHeight = showHeader ? 20 : 0;

        int portsPadding = 15;
        int portsSpacing = 35;

        int uiElementsNeededWidth = -1;
        for (int i = 0; i < bodyUIElements.Count; i++)
        {
            if (bodyUIElements[i].elemDesc is RectUIEDescription ruid)
            {
                if (ruid.width == null && ruid is HorizontalGroupDesc gDesc)
                {
                    uiElementsNeededWidth = Math.Max(uiElementsNeededWidth, (150 * gDesc.uiElements.Count) + (gDesc.uiElements.Count - 1) * gDesc.spacing);
                }
                else uiElementsNeededWidth = Math.Max(uiElementsNeededWidth, ruid.width ?? 150);
            }
            else uiElementsNeededWidth = Math.Max(uiElementsNeededWidth, Raylib_cs.Raylib.MeasureText(bodyUIElements[i].elemDesc.text, 15));
        }

        int titleWidth = showHeader ? (Raylib_cs.Raylib.MeasureText(title, 15) + 30) : 0;

        if (Flow == NodeFlow.Vertical)
        {
            List<(int id, string name)> execIn = [];
            List<(int id, string name)> dataIn = [];
            List<(int id, string name)> execOut = [];
            List<(int id, string name)> dataOut = [];

            for (int i = 0; i < inputPortIdNames.Count; i++)
            {
                Port p = n.InputPorts[inputPortIdNames[i].id];
                if (p.DataType.Category.HasFlag(DataCategory.Execution))
                    execIn.Add(inputPortIdNames[i]);
                else
                    dataIn.Add(inputPortIdNames[i]);
            }

            for (int i = 0; i < outputPortIdNames.Count; i++)
            {
                Port p = n.OutputPorts[outputPortIdNames[i].id];
                if (p.DataType.Category.HasFlag(DataCategory.Execution))
                    execOut.Add(outputPortIdNames[i]);
                else
                    dataOut.Add(outputPortIdNames[i]);
            }

            int maxDataInSize = -1;
            for (int i = 0; i < dataIn.Count; i++)
                maxDataInSize = Math.Max(maxDataInSize, PortVisual.GetPortTSize(dataIn[i].name));

            int maxDataOutSize = -1;
            for (int i = 0; i < dataOut.Count; i++)
                maxDataOutSize = Math.Max(maxDataOutSize, PortVisual.GetPortTSize(dataOut[i].name));

            int bodyHPaddingInputSide = dataIn.Count > 0 ? maxDataInSize + portsPadding + 20 : portsPadding + 10;
            int bodyHPaddingOutputSide = dataOut.Count > 0 ? maxDataOutSize + portsPadding + 20 : portsPadding + 10;
            int totalBodyHPadding = bodyHPaddingInputSide + bodyHPaddingOutputSide;

            float widthData = uiElementsNeededWidth > 0 ? uiElementsNeededWidth + totalBodyHPadding : defaultWidth;

            float execTopNeededWidth = 0;
            if (execIn.Count > 0)
            {
                float totalLabelsW = 0;
                for (int i = 0; i < execIn.Count; i++)
                    totalLabelsW += Math.Max(portsSpacing, PortVisual.GetPortTSize(execIn[i].name) + 20);
                execTopNeededWidth = totalLabelsW + portsPadding * 2;
            }

            float execBottomNeededWidth = 0;
            if (execOut.Count > 0)
            {
                float totalLabelsW = 0;
                for (int i = 0; i < execOut.Count; i++)
                    totalLabelsW += Math.Max(portsSpacing, PortVisual.GetPortTSize(execOut[i].name) + 20);
                execBottomNeededWidth = totalLabelsW + portsPadding * 2;
            }

            float width = Math.Max(defaultWidth, Math.Max(titleWidth, Math.Max(widthData, Math.Max(execTopNeededWidth, execBottomNeededWidth))));
            float headerWidth = width;

            float topMargin = execIn.Count > 0 ? (headerHeight + 25) : (headerHeight + 15);
            float bottomMargin = execOut.Count > 0 ? 25 : 15;

            int maxDataPorts = Math.Max(dataIn.Count, dataOut.Count);
            float dataPortsHeight = topMargin + (maxDataPorts > 0 ? (maxDataPorts - 1) * portsSpacing : 0) + bottomMargin;

            float uiBodyHeight = (bodyUIElements.Count * 25) + ((bodyUIElements.Count - 1) * 5);
            float uiTotalHeight = topMargin + (bodyUIElements.Count > 0 ? uiBodyHeight : 0) + bottomMargin;

            float height = Math.Max(defaultHeight, Math.Max(dataPortsHeight, uiTotalHeight));
            int bodyWidth = (int)(width - totalBodyHPadding);

            inputPorts = [];
            outputPorts = [];

            if (execIn.Count == 1)
            {
                Port p = n.InputPorts[execIn[0].id];
                PortVisual pv = new(execIn[0].id, PortFlowType.Input, new Vector2(width / 2f, 0), execIn[0].name, p.DataType.Id, this);
                inputPorts.Add(pv);
            }
            else if (execIn.Count > 1)
            {
                float step = (width - 2 * portsPadding) / (execIn.Count - 1);
                for (int i = 0; i < execIn.Count; i++)
                {
                    Port p = n.InputPorts[execIn[i].id];
                    PortVisual pv = new(execIn[i].id, PortFlowType.Input, new Vector2(portsPadding + i * step, 0), execIn[i].name, p.DataType.Id, this);
                    inputPorts.Add(pv);
                }
            }

            if (execOut.Count == 1)
            {
                Port p = n.OutputPorts[execOut[0].id];
                PortVisual pv = new(execOut[0].id, PortFlowType.Output, new Vector2(width / 2f, height), execOut[0].name, p.DataType.Id, this);
                outputPorts.Add(pv);
            }
            else if (execOut.Count > 1)
            {
                float step = (width - 2 * portsPadding) / (execOut.Count - 1);
                for (int i = 0; i < execOut.Count; i++)
                {
                    Port p = n.OutputPorts[execOut[i].id];
                    PortVisual pv = new(execOut[i].id, PortFlowType.Output, new Vector2(portsPadding + i * step, height), execOut[i].name, p.DataType.Id, this);
                    outputPorts.Add(pv);
                }
            }

            for (int i = 0; i < dataIn.Count; i++)
            {
                Port p = n.InputPorts[dataIn[i].id];
                PortVisual pv = new(dataIn[i].id, PortFlowType.Input, new Vector2(portsPadding, topMargin + i * portsSpacing), dataIn[i].name, p.DataType.Id, this);
                inputPorts.Add(pv);
            }

            for (int i = 0; i < dataOut.Count; i++)
            {
                Port p = n.OutputPorts[dataOut[i].id];
                PortVisual pv = new(dataOut[i].id, PortFlowType.Output, new Vector2(width - portsPadding, topMargin + i * portsSpacing), dataOut[i].name, p.DataType.Id, this);
                outputPorts.Add(pv);
            }

            rect = new Raylib_cs.Rectangle(RelativePosition.X, RelativePosition.Y, width, height);
            headerRect = showHeader ? new Raylib_cs.Rectangle(RelativePosition.X, RelativePosition.Y, headerWidth, headerHeight) : new Raylib_cs.Rectangle(0, 0, 0, 0);

            nodeBodyLayout = new ChildLayout(bodyUIElements,
                                                bodyHPaddingInputSide,
                                                (int)topMargin,
                                                bodyWidth,
                                                (int)(height - topMargin - bottomMargin), this);
        }
        else
        {
            int portsInitialYOffset = showHeader ? 30 : 15;
            int maxPortTSize = -1;

            for (int i = 0; i < inputPortIdNames.Count; i++)
                maxPortTSize = Math.Max(maxPortTSize, PortVisual.GetPortTSize(inputPortIdNames[i].name));

            for (int i = 0; i < outputPortIdNames.Count; i++)
                maxPortTSize = Math.Max(maxPortTSize, PortVisual.GetPortTSize(outputPortIdNames[i].name));

            if (maxPortTSize == -1)
                maxPortTSize = 10;

            int bodyHPaddingInputSide = inputPortIdNames.Count > 0 ? maxPortTSize + portsPadding + 20 : portsPadding + 10;
            int bodyHPaddingOutputSide = outputPortIdNames.Count > 0 ? maxPortTSize + portsPadding + 20 : portsPadding + 10;
            int totalBodyHPadding = bodyHPaddingInputSide + bodyHPaddingOutputSide;

            float width = defaultWidth;
            if (width - totalBodyHPadding < uiElementsNeededWidth)
                width = uiElementsNeededWidth + totalBodyHPadding;
            width = Math.Max(width, titleWidth);
            float headerWidth = width;

            int portsMax = Math.Max(inputPortIdNames.Count, outputPortIdNames.Count);
            float height = defaultHeight;
            if (portsMax > 2)
                height += (portsMax - 2) * portsSpacing + portsPadding;

            int uiElementsNeededHeight = portsInitialYOffset + (bodyUIElements.Count * 25) + ((bodyUIElements.Count - 1) * 5) + 10;
            if (height < uiElementsNeededHeight)
                height = uiElementsNeededHeight;

            int bodyWidth = (int)(width - totalBodyHPadding);

            inputPorts = [];
            outputPorts = [];

            for (int i = 0; i < inputPortIdNames.Count; i++)
            {
                Port p = n.InputPorts[inputPortIdNames[i].id];
                PortVisual pv = new(inputPortIdNames[i].id, PortFlowType.Input, new Vector2(portsPadding, portsInitialYOffset + i * portsSpacing), inputPortIdNames[i].name, p.DataType.Id, this);
                inputPorts.Add(pv);
            }

            for (int i = 0; i < outputPortIdNames.Count; i++)
            {
                Port p = n.OutputPorts[outputPortIdNames[i].id];
                PortVisual pv = new(outputPortIdNames[i].id, PortFlowType.Output, new Vector2(width - portsPadding, portsInitialYOffset + i * portsSpacing), outputPortIdNames[i].name, p.DataType.Id, this);
                outputPorts.Add(pv);
            }

            rect = new Raylib_cs.Rectangle(RelativePosition.X, RelativePosition.Y, width, height);
            headerRect = showHeader ? new Raylib_cs.Rectangle(RelativePosition.X, RelativePosition.Y, headerWidth, headerHeight) : new Raylib_cs.Rectangle(0, 0, 0, 0);

            nodeBodyLayout = new ChildLayout(bodyUIElements,
                                                bodyHPaddingInputSide,
                                                portsInitialYOffset,
                                                bodyWidth,
                                                (int)(height - portsInitialYOffset), this);
        }
    }

    protected override Drawable? OnChildrenHitTest(IWorldToScreenTransformer transformer, Vector2 mouseScreenPosition, Vector2 mouseWorldPosition)
    {
        for (int i = inputPorts.Count - 1; i >= 0; i--)
        {
            var hit = inputPorts[i].HitTest(transformer, mouseScreenPosition, mouseWorldPosition);
            if (hit != null) return hit;
        }

        for (int i = outputPorts.Count - 1; i >= 0; i--)
        {
            var hit = outputPorts[i].HitTest(transformer, mouseScreenPosition, mouseWorldPosition);
            if (hit != null) return hit;
        }

        return nodeBodyLayout.HitTest(transformer, mouseScreenPosition, mouseWorldPosition);
    }

    protected override void OnUpdate()
    {
        for (int i = 0; i < inputPorts.Count; i++)
            inputPorts[i].Update();

        for (int i = 0; i < outputPorts.Count; i++)
            outputPorts[i].Update();

        nodeBodyLayout.Update();

        potConnectionWireUI?.Update();
    }

    protected override void OnDraw()
    {
        rect.X = Position.X;
        rect.Y = Position.Y;
        headerRect.X = Position.X;
        headerRect.Y = Position.Y;

        Raylib_cs.Raylib.DrawRectangleRounded(rect, bgRectRoundness, (int)bgRectSegments, bgRectFillColor);
        Raylib_cs.Raylib.DrawRectangleRoundedLinesEx(rect, bgRectRoundness, (int)bgRectSegments, bgRectOutlineThickness, bgRectBorderColor);

        if (showHeader)
        {
            Raylib_cs.Raylib.DrawRectangleRounded(headerRect, hdRectRoundness, (int)hdRectSegments, hdRectFillColor);
            Raylib_cs.Raylib.DrawRectangleRoundedLinesEx(headerRect, hdRectRoundness, (int)hdRectSegments, hdRectOutlineThickness, hdRectBorderColor);
            LayoutEngine.DrawTextAbsolute(title, (int)rect.X + 5, (int)rect.Y + 2, titleColor, 15, Vector2.Zero);
        }

        nodeBodyLayout.Render();

        for (int i = 0; i < inputPorts.Count; i++)
            inputPorts[i].Render();

        for (int i = 0; i < outputPorts.Count; i++)
            outputPorts[i].Render();

        potConnectionWireUI?.Render();
    }

    protected override void OnDelete()
    {
        nodeBodyLayout.Delete();

        for (int i = 0; i < inputPorts.Count; i++)
            inputPorts[i].Delete();

        for (int i = 0; i < outputPorts.Count; i++)
            outputPorts[i].Delete();

        potConnectionWireUI?.Delete();

        NodeEditorEngine.NotifyDisconnectNodeAndUI(nodeId);
    }

    public bool OnMouseDown(PointerInteractEventData evt)
    {
        return false;
    }

    public bool OnDragStart(PointerInteractEventData evt)
    {
        if (evt.MouseButton != MouseButton.Left)
            return false;

        Engine.Instance.InteractionManager.CapturePointer(this);

        isDragging = true;
        dragOffset = new Vector2(evt.WorldPosition.X - RelativePosition.X, evt.WorldPosition.Y - RelativePosition.Y);

        return true;
    }

    public void OnDrag(PointerInteractEventData evt)
    {
        if (isDragging)
            RelativePosition = new Vector2(evt.WorldPosition.X - dragOffset.X, evt.WorldPosition.Y - dragOffset.Y);
    }

    public bool OnMouseUp(PointerInteractEventData evt)
    {
        if (evt.MouseButton != MouseButton.Left)
            return false;

        isDragging = false;

        Engine.Instance.InteractionManager.ReleasePointer();

        return true;
    }

    private void CleanupWire()
    {
        potConnectionWireUI?.Hide();
        potConnectionWireUI = null;
    }

    public void UIConnectionStart(PortVisual source)
    {
        potConnectionStartPortUI = source;
        potConnectionWireUI = new WireVisual(source);
        potConnectionWireUI.SetColor(source.PortColor);
        potConnectionWireUI.SetThickness(source.IsExecution ? 3.0f : 1.5f);

        potConnectionWireUI.SetStartPos(source.Position);
        potConnectionWireUI.SetEndPos(source.Position);

        potConnectionWireUI.Show();
    }

    public void UIConnectionMove(PointerInteractEventData evt)
    {
        potConnectionWireUI?.SetEndPos(evt.WorldPosition);
    }

    public void UIConnectionSuccess(PortVisual sourceUI, PortVisual targetUI)
    {
        NodeEditorEngine.ConnectionUIManager.OnAddNewConnection(sourceUI, targetUI);
        CleanupWire();
    }

    public void UIConnectionCanceled(PortVisual source)
    {
        //if (potConnectionStartPortUI != source)
            //Console.WriteLine($"Error: UI Connection started with port {potConnectionStartPortUI} and aborted with {source}!");

        potConnectionStartPortUI = null;
        CleanupWire();
    }

    public bool UIConnectionComplete(PortVisual source, PortVisual connect)
    {
        bool success = NodeEditorEngine.Graph.AddConnection(source.ParentNodeId, source.PortId, connect.ParentNodeId, connect.PortId);

        if (success) UIConnectionSuccess(source, connect);
        else UIConnectionCanceled(source);

        return success;
    }

    public void ChangeUIElement(int elementIdx, UIElementDescription desc)
    {
        nodeBodyLayout.ChangeUIElement(elementIdx, desc);
    }

    public List<object?> GetUIStatePayloads() => nodeBodyLayout.GetUIStatePayloads();
    public void SetUIStatePayloads(List<System.Text.Json.JsonElement?> savedPayloads) => nodeBodyLayout.SetUIStatePayloads(savedPayloads);
}
namespace Pratyaksh.Node.Editor;

using System.Numerics;

using Pratyaksh.Core;
using Pratyaksh.Core.Serialization;

using Pratyaksh.UI;
using Pratyaksh.UI.UIElements;

using Pratyaksh.Node.Core.DataModel;

using Pratyaksh.Node.Editor.UI;
using Pratyaksh.Node.Editor.Serialization;

public class NodeEditorEngine : BaseRaylibEngine
{
    public override float DeltaTime => Raylib_cs.Raylib.GetFrameTime();

    private static Canvas canvas;
    private static int searchMenuIdx;
    private static int contextMenuIdx;

    private static ConnectionVisualManager connectionUIManager;

    private static Graph graph;

    private static Dictionary<int, NodeVisual?> nodeToNodeUIDict;
    private static Dictionary<int, PortVisual?> portToPortUIDict;
    private static Dictionary<int, List<NodeVisual>> varToNodeUIsDict;

    private static int? currentlySelectedObjectId;

    public float ScreenWidth { get => InteractionManager.WorldToScreenTransformer.GetWidth(); }
    public float ScreenHeight { get => InteractionManager.WorldToScreenTransformer.GetHeight(); }
    public static Graph Graph { get => graph; }
    public static Canvas Canvas { get => canvas; }
    public static ConnectionVisualManager ConnectionUIManager { get => connectionUIManager; }
    public static Dictionary<int, NodeVisual?> NodeToNodeUIDict { get => nodeToNodeUIDict; }
    public static Dictionary<int, PortVisual?> PortToPortUIDict { get => portToPortUIDict; }
    public static int? CurrentlySelectedObjectId { get => currentlySelectedObjectId; }
    public static Dictionary<int, Raylib_cs.Color> DataTypeColors { get; private set; }
    public static NodeRegistry NodeRegistry { get; private set; }

    public static event Action<int?> OnGlobalSelectionChanged;

    private static List<(Rectangle rect, Raylib_cs.Color color)> deferredRects = new();

    private GraphSerializer graphSerializer;

    public NodeEditorEngine(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, "Raylib Node Library", "../../../Thirdparty/Fonts/Satoshi_Complete/Fonts/TTF/Satoshi-Variable.ttf", true, new Raylib_cs.Color((byte)30, (byte)30, (byte)30), true, false)
    {
        camera = new EditorCamera2D(screenWidth, screenHeight);
        Init(new InteractionManager(camera));

        InteractionManager.GlobalPointerEvent += HandleGlobalPointerEvent;
        InteractionManager.GlobalKBEvent += HandleGlobalKBEvents;

        graphSerializer = new GraphSerializer(new JsonSerializationEngine());

        Raylib_cs.Raylib.SetConfigFlags(Raylib_cs.ConfigFlags.ResizableWindow);
    }

    public void HandleGlobalPointerEvent(PointerInteractEventData evt, PointerEventType pet, bool wasBubble)
    {
        if (pet == PointerEventType.Up && (evt.MouseButton == MouseButton.Right || evt.MouseButton == MouseButton.Left))
            canvas.OnMouseUp(evt);

        if (pet == PointerEventType.DragStart && evt.MouseButton == MouseButton.Right)
            ((EditorCamera2D)camera).OnDragStart(evt);

        if ((pet == PointerEventType.Up || pet == PointerEventType.Down) && evt.MouseButton == MouseButton.Middle && evt is ScrollEventData sed)
            ((EditorCamera2D)camera).OnScroll(sed);
    }

    public void HandleGlobalKBEvents(KeyInteractEventData keyEvent)
    {
        // No object is focused. Handle global Editor Hotkeys here.
        // e.g., Ctrl+S to save the graph, Ctrl+Z to undo.
        if (keyEvent.Key == KeyboardKey.S && InteractionManager.InputContext.isCtrlDown)
        {
            string data = graphSerializer.Serialize(graph, nodeToNodeUIDict, NodeRegistry, IdGen.CurrentId, canvas);
            File.WriteAllText("save." + graphSerializer.Extension, data);
            Console.WriteLine("Saved graph to save." + graphSerializer.Extension);
        }
        else if (keyEvent.Key == KeyboardKey.L && InteractionManager.InputContext.isCtrlDown)
        {
            if (File.Exists("save." + graphSerializer.Extension))
            {
                string json = File.ReadAllText("save." + graphSerializer.Extension);
                var data = graphSerializer.Deserialize(json);
                if (data != null)
                {
                    ReconstructGraph(data);
                    Console.WriteLine("Loaded graph from save." + graphSerializer.Extension);
                }
            }
        }
    }

    public static void NotifyAddVar(int varId)
    {
        varToNodeUIsDict.Add(varId, []);
        
        if (graph != null)
        {
            Variable? v = graph.GetVariable(varId);
            if (v != null)
            {
                NodeTemplate t = new($"Get {v.VarName}", "Variables", [], [v.VarType.Name], 
                    [(UIElementType.Text, new TextDesc($"{v.VarName} ({v.VarType.Name}):", 15, Raylib_cs.Raylib.Fade(Raylib_cs.Color.White, 0.65f)))], varId);
                NodeRegistry.RegisterNode(t);
            }
        }
    }

    public static void NotifyAddNode(int nodeId)
    {
        nodeToNodeUIDict.Add(nodeId, null);
    }

    public static void NotifyUpdateNode(int nodeId)
    {
        if (nodeToNodeUIDict.TryGetValue(nodeId, out NodeVisual? nodeVis))
        {
            if (nodeVis != null)
                nodeVis.UpdateNodeVisual();
            else Console.WriteLine($"Error: While notify update node, connected node vis to Node id {nodeId} was not valid!");
        }
    }

    public static void NotifyAddPort(int portId)
    {
        portToPortUIDict.Add(portId, null);
    }

    public static void NotifyConnectNodeAndUI(int nodeId, NodeVisual nui)
    {
        if (nodeToNodeUIDict.ContainsKey(nodeId))
            nodeToNodeUIDict[nodeId] = nui;
    }

    public static void NotifyConnectPortAndUI(int portId, PortVisual pui)
    {
        if (portToPortUIDict.ContainsKey(portId))
            portToPortUIDict[portId] = pui;
    }

    public static void NotifyDisconnectNodeAndUI(int nodeId)
    {
        if (nodeToNodeUIDict.ContainsKey(nodeId))
            nodeToNodeUIDict[nodeId] = null;
    }

    public static void NotifyDisconnectPortAndUI(int portId)
    {
        if (portToPortUIDict.ContainsKey(portId))
            portToPortUIDict[portId] = null;
    }

    public static void NotifyRemoveNode(int nodeId)
    {
        nodeToNodeUIDict.Remove(nodeId);
    }

    public static void NotifyRemovePort(int portId)
    {
        portToPortUIDict.Remove(portId);
    }

    public static void NotifyRemoveVar(int varId)
    {
        varToNodeUIsDict.Remove(varId);
        NodeRegistry?.UnregisterNodeByPayload(varId);
    }

    public static void NotifyRemoveConnection(int sourcePort, int targetPort)
    {
        PortVisual? sourceP = portToPortUIDict[sourcePort];
        PortVisual? targetP = portToPortUIDict[targetPort];

        if (sourceP == null || targetP == null)
        {
            Console.WriteLine("Error: Couldn't get port ui from ports while notify remove connection!");
            return;
        }

        connectionUIManager.OnRemoveConnection(sourceP, targetP);
    }

    private void RemoveNodeAndUI(NodeVisual nodeVis)
    {
        int nodeId = nodeVis.NodeId;
        graph.RemoveNode(nodeId);

        actors.Remove(nodeVis);
        nodeVis.Delete();
    }

    private void SetupDefaultTypeColors()
    {
        DataTypeColors = [];

        DataType? execType = graph.Types.GetType("Execution");
        DataType? intType = graph.Types.GetType("Int");
        DataType? floatType = graph.Types.GetType("Float");
        DataType? numType = graph.Types.GetType("Number");
        DataType? strType = graph.Types.GetType("String");
        DataType? boolType = graph.Types.GetType("Bool");

        if (execType != null)
            DataTypeColors.Add(execType.Id, Raylib_cs.Raylib.Fade(Raylib_cs.Color.White, 0.55f));
        
        if (intType != null)
            DataTypeColors.Add(intType.Id, Raylib_cs.Color.Green);
        
        if (floatType != null)
            DataTypeColors.Add(floatType.Id, Raylib_cs.Color.DarkGreen);
        
        if (numType != null)
            DataTypeColors.Add(numType.Id, Raylib_cs.Color.DarkGreen);
        
        if (strType != null)
            DataTypeColors.Add(strType.Id, Raylib_cs.Color.Pink);
        
        if (boolType != null)
            DataTypeColors.Add(boolType.Id, Raylib_cs.Color.Red);
    }

    private void OnSelectVar(int? varId)
    {
        // On Var select.
        currentlySelectedObjectId = varId;
        OnGlobalSelectionChanged?.Invoke(varId);
    }

    private void OnAddVar()
    {
        // On add var.
        DataType? intT = graph.Types.GetType("Int");
        if (intT != null) graph.AddVariable("New Var", intT, 0);
    }

    private void OnRemoveVar(int varId)
    {
        // On remove var.
        if (varToNodeUIsDict.TryGetValue(varId, out List<NodeVisual>? varNodesVisList))
        {
            for (int i = 0; i < varNodesVisList.Count; i++)
                RemoveNodeAndUI(varNodesVisList[i]);
        }
        else Console.WriteLine("Error: Couldn't get var of id {varId} while removing from VarToNodeUIsDict!");

        graph.RemoveVariable(varId);
    }

    private void OnRenameVariable(int varId, string newName)
    {
        // On rename var
        if (graph.RenameVariable(varId, newName))
        {
            Variable? v = graph.GetVariable(varId);

            if (v == null)
            {
                Console.WriteLine("Error: THIS SHOULD NEVER HAPPEN!");
                return;
            }
            
            NodeRegistry.UnregisterNodeByPayload(varId);
            NodeTemplate t = new($"Get {v.VarName}", "Variables", [], [v.VarType.Name],
                [(UIElementType.Text, new TextDesc($"{v.VarName} ({v.VarType.Name}):", 15, Raylib_cs.Raylib.Fade(Raylib_cs.Color.White, 0.65f)))], varId);
            
            NodeRegistry.RegisterNode(t);

            if (varToNodeUIsDict.TryGetValue(varId, out List<NodeVisual>? nodeVisList))
            {
                for (int i = 0; i<nodeVisList.Count; i++)
                {
                    nodeVisList[i].ChangeUIElement(0, new TextDesc($"{newName} ({v.VarType.Name}):", 15, Raylib_cs.Raylib.Fade(Raylib_cs.Color.White, 0.65f)));
                    nodeVisList[i].UpdateTitle($"Get {v.VarName}");
                    
                    Node? n = graph.GetNode(nodeVisList[i].NodeId);
                    n?.TemplateId = t.Id;
                }
            }
            else Console.WriteLine($"Error: Couldn't rename variable of id {varId}!");
        }
        else Console.WriteLine($"Error: Couldn't rename variable of id {varId}!");
    }

    private void OnChangeVarType(int varId, DataType newType)
    {
        // On change var type
        graph.ChangeVariableType(varId, newType);

        Variable? v = graph.GetVariable(varId);
        if (v == null) return;

        NodeRegistry.UnregisterNodeByPayload(varId);
        NodeTemplate t = new($"Get {v.VarName}", "Variables", [], [v.VarType.Name],
            [(UIElementType.Text, new TextDesc($"{v.VarName} ({v.VarType.Name}):", 15, Raylib_cs.Raylib.Fade(Raylib_cs.Color.White, 0.65f)))], varId);
        NodeRegistry.RegisterNode(t);

        if (varToNodeUIsDict.TryGetValue(varId, out List<NodeVisual>? nodeVisList))
        {
            for (int i = 0; i < nodeVisList.Count; i++)
            {
                NodeVisual nui = nodeVisList[i];
                nui.ChangeUIElement(0, new TextDesc($"{v.VarName} ({v.VarType.Name}):", 15, Raylib_cs.Raylib.Fade(Raylib_cs.Color.White, 0.65f)));

                Node? n = graph.GetNode(nui.NodeId);
                if (n != null)
                {
                    n.TemplateId = t.Id;
                    if (n.OutputPorts.Count > 0)
                    {
                        int portId = n.OutputPortIds[0];
                        Port p = n.OutputPorts[portId];

                        p.DataType = newType;
                        p.PortName = newType.Name;

                        if (portToPortUIDict.TryGetValue(portId, out PortVisual? pui) && pui != null)
                            pui.UpdateDataType(newType.Id, newType.Name);

                        graph.DisconnectIncompatibleConnections(portId);
                    }
                }
            }
        }
    }

    private void OnChangeVarValue(int varId, object newValue)
    {
        Variable? v = graph.GetVariable(varId);
        v?.VarValue = newValue;
    }

    private bool OpenSearchMenu(int x, int y, List<(string, object)> items, Action<object> onItemSelected)
    {
        return canvas.OpenTransPanel<SearchMenu>(searchMenuIdx, x, y, 200, 300, items, onItemSelected, canvas, null) != null;
    }

    private bool OpenContextMenu(int x, int y, EditorObject? potentialTarget)
    {
        List<(string name, object payload)> menuItems = [("Delete", 0)];
        Action<Button> onButtonPressed = (button) => OnCanvasCtxMenuItemSelected(button, potentialTarget);

        return canvas.OpenTransPanel<ContextMenu>(contextMenuIdx, x, y, menuItems, onButtonPressed, canvas, null) != null;
    }

    private void OnCanvasSearchMenuItemSelected(object payload)
    {
        if (payload is not NodeTemplate template)
            return;
        
        Vector2 mp = InteractionManager.InputContext.mouseWorldPosition;
        NodeVisual? visual = NodeRegistry.SpawnNode(graph, template.Id, mp);

        if (visual != null)
        {
            actors.Add(visual);
            if (template.Payload is int varId)
            {
                if (varToNodeUIsDict.TryGetValue(varId, out List<NodeVisual>? list))
                    list.Add(visual);
                else varToNodeUIsDict.Add(varId, [visual]);
            }
        }

        canvas.CloseTransPanel(searchMenuIdx);
    }

    private void OnCanvasCtxMenuItemSelected(Button button, EditorObject? editorObj)
    {
        if (button.Payload is int intP && intP != 0) // 0 = Delete
            return;

        if (editorObj != null)
        {
            Actor ac = (Actor)editorObj;
            NodeVisual? nui = (NodeVisual)ac;

            if (nui == null)
            {
                Console.WriteLine("Didn't receive a not on ctx menu graph delete!");
                return;
            }

            List<int> keys = [.. varToNodeUIsDict.Keys];
            (int key, int idx)? marked = null;
            for (int i = 0; i < keys.Count; i++)
            {
                List<NodeVisual> nodeViss = varToNodeUIsDict[keys[i]];

                for (int j = 0; j < nodeViss.Count; j++)
                {
                    if (nodeViss[j] == nui)
                    {
                        marked = (keys[i], j);
                        break;
                    }
                }

                if (marked.HasValue)
                {
                    varToNodeUIsDict[marked.Value.key].RemoveAt(marked.Value.idx);
                    break;
                }
            }

            RemoveNodeAndUI(nui);
        }

        canvas.CloseTransPanel(contextMenuIdx);
    }

    private bool OnCanvasContextClick(PointerInteractEventData evt, EditorObject? target)
    {
        int posX = (int)evt.ScreenPosition.X;
        int posY = (int)evt.ScreenPosition.Y;

        bool success = false;

        if (Instance.InteractionManager.CurrentlyHit == null)
        {
            List<(string, object)> mis = [];

            foreach (var template in NodeRegistry.AllTemplates)
            {
                mis.Add((template.Name, template));
            }

            success = OpenSearchMenu(posX, posY, mis, OnCanvasSearchMenuItemSelected);
        }
        else if (Instance.InteractionManager.CurrentlyHit is NodeVisual nui)
        {
            success = OpenContextMenu(posX, posY, nui);
        }

        return success;
    }

    protected override void OnSetup()
    {
        Raylib_cs.Raylib.SetExitKey(Raylib_cs.KeyboardKey.Null);

        nodeToNodeUIDict = [];
        portToPortUIDict = [];
        varToNodeUIsDict = [];

        graph = new Graph();
        graph.OnVariableAdded += NotifyAddVar;
        graph.OnVariableRemoved += NotifyRemoveVar;
        graph.OnNodeAdded += NotifyAddNode;
        graph.OnNodeUpdated += NotifyUpdateNode;
        graph.OnNodeRemoved += NotifyRemoveNode;
        graph.OnPortAdded += NotifyAddPort;
        graph.OnPortRemoved += NotifyRemovePort;
        graph.OnConnectionRemoved += NotifyRemoveConnection;

        graph.Types.RegisterDefaultTypes();
        SetupDefaultTypeColors();

        connectionUIManager = new(null);

        NodeRegistry = new NodeRegistry();

        NodeRegistry.RegisterNode(new NodeTemplate("Empty Node", "Basic", [], [], [
            (UIElementType.Text, new TextDesc("Test Empty!", 15, Raylib_cs.Raylib.Fade(Raylib_cs.Color.White, 0.65f))),
            (UIElementType.InputField, new InputFieldDesc("Enter!", "", 150, 25)),
            (UIElementType.Button, new ButtonDesc("PRESS!", 150, 25, (btn) => { Console.WriteLine("CLICKED!"); })),
            (UIElementType.Toggle, new ToggleDesc("Toggle", true, 38, 20, (toggle) => { Console.WriteLine("Toggled: " + toggle.IsOn); }))
        ]));

        NodeRegistry.RegisterNode(new NodeTemplate("Vertical Entry Node", "Basic", ["String"], ["Execution", "String"],
            [(UIElementType.Text, new TextDesc("Vertical Text", 15, Raylib_cs.Color.White))], NodeFlow.Vertical));

        NodeRegistry.RegisterNode(new NodeTemplate("Vertical Node", "Basic", ["Execution", "String"], ["Execution", "String"],
            [(UIElementType.Text, new TextDesc("Vertical Text", 15, Raylib_cs.Color.White))], NodeFlow.Vertical, false));

        NodeRegistry.RegisterNode(new NodeTemplate("Class Node", "Basic", 
            ["Execution", "String", "Int", "Number"], 
            ["Execution", "Int", "String"], [
            (UIElementType.Text, new TextDesc("Enter Text:", 15, Raylib_cs.Raylib.Fade(Raylib_cs.Color.White, 0.65f))),
            (UIElementType.InputField, new InputFieldDesc("", "", 150, 25)),
            (UIElementType.Selectable, new SelectableDesc("Red", false, 150, 25, (sel) => { })),
            (UIElementType.Selectable, new SelectableDesc("Blue", false, 150, 25, (sel) => { })),
            (UIElementType.Group, new HorizontalGroupDesc("", 50,
                [(UIElementType.Text, new TextDesc("Enter: ", 15, Raylib_cs.Color.Red)),
                (UIElementType.InputField, new InputFieldDesc("edit...", "", null, null))], 150, 25)),
            (UIElementType.Button, new ButtonDesc("TTEESSTT", 150, 25, (b) => Console.WriteLine("CLICKED! " + b)))
        ]));

        NodeRegistry.RegisterNode(new NodeTemplate("Math Add", "Math",
            ["Float", "Float"], ["Float"], [
            (UIElementType.Text, new TextDesc("A + B", 15, Raylib_cs.Color.White))
        ]));

        canvas = new Canvas((int)ScreenWidth, (int)ScreenHeight, OnCanvasContextClick);

        VariablePanel varPan = new(10, 20, OnSelectVar, OnAddVar, OnRemoveVar, OnRenameVariable, OnChangeVarType,
        (x, y, items, selectItemAction) =>
        {
            OpenSearchMenu(x, y, items, (payload) =>
            {
                selectItemAction.Invoke(payload);
                canvas.CloseTransPanel(searchMenuIdx);
            });
        }, canvas);

        canvas.AddPanel(varPan, false, false);

        InspectorPanel inPan = new(-10, 20, OnRenameVariable, OnChangeVarType, OnChangeVarValue,
        (x, y, items, selectItemAction) =>
        {
            OpenSearchMenu(x, y, items, (payload) =>
            {
                selectItemAction.Invoke(payload);
                canvas.CloseTransPanel(searchMenuIdx);
            });
        }, canvas, ParentBasis.TopRight);
        
        canvas.AddPanel(inPan, false, false);

        DemoPanel demoPanel = new(60, 70, canvas);
        canvas.AddPanel(demoPanel, true, false);

        uiElements.Add(canvas);

        actors.Add(new GraphBG(15000, 15000, new Vector2(-7500, -7500)));
        actors.Add(connectionUIManager);

        searchMenuIdx = canvas.AddPanel(null, false, true);
        contextMenuIdx = canvas.AddPanel(null, false, true);
    }

    protected override void OnUpdateScreen(int newW, int newH)
    {
        canvas.Size = new Vector2(newW, newH);
    }

    protected override void OnUpdate() { }

    protected override void OnRender()
    {
        RenderDeferred();
    }

    private void RenderDeferred()
    {
        for (int i = 0; i < deferredRects.Count; i++)
            Raylib_cs.Raylib.DrawRectangle(
                                (int)deferredRects[i].rect.X,
                                (int)deferredRects[i].rect.Y,
                                (int)deferredRects[i].rect.Width,
                                (int)deferredRects[i].rect.Height,
                                deferredRects[i].color
                                );

        // Clear after draw to wait for next frame.
        deferredRects.Clear();
    }

    public void DrawDeferredWorldSpaceRect(Rectangle rect, Raylib_cs.Color color)
    {
        deferredRects.Add((rect, color));
    }

    public void ClearWorkspace()
    {
        graph.Clear();

        currentlySelectedObjectId = null;

        // Collect node visual actors to delete
        List<NodeVisual> nodesToDelete = new();
        foreach (var nui in nodeToNodeUIDict.Values)
        {
            if (nui != null) nodesToDelete.Add(nui);
        }

        foreach (var nui in nodesToDelete)
        {
            actors.Remove(nui);
            nui.Delete();
        }

        nodeToNodeUIDict.Clear();
        portToPortUIDict.Clear();
        varToNodeUIsDict.Clear();
    }

    public void ReconstructGraph(GraphSaveData data)
    {
        ClearWorkspace();

        IdGen.SetCurrentId(data.MaxId);

        if (data.NodeTemplates != null && data.NodeTemplates.Count > 0)
        {
            NodeRegistry.Clear();
            foreach (var tData in data.NodeTemplates)
            {
                var uiElements = new List<(UIElementType, UIElementDescription)>();
                foreach (var uiData in tData.UIElements)
                {
                    uiElements.Add(graphSerializer.DeserializeUIElement(uiData));
                }

                object? payload = null;
                if (tData.Payload.HasValue)
                {
                    if (tData.Payload.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                        payload = tData.Payload.Value.GetInt32();
                    else if (tData.Payload.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        payload = tData.Payload.Value.GetString();
                }

                NodeFlow flow = (NodeFlow)tData.Flow;
                NodeTemplate template = new(tData.Id, tData.Name, tData.Category, tData.InputPortTypeNames, tData.OutputPortTypeNames, uiElements, flow, tData.ShowHeader, payload);
                NodeRegistry.RegisterNode(template);
            }
        }

        foreach (var vData in data.Variables)
        {
            DataType? varType = graph.Types.GetType(vData.TypeName);
            if (varType == null) continue;

            object value = null;
            if (varType.CSharpType == typeof(int)) value = vData.Value.GetInt32();
            else if (varType.CSharpType == typeof(float)) value = vData.Value.GetSingle();
            else if (varType.CSharpType == typeof(string)) value = vData.Value.GetString();
            else if (varType.CSharpType == typeof(bool)) value = vData.Value.GetBoolean();
            
            Variable v = new(vData.Id, vData.Name, varType, value ?? 0);
            graph.AddVariableExplicit(v);

            if (!varToNodeUIsDict.ContainsKey(v.Id))
                varToNodeUIsDict.Add(v.Id, []);
        }

        foreach (var nd in data.Nodes)
        {
            NodeTemplate? template = NodeRegistry.GetTemplate(nd.TemplateId);

            // Fallback: If exact template ID was not found (e.g. from an older file or unlinked variable template),
            // search for a matching template by port signatures/payload.
            if (template == null)
            {
                foreach (var t in NodeRegistry.AllTemplates)
                {
                    if (t.InputPortTypeNames.Count == nd.InputPorts.Count &&
                        t.OutputPortTypeNames.Count == nd.OutputPorts.Count)
                    {
                        template = t;
                        nd.TemplateId = t.Id;
                        break;
                    }
                }
            }

            Node n = graph.AddNodeExplicit(nd.Id, nd.TemplateId);

            foreach (var pData in nd.InputPorts)
            {
                DataType? t = graph.Types.GetType(pData.DataTypeName);
                if (t != null) graph.AddInputPortExplicit(n, pData.Id, pData.Name, t);
            }

            foreach (var pData in nd.OutputPorts)
            {
                DataType? t = graph.Types.GetType(pData.DataTypeName);
                if (t != null) graph.AddOutputPortExplicit(n, pData.Id, pData.Name, t);
            }

            List<(UIElementType, UIElementDescription)> uiElems = template != null ? template.UIElements : [];
            string titleName = template != null ? template.Name : "Node " + n.Id;
            NodeFlow nodeFlow = (NodeFlow)nd.Flow;

            NodeVisual nodeVis = new(n.Id, uiElems, titleName, nd.PositionX, nd.PositionY, nodeFlow, nd.ShowHeader);
            if (nd.UIElementValues != null && nd.UIElementValues.Count > 0)
            {
                nodeVis.SetUIStatePayloads(nd.UIElementValues);
            }
            actors.Add(nodeVis);

            if (template != null && template.Payload is int varId)
            {
                if (varToNodeUIsDict.TryGetValue(varId, out List<NodeVisual>? list))
                    list.Add(nodeVis);
                else varToNodeUIsDict.Add(varId, [nodeVis]);
            }
        }

        foreach (var cData in data.Connections)
        {
            Connection c = new(cData.Id, cData.SourcePortId, cData.TargetPortId);
            graph.AddConnectionExplicit(c);

            bool hasSource = portToPortUIDict.TryGetValue(cData.SourcePortId, out PortVisual? sourcePUI);
            bool hasTarget = portToPortUIDict.TryGetValue(cData.TargetPortId, out PortVisual? targetPUI);

            if (hasSource && hasTarget && sourcePUI != null && targetPUI != null)
            {
                connectionUIManager.OnAddNewConnection(sourcePUI, targetPUI);
            }
            else
            {
                Console.WriteLine($"Warning: Could not connect ports visually ({cData.SourcePortId} -> {cData.TargetPortId}) because one or both PortVisuals were not found.");
            }
        }

        if (data.Panels != null && canvas != null)
        {
            canvas.RestorePanelsSaveData(data.Panels);
        }
    }
}
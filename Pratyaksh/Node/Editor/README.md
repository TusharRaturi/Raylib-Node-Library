# Pratyaksh.Node.Editor

[![NuGet Version](https://img.shields.io/nuget/v/Pratyaksh.Node.Editor.svg)](https://www.nuget.org/packages/Pratyaksh.Node.Editor/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

**Pratyaksh.Node.Editor** is an interactive, visual node-graph editing canvas and toolkit built on top of `Pratyaksh.Node.Core`, `Pratyaksh.UI`, `Pratyaksh.Core`, and [Raylib-cs](https://github.com/Subtixx/Raylib-cs). It provides a full-featured node editor with cubic bezier wire connections, infinite canvas navigation, embedded UI widgets inside node bodies, property inspection, variable management, and complete JSON serialization.

---

## 📦 Key Features

- **Interactive Visual Canvas**:
  - 2D Editor Camera (`EditorCamera2D`) with smooth right-mouse panning and mouse-wheel zooming.
  - Infinite grid background (`GraphBG`) with coordinate transformations.
- **Dynamic Node Visuals**:
  - `NodeVisual`: Rounded node card rendering, configurable vertical/horizontal execution flows, hidable colored headers, dragging, port pins, and auto-expanding heights.
  - **Embedded Node Widgets**: Embed arbitrary UI elements (`Text`, `InputField`, `Button`, `Toggle`, `Selectable`, `Group`) directly inside the node body.
- **Wire Routing & Visual Connections**:
  - `WireVisual` & `ConnectionVisualManager`: Smooth cubic bezier curve wires with type-based color coding and interactive drag-and-drop linking.
- **Integrated Tooling & Sidebars**:
  - `VariablePanel`: Manage graph-scoped variables (add, remove, rename, and change data types).
  - `InspectorPanel`: Real-time inspector with two-way data-bound inputs for variable names, types, and values.
  - `SearchMenu` & `ContextMenu`: Fuzzy node palette search and contextual actions.
- **Full JSON Persistence**:
  - `GraphSerializer`: Save and load node layouts, embedded widget states, connections, variables, templates, and panel configurations via JSON.
- **Keyboard Shortcuts**:
  - `Ctrl + S`: Serialize graph to `save.json`.
  - `Ctrl + L`: Load and reconstruct graph from `save.json`.

---

## 🚀 Installation

Install via the .NET CLI:

```bash
dotnet add package Pratyaksh.Node.Editor
```

Or via the NuGet Package Manager:

```powershell
Install-Package Pratyaksh.Node.Editor
```

---

## 🛠️ Usage Examples

### 1. Launching the Node Editor

```csharp
using Pratyaksh.Node.Editor;

public static class Program
{
    public static void Main(string[] args)
    {
        // 1. Initialize editor engine with window resolution (1024x576)
        NodeEditorEngine engine = new(1024, 576);

        // 2. Start the interactive run loop
        engine.Start();
    }
}
```

### 2. Registering Custom Node Templates

Define custom node types with input/output port signatures and embedded UI widgets:

```csharp
using Pratyaksh.Node.Editor;
using Pratyaksh.UI;
using Raylib_cs;

// Register a custom Math Add Node template
NodeEditorEngine.NodeRegistry.RegisterNode(new NodeTemplate(
    name: "Math Add",
    category: "Math",
    inputPortTypeNames: ["Float", "Float"],
    outputPortTypeNames: ["Float"],
    uiElements: [
        (UIElementType.Text, new TextDesc("Computes: A + B", Color.White))
    ],
    flow: NodeFlow.Horizontal,
    showHeader: true
));

// Register an interactive Form Node template with embedded inputs
NodeEditorEngine.NodeRegistry.RegisterNode(new NodeTemplate(
    name: "User Greeting",
    category: "Utility",
    inputPortTypeNames: ["Execution"],
    outputPortTypeNames: ["Execution", "String"],
    uiElements: [
        (UIElementType.Text, new TextDesc("Name:", Color.LightGray)),
        (UIElementType.InputField, new InputFieldDesc("Enter name...", "", 150, 25)),
        (UIElementType.Button, new ButtonDesc("Say Hello", 150, 25, (btn) =>
        {
            Console.WriteLine("Button clicked inside node visual!");
        }))
    ],
    flow: NodeFlow.Vertical,
    showHeader: false
));
```

### 3. Programmatic Serialization

```csharp
using Pratyaksh.Core;
using Pratyaksh.Core.Serialization;
using Pratyaksh.Node.Editor;
using Pratyaksh.Node.Editor.Serialization;

// 1. Instantiate serializer
var serializer = new GraphSerializer(new JsonSerializationEngine());

// 2. Serialize current editor graph state to JSON
string json = serializer.Serialize(
    NodeEditorEngine.Graph,
    NodeEditorEngine.NodeToNodeUIDict,
    NodeEditorEngine.NodeRegistry,
    IdGen.CurrentId,
    NodeEditorEngine.Canvas
);

File.WriteAllText("graph.json", json);

// 3. Load and reconstruct graph
string loadedJson = File.ReadAllText("graph.json");
var graphData = serializer.Deserialize(loadedJson);

if (graphData != null)
{
    // Reconstruct workspace visually
    engine.ReconstructGraph(graphData);
}
```

---

## 📄 License

This project is licensed under the MIT License. See [LICENSE](../../LICENSE) for details.

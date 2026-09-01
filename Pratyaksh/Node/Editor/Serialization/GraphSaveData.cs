namespace Pratyaksh.Node.Editor.Serialization;

using Pratyaksh.Core.Serialization;
using System.Collections.Generic;
using System.Text.Json;

public class GraphSaveData : BaseDTO
{
    public int MaxId { get; set; }
    public List<NodeTemplateSaveData> NodeTemplates { get; set; } = new();
    public List<VariableSaveData> Variables { get; set; } = new();
    public List<NodeSaveData> Nodes { get; set; } = new();
    public List<ConnectionSaveData> Connections { get; set; } = new();
    public Dictionary<string, JsonElement> Panels { get; set; } = new();
}

public class NodeTemplateSaveData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> InputPortTypeNames { get; set; } = new();
    public List<string> OutputPortTypeNames { get; set; } = new();
    public List<UIElementSaveData> UIElements { get; set; } = new();
    public int Flow { get; set; } // 0 for Vertical, 1 for Horizontal
    public bool ShowHeader { get; set; } = true;
    public JsonElement? Payload { get; set; }
}

public class UIElementSaveData
{
    public int ElementType { get; set; }
    public string Text { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public byte ColorR { get; set; } = 255;
    public byte ColorG { get; set; } = 255;
    public byte ColorB { get; set; } = 255;
    public byte ColorA { get; set; } = 255;
    public string PlaceholderText { get; set; } = string.Empty;
    public bool StartingState { get; set; }
    public List<string> Options { get; set; } = new();
    public int SelectedIndex { get; set; }
    public int Spacing { get; set; }
    public float Value { get; set; }
    public float MinValue { get; set; }
    public float MaxValue { get; set; }
    public float? Step { get; set; }
    public bool ShowValue { get; set; } = true;
    public string? Format { get; set; }
    public List<UIElementSaveData> Children { get; set; } = new();
}

public class VariableSaveData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
}

public class NodeSaveData
{
    public int Id { get; set; }
    public int TemplateId { get; set; } = -1;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public int Flow { get; set; } // 0 for Vertical, 1 for Horizontal
    public bool ShowHeader { get; set; } = true;
    public List<PortSaveData> InputPorts { get; set; } = new();
    public List<PortSaveData> OutputPorts { get; set; } = new();
    public List<JsonElement?> UIElementValues { get; set; } = new();
}

public class PortSaveData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int FlowType { get; set; } // 0 for Input, 1 for Output
    public string DataTypeName { get; set; } = string.Empty;
}

public class ConnectionSaveData
{
    public int Id { get; set; }
    public int SourcePortId { get; set; }
    public int TargetPortId { get; set; }
}

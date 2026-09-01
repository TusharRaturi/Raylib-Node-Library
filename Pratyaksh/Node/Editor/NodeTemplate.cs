using Pratyaksh.Core;
using Pratyaksh.UI;

namespace Pratyaksh.Node.Editor;

public class NodeTemplate
{
    public int Id { get; }
    public string Name { get; }
    public string Category { get; }
    
    public List<string> InputPortTypeNames { get; }
    public List<string> OutputPortTypeNames { get; }
    public List<(UIElementType elemType, UIElementDescription elemDesc)> UIElements { get; }
    public NodeFlow Flow { get; }
    public bool ShowHeader { get; }
    public object? Payload { get; }

    public NodeTemplate(string name, string category, 
                        List<string> inputPortTypeNames, 
                        List<string> outputPortTypeNames, 
                        List<(UIElementType, UIElementDescription)> uiElements,
                        object? payload = null) 
        : this(IdGen.GetNewID(), name, category, inputPortTypeNames, outputPortTypeNames, uiElements, NodeFlow.Horizontal, true, payload)
    {
    }

    public NodeTemplate(string name, string category, 
                        List<string> inputPortTypeNames, 
                        List<string> outputPortTypeNames, 
                        List<(UIElementType, UIElementDescription)> uiElements,
                        NodeFlow flow,
                        bool showHeader = true,
                        object? payload = null) 
        : this(IdGen.GetNewID(), name, category, inputPortTypeNames, outputPortTypeNames, uiElements, flow, showHeader, payload)
    {
    }

    public NodeTemplate(int id, string name, string category, 
                        List<string> inputPortTypeNames, 
                        List<string> outputPortTypeNames, 
                        List<(UIElementType, UIElementDescription)> uiElements,
                        object? payload = null)
        : this(id, name, category, inputPortTypeNames, outputPortTypeNames, uiElements, NodeFlow.Horizontal, true, payload)
    {
    }

    public NodeTemplate(int id, string name, string category, 
                        List<string> inputPortTypeNames, 
                        List<string> outputPortTypeNames, 
                        List<(UIElementType, UIElementDescription)> uiElements,
                        NodeFlow flow,
                        bool showHeader = true,
                        object? payload = null)
    {
        Id = id;
        Name = name;
        Category = category;
        InputPortTypeNames = inputPortTypeNames;
        OutputPortTypeNames = outputPortTypeNames;
        UIElements = uiElements;
        Flow = flow;
        ShowHeader = showHeader;
        Payload = payload;
    }
}

using System.Numerics;
using Pratyaksh.Node.Core.DataModel;

namespace Pratyaksh.Node.Editor;

public class NodeRegistry
{
    private Dictionary<int, NodeTemplate> templatesById = [];
    private Dictionary<string, List<NodeTemplate>> templatesByCategory = [];

    public IReadOnlyList<NodeTemplate> AllTemplates => [.. templatesById.Values];

    public void Clear()
    {
        templatesById.Clear();
        templatesByCategory.Clear();
    }

    public void RegisterNode(NodeTemplate template)
    {
        templatesById[template.Id] = template;

        if (!templatesByCategory.ContainsKey(template.Category))
            templatesByCategory[template.Category] = [];
        
        if (!templatesByCategory[template.Category].Contains(template))
            templatesByCategory[template.Category].Add(template);
    }

    public void UnregisterNode(int id)
    {
        if (templatesById.TryGetValue(id, out var template))
        {
            templatesById.Remove(id);
            if (templatesByCategory.ContainsKey(template.Category))
            {
                templatesByCategory[template.Category].Remove(template);
                if (templatesByCategory[template.Category].Count == 0)
                {
                    templatesByCategory.Remove(template.Category);
                }
            }
        }
    }

    public void UnregisterNodeByPayload(object payload)
    {
        int? idToRemove = null;
        foreach (var template in templatesById.Values)
        {
            if (template.Payload != null && template.Payload.Equals(payload))
            {
                idToRemove = template.Id;
                break;
            }
        }
        if (idToRemove != null) UnregisterNode(idToRemove.Value);
    }

    public NodeTemplate? GetTemplate(int id)
    {
        return templatesById.TryGetValue(id, out var template) ? template : null;
    }

    public IReadOnlyList<NodeTemplate> GetTemplatesInCategory(string category)
    {
        return templatesByCategory.TryGetValue(category, out var list) ? list : [];
    }

    public IReadOnlyList<string> GetCategories()
    {
        return [.. templatesByCategory.Keys];
    }
    
    public NodeVisual? SpawnNode(Graph graph, int templateId, Vector2 position, NodeFlow? overrideFlow = null, bool? overrideShowHeader = null)
    {
        NodeTemplate? template = GetTemplate(templateId);
        if (template == null) return null;
        
        List<DataType> inPorts = [];
        foreach (var typeName in template.InputPortTypeNames)
        {
            DataType? t = graph.Types.GetType(typeName);
            if (t != null) inPorts.Add(t);
        }
        
        List<DataType> outPorts = [];
        foreach (var typeName in template.OutputPortTypeNames)
        {
            DataType? t = graph.Types.GetType(typeName);
            if (t != null) outPorts.Add(t);
        }

        Core.DataModel.Node n = graph.AddNode(template.Id, inPorts, outPorts);
        
        NodeFlow flow = overrideFlow ?? template.Flow;
        bool showHeader = overrideShowHeader ?? template.ShowHeader;
        NodeVisual nodeVis = new(n.Id, template.UIElements, template.Name, position.X, position.Y, flow, showHeader);
        return nodeVis;
    }
}

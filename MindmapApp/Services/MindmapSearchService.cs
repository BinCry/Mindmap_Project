using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MindmapApp.ViewModels;

namespace MindmapApp.Services;

public class MindmapSearchService
{
    public ObservableCollection<NodeViewModel> SearchNodes(IEnumerable<NodeViewModel> nodes, string keyword)
    {
        var results = new ObservableCollection<NodeViewModel>();
        if (nodes == null)
        {
            return results;
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            foreach (var node in nodes)
            {
                results.Add(node);
            }
            return results;
        }

        keyword = keyword.Trim();
        foreach (var node in nodes.Where(n => n.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                              (n.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)))
        {
            results.Add(node);
        }
        return results;
    }
}

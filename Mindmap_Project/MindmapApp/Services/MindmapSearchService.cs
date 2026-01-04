using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions; // Thêm thư viện Regex
using MindmapApp.ViewModels;

namespace MindmapApp.Services;

public class MindmapSearchService
{
    public ObservableCollection<NodeViewModel> SearchNodes(IEnumerable<NodeViewModel> nodes, string keyword)
    {
        var results = new ObservableCollection<NodeViewModel>();
        if (nodes == null) return results;

        if (string.IsNullOrWhiteSpace(keyword))
        {
            foreach (var node in nodes) results.Add(node);
            return results;
        }

        keyword = keyword.Trim();

        foreach (var node in nodes)
        {
            bool match = false;

            // 1. Tìm trong Tiêu đề
            if (node.Title != null && node.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                match = true;

            // 2. Tìm trong Nội dung RichText (ContentXaml)
            else if (!string.IsNullOrEmpty(node.ContentXaml))
            {
                // Lọc bỏ thẻ XAML để lấy chữ thuần: <Run>Hello</Run> -> Hello
                string plainText = Regex.Replace(node.ContentXaml, "<.*?>", " ");
                if (plainText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    match = true;
            }

            if (match)
            {
                results.Add(node);
            }
        }
        return results;
    }
}
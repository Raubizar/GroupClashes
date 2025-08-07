using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api.Clash;
using Autodesk.Navisworks.Api;

namespace GroupClashes
{
    public enum GroupMode 
    { 
        Category, 
        File, 
        Layer, 
        First, 
        Last, 
        LastUnique 
    }

    public class GroupingEngine
    {
        // TEMP: change this constant to pick the mode until UI is built
        const GroupMode Mode = GroupMode.File;

        static string GetProp(ModelItem mi, string prop)
        {
            foreach (var cat in mi.PropertyCategories)
                foreach (var p in cat.Properties)
                    if (p.DisplayName.Equals(prop, StringComparison.OrdinalIgnoreCase))
                        return p.Value?.ToDisplayString() ?? "";
            return "";
        }

        static string FileName(ModelItem mi) =>
            mi.Model?.SourceFileName
            ?? GetProp(mi, "Source File Name")
            ?? "Unknown";

        static string UniquePair(ModelItem a, ModelItem b)
        {
            string g1 = a.InstanceGuid?.ToString() ?? a.SourceGuid.ToString();
            string g2 = b.InstanceGuid?.ToString() ?? b.SourceGuid.ToString();
            return $"Pair-{string.Concat(new[]{g1,g2}.OrderBy(s=>s))}";
        }

        public static void GroupClashes(ClashTest clashTest)
        {
            var clashResults = new List<ClashResult>();
            var groups = new Dictionary<string, ClashResultGroup>();

            // Collect all individual clash results
            foreach (var child in clashTest.Children)
            {
                if (child is ClashResult result)
                    clashResults.Add(result);
            }

            // Group clashes based on selected mode
            foreach (var clash in clashResults)
            {
                var item1 = clash.Item1;
                var item2 = clash.Item2;
                
                // Get category for fallback
                string Category = item1?.PropertyCategories?.FirstOrDefault()?.DisplayName ?? "Unknown";

                string keyCore = Mode switch
                {
                    GroupMode.File       => FileName(item1),
                    GroupMode.Layer      => GetProp(item1, "Layer"),
                    GroupMode.First      => item1.InstanceGuid?.ToString() ?? item1.DisplayName,
                    GroupMode.Last       => item2.InstanceGuid?.ToString() ?? item2.DisplayName,
                    GroupMode.LastUnique => UniquePair(item1, item2),
                    _                    => Category
                };

                string key = $"{keyCore} | [{0}] {Category}";

                if (!groups.ContainsKey(key))
                {
                    var group = new ClashResultGroup();
                    group.DisplayName = key;
                    groups[key] = group;
                }

                groups[key].Children.Add(clash);
            }

            // Replace clash test contents with groups
            using (Transaction trans = Application.MainDocument.BeginTransaction("Group Clashes"))
            {
                clashTest.Children.Clear();
                
                foreach (var group in groups.Values)
                {
                    if (group.Children.Count > 1)
                        clashTest.Children.Add(group);
                    else if (group.Children.Count == 1)
                        clashTest.Children.Add(group.Children[0]);
                }
                
                trans.Commit();
            }
        }
    }
}

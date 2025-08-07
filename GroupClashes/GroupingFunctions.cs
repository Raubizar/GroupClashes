using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api.Clash;
using Autodesk.Navisworks.Api;
using System.ComponentModel;

namespace GroupClashes
{
    class GroupingFunctions
    {
        public static void GroupClashes(ClashTest selectedClashTest, GroupingMode groupingMode, GroupingMode subgroupingMode, bool keepExistingGroups)
        {
            //Get existing clash result
            List<ClashResult> clashResults = GetIndividualClashResults(selectedClashTest,keepExistingGroups).ToList();
            List<ClashResultGroup> clashResultGroups = new List<ClashResultGroup>();

            //Create groups according to the first grouping mode
            CreateGroup(ref clashResultGroups, groupingMode, clashResults,"");

            //Optionnaly, create subgroups
            if (subgroupingMode != GroupingMode.None)
            {
                CreateSubGroups(ref clashResultGroups, subgroupingMode);
            }

            //Remove groups with only one clash
            List<ClashResult> ungroupedClashResults = RemoveOneClashGroup(ref clashResultGroups);

            //Backup the existing group, if necessary
            if (keepExistingGroups) clashResultGroups.AddRange(BackupExistingClashGroups(selectedClashTest));

            //Process these groups and clashes into the clash test
            ProcessClashGroup(clashResultGroups, ungroupedClashResults, selectedClashTest);
        }

        private static void CreateGroup(ref List<ClashResultGroup> clashResultGroups, GroupingMode groupingMode, List<ClashResult> clashResults, string initialName)
        {
            //group all clashes
            switch (groupingMode)
            {
                case GroupingMode.None:
                    return;
                case GroupingMode.Level:
                    clashResultGroups = GroupByLevel(clashResults, initialName);
                    break;
                case GroupingMode.GridIntersection:
                    clashResultGroups = GroupByGridIntersection(clashResults, initialName);
                    break;
                case GroupingMode.SelectionA:
                case GroupingMode.SelectionB:
                    clashResultGroups = GroupByElementOfAGivenSelection(clashResults, groupingMode, initialName);
                    break;
                case GroupingMode.ModelA:
                case GroupingMode.ModelB:
                    clashResultGroups = GroupByElementOfAGivenModel(clashResults, groupingMode, initialName);
                    break;
                case GroupingMode.ApprovedBy:
                case GroupingMode.AssignedTo:
                case GroupingMode.Status:
                    clashResultGroups = GroupByProperties(clashResults, groupingMode, initialName);
                    break;
                case GroupingMode.File:
                    clashResultGroups = GroupByFile(clashResults, initialName);
                    break;
                case GroupingMode.Layer:
                    clashResultGroups = GroupByLayer(clashResults, initialName);
                    break;
                case GroupingMode.First:
                    clashResultGroups = GroupByFirst(clashResults, initialName);
                    break;
                case GroupingMode.Last:
                    clashResultGroups = GroupByLast(clashResults, initialName);
                    break;
                case GroupingMode.LastUnique:
                    clashResultGroups = GroupByLastUnique(clashResults, initialName);
                    break;
            }
        }

        private static void CreateSubGroups(ref List<ClashResultGroup> clashResultGroups, GroupingMode mode)
        {
            List<ClashResultGroup> clashResultSubGroups = new List<ClashResultGroup>();

            foreach (ClashResultGroup group in clashResultGroups)
            {

                List<ClashResult> clashResults = new List<ClashResult>();

                foreach (SavedItem item in group.Children)
                {
                    ClashResult clashResult = item as ClashResult;
                    if (clashResult != null)
                    {
                        clashResults.Add(clashResult);
                    }
                }

                List<ClashResultGroup> clashResultTempSubGroups = new List<ClashResultGroup>();
                CreateGroup(ref clashResultTempSubGroups, mode, clashResults,group.DisplayName + "_");
                clashResultSubGroups.AddRange(clashResultTempSubGroups);
            }

            clashResultGroups = clashResultSubGroups;
        }

        public static void UnGroupClashes(ClashTest selectedClashTest)
        {
            List<ClashResultGroup> groups = new List<ClashResultGroup>();
            List<ClashResult> results = GetIndividualClashResults(selectedClashTest,false).ToList();
            List<ClashResult> copiedResult = new List<ClashResult>();

            foreach (ClashResult result in results)
            {
                copiedResult.Add((ClashResult)result.CreateCopy());
            }

            //Process this empty group list and clashes into the clash test
            ProcessClashGroup(groups, copiedResult, selectedClashTest);

        }

        #region grouping functions
        private static List<ClashResultGroup> GroupByLevel(List<ClashResult> results, string initialName)
        {
            //I already checked if it exists
            GridSystem gridSystem = Application.MainDocument.Grids.ActiveSystem;
            Dictionary<GridLevel, ClashResultGroup> groups = new Dictionary<GridLevel, ClashResultGroup>();
            ClashResultGroup currentGroup;

            //Create a group for the null GridIntersection
            ClashResultGroup nullGridGroup = new ClashResultGroup();
            nullGridGroup.DisplayName = initialName + "No Level";

            foreach (ClashResult result in results)
            {
                //Cannot add original result to new clash test, so I create a copy
                ClashResult copiedResult = (ClashResult)result.CreateCopy();
                
                if (gridSystem.ClosestIntersection(copiedResult.Center) != null)
                {
                    GridLevel closestLevel = gridSystem.ClosestIntersection(copiedResult.Center).Level;

                    if (!groups.TryGetValue(closestLevel, out currentGroup))
                    {
                        currentGroup = new ClashResultGroup();
                        string displayName = closestLevel.DisplayName;
                        if (string.IsNullOrEmpty(displayName)) { displayName = "Unnamed Level"; }
                        currentGroup.DisplayName = initialName + displayName;
                        groups.Add(closestLevel, currentGroup);
                    }
                    currentGroup.Children.Add(copiedResult);
                }
                else
                {
                    nullGridGroup.Children.Add(copiedResult);
                }
            }

            IOrderedEnumerable<KeyValuePair<GridLevel, ClashResultGroup>> list = groups.OrderBy(key => key.Key.Elevation);
            groups = list.ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);

            List<ClashResultGroup> groupsByLevel = groups.Values.ToList();
            if (nullGridGroup.Children.Count != 0) groupsByLevel.Add(nullGridGroup);

            return groupsByLevel;
        }

        private static List<ClashResultGroup> GroupByGridIntersection(List<ClashResult> results, string initialName)
        {
            //I already check if it exists
            GridSystem gridSystem = Application.MainDocument.Grids.ActiveSystem;
            Dictionary<GridIntersection, ClashResultGroup> groups = new Dictionary<GridIntersection, ClashResultGroup>();
            ClashResultGroup currentGroup;

            //Create a group for the null GridIntersection
            ClashResultGroup nullGridGroup = new ClashResultGroup();
            nullGridGroup.DisplayName = initialName + "No Grid intersection";

            foreach (ClashResult result in results)
            {
                //Cannot add original result to new clash test, so I create a copy
                ClashResult copiedResult = (ClashResult)result.CreateCopy();

                if (gridSystem.ClosestIntersection(copiedResult.Center) != null)
                {
                    GridIntersection closestGridIntersection = gridSystem.ClosestIntersection(copiedResult.Center);

                    if (!groups.TryGetValue(closestGridIntersection, out currentGroup))
                    {
                        currentGroup = new ClashResultGroup();
                        string displayName = closestGridIntersection.DisplayName;
                        if (string.IsNullOrEmpty(displayName)) { displayName = "Unnamed Grid Intersection"; }
                        currentGroup.DisplayName = initialName + displayName;
                        groups.Add(closestGridIntersection, currentGroup);
                    }
                    currentGroup.Children.Add(copiedResult);
                }
                else
                {
                    nullGridGroup.Children.Add(copiedResult);
                }
            }
           
            IOrderedEnumerable<KeyValuePair<GridIntersection, ClashResultGroup>> list = groups.OrderBy(key => key.Key.Position.X).OrderBy(key => key.Key.Level.Elevation);
            groups = list.ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);

            List<ClashResultGroup> groupsByGridIntersection = groups.Values.ToList();
            if (nullGridGroup.Children.Count != 0) groupsByGridIntersection.Add(nullGridGroup);

            return groupsByGridIntersection;
        }

        private static List<ClashResultGroup> GroupByElementOfAGivenSelection(List<ClashResult> results, GroupingMode mode, string initialName)
        {
            Dictionary<ModelItem, ClashResultGroup> groups = new Dictionary<ModelItem, ClashResultGroup>();
            ClashResultGroup currentGroup;
            List<ClashResultGroup> emptyClashResultGroups = new List<ClashResultGroup>();

            foreach (ClashResult result in results)
            {

                //Cannot add original result to new clash test, so I create a copy
                ClashResult copiedResult = (ClashResult)result.CreateCopy();
                ModelItem modelItem = null;

                if (mode == GroupingMode.SelectionA)
                {
                    if (copiedResult.CompositeItem1 != null)
                    {
                        modelItem = GetSignificantAncestorOrSelf(copiedResult.CompositeItem1);
                    }
                    else if (copiedResult.CompositeItem2 != null)
                    {
                        modelItem = GetSignificantAncestorOrSelf(copiedResult.CompositeItem2);
                    }
                }
                else if (mode == GroupingMode.SelectionB)
                {
                    if (copiedResult.CompositeItem2 != null)
                    {
                        modelItem = GetSignificantAncestorOrSelf(copiedResult.CompositeItem2);
                    }
                    else if (copiedResult.CompositeItem1 != null)
                    {
                        modelItem = GetSignificantAncestorOrSelf(copiedResult.CompositeItem1);
                    }
                }

                string displayName = "Empty clash";
                if (modelItem != null)
                {
                    displayName = modelItem.DisplayName;
                    //Create a group
                    if (!groups.TryGetValue(modelItem, out currentGroup))
                    {
                        currentGroup = new ClashResultGroup();
                        if (string.IsNullOrEmpty(displayName)){ displayName = modelItem.Parent.DisplayName; }
                        if (string.IsNullOrEmpty(displayName)) { displayName = "Unnamed Parent"; }
                        currentGroup.DisplayName = initialName + displayName;
                        groups.Add(modelItem, currentGroup);
                    }

                    //Add to the group
                    currentGroup.Children.Add(copiedResult);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("test");
                    ClashResultGroup oneClashResultGroup = new ClashResultGroup();
                    oneClashResultGroup.DisplayName = "Empty clash";
                    oneClashResultGroup.Children.Add(copiedResult);
                    emptyClashResultGroups.Add(oneClashResultGroup);
                }




            }

            List<ClashResultGroup> allGroups = groups.Values.ToList();
            allGroups.AddRange(emptyClashResultGroups);
            return allGroups;
        }

        private static List<ClashResultGroup> GroupByElementOfAGivenModel(List<ClashResult> results, GroupingMode mode, string initialName)
        {
            Dictionary<ModelItem, ClashResultGroup> groups = new Dictionary<ModelItem, ClashResultGroup>();
            ClashResultGroup currentGroup;
            List<ClashResultGroup> emptyClashResultGroups = new List<ClashResultGroup>();

            foreach (ClashResult result in results)
            {

                //Cannot add original result to new clash test, so I create a copy
                ClashResult copiedResult = (ClashResult)result.CreateCopy();
                // ModelItem modelItem = null;
                ModelItem rootModel = null;

                if (mode == GroupingMode.ModelA)
                {
                    if (copiedResult.CompositeItem1 != null)
                    {
                        rootModel = GetFileAncestor(copiedResult.CompositeItem1);
                    }
                    else if (copiedResult.CompositeItem2 != null)
                    {
                        rootModel = GetFileAncestor(copiedResult.CompositeItem2);
                    }
                }
                else if (mode == GroupingMode.ModelB)
                {
                    if (copiedResult.CompositeItem2 != null)
                    {
                        rootModel = GetFileAncestor(copiedResult.CompositeItem2);
                    }
                    else if (copiedResult.CompositeItem1 != null)
                    {
                        rootModel = GetFileAncestor(copiedResult.CompositeItem1);
                    }
                }

                string displayName = "Empty clash";
                if (rootModel != null)
                {
                    displayName = rootModel.DisplayName;
                    //Create a group
                    if (!groups.TryGetValue(rootModel, out currentGroup))
                    {
                        currentGroup = new ClashResultGroup();
                        // if (string.IsNullOrEmpty(displayName)) { displayName = rootModel.Parent.DisplayName; }
                        if (string.IsNullOrEmpty(displayName)) { displayName = "Unnamed Model"; }
                        currentGroup.DisplayName = initialName + displayName;
                        groups.Add(rootModel, currentGroup);
                    }

                    //Add to the group
                    currentGroup.Children.Add(copiedResult);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("test");
                    ClashResultGroup oneClashResultGroup = new ClashResultGroup();
                    oneClashResultGroup.DisplayName = "Empty clash";
                    oneClashResultGroup.Children.Add(copiedResult);
                    emptyClashResultGroups.Add(oneClashResultGroup);
                }
            }

            List<ClashResultGroup> allGroups = groups.Values.ToList();
            allGroups.AddRange(emptyClashResultGroups);
            return allGroups;
        }

        private static List<ClashResultGroup> GroupByProperties(List<ClashResult> results, GroupingMode mode, string initialName)
        {
            Dictionary<string, ClashResultGroup> groups = new Dictionary<string, ClashResultGroup>();
            ClashResultGroup currentGroup;

            foreach (ClashResult result in results)
            {
                //Cannot add original result to new clash test, so I create a copy
                ClashResult copiedResult = (ClashResult)result.CreateCopy();
                string clashProperty = null;

                if (mode == GroupingMode.ApprovedBy)
                {
                    clashProperty = copiedResult.ApprovedBy;
                }
                else if (mode == GroupingMode.AssignedTo)
                {
                    clashProperty = copiedResult.AssignedTo;
                }
                else if (mode == GroupingMode.Status)
                {
                    clashProperty = copiedResult.Status.ToString();
                }

                if (string.IsNullOrEmpty(clashProperty)) { clashProperty = "Unspecified"; }

                if (!groups.TryGetValue(clashProperty, out currentGroup))
                {
                    currentGroup = new ClashResultGroup();
                    currentGroup.DisplayName = initialName + clashProperty;
                    groups.Add(clashProperty, currentGroup);
                }
                currentGroup.Children.Add(copiedResult);
            }

            return groups.Values.ToList();
        }

        #endregion


        #region helpers
        private static void ProcessClashGroup(List<ClashResultGroup> clashGroups, List<ClashResult> ungroupedClashResults, ClashTest selectedClashTest)
        {
            using (Transaction tx = Application.MainDocument.BeginTransaction("Group clashes"))
            {
                ClashTest copiedClashTest = (ClashTest)selectedClashTest.CreateCopyWithoutChildren();
                //When we replace theTest with our new test, theTest will be disposed. If the operation is cancelled, we need a non-disposed copy of theTest with children to sub back in.
                ClashTest BackupTest = (ClashTest)selectedClashTest.CreateCopy();
                DocumentClash documentClash = Application.MainDocument.GetClash();
                int indexOfClashTest = documentClash.TestsData.Tests.IndexOf(selectedClashTest);
                documentClash.TestsData.TestsReplaceWithCopy(indexOfClashTest, copiedClashTest);

                int CurrentProgress = 0;
                int TotalProgress = ungroupedClashResults.Count + clashGroups.Count;
                Progress ProgressBar = Application.BeginProgress("Copying Results", "Copying results from " + selectedClashTest.DisplayName + " to the Group Clashes pane...");
                foreach (ClashResultGroup clashResultGroup in clashGroups)
                {
                    if (ProgressBar.IsCanceled) break;
                    documentClash.TestsData.TestsAddCopy((GroupItem)documentClash.TestsData.Tests[indexOfClashTest], clashResultGroup);
                    CurrentProgress++;
                    ProgressBar.Update((double)CurrentProgress / TotalProgress);
                }
                foreach (ClashResult clashResult in ungroupedClashResults)
                {
                    if (ProgressBar.IsCanceled) break;
                    documentClash.TestsData.TestsAddCopy((GroupItem)documentClash.TestsData.Tests[indexOfClashTest], clashResult);
                    CurrentProgress++;
                    ProgressBar.Update((double)CurrentProgress / TotalProgress);
                }
                if (ProgressBar.IsCanceled) documentClash.TestsData.TestsReplaceWithCopy(indexOfClashTest, BackupTest);
                tx.Commit();
                Application.EndProgress();
            }
        }

        private static List<ClashResult> RemoveOneClashGroup(ref List<ClashResultGroup> clashResultGroups)
        {
            List<ClashResult> ungroupedClashResults = new List<ClashResult>();
            List<ClashResultGroup> temporaryClashResultGroups = new List<ClashResultGroup>();
            temporaryClashResultGroups.AddRange(clashResultGroups);

            foreach (ClashResultGroup group in temporaryClashResultGroups)
            {
                if (group.Children.Count == 1)
                {
                    ClashResult result = (ClashResult)group.Children.FirstOrDefault();
                    //result.DisplayName = group.DisplayName;
                    ungroupedClashResults.Add(result);
                    clashResultGroups.Remove(group);
                }
            }

            return ungroupedClashResults;
        }

        private static IEnumerable<ClashResult> GetIndividualClashResults(ClashTest clashTest, bool keepExistingGroup)
        {
            for (var i = 0; i < clashTest.Children.Count; i++)
            {
                if (clashTest.Children[i].IsGroup)
                {
                    if (!keepExistingGroup)
                    {
                        IEnumerable<ClashResult> GroupResults = GetGroupResults((ClashResultGroup)clashTest.Children[i]);
                        foreach (ClashResult clashResult in GroupResults)
                        {
                            yield return clashResult;
                        }
                    }
                }
                else yield return (ClashResult)clashTest.Children[i];
            }
        }

        private static IEnumerable<ClashResultGroup> BackupExistingClashGroups(ClashTest clashTest)
        {
            for (var i = 0; i < clashTest.Children.Count; i++)
            {
                if (clashTest.Children[i].IsGroup)
                {
                    yield return (ClashResultGroup)clashTest.Children[i].CreateCopy();
                }
            }
        }

        private static IEnumerable<ClashResult> GetGroupResults(ClashResultGroup clashResultGroup)
        {
            for (var i = 0; i < clashResultGroup.Children.Count; i++)
            {
                yield return (ClashResult)clashResultGroup.Children[i];
            }
        }

        private static ModelItem GetSignificantAncestorOrSelf(ModelItem item)
        {
            ModelItem originalItem = item;
            ModelItem currentComposite = null;

            //Get last composite item.
            while (item.Parent != null)
            {
                item = item.Parent;
                if (item.IsComposite) currentComposite = item;
            }

            return currentComposite ?? originalItem;
        }

        private static ModelItem GetFileAncestor(ModelItem item)
        {
            ModelItem originalItem = item;

            ModelItem currentComposite = null;

            //Get last composite item.
            while (item.Parent != null)
            {
                item = item.Parent;
                if (item.HasModel)
                {
                    currentComposite = item;
                    break;
                }
            }

            return currentComposite ?? originalItem;
        }

        #endregion

        #region New Grouping Methods

        /// <summary>
        /// Groups clashes by the file they come from (similar to ModelA/ModelB but focusing on file path)
        /// </summary>
        private static List<ClashResultGroup> GroupByFile(List<ClashResult> results, string initialName)
        {
            List<ClashResultGroup> clashResultGroups = new List<ClashResultGroup>();

            foreach (var clashResult in results)
            {
                string fileName = "Unknown File";
                
                try
                {
                    // Get the file ancestor from the clash result
                    ModelItem fileAncestor = GetFileAncestor(clashResult.CompositeItem1);
                    if (fileAncestor == null)
                    {
                        fileAncestor = GetFileAncestor(clashResult.CompositeItem2);
                    }
                    
                    if (fileAncestor != null && !string.IsNullOrEmpty(fileAncestor.DisplayName))
                    {
                        fileName = fileAncestor.DisplayName;
                    }
                }
                catch
                {
                    fileName = "Unknown File";
                }

                string groupName = initialName + fileName;
                
                ClashResultGroup existingGroup = clashResultGroups.FirstOrDefault(x => x.DisplayName == groupName);
                if (existingGroup == null)
                {
                    ClashResultGroup newGroup = new ClashResultGroup();
                    newGroup.DisplayName = groupName;
                    newGroup.Children.Add(clashResult.CreateCopy());
                    clashResultGroups.Add(newGroup);
                }
                else
                {
                    existingGroup.Children.Add(clashResult.CreateCopy());
                }
            }

            return clashResultGroups.OrderBy(x => x.DisplayName).ToList();
        }

        /// <summary>
        /// Groups clashes by layer information from the clashing elements
        /// </summary>
        private static List<ClashResultGroup> GroupByLayer(List<ClashResult> results, string initialName)
        {
            List<ClashResultGroup> clashResultGroups = new List<ClashResultGroup>();

            foreach (var clashResult in results)
            {
                string layerName = "No Layer";
                
                try
                {
                    // Try to get layer from properties
                    var item1 = GetSignificantAncestorOrSelf(clashResult.CompositeItem1);
                    var item2 = GetSignificantAncestorOrSelf(clashResult.CompositeItem2);
                    
                    // Check for layer property in item1
                    if (item1?.PropertyCategories != null)
                    {
                        foreach (var category in item1.PropertyCategories)
                        {
                            foreach (var prop in category.Properties)
                            {
                                if (prop.DisplayName.ToLower().Contains("layer"))
                                {
                                    layerName = prop.Value.ToDisplayString();
                                    break;
                                }
                            }
                            if (layerName != "No Layer") break;
                        }
                    }
                    
                    // If still no layer found, check item2
                    if (layerName == "No Layer" && item2?.PropertyCategories != null)
                    {
                        foreach (var category in item2.PropertyCategories)
                        {
                            foreach (var prop in category.Properties)
                            {
                                if (prop.DisplayName.ToLower().Contains("layer"))
                                {
                                    layerName = prop.Value.ToDisplayString();
                                    break;
                                }
                            }
                            if (layerName != "No Layer") break;
                        }
                    }
                }
                catch
                {
                    layerName = "No Layer";
                }

                string groupName = initialName + layerName;
                
                ClashResultGroup existingGroup = clashResultGroups.FirstOrDefault(x => x.DisplayName == groupName);
                if (existingGroup == null)
                {
                    ClashResultGroup newGroup = new ClashResultGroup();
                    newGroup.DisplayName = groupName;
                    newGroup.Children.Add(clashResult.CreateCopy());
                    clashResultGroups.Add(newGroup);
                }
                else
                {
                    existingGroup.Children.Add(clashResult.CreateCopy());
                }
            }

            return clashResultGroups.OrderBy(x => x.DisplayName).ToList();
        }

        /// <summary>
        /// Groups clashes by the first element (Selection A)
        /// </summary>
        private static List<ClashResultGroup> GroupByFirst(List<ClashResult> results, string initialName)
        {
            List<ClashResultGroup> clashResultGroups = new List<ClashResultGroup>();

            foreach (var clashResult in results)
            {
                string elementName = "Empty clash";
                
                try
                {
                    ModelItem significantItem = GetSignificantAncestorOrSelf(clashResult.CompositeItem1);
                    
                    if (significantItem != null)
                    {
                        elementName = !string.IsNullOrEmpty(significantItem.DisplayName) ? 
                            significantItem.DisplayName : "Unnamed Element";
                    }
                }
                catch
                {
                    elementName = "Empty clash";
                }

                string groupName = initialName + elementName;
                
                ClashResultGroup existingGroup = clashResultGroups.FirstOrDefault(x => x.DisplayName == groupName);
                if (existingGroup == null)
                {
                    ClashResultGroup newGroup = new ClashResultGroup();
                    newGroup.DisplayName = groupName;
                    newGroup.Children.Add(clashResult.CreateCopy());
                    clashResultGroups.Add(newGroup);
                }
                else
                {
                    existingGroup.Children.Add(clashResult.CreateCopy());
                }
            }

            return clashResultGroups.OrderBy(x => x.DisplayName).ToList();
        }

        /// <summary>
        /// Groups clashes by the last element (Selection B)
        /// </summary>
        private static List<ClashResultGroup> GroupByLast(List<ClashResult> results, string initialName)
        {
            List<ClashResultGroup> clashResultGroups = new List<ClashResultGroup>();

            foreach (var clashResult in results)
            {
                string elementName = "Empty clash";
                
                try
                {
                    ModelItem significantItem = GetSignificantAncestorOrSelf(clashResult.CompositeItem2);
                    
                    if (significantItem != null)
                    {
                        elementName = !string.IsNullOrEmpty(significantItem.DisplayName) ? 
                            significantItem.DisplayName : "Unnamed Element";
                    }
                }
                catch
                {
                    elementName = "Empty clash";
                }

                string groupName = initialName + elementName;
                
                ClashResultGroup existingGroup = clashResultGroups.FirstOrDefault(x => x.DisplayName == groupName);
                if (existingGroup == null)
                {
                    ClashResultGroup newGroup = new ClashResultGroup();
                    newGroup.DisplayName = groupName;
                    newGroup.Children.Add(clashResult.CreateCopy());
                    clashResultGroups.Add(newGroup);
                }
                else
                {
                    existingGroup.Children.Add(clashResult.CreateCopy());
                }
            }

            return clashResultGroups.OrderBy(x => x.DisplayName).ToList();
        }

        /// <summary>
        /// Groups clashes by unique combinations of both elements
        /// </summary>
        private static List<ClashResultGroup> GroupByLastUnique(List<ClashResult> results, string initialName)
        {
            List<ClashResultGroup> clashResultGroups = new List<ClashResultGroup>();

            foreach (var clashResult in results)
            {
                string element1Name = "Unknown1";
                string element2Name = "Unknown2";
                
                try
                {
                    ModelItem item1 = GetSignificantAncestorOrSelf(clashResult.CompositeItem1);
                    ModelItem item2 = GetSignificantAncestorOrSelf(clashResult.CompositeItem2);
                    
                    element1Name = item1?.DisplayName ?? "Unknown1";
                    element2Name = item2?.DisplayName ?? "Unknown2";
                }
                catch
                {
                    element1Name = "Unknown1";
                    element2Name = "Unknown2";
                }

                // Create a unique combination name
                string groupName = initialName + element1Name + " vs " + element2Name;
                
                ClashResultGroup existingGroup = clashResultGroups.FirstOrDefault(x => x.DisplayName == groupName);
                if (existingGroup == null)
                {
                    ClashResultGroup newGroup = new ClashResultGroup();
                    newGroup.DisplayName = groupName;
                    newGroup.Children.Add(clashResult.CreateCopy());
                    clashResultGroups.Add(newGroup);
                }
                else
                {
                    existingGroup.Children.Add(clashResult.CreateCopy());
                }
            }

            return clashResultGroups.OrderBy(x => x.DisplayName).ToList();
        }

        #endregion

    }

    public enum GroupingMode
    {
        [Description("<None>")]
        None,
        [Description("Level")]
        Level,
        [Description("Grid Intersection")]
        GridIntersection,
        [Description("Selection A")]
        SelectionA,
        [Description("Selection B")]
        SelectionB,
        [Description("Model A")]
        ModelA,
        [Description("Model B")]
        ModelB,
        [Description("Assigned To")]
        AssignedTo,
        [Description("Approved By")]
        ApprovedBy,
        [Description("Status")]
        Status,
        [Description("File")]
        File,
        [Description("Layer")]
        Layer,
        [Description("First")]
        First,
        [Description("Last")]
        Last,
        [Description("Last Unique")]
        LastUnique
    }

}

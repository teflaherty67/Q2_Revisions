using Autodesk.Revit.DB.Architecture;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Q2_Revisions.Common
{
    internal static class Utils
    {
               

        #region Area Scheme

        internal static AreaScheme GetAreaSchemeByName(Document curDoc, string schemeName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(curDoc);
            collector.OfClass(typeof(AreaScheme));

            foreach (AreaScheme areaScheme in collector)
            {
                if (areaScheme.Name == schemeName)
                {
                    return areaScheme;
                }
            }

            return null;
        }

        #endregion

        #region Color Fill Scheme

        internal static ColorFillScheme GetColorFillSchemeByName(Document curDoc, string schemeName, AreaScheme areaScheme)
        {
            try
            {
                ColorFillScheme colorfill = new FilteredElementCollector(curDoc)
               .OfCategory(BuiltInCategory.OST_ColorFillSchema)
               .Cast<ColorFillScheme>()
               .Where(x => x.Name.Equals(schemeName) && x.AreaSchemeId.Equals(areaScheme.Id))
               .First();

                return colorfill;
            }
            catch
            {
                return null;
            }
        }

        internal static void AddColorLegend(View view, ColorFillScheme scheme)
        {
            ElementId areaCatId = new ElementId(BuiltInCategory.OST_Areas);
            ElementId curLegendId = view.GetColorFillSchemeId(areaCatId);

            XYZ curOrigin = view.Origin + view.RightDirection * 10 + view.UpDirection * 5;

            if (curLegendId == ElementId.InvalidElementId)
                view.SetColorFillSchemeId(areaCatId, scheme.Id);

            ColorFillLegend.Create(view.Document, view.Id, areaCatId, curOrigin);
        }

        #endregion

        #region Convert

        internal static double ConvertINToFT(double INDim)
        {
            double convert = INDim / 12;

            return convert;
        }

        #endregion

        #region Elements - Architectural

        //return list of all doors in the current model
        public static List<FamilyInstance> GetAllDoors(Document curDoc)
        {
            //get all doors
            var returnList = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Doors)
                .Cast<FamilyInstance>()
                .ToList();

            return returnList;
        }

        internal static List<FamilyInstance> GetAllDoorsInActiveView(Document curDoc)
        {
            // get all the doors in the current view
            var m_returnList = new FilteredElementCollector(curDoc, curDoc.ActiveView.Id)
                 .OfClass(typeof(FamilyInstance))
                 .OfCategory(BuiltInCategory.OST_Doors)
                 .Cast<FamilyInstance>()
                 .ToList();

            // return the list
            return m_returnList;
        }

        //return list of all windows in the current model
        public static List<FamilyInstance> GetAllWindows(Document curDoc)
        {
            //get all windows
            var returnList = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            return returnList;
        }

        internal static List<FamilyInstance> GetWindowsByLevel(Document curDoc, string levelName)
        {
            // get all windows
            List<FamilyInstance> m_allWindows = Utils.GetAllWindows(curDoc);

            // filter list by level name
            List<FamilyInstance> m_windowsByLevel = m_allWindows
                .Where(win => win.LevelId != null)
                .Where(win => {
                    var level = curDoc.GetElement(win.LevelId) as Level;
                    return level != null && level.Name == levelName;
                })
                .ToList();

            return m_windowsByLevel;
        }

        #endregion

        #region Families

        internal static Family LoadFamilyFromLibrary(Document curDoc, String filePath, string familyName)
        {
            // create the full path to the family file
            string familyPath = Path.Combine(filePath, familyName + ".rfa");

            // Check if the family file exists at the specified path
            if (!System.IO.File.Exists(familyPath))
            {
                Utils.TaskDialogError("Error", "Spec Conversion", $"Family file not found at: {familyPath}");
                return null;
            }

            try
            {
                var loadOptions = new FamilyLoadOptions();
                curDoc.LoadFamily(familyPath, loadOptions, out Family loadedFamily);
                return loadedFamily; // This will be null if loading failed
            }
            catch (Exception ex)
            {
                Utils.TaskDialogError("Error", "Spec Conversion", $"Error loading family: {ex.Message}");
                return null; // Return null if an error occurs during loading
            }
        }

        /// <summary>
        /// Family load options class to handle overwrite behavior
        /// </summary>
        public class FamilyLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = true;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = true;
                return true;
            }
        }

        internal static FamilySymbol FindFamilySymbol(Document curDoc, string familyName, string typeName)
        {
            return new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.Family.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                                     fs.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
        }

        public static List<Family> GetAllFamilies(Document curDoc)
        {
            List<Family> m_returnList = new List<Family>();

            FilteredElementCollector collector = new FilteredElementCollector(curDoc);
            collector.OfClass(typeof(Family));

            foreach (Family family in collector)
            {
                m_returnList.Add(family);
            }

            return m_returnList;
        }

        public static List<Family> GetFamilyByNameContains(Document curDoc, string familyName)
        {
            List<Family> m_famList = GetAllFamilies(curDoc);

            List<Family> m_returnList = new List<Family>();

            //loop through family symbols in current project and look for a match
            foreach (Family curFam in m_famList)
            {
                if (curFam.Name.Contains(familyName))
                {
                    m_returnList.Add(curFam);
                }
            }

            return m_returnList;
        }

        public static Family GetFamilyByName(Document curDoc, string familyName)
        {
            List<Family> famList = GetAllFamilies(curDoc);

            foreach (Family curFam in famList)
            {
                if (curFam.Name == familyName)
                    return curFam;
            }

            return null;
        }

        #endregion

        #region Levels

        public static List<Level> GetAllLevels(Document curDoc)
        {
            FilteredElementCollector colLevels = new FilteredElementCollector(curDoc);
            colLevels.OfCategory(BuiltInCategory.OST_Levels);

            List<Level> levels = new List<Level>();
            foreach (Element x in colLevels.ToElements())
            {
                if (x.GetType() == typeof(Level))
                {
                    levels.Add((Level)x);
                }
            }

            return levels;
        }

        internal static List<Level> GetLevelByNameContains(Document curDoc, string levelWord)
        {
            List<Level> levels = GetAllLevels(curDoc);

            List<Level> returnList = new List<Level>();

            foreach (Level curLevel in levels)
            {
                if (curLevel.Name.Contains(levelWord))
                    returnList.Add(curLevel);
            }

            return returnList;
        }

        internal static Level GetLevelByName(Document curDoc, string levelName)
        {
            List<Level> levels = GetAllLevels(curDoc);

            foreach (Level curLevel in levels)
            {
                Debug.Print(curLevel.Name);

                if (curLevel.Name.Equals(levelName))
                    return curLevel;
            }

            return null;
        }        

        #endregion

        #region Lines

        internal static LinePatternElement GetLinePatternByName(Document curDoc, string typeName)
        {
            if (typeName != null)
                return LinePatternElement.GetLinePatternElementByName(curDoc, typeName);
            else
                return null;
        }

        internal static GraphicsStyle GetLinestyleByName(Document curDoc, string styleName)
        {
            GraphicsStyle retlinestyle = null;

            FilteredElementCollector gstylescollector = new FilteredElementCollector(curDoc);
            gstylescollector.OfClass(typeof(GraphicsStyle));

            foreach (Element element in gstylescollector)
            {
                GraphicsStyle curLS = element as GraphicsStyle;

                if (curLS.Name == styleName)
                    retlinestyle = curLS;
            }

            return retlinestyle;
        }

        #endregion

        #region Parameters

        public struct ParameterData
        {
            public Definition def;
            public ElementBinding binding;
            public string name;
            public bool IsSharedStatusKnown;
            public bool IsShared;
            public string GUID;
            public ElementId id;
        }

        public static List<ParameterData> GetAllProjectParameters(Document curDoc)
        {
            if (curDoc.IsFamilyDocument)
            {
                TaskDialog.Show("Error", "Cannot be a family curDocument.");
                return null;
            }

            List<ParameterData> paraList = new List<ParameterData>();

            BindingMap map = curDoc.ParameterBindings;
            DefinitionBindingMapIterator iter = map.ForwardIterator();
            iter.Reset();
            while (iter.MoveNext())
            {
                ParameterData pd = new ParameterData();
                pd.def = iter.Key;
                pd.name = iter.Key.Name;
                pd.binding = iter.Current as ElementBinding;
                paraList.Add(pd);
            }

            return paraList;
        }

        public static bool DoesProjectParamExist(Document curDoc, string pName)
        {
            List<ParameterData> pdList = GetAllProjectParameters(curDoc);
            foreach (ParameterData pd in pdList)
            {
                if (pd.name == pName)
                {
                    return true;
                }
            }
            return false;
        }
        public static void CreateSharedParam(Document curDoc, string groupName, string paramName, BuiltInCategory cat)
        {
            Definition curDef = null;

            //check if current file has shared param file - if not then exit
            DefinitionFile defFile = curDoc.Application.OpenSharedParameterFile();

            //check if file has shared parameter file
            if (defFile == null)
            {
                TaskDialog.Show("Error", "No shared parameter file.");
                //Throw New Exception("No Shared Parameter File!")
            }

            //check if shared parameter exists in shared param file - if not then create
            if (ParamExists(defFile.Groups, groupName, paramName) == false)
            {
                //create param
                curDef = AddParamToFile(defFile, groupName, paramName);
            }
            else
            {
                curDef = GetParameterDefinitionFromFile(defFile, groupName, paramName);
            }

            //check if param is added to views - if not then add
            if (ParamAddedToFile(curDoc, paramName) == false)
            {
                //add parameter to current Revitfile
                AddParamToDocument(curDoc, curDef, cat);
            }
        }

        internal static string GetParameterValueByName(Element element, string paramName)
        {
            IList<Parameter> paramList = element.GetParameters(paramName);

            if (paramList != null)
                try
                {
                    Parameter param = paramList[0];
                    string paramValue = param.AsValueString();
                    return paramValue;
                }
                catch (System.ArgumentOutOfRangeException)
                {
                    return null;
                }

            return "";
        }

        internal static Parameter GetParameterByName(Element curElem, string paramName)
        {
            foreach (Parameter curParam in curElem.Parameters)
            {
                if (curParam.Definition.Name.ToString() == paramName)
                    return curParam;
            }

            return null;
        }

        internal static Parameter GetParameterByNameAndWritable(Element curElem, string paramName)
        {
            foreach (Parameter curParam in curElem.Parameters)
            {
                if (curParam.Definition.Name.ToString() == paramName && curParam.IsReadOnly == false)
                    return curParam;
            }

            return null;
        }

        internal static ElementId GetProjectParameterId(Document curDoc, string name)
        {
            ParameterElement pElem = new FilteredElementCollector(curDoc)
                .OfClass(typeof(ParameterElement))
                .Cast<ParameterElement>()
                .Where(e => e.Name.Equals(name))
                .FirstOrDefault();

            return pElem?.Id;
        }

        internal static ElementId GetBuiltInParameterId(Document curDoc, BuiltInCategory cat, BuiltInParameter bip)
        {
            FilteredElementCollector collector = new FilteredElementCollector(curDoc);
            collector.OfCategory(cat);

            Parameter curParam = collector.FirstElement().get_Parameter(bip);

            return curParam?.Id;
        }

        internal static string SetParameterByNameAndWritable(Element curElem, string paramName, string value)
        {
            Parameter curParam = GetParameterByNameAndWritable(curElem, paramName);

            curParam.Set(value);
            return curParam.ToString();
        }

        internal static void SetParameterByName(Element element, string paramName, string value)
        {
            IList<Parameter> paramList = element.GetParameters(paramName);

            if (paramList != null)
            {
                Parameter param = paramList[0];

                param.Set(value);
            }
        }

        internal static void SetParameterByName(Element element, string paramName, int value)
        {
            IList<Parameter> paramList = element.GetParameters(paramName);

            if (paramList != null)
            {
                Parameter param = paramList[0];

                param.Set(value);
            }
        }

        internal static bool SetParameterValue(Element curElem, string paramName, string value)
        {
            Parameter curParam = GetParameterByName(curElem, paramName);

            if (curParam != null)
            {
                curParam.Set(value);
                return true;
            }

            return false;
        }

        private static Definition GetParameterDefinitionFromFile(DefinitionFile defFile, string groupName, string paramName)
        {
            // iterate the Definition groups of this file
            foreach (DefinitionGroup group in defFile.Groups)
            {
                if (group.Name == groupName)
                {
                    // iterate the difinitions
                    foreach (Definition definition in group.Definitions)
                    {
                        if (definition.Name == paramName)
                            return definition;
                    }
                }
            }
            return null;
        }

        //check if specified parameter is already added to Revit file
        public static bool ParamAddedToFile(Document curDoc, string paramName)
        {
            foreach (Parameter curParam in curDoc.ProjectInformation.Parameters)
            {
                if (curParam.Definition.Name.Equals(paramName))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool AddParamToDocument(Document curDoc, Definition curDef, BuiltInCategory cat)
        {
            bool paramAdded = false;

            //define category for shared param
            Category myCat = curDoc.Settings.Categories.get_Item(cat);
            CategorySet myCatSet = curDoc.Application.Create.NewCategorySet();
            myCatSet.Insert(myCat);

            //create binding
            ElementBinding curBinding = curDoc.Application.Create.NewInstanceBinding(myCatSet);

            //do something
            //paramAdded = curDoc.ParameterBindings.Insert(curDef, curBinding, BuiltInParameterGroup.PG_IDENTITY_DATA);

            return paramAdded;
        }


        //check if specified parameter exists in shared parameter file
        public static bool ParamExists(DefinitionGroups groupList, string groupName, string paramName)
        {
            //loop through groups and look for match
            foreach (DefinitionGroup curGroup in groupList)
            {
                if (curGroup.Name.Equals(groupName) == true)
                {
                    //check if param exists
                    foreach (Definition curDef in curGroup.Definitions)
                    {
                        if (curDef.Name.Equals(paramName))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        //add parameter to specified shared parameter file
        public static Definition AddParamToFile(DefinitionFile defFile, string groupName, string paramName)
        {
            //create new shared parameter in specified file
            DefinitionGroup defGroup = GetDefinitionGroup(defFile, groupName);

            //check if group exists - if not then create
            if (defGroup == null)
            {
                //create group
                defGroup = defFile.Groups.Create(groupName);
            }

            //create parameter in group
            ExternalDefinitionCreationOptions curOptions = new ExternalDefinitionCreationOptions(paramName, SpecTypeId.String.Text);
            curOptions.Visible = true;

            Definition newParam = defGroup.Definitions.Create(curOptions);

            return newParam;
        }

        public static DefinitionGroup GetDefinitionGroup(DefinitionFile defFile, string groupName)
        {
            //loop through groups and look for match
            foreach (DefinitionGroup curGroup in defFile.Groups)
            {
                if (curGroup.Name.Equals(groupName))
                {
                    return curGroup;
                }
            }

            return null;
        }

        #endregion

        #region Print Sets

        internal static List<ViewSheetSet> GetAndSortAllPrintSets(Document curDoc)
        {
            var m_returnList = new FilteredElementCollector(curDoc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .OrderBy(vss => vss.Name)
                .ToList();

            return m_returnList;
        }

        #endregion

        #region Purge

        internal static void PurgeUnusedFamilySymbols(Document curDoc)
        {
            try
            {
                // Collect all family symbols
                var allSymbols = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .ToList();

                // Collect used symbol IDs from family instances
                var usedSymbolIds = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Select(fi => fi.GetTypeId())
                    .Where(id => id != ElementId.InvalidElementId)
                    .ToHashSet();

                // Delete unused symbols
                var symbolsToDelete = allSymbols
                    .Where(s => !s.IsActive && !usedSymbolIds.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToList();

                foreach (var symbolId in symbolsToDelete)
                {
                    try
                    {
                        curDoc.Delete(symbolId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not delete family symbol {symbolId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error purging family symbols: {ex.Message}");
            }
        }

        internal static void PurgeUnusedViewTemplates(Document curDoc)
        {
            List<View> templates = new FilteredElementCollector(curDoc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.IsTemplate)
            .ToList<View>();

            var usedTemplateIds = new FilteredElementCollector(curDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewTemplateId != ElementId.InvalidElementId)
                .Select(v => v.ViewTemplateId)
                .ToHashSet();

            foreach (var template in templates)
            {
                if (!usedTemplateIds.Contains(template.Id))
                {
                    try
                    {
                        curDoc.Delete(template.Id);
                    }
                    catch { }
                }
            }
        }

        internal static void PurgeUnusedFilters(Document curDoc)
        {
            List<ParameterFilterElement> filters = null;
            HashSet<ElementId> usedFilterIds = null;

            try
            {
                // Collect all parameter filters with error handling
                try
                {
                    filters = new FilteredElementCollector(curDoc)
                        .OfClass(typeof(ParameterFilterElement))
                        .Cast<ParameterFilterElement>()
                        .Where(f => f != null) // Filter out any null elements
                        .ToList<ParameterFilterElement>();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to collect parameter filters", ex);
                }

                // Collect all views with error handling
                IEnumerable<View> views = null;
                try
                {
                    views = new FilteredElementCollector(curDoc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => v != null); // Filter out any null views
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to collect views", ex);
                }

                // Build set of used filter IDs
                usedFilterIds = new HashSet<ElementId>();

                foreach (var view in views)
                {
                    if (view == null) continue;

                    try
                    {
                        // Get filters for this view
                        var viewFilters = view.GetFilters();
                        if (viewFilters != null)
                        {
                            foreach (var id in viewFilters)
                            {
                                if (id != null && id != ElementId.InvalidElementId)
                                {
                                    usedFilterIds.Add(id);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but continue processing other views
                        // You might want to add logging here
                        System.Diagnostics.Debug.WriteLine($"Error getting filters for view {view.Name}: {ex.Message}");
                        continue;
                    }
                }

                // Delete unused filters
                if (filters != null && filters.Count > 0)
                {
                    foreach (var filter in filters)
                    {
                        if (filter == null || filter.Id == null || filter.Id == ElementId.InvalidElementId)
                            continue;

                        try
                        {
                            // Check if filter is not in use
                            if (!usedFilterIds.Contains(filter.Id))
                            {
                                // Additional check to ensure the element is still valid
                                if (curDoc.GetElement(filter.Id) != null)
                                {
                                    curDoc.Delete(filter.Id);
                                }
                            }
                        }
                        catch (Autodesk.Revit.Exceptions.ArgumentException)
                        {
                            // Element might already be deleted or invalid
                            System.Diagnostics.Debug.WriteLine($"Cannot delete filter {filter.Id}: Invalid element");
                        }
                        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                        {
                            // Element might be in use by something we didn't detect
                            System.Diagnostics.Debug.WriteLine($"Cannot delete filter {filter.Id}: Element is in use");
                        }
                        catch (Exception ex)
                        {
                            // Catch any other unexpected exceptions
                            System.Diagnostics.Debug.WriteLine($"Unexpected error deleting filter {filter.Id}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Re-throw with more context
                throw new InvalidOperationException("Failed to purge unused filters", ex);
            }
        }

        internal static void PurgeUnusedMaterials(Document curDoc)
        {
            var allMaterialIds = new FilteredElementCollector(curDoc)
            .OfClass(typeof(Material))
            .ToElementIds();

            var usedMaterialIds = new HashSet<ElementId>();

            var elementCollector = new FilteredElementCollector(curDoc)
                .WhereElementIsNotElementType();

            foreach (var elem in elementCollector)
            {
                var matIds = elem.GetMaterialIds(false);
                foreach (var id in matIds)
                    usedMaterialIds.Add(id);
            }

            foreach (var matId in allMaterialIds)
            {
                if (!usedMaterialIds.Contains(matId))
                {
                    try
                    {
                        curDoc.Delete(matId);
                    }
                    catch { }
                }
            }
        }

        internal static void PurgeUnusedLinePatterns(Document curDoc)
        {
            // Check if document is valid
            if (curDoc == null)
            {
                // Silent return - no exception thrown
                System.Diagnostics.Debug.WriteLine("PurgeUnusedLinePatterns: Document is null");
                return;
            }

            IEnumerable<LinePatternElement> allPatterns = null;
            HashSet<ElementId> usedPatternIds = null;

            try
            {
                // Collect all line patterns with error handling
                try
                {
                    allPatterns = new FilteredElementCollector(curDoc)
                        .OfClass(typeof(LinePatternElement))
                        .Cast<LinePatternElement>()
                        .Where(lpe => lpe != null &&
                               lpe.Name != null &&
                               !lpe.Name.StartsWith("<")); // Filter out system patterns
                }
                catch (Exception ex)
                {
                    // Silent handling - just log and return
                    System.Diagnostics.Debug.WriteLine($"Failed to collect line patterns: {ex.Message}");
                    return;
                }

                // Initialize the set for used pattern IDs
                usedPatternIds = new HashSet<ElementId>();

                // Check document settings and categories
                if (curDoc.Settings == null || curDoc.Settings.Categories == null)
                {
                    // Silent handling - just log and return
                    System.Diagnostics.Debug.WriteLine("Document settings or categories are not accessible");
                    return;
                }

                // Get lines category with error handling
                Category linesCategory = null;
                try
                {
                    linesCategory = curDoc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                }
                catch (Exception ex)
                {
                    // Silent handling - just log and return
                    System.Diagnostics.Debug.WriteLine($"Failed to get Lines category: {ex.Message}");
                    return;
                }

                if (linesCategory == null)
                {
                    // Silent handling - just log and return
                    System.Diagnostics.Debug.WriteLine("Lines category not found in document");
                    return;
                }

                // Check if we have an active view for getting overrides
                if (curDoc.ActiveView == null)
                {
                    System.Diagnostics.Debug.WriteLine("Warning: No active view available for checking category overrides");
                }

                // Process subcategories
                if (linesCategory.SubCategories != null)
                {
                    foreach (Category subcat in linesCategory.SubCategories)
                    {
                        if (subcat == null || subcat.Id == null || subcat.Id == ElementId.InvalidElementId)
                            continue;

                        try
                        {
                            // Get graphics style
                            Element element = curDoc.GetElement(subcat.Id);
                            if (element == null) continue;

                            GraphicsStyle gs = element as GraphicsStyle;
                            if (gs != null && gs.Id != null && gs.Id != ElementId.InvalidElementId)
                            {
                                try
                                {
                                    // Only try to get overrides if we have an active view
                                    if (curDoc.ActiveView != null)
                                    {
                                        // Verify the graphics style has a valid category
                                        if (gs.GraphicsStyleCategory != null &&
                                            gs.GraphicsStyleCategory.Id != null &&
                                            gs.GraphicsStyleCategory.Id != ElementId.InvalidElementId)
                                        {
                                            OverrideGraphicSettings ogs = curDoc.ActiveView.GetCategoryOverrides(gs.GraphicsStyleCategory.Id);

                                            if (ogs != null)
                                            {
                                                // Check projection line pattern
                                                if (ogs.ProjectionLinePatternId != null &&
                                                    ogs.ProjectionLinePatternId != ElementId.InvalidElementId)
                                                {
                                                    usedPatternIds.Add(ogs.ProjectionLinePatternId);
                                                }

                                                // Also check cut line pattern if you want to be thorough
                                                if (ogs.CutLinePatternId != null &&
                                                    ogs.CutLinePatternId != ElementId.InvalidElementId)
                                                {
                                                    usedPatternIds.Add(ogs.CutLinePatternId);
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                                {
                                    // Category might not support overrides
                                    System.Diagnostics.Debug.WriteLine($"Cannot get overrides for category {gs.Name}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error processing graphics style {gs.Name}: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log but continue processing other subcategories
                            System.Diagnostics.Debug.WriteLine($"Error processing subcategory: {ex.Message}");
                            continue;
                        }
                    }
                }

                // Delete unused patterns
                if (allPatterns != null)
                {
                    foreach (var pattern in allPatterns)
                    {
                        if (pattern == null || pattern.Id == null || pattern.Id == ElementId.InvalidElementId)
                            continue;

                        try
                        {
                            // Check if pattern is not in use
                            if (!usedPatternIds.Contains(pattern.Id))
                            {
                                // Additional check to ensure the element is still valid
                                if (curDoc.GetElement(pattern.Id) != null)
                                {
                                    curDoc.Delete(pattern.Id);
                                    System.Diagnostics.Debug.WriteLine($"Deleted unused line pattern: {pattern.Name}");
                                }
                            }
                        }
                        catch (Autodesk.Revit.Exceptions.ArgumentException)
                        {
                            // Element might already be deleted or invalid
                            System.Diagnostics.Debug.WriteLine($"Cannot delete line pattern {pattern.Name}: Invalid element");
                        }
                        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                        {
                            // Element might be in use by something we didn't detect
                            System.Diagnostics.Debug.WriteLine($"Cannot delete line pattern {pattern.Name}: Element is in use");
                        }
                        catch (Exception ex)
                        {
                            // Catch any other unexpected exceptions
                            System.Diagnostics.Debug.WriteLine($"Unexpected error deleting line pattern {pattern.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silent handling - just log the error
                System.Diagnostics.Debug.WriteLine($"Failed to purge unused line patterns: {ex.Message}");
                // Method completes without throwing exception
            }
        }

        internal static void PurgeUnusedFillPatterns(Document curDoc)
        {
            IEnumerable<FillPatternElement> patterns = null;
            HashSet<ElementId> usedPatternIds = null;

            try
            {
                // Collect all fill patterns with error handling
                try
                {
                    patterns = new FilteredElementCollector(curDoc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .Where(p => p != null)
                        .ToList(); // Convert to List to avoid iterator issues
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to collect fill patterns", ex);
                }

                // Initialize the set for used pattern IDs
                usedPatternIds = new HashSet<ElementId>();

                // Collect views with error handling
                IEnumerable<View> views = null;
                try
                {
                    views = new FilteredElementCollector(curDoc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => v != null && !v.IsTemplate)
                        .ToList(); // Convert to List to avoid iterator issues
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to collect views", ex);
                }

                // Process each view
                foreach (var view in views)
                {
                    if (view == null || view.Id == null || view.Id == ElementId.InvalidElementId)
                        continue;

                    try
                    {
                        // Check if view is valid for element collection
                        if (!view.IsValidObject)
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipping invalid view: {view.Name}");
                            continue;
                        }

                        // Collect elements in the view
                        ICollection<Element> elements = null;
                        try
                        {
                            elements = new FilteredElementCollector(curDoc, view.Id)
                                .WhereElementIsNotElementType()
                                .ToElements();
                        }
                        catch (Autodesk.Revit.Exceptions.ArgumentException)
                        {
                            // View might not support element collection
                            System.Diagnostics.Debug.WriteLine($"Cannot collect elements from view: {view.Name}");
                            continue;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error collecting elements from view {view.Name}: {ex.Message}");
                            continue;
                        }

                        if (elements == null || elements.Count == 0)
                            continue;

                        // Process each element in the view
                        foreach (var elem in elements)
                        {
                            if (elem == null || elem.Id == null || elem.Id == ElementId.InvalidElementId)
                                continue;

                            try
                            {
                                // Get element overrides
                                OverrideGraphicSettings ogs = view.GetElementOverrides(elem.Id);

                                if (ogs != null)
                                {
                                    // Check surface foreground pattern
                                    if (ogs.SurfaceForegroundPatternId != null &&
                                        ogs.SurfaceForegroundPatternId != ElementId.InvalidElementId)
                                    {
                                        usedPatternIds.Add(ogs.SurfaceForegroundPatternId);
                                    }

                                    // Check surface background pattern
                                    if (ogs.SurfaceBackgroundPatternId != null &&
                                        ogs.SurfaceBackgroundPatternId != ElementId.InvalidElementId)
                                    {
                                        usedPatternIds.Add(ogs.SurfaceBackgroundPatternId);
                                    }

                                    // Check cut foreground pattern
                                    if (ogs.CutForegroundPatternId != null &&
                                        ogs.CutForegroundPatternId != ElementId.InvalidElementId)
                                    {
                                        usedPatternIds.Add(ogs.CutForegroundPatternId);
                                    }

                                    // Check cut background pattern
                                    if (ogs.CutBackgroundPatternId != null &&
                                        ogs.CutBackgroundPatternId != ElementId.InvalidElementId)
                                    {
                                        usedPatternIds.Add(ogs.CutBackgroundPatternId);
                                    }
                                }
                            }
                            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                            {
                                // Element might not support overrides in this view
                                continue;
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException)
                            {
                                // Element might be invalid or deleted
                                continue;
                            }
                            catch (Exception ex)
                            {
                                // Log but continue processing other elements
                                System.Diagnostics.Debug.WriteLine($"Error getting overrides for element {elem.Id}: {ex.Message}");
                                continue;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but continue processing other views
                        System.Diagnostics.Debug.WriteLine($"Error processing view {view.Name}: {ex.Message}");
                        continue;
                    }
                }

                // Also check materials for fill patterns (additional safety)
                try
                {
                    var materials = new FilteredElementCollector(curDoc)
                        .OfClass(typeof(Material))
                        .Cast<Material>()
                        .Where(m => m != null)
                        .ToList(); // Convert to List to avoid iterator issues

                    foreach (var material in materials)
                    {
                        try
                        {
                            if (material.SurfaceForegroundPatternId != null &&
                                material.SurfaceForegroundPatternId != ElementId.InvalidElementId)
                            {
                                usedPatternIds.Add(material.SurfaceForegroundPatternId);
                            }

                            if (material.SurfaceBackgroundPatternId != null &&
                                material.SurfaceBackgroundPatternId != ElementId.InvalidElementId)
                            {
                                usedPatternIds.Add(material.SurfaceBackgroundPatternId);
                            }

                            if (material.CutForegroundPatternId != null &&
                                material.CutForegroundPatternId != ElementId.InvalidElementId)
                            {
                                usedPatternIds.Add(material.CutForegroundPatternId);
                            }

                            if (material.CutBackgroundPatternId != null &&
                                material.CutBackgroundPatternId != ElementId.InvalidElementId)
                            {
                                usedPatternIds.Add(material.CutBackgroundPatternId);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error checking material patterns: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error collecting materials: {ex.Message}");
                }

                // Collect patterns to delete first (to avoid iterator modification issues)
                List<ElementId> patternsToDelete = new List<ElementId>();

                if (patterns != null)
                {
                    foreach (var pattern in patterns)
                    {
                        if (pattern == null || pattern.Id == null || pattern.Id == ElementId.InvalidElementId)
                            continue;

                        try
                        {
                            // Check if pattern is not in use
                            if (!usedPatternIds.Contains(pattern.Id))
                            {
                                // Additional check to ensure the element is still valid
                                if (curDoc.GetElement(pattern.Id) != null)
                                {
                                    patternsToDelete.Add(pattern.Id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error checking pattern {pattern.Name}: {ex.Message}");
                        }
                    }
                }

                // Now delete the collected patterns
                foreach (var patternId in patternsToDelete)
                {
                    try
                    {
                        var pattern = curDoc.GetElement(patternId) as FillPatternElement;
                        if (pattern != null)
                        {
                            curDoc.Delete(patternId);
                            System.Diagnostics.Debug.WriteLine($"Deleted unused fill pattern: {pattern.Name}");
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException)
                    {
                        // Element might already be deleted or invalid
                        System.Diagnostics.Debug.WriteLine($"Cannot delete fill pattern {patternId}: Invalid element");
                    }
                    catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                    {
                        // Element might be in use by something we didn't detect
                        System.Diagnostics.Debug.WriteLine($"Cannot delete fill pattern {patternId}: Element is in use");
                    }
                    catch (Exception ex)
                    {
                        // Catch any other unexpected exceptions
                        System.Diagnostics.Debug.WriteLine($"Unexpected error deleting fill pattern {patternId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Re-throw with more context
                throw new InvalidOperationException("Failed to purge unused fill patterns", ex);
            }
        }

        internal static void PurgeUnusedGroups(Document curDoc)
        {
            // Collect all groups and convert to List to avoid iterator issues
            var groups = new FilteredElementCollector(curDoc)
                .OfClass(typeof(GroupType))
                .Cast<GroupType>()
                .ToList();

            // Collect groups to delete first
            List<ElementId> groupsToDelete = new List<ElementId>();

            foreach (var group in groups)
            {
                if (group.Groups.Size == 0)
                {
                    groupsToDelete.Add(group.Id);
                }
            }

            // Now delete the collected groups
            foreach (var groupId in groupsToDelete)
            {
                try
                {
                    curDoc.Delete(groupId);
                }
                catch { }
            }
        }

        internal static void PurgeUnusedTextStyles(Document curDoc)
        {
            try
            {
                // Collect all text note types and convert to List
                var textStyles = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .ToList();

                // Collect used text style IDs
                HashSet<ElementId> usedStyleIds = new HashSet<ElementId>();

                // Check text notes for used styles
                var textNotes = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .ToList();

                foreach (var textNote in textNotes)
                {
                    if (textNote?.GetTypeId() != null && textNote.GetTypeId() != ElementId.InvalidElementId)
                    {
                        usedStyleIds.Add(textNote.GetTypeId());
                    }
                }

                // Collect styles to delete
                List<ElementId> stylesToDelete = new List<ElementId>();

                foreach (var style in textStyles)
                {
                    if (style?.Id != null && !usedStyleIds.Contains(style.Id))
                    {
                        stylesToDelete.Add(style.Id);
                    }
                }

                // Delete unused styles
                foreach (var styleId in stylesToDelete)
                {
                    try
                    {
                        curDoc.Delete(styleId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not delete text style {styleId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error purging text styles: {ex.Message}");
            }
        }

        internal static void PurgeUnusedDimensionStyles(Document curDoc)
        {
            try
            {
                // Collect all dimension types
                var dimensionTypes = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(DimensionType))
                    .Cast<DimensionType>()
                    .ToList();

                // Collect used dimension type IDs
                HashSet<ElementId> usedTypeIds = new HashSet<ElementId>();

                // Check dimensions for used types
                var dimensions = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(Dimension))
                    .Cast<Dimension>()
                    .ToList();

                foreach (var dimension in dimensions)
                {
                    if (dimension?.GetTypeId() != null && dimension.GetTypeId() != ElementId.InvalidElementId)
                    {
                        usedTypeIds.Add(dimension.GetTypeId());
                    }
                }

                // Collect types to delete
                List<ElementId> typesToDelete = new List<ElementId>();

                foreach (var dimType in dimensionTypes)
                {
                    if (dimType?.Id != null && !usedTypeIds.Contains(dimType.Id))
                    {
                        typesToDelete.Add(dimType.Id);
                    }
                }

                // Delete unused types
                foreach (var typeId in typesToDelete)
                {
                    try
                    {
                        curDoc.Delete(typeId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not delete dimension type {typeId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error purging dimension styles: {ex.Message}");
            }
        }

        internal static void PurgeUnusedLineStyles(Document curDoc)
        {
            try
            {
                // Get line styles (subcategory of Lines category)
                Category lineCategory = curDoc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                if (lineCategory?.SubCategories == null) return;

                // Collect used line style IDs
                HashSet<ElementId> usedLineStyleIds = new HashSet<ElementId>();

                // Check detail lines for used styles
                var detailLines = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(DetailLine))
                    .Cast<DetailLine>()
                    .ToList();

                foreach (var line in detailLines)
                {
                    if (line?.LineStyle?.Id != null)
                    {
                        usedLineStyleIds.Add(line.LineStyle.Id);
                    }
                }

                // Check model lines for used styles
                var modelLines = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(ModelLine))
                    .Cast<ModelLine>()
                    .ToList();

                foreach (var line in modelLines)
                {
                    if (line?.LineStyle?.Id != null)
                    {
                        usedLineStyleIds.Add(line.LineStyle.Id);
                    }
                }

                // Collect line styles to delete
                List<ElementId> stylesToDelete = new List<ElementId>();

                foreach (Category subCat in lineCategory.SubCategories)
                {
                    if (subCat?.Id != null && !usedLineStyleIds.Contains(subCat.Id))
                    {
                        stylesToDelete.Add(subCat.Id);
                    }
                }

                // Delete unused line styles
                foreach (var styleId in stylesToDelete)
                {
                    try
                    {
                        curDoc.Delete(styleId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not delete line style {styleId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error purging line styles: {ex.Message}");
            }
        }

        internal static void PurgeUnusedAnnotationSymbols(Document curDoc)
        {
            try
            {
                // Collect all annotation symbol types
                var annotationTypes = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(AnnotationSymbolType))
                    .Cast<AnnotationSymbolType>()
                    .ToList();

                // Collect used annotation type IDs
                HashSet<ElementId> usedTypeIds = new HashSet<ElementId>();

                // Check annotation symbols for used types
                var annotations = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(AnnotationSymbol))
                    .Cast<AnnotationSymbol>()
                    .ToList();

                foreach (var annotation in annotations)
                {
                    if (annotation?.GetTypeId() != null && annotation.GetTypeId() != ElementId.InvalidElementId)
                    {
                        usedTypeIds.Add(annotation.GetTypeId());
                    }
                }

                // Collect types to delete
                List<ElementId> typesToDelete = new List<ElementId>();

                foreach (var annoType in annotationTypes)
                {
                    if (annoType?.Id != null && !usedTypeIds.Contains(annoType.Id))
                    {
                        typesToDelete.Add(annoType.Id);
                    }
                }

                // Delete unused types
                foreach (var typeId in typesToDelete)
                {
                    try
                    {
                        curDoc.Delete(typeId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not delete annotation type {typeId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error purging annotation symbols: {ex.Message}");
            }
        }

        internal static void PurgeUnusedAppearanceAssets(Document curDoc)
        {
            try
            {
                // Collect all appearance assets
                var appearanceAssets = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(AppearanceAssetElement))
                    .Cast<AppearanceAssetElement>()
                    .ToList();

                // Collect used appearance asset IDs
                HashSet<ElementId> usedAssetIds = new HashSet<ElementId>();

                // Check materials for used appearance assets
                var materials = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .ToList();

                foreach (var material in materials)
                {
                    try
                    {
                        var appearanceAssetId = material.AppearanceAssetId;
                        if (appearanceAssetId != null && appearanceAssetId != ElementId.InvalidElementId)
                        {
                            usedAssetIds.Add(appearanceAssetId);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error checking material appearance asset: {ex.Message}");
                    }
                }

                // Collect assets to delete
                List<ElementId> assetsToDelete = new List<ElementId>();

                foreach (var asset in appearanceAssets)
                {
                    if (asset?.Id != null && !usedAssetIds.Contains(asset.Id))
                    {
                        assetsToDelete.Add(asset.Id);
                    }
                }

                // Delete unused assets
                foreach (var assetId in assetsToDelete)
                {
                    try
                    {
                        curDoc.Delete(assetId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not delete appearance asset {assetId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error purging appearance assets: {ex.Message}");
            }
        }

        internal static void PurgeUnusedRenderingMaterials(Document curDoc)
        {
            try
            {
                // This method attempts to purge unused rendering materials/assets
                // Note: Some rendering materials might be harder to detect usage for

                var renderingElements = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .Where(m => m != null)
                    .ToList();

                // Collect used material IDs from elements
                HashSet<ElementId> usedMaterialIds = new HashSet<ElementId>();

                // Check all elements for material usage
                var allElements = new FilteredElementCollector(curDoc)
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (var element in allElements)
                {
                    try
                    {
                        // Try to get material IDs from the element
                        var materialIds = element.GetMaterialIds(false);
                        foreach (var matId in materialIds)
                        {
                            if (matId != null && matId != ElementId.InvalidElementId)
                            {
                                usedMaterialIds.Add(matId);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Some elements might not support GetMaterialIds
                        continue;
                    }
                }

                // Also check element types for materials
                var elementTypes = new FilteredElementCollector(curDoc)
                    .WhereElementIsElementType()
                    .ToList();

                foreach (var elementType in elementTypes)
                {
                    try
                    {
                        var materialIds = elementType.GetMaterialIds(false);
                        foreach (var matId in materialIds)
                        {
                            if (matId != null && matId != ElementId.InvalidElementId)
                            {
                                usedMaterialIds.Add(matId);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

                // This method focuses on identifying unused materials more conservatively
                // since materials can be referenced in many ways
                System.Diagnostics.Debug.WriteLine($"Found {usedMaterialIds.Count} materials in use");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error purging rendering materials: {ex.Message}");
            }
        }

        #endregion

        #region Ribbon

        internal static RibbonPanel CreateRibbonPanel(UIControlledApplication app, string tabName, string panelName)
        {
            RibbonPanel currentPanel = GetRibbonPanelByName(app, tabName, panelName);

            if (currentPanel == null)
                currentPanel = app.CreateRibbonPanel(tabName, panelName);

            return currentPanel;
        }

        internal static RibbonPanel GetRibbonPanelByName(UIControlledApplication app, string tabName, string panelName)
        {
            foreach (RibbonPanel tmpPanel in app.GetRibbonPanels(tabName))
            {
                if (tmpPanel.Name == panelName)
                    return tmpPanel;
            }

            return null;
        }

        internal static List<string> GetCommandsFromRibbonTab(UIApplication uiapp, string tabName)
        {
            List<string> m_cmdList = new List<string>();

            foreach (RibbonPanel curPanel in uiapp.GetRibbonPanels(tabName))
            {
                foreach (RibbonItem curItem in curPanel.GetItems())
                {
                    if (curItem is PushButton pushButton)
                    {
                        m_cmdList.Add(pushButton.ItemText.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "));
                    }
                    else if (curItem is PulldownButton pulldownButton)
                    {
                        m_cmdList.Add(pulldownButton.ItemText.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "));
                        foreach (RibbonItem pulldownItem in pulldownButton.GetItems())
                        {
                            if (pulldownItem is PushButton subButton)
                            {
                                m_cmdList.Add(subButton.ItemText.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "));
                            }
                        }
                    }
                    else if (curItem is SplitButton splitButton)
                    {
                        m_cmdList.Add(splitButton.ItemText.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "));
                        foreach (RibbonItem splitButtonItem in splitButton.GetItems())
                        {
                            if (splitButtonItem is PushButton subButton)
                            {
                                m_cmdList.Add(subButton.ItemText.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "));
                            }
                        }
                    }
                }
            }

            return m_cmdList;
        }

        internal static BitmapImage BitmapToImageSource(Bitmap bm)
        {
            using (MemoryStream mem = new MemoryStream())
            {
                bm.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
                mem.Position = 0;
                BitmapImage bmi = new BitmapImage();
                bmi.BeginInit();
                bmi.StreamSource = mem;
                bmi.CacheOption = BitmapCacheOption.OnLoad;
                bmi.EndInit();

                return bmi;
            }
        }

        #endregion

        #region Rooms

        internal static List<Room> GetAllRooms(Document curDoc)
        {
            FilteredElementCollector m_colRooms = new FilteredElementCollector(curDoc);
            var m_rooms = m_colRooms.OfCategory(BuiltInCategory.OST_Rooms)
                                    .WhereElementIsNotElementType()
                                    .Cast<Room>()
                                    .ToList();

            return m_rooms;
        }

        internal static List<RoomTag> GetAllRoomTags(Document curDoc)
        {
            List<RoomTag> m_roomTags = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_RoomTags)
                .WhereElementIsNotElementType()
                .Cast<RoomTag>()
                .ToList();

            return m_roomTags;
        }


        #endregion

        #region Schedules

        internal static ViewSchedule CreateAreaSchedule(Document curDoc, string schedName, AreaScheme curAreaScheme)
        {
            ElementId catId = new ElementId(BuiltInCategory.OST_Areas);
            ViewSchedule newSchedule = ViewSchedule.CreateSchedule(curDoc, catId, curAreaScheme.Id);
            newSchedule.Name = schedName;

            return newSchedule;
        }

        internal static List<ViewSchedule> GetAllSchedulesByElevation(Document curDoc, string newElev)
        {
            List<ViewSchedule> m_scheduleList = GetAllSchedules(curDoc);

            List<ViewSchedule> m_returnList = new List<ViewSchedule>();

            foreach (ViewSchedule curVS in m_scheduleList)
            {
                if (curVS.Name.EndsWith(newElev))
                {
                    m_returnList.Add(curVS);
                }
            }

            return m_returnList;
        }

        internal static List<ViewSchedule> GetAllSchedules(Document curDoc)
        {
            List<ViewSchedule> m_schedList = new List<ViewSchedule>();

            FilteredElementCollector curCollector = new FilteredElementCollector(curDoc);
            curCollector.OfClass(typeof(ViewSchedule));
            curCollector.WhereElementIsNotElementType();

            //loop through views and check if schedule - if so then put into schedule list
            foreach (ViewSchedule curView in curCollector)
            {
                if (curView.ViewType == ViewType.Schedule)
                {
                    if (curView.IsTemplate == false)
                    {
                        if (curView.Name.Contains("<") && curView.Name.Contains(">"))
                            continue;
                        else
                            m_schedList.Add((ViewSchedule)curView);
                    }
                }
            }

            return m_schedList;
        }

        internal static List<ViewSchedule> GetAllScheduleTemplates(Document curDoc)
        {
            List<ViewSchedule> m_schedList = new List<ViewSchedule>();

            FilteredElementCollector curCollector = new FilteredElementCollector(curDoc);
            curCollector.OfClass(typeof(ViewSchedule));
            curCollector.WhereElementIsNotElementType();

            foreach (ViewSchedule curView in curCollector)
            {
                if (curView.ViewType == ViewType.Schedule)
                {
                    if (curView.IsTemplate == true) // Only templates
                    {
                        m_schedList.Add((ViewSchedule)curView);
                    }
                }
            }

            return m_schedList;
        }

        internal static ViewSchedule GetScheduleByName(Document curDoc, string v)
        {
            FilteredElementCollector collector = new FilteredElementCollector(curDoc);
            collector.OfClass(typeof(ViewSchedule));

            foreach (ViewSchedule curSchedule in collector)
            {
                if (curSchedule.Name == v)
                    return curSchedule;
            }

            return null;
        }

        internal static List<ViewSchedule> GetAllScheduleByNameContains(Document curDoc, string schedName)
        {
            List<ViewSchedule> m_scheduleList = GetAllSchedules(curDoc);

            List<ViewSchedule> m_returnList = new List<ViewSchedule>();

            foreach (ViewSchedule curSchedule in m_scheduleList)
            {
                if (curSchedule.Name.Contains(schedName))
                    m_returnList.Add(curSchedule);
            }

            return m_returnList;
        }

        private static List<ViewSchedule> GetAllViewScheduleTemplates(Document curDoc)
        {
            List<ViewSchedule> returnList = new List<ViewSchedule>();
            List<ViewSchedule> viewList = GetAllScheduleTemplates(curDoc);

            //loop through views and check if is view template
            foreach (ViewSchedule v in viewList)
            {
                if (v.IsTemplate == true)
                {
                    //add view template to list
                    returnList.Add(v);
                }
            }

            return returnList;
        }

        public static ViewSchedule GetViewScheduleTemplateByName(Document curDoc, string viewSchedTemplateName)
        {
            List<ViewSchedule> viewSchedTemplateList = GetAllViewScheduleTemplates(curDoc);

            foreach (ViewSchedule v in viewSchedTemplateList)
            {
                if (v.Name == viewSchedTemplateName)
                {
                    return v;
                }
            }

            return null;
        }

        internal static List<ScheduleSheetInstance> GetAllScheduleSheetInstancesByNameAndView(Document curDoc, string elevName, View activeView)
        {
            List<ScheduleSheetInstance> ssiList = GetAllScheduleSheetInstancesByView(curDoc, activeView);

            List<ScheduleSheetInstance> returnList = new List<ScheduleSheetInstance>();

            foreach (ScheduleSheetInstance curInstance in ssiList)
            {
                if (curInstance.Name.Contains(elevName))
                    returnList.Add(curInstance);
            }

            return returnList;
        }

        internal static List<ScheduleSheetInstance> GetAllScheduleSheetInstancesByView(Document curDoc, View activeView)
        {
            FilteredElementCollector colSSI = new FilteredElementCollector(curDoc, activeView.Id);
            colSSI.OfClass(typeof(ScheduleSheetInstance));

            List<ScheduleSheetInstance> returnList = new List<ScheduleSheetInstance>();

            foreach (ScheduleSheetInstance curInstance in colSSI)
            {
                returnList.Add(curInstance);
            }

            return returnList;
        }

        internal static List<ScheduleSheetInstance> GetAllScheduleSheetInstancesByName(Document curDoc, string elevName)
        {
            List<ScheduleSheetInstance> ssiList = GetAllScheduleSheetInstances(curDoc);

            List<ScheduleSheetInstance> returnList = new List<ScheduleSheetInstance>();

            foreach (ScheduleSheetInstance curInstance in ssiList)
            {
                if (curInstance.Name.Contains(elevName))
                    returnList.Add(curInstance);
            }

            return returnList;
        }

        internal static List<ScheduleSheetInstance> GetAllScheduleSheetInstances(Document curDoc)
        {
            FilteredElementCollector colSSI = new FilteredElementCollector(curDoc);
            colSSI.OfClass(typeof(ScheduleSheetInstance));

            List<ScheduleSheetInstance> returnList = new List<ScheduleSheetInstance>();

            foreach (ScheduleSheetInstance curInstance in colSSI)
            {
                returnList.Add(curInstance);
            }

            return returnList;
        }

        internal static ViewSchedule GetScheduleByNameContains(Document curDoc, string scheduleString)
        {
            List<ViewSchedule> m_scheduleList = GetAllSchedules(curDoc);

            foreach (ViewSchedule curSchedule in m_scheduleList)
            {
                if (curSchedule.Name.Contains(scheduleString))
                    return curSchedule;
            }

            return null;
        }

        internal static List<ViewSchedule> GetScheduleToRenameByNameContains(Document curDoc, string scheduleString)
        {
            List<ViewSchedule> m_scheduleList = GetAllSchedules(curDoc);

            List<ViewSchedule> m_returnList = new List<ViewSchedule>();

            foreach (ViewSchedule curSchedule in m_scheduleList)
            {
                if (curSchedule.Name.Contains(scheduleString))
                    m_returnList.Add(curSchedule);
            }

            return m_returnList;
        }

        internal static List<string> GetAllScheduleNames(Document curDoc)
        {
            List<ViewSchedule> m_schedList = GetAllSchedules(curDoc);

            List<string> m_Names = new List<string>();

            foreach (ViewSchedule curSched in m_schedList)
            {
                m_Names.Add(curSched.Name);
            }

            return m_Names;
        }

        internal static List<string> GetSchedulesNotUsed(List<string> schedNames, List<string> schedInstances)
        {
            IEnumerable<string> m_returnList;

            m_returnList = schedNames.Except(schedInstances);

            return m_returnList.ToList();
        }

        internal static List<ViewSchedule> GetSchedulesToDelete(Document curDoc, List<string> schedNotUsed)
        {
            List<ViewSchedule> m_returnList = new List<ViewSchedule>();

            foreach (string schedName in schedNotUsed)
            {
                string curName = schedName;

                ViewSchedule curSched = GetViewScheduleByName(curDoc, curName);

                if (curSched != null)
                {
                    m_returnList.Add(curSched);
                }
            }

            return m_returnList;
        }

        internal static ViewSchedule GetViewScheduleByName(Document curDoc, string viewScheduleName)
        {
            List<ViewSchedule> m_SchedList = GetAllSchedules(curDoc);

            ViewSchedule m_viewSchedNotFound = null;

            foreach (ViewSchedule curViewSched in m_SchedList)
            {
                if (curViewSched.Name == viewScheduleName)
                {
                    return curViewSched;
                }
            }

            return m_viewSchedNotFound;
        }

        internal static List<string> GetAllSSINames(Document curDoc)
        {
            FilteredElementCollector m_colSSI = new FilteredElementCollector(curDoc);
            m_colSSI.OfClass(typeof(ScheduleSheetInstance));

            List<string> m_returnList = new List<string>();

            foreach (ScheduleSheetInstance curInstance in m_colSSI)
            {
                string schedName = curInstance.Name as string;
                m_returnList.Add(schedName);
            }

            return m_returnList;
        }        

        #endregion

        #region Sheets

        internal static List<ViewSheet> GetAndSortAllSheets(Document curDoc)
        {
            var m_returnList = new FilteredElementCollector(curDoc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .ToList();

            return m_returnList;
        }

        internal static List<ViewSheet> GetAllSheets(Document curDoc)
        {
            //get all sheets
            FilteredElementCollector m_colSheets = new FilteredElementCollector(curDoc);
            m_colSheets.OfCategory(BuiltInCategory.OST_Sheets);

            List<ViewSheet> m_returnSheets = new List<ViewSheet>();
            foreach (ViewSheet curSheet in m_colSheets.ToElements())
            {
                m_returnSheets.Add(curSheet);
            }

            return m_returnSheets;
        }

        internal static ViewSheet GetSheetByElevationAndNameContains(Document curDoc, string newElev, string sheetName)
        {
            List<ViewSheet> sheetList = GetAllSheets(curDoc);

            foreach (ViewSheet curVS in sheetList)
            {
                if (curVS.SheetNumber.Contains(newElev.ToLower()) && curVS.Name.Contains(sheetName))
                {
                    return curVS;
                }
            }

            return null;
        }

        internal static List<ViewSheet> GetAllSheetsByElevation(Document curDoc, string elevDesignation)
        {
            //get all sheets
            List<ViewSheet> m_sheetList = GetAllSheets(curDoc);

            List<ViewSheet> m_returnSheets = new List<ViewSheet>();

            foreach (ViewSheet curSheet in m_sheetList)
            {
                if (curSheet.SheetNumber.Contains(elevDesignation))
                    m_returnSheets.Add(curSheet);
            }

            return m_returnSheets;
        }

        public static List<ViewSheet> GetSheetsByNumber(Document curDoc, string sheetNumber)
        {
            List<ViewSheet> returnSheets = new List<ViewSheet>();

            //get all sheets
            List<ViewSheet> curSheets = GetAllSheets(curDoc);

            //loop through sheets and check sheet name
            foreach (ViewSheet curSheet in curSheets)
            {
                if (curSheet.SheetNumber.Contains(sheetNumber))
                {
                    returnSheets.Add(curSheet);
                }
            }

            return returnSheets;
        }

        internal static List<string> GetAllSheetGroupsByCategory(Document curDoc, string categoryValue)
        {
            List<string> m_groups = new List<string>();

            // Get all sheet views in the project that have the specified category value
            List<ViewSheet> m_sheets = GetAllSheetsByCategory(curDoc, categoryValue);

            // Iterate through each sheet view and get the value of the "Group" parameter
            foreach (ViewSheet sheet in m_sheets)
            {
                // Get the "Group" parameter of the sheet view
                Parameter groupParameter = sheet.LookupParameter("Group");

                // Check if the "Group" parameter is valid and get its value
                if (groupParameter != null && groupParameter.Definition.Name == "Group")
                {
                    string groupValue = groupParameter.AsString();

                    // Check if the group value is not null or empty, and if it hasn't already been added to the list
                    if (!string.IsNullOrEmpty(groupValue) && !m_groups.Contains(groupValue))
                    {
                        m_groups.Add(groupValue);
                    }
                }
            }

            return m_groups;
        }

        public static List<ViewSheet> GetAllSheetsByCategory(Document curDoc, string categoryValue)
        {
            List<ViewSheet> m_sheets = new List<ViewSheet>();

            // Get all sheets in the project
            FilteredElementCollector sheetCollector = new FilteredElementCollector(curDoc);
            ICollection<Element> sheetElements = sheetCollector.OfClass(typeof(ViewSheet)).ToElements();

            // Iterate through each sheet and check if it has the specified category parameter with the value of "Inactive"
            foreach (Element sheetElement in sheetElements)
            {
                ViewSheet sheet = sheetElement as ViewSheet;
                if (sheet != null)
                {
                    // Get the category parameter of the sheet
                    Parameter categoryParameter = sheet.LookupParameter("Category");

                    // Check if the category parameter is valid and has the expected value
                    if (categoryParameter != null && categoryParameter.Definition.Name == "Category" &&
                        categoryParameter.AsValueString() == categoryValue)
                    {
                        m_sheets.Add(sheet);
                    }
                }
            }

            return m_sheets;
        }

        internal static List<ViewSheet> GetSheetsByGroupName(Document curDoc, string stringValue)
        {
            List<ViewSheet> m_viewSheets = GetAllSheets(curDoc);

            List<ViewSheet> m_returnGroups = new List<ViewSheet>();

            foreach (ViewSheet curSheet in m_viewSheets)
            {
                // Get the "Group" parameter of the sheet view
                Parameter groupParameter = curSheet.LookupParameter("Group");

                // Check for the "Group" parameter and add sheet tolist
                if (groupParameter != null && groupParameter.AsValueString() == stringValue)
                    m_returnGroups.Add(curSheet);
            }

            return m_returnGroups;
        }

        internal static List<ViewSheet> GetAllSheetsByNameContains(Document curDoc, string sheetName)
        {
            List<ViewSheet> m_viewSheets = GetAllSheets(curDoc);

            List<ViewSheet> m_returnSheets = new List<ViewSheet>();

            foreach (ViewSheet curSheet in m_viewSheets)
            {
                if (curSheet.Name == sheetName)
                    m_returnSheets.Add(curSheet);
            }

            return m_returnSheets;
        }

        #endregion

        #region Sheet Collections

        internal static void DeleteAllSheetCollections(Document curDoc)
        {
            try
            {
                // Alternative approach - delete all ViewSheetSet elements (sheet collections)
                var m_colSheets = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(SheetCollection))
                    .ToList();

                foreach (var curSheetCol in m_colSheets)
                {
                    try
                    {
                        curDoc.Delete(curSheetCol.Id);
                    }
                    catch { }
                }
            }
            catch { }
        }

        #endregion

        #region Strings

        internal static string GetLastCharacterInString(string grpName, string curElev, string newElev)
        {
            char lastChar = grpName[grpName.Length - 1];

            char firstChar = grpName[0];

            string grpLastChar = lastChar.ToString();

            string grpFirstChar = firstChar.ToString();

            if (grpLastChar == curElev)
            {
                return "Elevation " + newElev;
            }
            else if (grpFirstChar == curElev)
            {
                return newElev + grpName.Substring(1);
            }
            else
            {
                return grpName;
            }
        }

        internal static string GetStringBetweenCharacters(string input, string charFrom, string charTo)
        {
            //string cleanInput = CleanSheetNumber(input);

            int posFrom = input.IndexOf(charFrom);
            if (posFrom != -1) //if found char
            {
                int posTo = input.IndexOf(charTo, posFrom + 1);
                if (posTo != -1) //if found char
                {
                    return input.Substring(posFrom + 1, posTo - posFrom - 1);
                }
            }

            return string.Empty;
        }

        internal static string CleanSheetNumber(string sheetNumber)
        {
            Regex regex = new Regex(@"[^a-zA-Z0-9\s]", (RegexOptions)0);
            string replaceText = regex.Replace(sheetNumber, "");

            return replaceText;
        }

        internal static int GetIndexOfFirstLetter(string schedTitle)
        {
            var index = 0;
            foreach (var c in schedTitle)
                if (char.IsLetter(c))
                    return index;
                else
                    index++;

            return schedTitle.Length;
        }

        #endregion

        #region Titleblocks

        internal static FamilySymbol GetTitleBlockByNameContains(Document curDoc, string tBlockName)
        {
            FilteredElementCollector m_tBlocks = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType();

            foreach (FamilySymbol curTBlock in m_tBlocks)
            {
                if (curTBlock.Name.Contains(tBlockName))
                    return curTBlock;
            }

            return null;
        }

        #endregion

        #region Task Dialog

        /// <summary>
        /// Displays a warning dialog to the user with custom title and message
        /// </summary>
        /// <param name="tdName">The internal name of the TaskDialog</param>
        /// <param name="tdTitle">The title displayed in the dialog header</param>
        /// <param name="textMessage">The main message content to display to the user</param>
        internal static void TaskDialogWarning(string tdName, string tdTitle, string textMessage)
        {
            // Create a new TaskDialog with the specified name
            TaskDialog m_Dialog = new TaskDialog(tdName);

            // Set the warning icon to indicate this is a warning message
            m_Dialog.MainIcon = Icon.TaskDialogIconWarning;

            // Set the custom title for the dialog
            m_Dialog.Title = tdTitle;

            // Disable automatic title prefixing to use our custom title exactly as specified
            m_Dialog.TitleAutoPrefix = false;

            // Set the main message content that will be displayed to the user
            m_Dialog.MainContent = textMessage;

            // Add a Close button for the user to dismiss the dialog
            m_Dialog.CommonButtons = TaskDialogCommonButtons.Close;

            // Display the dialog and capture the result (though we don't use it for warnings)
            TaskDialogResult m_DialogResult = m_Dialog.Show();
        }

        /// <summary>
        /// Displays an information dialog to the user with custom title and message
        /// </summary>
        /// <param name="tdName">The internal name of the TaskDialog</param>
        /// <param name="tdTitle">The title displayed in the dialog header</param>
        /// <param name="textMessage">The main message content to display to the user</param>
        internal static void TaskDialogInformation(string tdName, string tdTitle, string textMessage)
        {
            // Create a new TaskDialog with the specified name
            TaskDialog m_Dialog = new TaskDialog(tdName);

            // Set the warning icon to indicate this is a warning message
            m_Dialog.MainIcon = Icon.TaskDialogIconInformation;

            // Set the custom title for the dialog
            m_Dialog.Title = tdTitle;

            // Disable automatic title prefixing to use our custom title exactly as specified
            m_Dialog.TitleAutoPrefix = false;

            // Set the main message content that will be displayed to the user
            m_Dialog.MainContent = textMessage;

            // Add a Close button for the user to dismiss the dialog
            m_Dialog.CommonButtons = TaskDialogCommonButtons.Close;

            // Display the dialog and capture the result (though we don't use it for warnings)
            TaskDialogResult m_DialogResult = m_Dialog.Show();
        }

        /// <summary>
        /// Displays an error dialog to the user with custom title and message
        /// </summary>
        /// <param name="tdName">The internal name of the TaskDialog</param>
        /// <param name="tdTitle">The title displayed in the dialog header</param>
        /// <param name="textMessage">The main message content to display to the user</param>
        internal static void TaskDialogError(string tdName, string tdTitle, string textMessage)
        {
            // Create a new TaskDialog with the specified name
            TaskDialog m_Dialog = new TaskDialog(tdName);

            // Set the warning icon to indicate this is a warning message
            m_Dialog.MainIcon = Icon.TaskDialogIconError;

            // Set the custom title for the dialog
            m_Dialog.Title = tdTitle;

            // Disable automatic title prefixing to use our custom title exactly as specified
            m_Dialog.TitleAutoPrefix = false;

            // Set the main message content that will be displayed to the user
            m_Dialog.MainContent = textMessage;

            // Add a Close button for the user to dismiss the dialog
            m_Dialog.CommonButtons = TaskDialogCommonButtons.Close;

            // Display the dialog and capture the result (though we don't use it for warnings)
            TaskDialogResult m_DialogResult = m_Dialog.Show();
        }


        #endregion

        #region Views

        public static List<View> GetAllNonTemplateViews(Document curDoc)
        {
            FilteredElementCollector m_colviews = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Views);

            List<View> m_returnViews = new List<View>();

            // loop through views and check if not a template
            foreach (View curView in m_colviews.ToElements())
            {
                m_returnViews.Add(curView);
            }

            return m_returnViews;
        }

        public static List<View> GetAllElevationViews(Document curDoc)
        {
            List<View> returnList = new List<View>();

            FilteredElementCollector colViews = new FilteredElementCollector(curDoc);
            colViews.OfClass(typeof(View));

            // loop through views and check for elevation views
            foreach (View x in colViews)
            {
                if (x.GetType() == typeof(ViewSection))
                {
                    if (x.IsTemplate == false)
                    {
                        if (x.ViewType == ViewType.Elevation)
                        {
                            // add view to list
                            returnList.Add(x);
                        }
                    }
                }
            }

            return returnList;
        }

        public static List<View> GetAllSectionViews(Document curDoc)
        {
            //get all ViewSection views
            FilteredElementCollector m_colViews = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Views)
                .OfClass(typeof(ViewSection));

            // create an empty list to hold the views
            List<View> m_Views = new List<View>();

            // loop through each view & add it to the list
            foreach (View x in m_colViews)
            {
                if (x.IsTemplate == false)
                {
                    m_Views.Add(x);
                }
            }

            // return the list of views
            return m_Views;
        }


        internal static List<View> GetAllViewsByCategory(Document curDoc, string catName)
        {
            List<View> m_colViews = GetAllViews(curDoc);

            List<View> m_returnList = new List<View>();

            foreach (View curView in m_colViews)
            {
                string viewCat = GetParameterValueByName(curView, "Category");
                if (!string.IsNullOrEmpty(viewCat))

                {
                    if (viewCat.Contains(catName))
                        m_returnList.Add(curView);
                }

            }

            return m_returnList;
        }

        internal static List<View> GetAllViewsByCategoryAndViewTemplate(Document curDoc, string catName, string vtName)
        {
            List<View> m_colViews = GetAllViewsByCategory(curDoc, catName);

            List<View> m_returnList = new List<View>();

            foreach (View curView in m_colViews)
            {
                ElementId vtId = curView.ViewTemplateId;

                if (vtId != ElementId.InvalidElementId)
                {
                    View vt = curDoc.GetElement(vtId) as View;

                    if (vt.Name == vtName)
                        m_returnList.Add(curView);
                }
            }

            return m_returnList;
        }

        internal static List<View> GetAllViews(Document curDoc)
        {
            FilteredElementCollector m_colviews = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Views);

            List<View> m_views = new List<View>();

            // loop through views and add to list
            foreach (View x in m_colviews.ToElements())
            {
                m_views.Add(x);
            }

            return m_views;
        }

        internal static List<View> GetAllViewsByCategoryContains(Document curDoc, string catName)
        {
            List<View> m_colViews = GetAllViewsByCategory(curDoc, catName);

            List<View> m_returnList = new List<View>();

            foreach (View curView in m_colViews)
            {
                string viewCat = GetParameterValueByName(curView, "Category");

                if (viewCat.Contains(catName))
                    m_returnList.Add(curView);
            }

            return m_returnList;
        }

        #endregion

        #region Viewports

        internal static List<Viewport> GetAllViewports(Document curDoc)
        {
            //get all viewports
            FilteredElementCollector m_vpCollector = new FilteredElementCollector(curDoc);
            m_vpCollector.OfCategory(BuiltInCategory.OST_Viewports);

            //output viewports to list
            List<Viewport> m_vpList = new List<Viewport>();
            foreach (Viewport curVP in m_vpCollector)
            {
                //add to list
                m_vpList.Add(curVP);
            }

            return m_vpList;
        }

        #endregion

        #region View Templates

        public static List<View> GetAllViewTemplates(Document curDoc)
        {
            List<View> returnList = new List<View>();
            List<View> viewList = GetAllNonTemplateViews(curDoc);

            //loop through views and check if is view template
            foreach (View v in viewList)
            {
                if (v.IsTemplate == true)
                {
                    //add view template to list
                    returnList.Add(v);
                }
            }

            return returnList;
        }

        public static List<string> GetAllViewTemplateNames(Document m_doc)
        {
            //returns list of view templates
            List<string> viewTempList = new List<string>();
            List<View> viewList = new List<View>();
            viewList = GetAllNonTemplateViews(m_doc);

            //loop through views and check if is view template
            foreach (View v in viewList)
            {
                if (v.IsTemplate == true)
                {
                    //add view template to list
                    viewTempList.Add(v.Name);
                }
            }

            return viewTempList;
        }

        public static View GetViewTemplateByName(Document curDoc, string viewTemplateName)
        {
            List<View> viewTemplateList = GetAllViewTemplates(curDoc);

            foreach (View v in viewTemplateList)
            {
                if (v.Name == viewTemplateName)
                {
                    return v;
                }
            }

            return null;
        }

        internal static ElementId ImportViewTemplates(Document sourceDoc, View sourceTemplate, Document targetDoc)
        {
            CopyPasteOptions copyPasteOptions = new CopyPasteOptions();

            ElementId sourceTemplateId = sourceTemplate.Id;

            List<ElementId> elementIds = new List<ElementId>();
            elementIds.Add(sourceTemplate.Id);

            ElementTransformUtils.CopyElements(sourceDoc, elementIds, targetDoc, Autodesk.Revit.DB.Transform.Identity, copyPasteOptions);

            return sourceTemplate.Id;
        }

        internal static View GetViewTemplateByNameContains(Document curDoc, string vtName)
        {
            List<View> m_colVTs = Utils.GetAllViewTemplates(curDoc);

            foreach (View curVT in m_colVTs)
            {
                if (curVT.Name.Contains(vtName))
                    return curVT;
            }

            return null;
        }

        internal static View GetViewTemplateByCategoryEquals(Document curDoc, string vtName)
        {
            List<View> m_colVTs = Utils.GetAllViewTemplates(curDoc);

            foreach (View curVT in m_colVTs)
            {
                if (curVT.Category.Equals(vtName))
                    return curVT;
            }

            return null;
        }

        internal static List<View> GetViewsByViewTemplateName(Document curDoc, string templateName)
        {
            // Find the template by name
            View template = Utils.GetViewTemplateByName(curDoc, templateName);
            if (template == null) return new List<View>();

            // Find all views that currently use this template
            return new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Views)
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewTemplateId == template.Id)
                .ToList();
        }        

        internal static void AssignTemplateToView(List<View> allViews, string newTemplateName, Document curDoc, ref int viewsUpdated)
        {
            View newViewTemp = GetViewTemplateByName(curDoc, newTemplateName);

            if (newViewTemp != null)
            {
                foreach (View curView in allViews)
                {
                    curView.ViewTemplateId = newViewTemp.Id;
                    viewsUpdated++;
                }
            }
        }

        #endregion

        internal static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T foundChild = FindVisualChild<T>(child);
                    if (foundChild != null)
                        return foundChild;
                }
            }
            return null;
        }

        internal static BitmapImage GetEmbeddedImage(string resourcePath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream == null) return null;

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                return image;
            }
        }

        internal static double GetParamValueInFeet(Element elem, string paramName)
        {
            Parameter p = elem.LookupParameter(paramName);
            return (p != null && p.StorageType == StorageType.Double) ? p.AsDouble() : 0.0;
        }

        internal static void SetParamValueInFeet(Element elem, string paramName, double valueInFeet)
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                p.Set(valueInFeet);
        }

        internal static void SetParamInt(Element elem, string paramName, int value)
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Integer)
                p.Set(value);
        }
    }
}
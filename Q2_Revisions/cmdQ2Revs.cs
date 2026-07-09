using Q2_Revisions.Common;

namespace Q2_Revisions
{
    [Transaction(TransactionMode.Manual)]
    public class cmdQ2Revs : IExternalCommand
    {
        // set variables for file paths
        private const string ShelvingFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Generic Model\Interior";
        private const string CeilingItemsPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Generic Model\Interior";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document curDoc = uidoc.Document;

            // create & set required variables for the command
            string planName = curDoc.ProjectInformation.LookupParameter("Plan Name")?.AsString() ?? curDoc.Title;


            // launch the form to get user input for spec level
            frmQ2Revs curForm = new frmQ2Revs();
            curForm.ShowDialog();

            // create & set variables for the spec level selected by the user
            string specLevel = curForm.SpecLevel;

            // null check for spec level selection
            if (specLevel == null)
                return Result.Cancelled;

            #region Floor Plan Revisions

            // set the active view to the First Floor Plan
            View annotationView = GetFirstFloorAnnotationView(curDoc);
            if (annotationView != null)
                uidoc.ActiveView = annotationView;

            #region Revision 1: update 5-stack shelving to 4-stack shelving

            // create variables for shelving count
            int shelfCount = 0;

            // create a transaction
            using (Transaction t1 = new Transaction(curDoc, "Update Shelving"))
            {
                // start the transaction
                t1.Start();

                // call the method to update the shelving family
                shelfCount = UpdateShelving(curDoc);

                // commit the transaction
                t1.Commit();
            }

            // notify the user shelf update complete
            Utils.TaskDialogInformation("Q2 Revisions", "Update Shelving",
               $"{shelfCount} shelf stack(s) were updated to 4 Shelves.");

            #endregion

            #region Revision 2: HS at First Floor (except Terrata)

            // create variable for rooms where floor finish was updated
            List<string> updatedRooms = new List<string>();

            // check value of specLevel and only update floor materials if not Terrata
            if (specLevel != "Terrata")
            {
                // create a transaction
                using (Transaction t2 = new Transaction(curDoc, "Update Floor Materials"))
                {
                    // start the transaction
                    t2.Start();

                    // call the method to update floor materials
                    updatedRooms =UpdateFloorMaterials(curDoc);

                    // commit the transaction
                    t2.Commit();
                }

                // create notificaiotn message
                string flooringMsg = updatedRooms.Count == 0
                   ? "The flooring in all First Floor rooms is already HS."
                   : $"The flooring was changed in the following {updatedRooms.Count} room(s):\n" +
                     string.Join("\n", updatedRooms.Select(r => $"• {r}"));

                // notify the user of flooring updates
                Utils.TaskDialogInformation("Q2 Revisions", "Update Floor Materials", flooringMsg);
            }

            #endregion

            #region Revision 3: Update Ceiling Items Family

            // create a transaction
            using (Transaction t3 = new Transaction(curDoc, "Update Clg Items"))
            {
                // start the transaction
                t3.Start();

                // call the method to update the Disp Stairs family
                UpdateClgItems(curDoc);

                // commit the transaction
                t3.Commit();
            }



            #endregion

            #endregion

            // notify the user of results
            Utils.TaskDialogInformation("Q2 Revisions", "Q2 Revisions Complete",
                $"Q2 Revisions completed. Refer to {planName}.txt file for revisions to complete manually.");

            return Result.Succeeded;
        }        

        #region Floor Plan Revisions Methods

        /// <summary>
        /// method to set active view to the First Floor Plan Annotation view, if it exists.
        /// </summary>
        private View GetFirstFloorAnnotationView(Document curDoc)
        {
            Level firstFloor = new FilteredElementCollector(curDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals("First Floor", StringComparison.OrdinalIgnoreCase));

            if (firstFloor == null) return null;

            return new FilteredElementCollector(curDoc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v => v.GenLevel?.Id == firstFloor.Id &&
                                     v.Name.IndexOf("Annotation", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// method to update all instances of the "Shelving" 
        /// family from "5 Shelves" to "4 Shelves" in the current document.
        /// </summary>
        private int UpdateShelving(Document curDoc)
        {
            Utils.LoadFamilyFromLibrary(curDoc, ShelvingFamilyPath, "LD_GM_Shelving");

            FamilySymbol newType = Utils.FindFamilySymbol(curDoc, "LD_GM_Shelving", "4 Shelves");
            if (newType == null) return 0;
            if (!newType.IsActive) newType.Activate();

            List<FamilyInstance> oldInstances = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName == "Shelving" && fi.Symbol.Name == "5 Shelves")
                .ToList();

            foreach (FamilyInstance inst in oldInstances)
            {
                double depth1 = Utils.GetParamValueInFeet(inst, "Depth1");
                double depth2 = Utils.GetParamValueInFeet(inst, "Depth2");
                double depth3 = Utils.GetParamValueInFeet(inst, "Depth3");
                double depth4 = Utils.GetParamValueInFeet(inst, "Depth4");
                double depth5 = Utils.GetParamValueInFeet(inst, "Depth5");

                bool shallowUppers = depth4 < depth3 || depth5 < depth3;

                inst.ChangeTypeId(newType.Id);

                Utils.SetParamValueInFeet(inst, "Depth1", depth1);
                Utils.SetParamValueInFeet(inst, "Depth2", depth2);
                Utils.SetParamValueInFeet(inst, "Depth3", depth3);
                Utils.SetParamValueInFeet(inst, "Depth4", depth4);

                if (shallowUppers)
                    Utils.SetParamInt(inst, "Shallow Uppers", 1);
            }

            return oldInstances.Count;
        }

        /// <summary>
        /// method to update flooring in all rooms on the first floor to "HS" (Hard Surface)
        /// if they are not already "Concrete", "Conc", or "HS".
        /// </summary>
        private List<string> UpdateFloorMaterials(Document curDoc)
        {
            List<string> updatedRooms = new List<string>();

            Level firstFloor = new FilteredElementCollector(curDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();

            if (firstFloor == null) return updatedRooms;

            // Set all 1st-floor rooms that are carpet to Hard Surface (HS)
            foreach (SpatialElement room in new FilteredElementCollector(curDoc)
                .OfClass(typeof(SpatialElement))
                .Cast<SpatialElement>()
                .Where(r => r.Location != null && r.LevelId == firstFloor.Id))
            {
                Parameter floorFinish = room.LookupParameter("Floor Finish");
                if (floorFinish == null || floorFinish.IsReadOnly) continue;

                string current = floorFinish.AsString() ?? string.Empty;
                if (current.Equals("Concrete", StringComparison.OrdinalIgnoreCase) ||
                    current.Equals("Conc", StringComparison.OrdinalIgnoreCase) ||
                    current.Equals("HS", StringComparison.OrdinalIgnoreCase))
                    continue;

                floorFinish.Set("HS");
                updatedRooms.Add(room.LookupParameter("Name")?.AsString() ?? $"Room {room.Id}");
            }

            // Delete floor break symbols where Floor 1 or Floor 2 = "C"
            List<ElementId> toDelete = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_FurnitureSystems)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName == "Floor Material" && fi.Symbol.Name == "Type 1")
                .Where(fi =>
                {
                    string f1 = fi.LookupParameter("Floor 1")?.AsString() ?? string.Empty;
                    string f2 = fi.LookupParameter("Floor 2")?.AsString() ?? string.Empty;
                    return f1.Equals("C", StringComparison.OrdinalIgnoreCase) ||
                           f2.Equals("C", StringComparison.OrdinalIgnoreCase);
                })
                .Select(fi => fi.Id)
                .ToList();

            foreach (ElementId id in toDelete)
                curDoc.Delete(id);

            return updatedRooms;
        }

        /// <summary>
        /// method to update Ceiliing Items family in the current document
        /// to the show current version of the "Disp Stairs" family type.
        /// </summary>
        private void UpdateClgItems(Document curDoc)
        {
            Utils.LoadFamilyFromLibrary(curDoc, CeilingItemsPath, "LD_GM_Ceiling_Items");

            // Find the base "Disp Stair" type to use as a template for duplicating sized types
            FamilySymbol baseType = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.FamilyName == "LD_GM_Ceiling_Items" &&
                                      fs.Name.StartsWith("Disp Stair", StringComparison.OrdinalIgnoreCase));

            if (baseType == null) return;

            // Only swap instances that are "Disp Stair" types in --Ceiling Items--
            List<FamilyInstance> oldInstances = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName == "--Ceiling Items--" &&
                             fi.Symbol.Name.StartsWith("Disp Stair", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Dictionary<string, FamilySymbol> sizedTypes = new Dictionary<string, FamilySymbol>();

            foreach (FamilyInstance inst in oldInstances)
            {
                double oldWidth = inst.Symbol.LookupParameter("Width")?.AsDouble() ?? 0;
                double oldLength = inst.Symbol.LookupParameter("Length")?.AsDouble() ?? 0;
                string typeComments = inst.Symbol.LookupParameter("Type Comments")?.AsString() ?? string.Empty;

                double baseWidth = baseType.LookupParameter("Width")?.AsDouble() ?? 0;
                double baseLength = baseType.LookupParameter("Length")?.AsDouble() ?? 0;

                bool dimensionsMatch = Math.Abs(oldWidth - baseWidth) < 0.01 &&
                                       Math.Abs(oldLength - baseLength) < 0.01;

                FamilySymbol targetType;

                if (dimensionsMatch)
                {
                    targetType = baseType;
                }
                else
                {
                    int wIn = (int)Math.Round(oldWidth * 12);
                    int lIn = (int)Math.Round(oldLength * 12);
                    string typeName = $"Disp Stair {wIn}\"x{lIn}\"";

                    if (!sizedTypes.TryGetValue(typeName, out targetType))
                    {
                        targetType = new FilteredElementCollector(curDoc)
                            .OfClass(typeof(FamilySymbol))
                            .Cast<FamilySymbol>()
                            .FirstOrDefault(fs => fs.FamilyName == "LD_GM_Ceiling_Items" && fs.Name == typeName);

                        if (targetType == null)
                        {
                            targetType = baseType.Duplicate(typeName) as FamilySymbol;
                            if (targetType == null) continue;

                            Parameter wp = targetType.LookupParameter("Width");
                            Parameter lp = targetType.LookupParameter("Length");
                            Parameter cp = targetType.LookupParameter("Type Comments");
                            if (wp != null && !wp.IsReadOnly) wp.Set(oldWidth);
                            if (lp != null && !lp.IsReadOnly) lp.Set(oldLength);
                            if (cp != null && !cp.IsReadOnly && !string.IsNullOrEmpty(typeComments))
                                cp.Set(typeComments);
                        }

                        sizedTypes[typeName] = targetType;
                    }
                }

                if (!targetType.IsActive) targetType.Activate();

                // Set Arrow = Yes on the type
                Parameter arrow = targetType.LookupParameter("Arrow");
                if (arrow != null && !arrow.IsReadOnly) arrow.Set(1);

                inst.ChangeTypeId(targetType.Id);
            }
        }

        #endregion
    }
}
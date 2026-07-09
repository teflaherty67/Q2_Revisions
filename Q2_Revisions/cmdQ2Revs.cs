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

            // create variable for disp stair count
            int dispStairCount = 0;

            // create a transaction
            using (Transaction t3 = new Transaction(curDoc, "Update Clg Items"))
            {
                // start the transaction
                t3.Start();

                // call the method to update the Disp Stairs family
                dispStairCount = UpdateClgItems(curDoc);

                // commit the transaction
                t3.Commit();
            }

            // notify the user of ceiling items update
            Utils.TaskDialogInformation("Q2 Revisions", "Update Ceiling Items",
                dispStairCount == 0
                    ? "No Disp Stair types were found in the --Ceiling Items-- family."
                     : $"The --Ceiling Items-- family was updated to the new LD_GM_Ceiling_Items family." +
                     $" {dispStairCount} Disp Stair {(dispStairCount == 1 ? "type was" : "types were")} replaced with the new family type.");

            #endregion

            #region Revision 4: Remove SROs

            // create variable for SRO count
            int countSRO = 0;

            // create a transaction
            using (Transaction t4 = new Transaction(curDoc, "Remove SROs"))
            {
                // start the transaction
                t4.Start();

                // call the method to remove SROs and heal walls
                countSRO = RemoveSROs(curDoc);

                // commit the transaction
                t4.Commit();
            }

            // notify the user of SRO removal
            Utils.TaskDialogInformation("Q2 Revisions", "Remove SROs",
                countSRO == 0
                    ? "No SROs were found in the project."
                    : $"{countSRO} SRO{(countSRO == 1 ? " was" : "s were")} removed from the project.");

            #endregion

            #endregion

            // build the list of manual checklist items for the .txt file
            string txtFilePath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(curDoc.PathName), $"{planName}.txt");

            // write the manual checklist items to the .txt file
            System.IO.File.WriteAllLines(txtFilePath, new[]
            {
                $"{planName} – Q2 Revisions: Items to Complete Manually",
                string.Empty,
                "1. Review client redlines for SRO stem wall removal. Stem walls that contain electrical elements shall remain regardless of client redlines.",
            });


            // notify the user of results
            Utils.TaskDialogInformation("Q2 Revisions", "Q2 Revisions Complete",
                $"Q2 Revisions completed. Refer to {planName}.txt file for revisions to complete manually.");

            // launch the .txt file automatically after the user closes the dialog
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(txtFilePath)
            {
                UseShellExecute = true
            });

            return Result.Succeeded;
        }        

        #region Floor Plan Revisions Methods

        /// <summary>
        /// method to set active view to the First Floor Plan Annotation view, if it exists.
        /// </summary>
        private View GetFirstFloorAnnotationView(Document curDoc)
        {
            // find the level named "First Floor"
            Level firstFloor = new FilteredElementCollector(curDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals("First Floor", StringComparison.OrdinalIgnoreCase));

            // return null if the level is not found
            if (firstFloor == null) return null;

            // find and return a ViewPlan associated with First Floor whose name contains "Annotation"
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
            // load the new shelving family from the library
            Utils.LoadFamilyFromLibrary(curDoc, ShelvingFamilyPath, "LD_GM_Shelving");

            // find the "4 Shelves" type in the new family
            FamilySymbol newType = Utils.FindFamilySymbol(curDoc, "LD_GM_Shelving", "4 Shelves");

            // return 0 if the new type is not found
            if (newType == null) return 0;

            // activate the new type if it is not already active
            if (!newType.IsActive) newType.Activate();

            // collect all instances of the old "5 Shelves" type
            List<FamilyInstance> oldInstances = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName == "Shelving" && fi.Symbol.Name == "5 Shelves")
                .ToList();

            // loop through each instance and swap to the new type
            foreach (FamilyInstance inst in oldInstances)
            {
                // capture the existing depth parameters before swapping
                double depth1 = Utils.GetParamValueInFeet(inst, "Depth1");
                double depth2 = Utils.GetParamValueInFeet(inst, "Depth2");
                double depth3 = Utils.GetParamValueInFeet(inst, "Depth3");
                double depth4 = Utils.GetParamValueInFeet(inst, "Depth4");
                double depth5 = Utils.GetParamValueInFeet(inst, "Depth5");

                // determine if shallow uppers are needed based on depth comparison
                bool shallowUppers = depth4 < depth3 || depth5 < depth3;

                // swap the instance to the new 4-shelf type
                inst.ChangeTypeId(newType.Id);

                // restore the depth parameters on the new type
                Utils.SetParamValueInFeet(inst, "Depth1", depth1);
                Utils.SetParamValueInFeet(inst, "Depth2", depth2);
                Utils.SetParamValueInFeet(inst, "Depth3", depth3);
                Utils.SetParamValueInFeet(inst, "Depth4", depth4);

                // set shallow uppers flag if applicable
                if (shallowUppers)
                    Utils.SetParamInt(inst, "Shallow Uppers", 1);
            }

            // return the number of instances updated
            return oldInstances.Count;
        }

        /// <summary>
        /// method to update flooring in all rooms on the first floor to "HS" (Hard Surface)
        /// if they are not already "Concrete", "Conc", or "HS", and delete any floor break
        /// symbols where Floor 1 or Floor 2 = "C".
        /// </summary>
        private List<string> UpdateFloorMaterials(Document curDoc)
        {
            // create a list to track rooms where the floor finish was updated
            List<string> updatedRooms = new List<string>();

            // find the lowest level in the document (First Floor)
            Level firstFloor = new FilteredElementCollector(curDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();

            // return empty list if no level is found
            if (firstFloor == null) return updatedRooms;

            // collect all rooms on the first floor with a valid location
            foreach (SpatialElement room in new FilteredElementCollector(curDoc)
                .OfClass(typeof(SpatialElement))
                .Cast<SpatialElement>()
                .Where(r => r.Location != null && r.LevelId == firstFloor.Id))
            {
                // get the Floor Finish parameter
                Parameter floorFinish = room.LookupParameter("Floor Finish");

                // skip if the parameter is missing or read-only
                if (floorFinish == null || floorFinish.IsReadOnly) continue;

                // get the current floor finish value
                string current = floorFinish.AsString() ?? string.Empty;

                // skip rooms already set to Concrete, Conc, or HS
                if (current.Equals("Concrete", StringComparison.OrdinalIgnoreCase) ||
                    current.Equals("Conc", StringComparison.OrdinalIgnoreCase) ||
                    current.Equals("HS", StringComparison.OrdinalIgnoreCase))
                    continue;

                // set the floor finish to HS
                floorFinish.Set("HS");

                // add the room name to the updated list
                updatedRooms.Add(room.LookupParameter("Name")?.AsString() ?? $"Room {room.Id}");
            }

            // collect floor break symbols where Floor 1 or Floor 2 = "C"
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

            // delete each floor break symbol
            foreach (ElementId id in toDelete)
                curDoc.Delete(id);

            // return the list of updated room names
            return updatedRooms;
        }

        /// <summary>
        /// method to update Ceiliing Items family in the current document
        /// to the show current version of the "Disp Stairs" family type.
        /// returns number of instances updated
        /// </summary>
        private int UpdateClgItems(Document curDoc)
        {
            // load the new ceiling items family from the library
            Utils.LoadFamilyFromLibrary(curDoc, CeilingItemsPath, "LD_GM_Ceiling_Items");

            // find the base "Disp Stair" type to use as a template for duplicating sized types
            FamilySymbol baseType = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.FamilyName == "LD_GM_Ceiling_Items" &&
                                      fs.Name.StartsWith("Disp Stair", StringComparison.OrdinalIgnoreCase));

            // return 0 if the base type is not found
            if (baseType == null) return 0;

            // collect all instances of "Disp Stair" types in the "--Ceiling Items--" family
            List<FamilyInstance> oldInstances = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Family.Name == "--Ceiling Items--" &&
                             fi.Symbol.Name.StartsWith("Disp Stair", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // create a dictionary to cache sized types already created this session
            Dictionary<string, FamilySymbol> sizedTypes = new Dictionary<string, FamilySymbol>();

            // loop through each instance and swap to the new family type
            foreach (FamilyInstance inst in oldInstances)
            {
                // get the width and length from the old type
                double oldWidth = inst.Symbol.LookupParameter("Width")?.AsDouble() ?? 0;
                double oldLength = inst.Symbol.LookupParameter("Length")?.AsDouble() ?? 0;
                string typeComments = inst.Symbol.LookupParameter("Type Comments")?.AsString() ?? string.Empty;

                // get the base type dimensions for comparison
                double baseWidth = baseType.LookupParameter("Width")?.AsDouble() ?? 0;
                double baseLength = baseType.LookupParameter("Length")?.AsDouble() ?? 0;

                // check if the old type dimensions match the base type
                bool dimensionsMatch = Math.Abs(oldWidth - baseWidth) < 0.01 &&
                                       Math.Abs(oldLength - baseLength) < 0.01;

                FamilySymbol targetType;

                if (dimensionsMatch)
                {
                    // use the base type directly if dimensions match
                    targetType = baseType;
                }
                else
                {
                    // build a type name based on dimensions in inches
                    int wIn = (int)Math.Round(oldWidth * 12);
                    int lIn = (int)Math.Round(oldLength * 12);
                    string typeName = $"Disp Stair {wIn}\"x{lIn}\"";

                    if (!sizedTypes.TryGetValue(typeName, out targetType))
                    {
                        // check if a matching sized type already exists in the document
                        targetType = new FilteredElementCollector(curDoc)
                            .OfClass(typeof(FamilySymbol))
                            .Cast<FamilySymbol>()
                            .FirstOrDefault(fs => fs.FamilyName == "LD_GM_Ceiling_Items" && fs.Name == typeName);

                        if (targetType == null)
                        {
                            // duplicate the base type and set its dimensions
                            targetType = baseType.Duplicate(typeName) as FamilySymbol;
                            if (targetType == null) continue;

                            Parameter wp = targetType.LookupParameter("Width");
                            Parameter lp = targetType.LookupParameter("Length");
                            Parameter cp = targetType.LookupParameter("Type Comments");

                            // set width, length, and type comments on the new type
                            if (wp != null && !wp.IsReadOnly) wp.Set(oldWidth);
                            if (lp != null && !lp.IsReadOnly) lp.Set(oldLength);
                            if (cp != null && !cp.IsReadOnly && !string.IsNullOrEmpty(typeComments))
                                cp.Set(typeComments);
                        }

                        // cache the sized type for reuse
                        sizedTypes[typeName] = targetType;
                    }
                }

                // activate the target type if needed
                if (!targetType.IsActive) targetType.Activate();

                // set the Arrow parameter to Yes on the target type
                Parameter arrow = targetType.LookupParameter("Arrow");
                if (arrow != null && !arrow.IsReadOnly) arrow.Set(1);

                // swap the instance to the new type
                inst.ChangeTypeId(targetType.Id);
            }

            // return the number of instances updated
            return oldInstances.Count;
        }

        /// <summary>
        /// method to remove all SROs from the current document
        /// </summary>
        private int RemoveSROs(Document curDoc)
        {
            // collect all door instances with "SR" in their Type Comments
            List<FamilyInstance> sroList = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(d =>
                {
                    string comments = d.Symbol.LookupParameter("Type Comments")?.AsString() ?? string.Empty;
                    return comments.IndexOf("SR", StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            // loop through each SRO and remove it, healing the host wall
            foreach (FamilyInstance sro in sroList)
            {
                // get the host wall
                Wall hostWall = sro.Host as Wall;
                if (hostWall == null) continue;

                // get the center point of the SRO opening
                XYZ centerPt = (sro.Location as LocationPoint)?.Point;
                if (centerPt == null) continue;

                // get the width of the SRO
                double width = sro.Symbol.LookupParameter("Width")?.AsDouble() ?? 0;
                if (width <= 0) continue;

                // get the wall location curve and direction
                LocationCurve wallLocCurve = hostWall.Location as LocationCurve;
                Line wallLine = wallLocCurve?.Curve as Line;
                if (wallLine == null) continue;

                // capture the original wall start and end points
                XYZ wallStart = wallLine.GetEndPoint(0);
                XYZ wallEnd = wallLine.GetEndPoint(1);
                XYZ wallDir = wallLine.Direction;

                // calculate the left and right edges of the SRO opening
                XYZ leftEdge = centerPt - wallDir * (width / 2.0);
                XYZ rightEdge = centerPt + wallDir * (width / 2.0);

                // delete the SRO so the wall heals
                curDoc.Delete(sro.Id);

                // shorten the host wall to stop at the left edge of the opening
                wallLocCurve.Curve = Line.CreateBound(wallStart, leftEdge);

                // create a new wall from the right edge of the opening to the original wall end
                Wall.Create(curDoc,
                    Line.CreateBound(rightEdge, wallEnd),
                    hostWall.WallType.Id,
                    hostWall.LevelId,
                    hostWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(),
                    0, hostWall.Flipped, false);
            }

            // return the number of SROs removed
            return sroList.Count;
        }


        #endregion
    }
}
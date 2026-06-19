namespace Q2_Revisions
{
    [Transaction(TransactionMode.Manual)]
    public class cmdQ2Revs : IExternalCommand
    {
        private const string ShelvingFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Generic Model\Interior";
        private const string SwitchFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Lighting\Devices";
        private const string DoorFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Doors";
        private const string LightingFixturesPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Lighting\Fixtures";
        private const string CaseworkKitchenPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Casework\Kitchen";
        private const string CaseworkBathPath    = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Casework\Bath";
        private const string GenericModelBathPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Generic Model\Bath";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document cuDoc = uidoc.Document;

            // Item 7 pre-check: ask if this is a Terrata plan before running anything
            TaskDialog td = new TaskDialog("Q2 Revisions – Plan Type");
            td.MainInstruction = "Is this a Terrata plan?";
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Yes – Terrata plan (skip hard surface flooring)");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "No – Apply hard surface flooring to 1st floor");
            TaskDialogResult planTypeResult = td.Show();
            bool isTerrata = planTypeResult == TaskDialogResult.CommandLink1;

            // Item 1: Replace Shelving / 5 Shelves with LD_GM_Shelving / 4 Shelves
            using (Transaction t = new Transaction(cuDoc, "Update Shelving"))
            {
                t.Start();
                UpdateShelving(cuDoc);
                t.Commit();
            }

            // Item 2: Load new switch family
            using (Transaction tLoad = new Transaction(cuDoc, "Load Switch Family"))
            {
                tLoad.Start();
                Utils.LoadFamilyFromLibrary(cuDoc, SwitchFamilyPath, "LD_LD_Switch-Wall");
                tLoad.Commit();
            }

            FamilySymbol newSwitchType = Utils.FindFamilySymbol(cuDoc, "LD_LD_Switch-Wall", "Switch");
            if (newSwitchType == null)
                return Result.Failed;

            // Get all bath rooms sorted by level elevation, then room name
            List<Room> bathRooms = new FilteredElementCollector(cuDoc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .Where(r => r.Location != null && IsBathRoom(r.Name))
                .OrderBy(r => r.Level.Elevation)
                .ThenBy(r => r.Name)
                .ToList();

            // Item 4: Replace front doors (Exterior Entry, 36" wide) with new CH/CHP family
            using (Transaction t = new Transaction(cuDoc, "Update Front Doors"))
            {
                t.Start();
                UpdateFrontDoors(cuDoc);
                t.Commit();
            }

            // Item 5: Remove SROs and place reference planes at their edges
            using (Transaction t = new Transaction(cuDoc, "Remove SROs"))
            {
                t.Start();
                RemoveSROs(cuDoc);
                t.Commit();
            }

            // Item 6: Replace ceiling fan and add 6 LED puck lights in living/family room
            using (Transaction t = new Transaction(cuDoc, "Update Living Room Lights"))
            {
                t.Start();
                UpdateLivingRoomLights(cuDoc);
                t.Commit();
            }

            // Item 7: Hard surface flooring on 1st floor (skip for Terrata plans)
            if (!isTerrata)
            {
                using (Transaction t = new Transaction(cuDoc, "Update Floor Materials"))
                {
                    t.Start();
                    UpdateFloorMaterials(cuDoc);
                    t.Commit();
                }
            }

            // Items 8 & 16: Swap cabinet families and set cabinet/counter heights
            using (Transaction t = new Transaction(cuDoc, "Update Cabinets"))
            {
                t.Start();
                UpdateCabinets(cuDoc);
                t.Commit();
            }

            foreach (Room room in bathRooms)
            {
                // Switch to the floor plan for this room's level before prompting
                ViewPlan floorPlan = GetFloorPlanForLevel(cuDoc, room.Level);
                if (floorPlan != null)
                    uidoc.ActiveView = floorPlan;

                try
                {
                    Reference pickedRef = uidoc.Selection.PickObject(
                        Autodesk.Revit.UI.Selection.ObjectType.Element,
                        $"Pick switch at {room.Name} — press Escape to skip");

                    FamilyInstance selectedSwitch = cuDoc.GetElement(pickedRef) as FamilyInstance;
                    if (selectedSwitch == null)
                        continue;

                    using (Transaction t = new Transaction(cuDoc, $"Add Bath Switch – {room.Name}"))
                    {
                        t.Start();
                        DuplicateSwitchAndCleanWiring(cuDoc, selectedSwitch, newSwitchType);
                        t.Commit();
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User skipped this bath — continue to the next one
                    continue;
                }
            }

            return Result.Succeeded;
        }

        // Item 1 -------------------------------------------------------------------------

        private void UpdateShelving(Document curDoc)
        {
            Utils.LoadFamilyFromLibrary(curDoc, ShelvingFamilyPath, "LD_GM_Shelving");

            FamilySymbol newShelvingType = Utils.FindFamilySymbol(curDoc, "LD_GM_Shelving", "4 Shelves");
            if (newShelvingType == null)
                return;

            if (!newShelvingType.IsActive)
                newShelvingType.Activate();

            List<FamilyInstance> oldInstances = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName == "Shelving" && fi.Symbol.Name == "5 Shelves")
                .ToList();

            foreach (FamilyInstance oldInstance in oldInstances)
            {
                double depth1 = GetParamValueInFeet(oldInstance, "Depth1");
                double depth2 = GetParamValueInFeet(oldInstance, "Depth2");
                double depth3 = GetParamValueInFeet(oldInstance, "Depth3");
                double depth4 = GetParamValueInFeet(oldInstance, "Depth4");
                double depth5 = GetParamValueInFeet(oldInstance, "Depth5");

                bool shallowUppers = depth4 < depth3 || depth5 < depth3;

                oldInstance.ChangeTypeId(newShelvingType.Id);

                SetParamValueInFeet(oldInstance, "Depth1", depth1);
                SetParamValueInFeet(oldInstance, "Depth2", depth2);
                SetParamValueInFeet(oldInstance, "Depth3", depth3);
                SetParamValueInFeet(oldInstance, "Depth4", depth4);

                if (shallowUppers)
                    SetParamInt(oldInstance, "Shallow Uppers", 1);
            }
        }

        // Items 8 & 16 ------------------------------------------------------------------

        private void UpdateCabinets(Document curDoc)
        {
            // Load all new cabinet families from the library
            foreach (CabinetMapping mapping in CabinetMapping.AllMappings)
            {
                string libraryPath = mapping.LibrarySubfolder == "Kitchen"
                    ? CaseworkKitchenPath
                    : CaseworkBathPath;
                Utils.LoadFamilyFromLibrary(curDoc, libraryPath, mapping.NewFamilyName);
            }

            // Collect all casework instances that match a mapping entry
            List<FamilyInstance> cabinets = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Casework)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => CabinetMapping.AllMappings.Any(m => m.OldFamilyName == fi.Symbol.FamilyName))
                .ToList();

            foreach (FamilyInstance cabinet in cabinets)
            {
                CabinetMapping mapping = CabinetMapping.AllMappings
                    .FirstOrDefault(m => m.OldFamilyName == cabinet.Symbol.FamilyName);
                if (mapping == null) continue;

                // Match on the same type name in the new family
                FamilySymbol newType = Utils.FindFamilySymbol(curDoc, mapping.NewFamilyName, cabinet.Symbol.Name);
                if (newType == null) continue;
                if (!newType.IsActive) newType.Activate();

                cabinet.ChangeTypeId(newType.Id);

                // Item 16: set cabinet finish height to 2'-10½"
                SetParamValueInFeet(cabinet, "Cabinet Height", 34.5 / 12.0);
            }

            // Replace vanity countertops: --Vanity Counter-- / Type 1 → LD_GM_Counter_Vanity_Top-Mount / Round Lav
            string counterFamilyName = "LD_GM_Counter_Vanity_Top-Mount";
            Utils.LoadFamilyFromLibrary(curDoc, GenericModelBathPath, counterFamilyName);

            FamilySymbol newCounterType = Utils.FindFamilySymbol(curDoc, counterFamilyName, "Round Lav");
            if (newCounterType != null)
            {
                if (!newCounterType.IsActive) newCounterType.Activate();

                List<FamilyInstance> countertops = new FilteredElementCollector(curDoc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol.FamilyName == "--Vanity Counter--" && fi.Symbol.Name == "Type 1")
                    .ToList();

                foreach (FamilyInstance counter in countertops)
                {
                    counter.ChangeTypeId(newCounterType.Id);

                    // Item 16: set counter finish height to 3'-0"
                    SetParamValueInFeet(counter, "Counter Height", 3.0);
                }
            }
        }

        // Item 7 -------------------------------------------------------------------------

        private void UpdateFloorMaterials(Document curDoc)
        {
            // Identify the first floor as the level with the lowest elevation
            Level firstFloor = new FilteredElementCollector(curDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();

            if (firstFloor == null) return;

            // Update Floor Finish on all first-floor rooms that are not already Concrete or HS
            List<Room> firstFloorRooms = new FilteredElementCollector(curDoc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .Where(r => r.Location != null && r.LevelId == firstFloor.Id)
                .ToList();

            foreach (Room room in firstFloorRooms)
            {
                Parameter floorFinish = room.LookupParameter("Floor Finish");
                if (floorFinish == null || floorFinish.IsReadOnly) continue;

                string current = floorFinish.AsString() ?? string.Empty;
                if (current.Equals("Concrete", StringComparison.OrdinalIgnoreCase) ||
                    current.Equals("HS", StringComparison.OrdinalIgnoreCase))
                    continue;

                floorFinish.Set("HS");
            }

            // Delete Floor Material break symbols where Floor 1 or Floor 2 = "C"
            List<ElementId> toDelete = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_FurnitureSystems)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName == "Floor Material" && fi.Symbol.Name == "Type 1")
                .Where(fi =>
                {
                    string floor1 = fi.LookupParameter("Floor 1")?.AsString() ?? string.Empty;
                    string floor2 = fi.LookupParameter("Floor 2")?.AsString() ?? string.Empty;
                    return floor1.Equals("C", StringComparison.OrdinalIgnoreCase) ||
                           floor2.Equals("C", StringComparison.OrdinalIgnoreCase);
                })
                .Select(fi => fi.Id)
                .ToList();

            foreach (ElementId id in toDelete)
                curDoc.Delete(id);
        }

        // Item 6 -------------------------------------------------------------------------

        private void UpdateLivingRoomLights(Document curDoc)
        {
            Utils.LoadFamilyFromLibrary(curDoc, LightingFixturesPath, "LD_LF_None");

            FamilySymbol ceilingFanType = Utils.FindFamilySymbol(curDoc, "LD_LF_None", "Ceiling Fan");
            FamilySymbol ledType        = Utils.FindFamilySymbol(curDoc, "LD_LF_None", "LED");

            if (ceilingFanType == null || ledType == null) return;
            if (!ceilingFanType.IsActive) ceilingFanType.Activate();
            if (!ledType.IsActive) ledType.Activate();

            // 6 puck light offsets: 3' on each axis from the fan center
            XYZ[] offsets = new[]
            {
                new XYZ(-3,  3, 0), // up & left
                new XYZ( 0,  3, 0), // up
                new XYZ( 3,  3, 0), // up & right
                new XYZ( 3, -3, 0), // down & right
                new XYZ( 0, -3, 0), // down
                new XYZ(-3, -3, 0), // down & left
            };

            // Find all LT-No Base / Ceiling Fan instances in Living or Family rooms
            List<FamilyInstance> fans = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_LightingFixtures)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName == "LT-No Base" && fi.Symbol.Name == "Ceiling Fan")
                .Where(fi => IsLivingRoom(FindRoomContainingPoint(curDoc, (fi.Location as LocationPoint)?.Point)))
                .ToList();

            foreach (FamilyInstance fan in fans)
            {
                XYZ fanPt = (fan.Location as LocationPoint)?.Point;
                if (fanPt == null) continue;

                Level level = curDoc.GetElement(fan.LevelId) as Level;

                // Replace existing ceiling fan with new family type
                fan.ChangeTypeId(ceilingFanType.Id);

                // Place 6 LED puck lights around the fan
                foreach (XYZ offset in offsets)
                    curDoc.Create.NewFamilyInstance(fanPt + offset, ledType, level, StructuralType.NonStructural);
            }
        }

        private bool IsLivingRoom(Room room)
        {
            if (room == null) return false;
            return room.Name.IndexOf("Family",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                   room.Name.IndexOf("Living",  StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Item 5 -------------------------------------------------------------------------

        private void RemoveSROs(Document curDoc)
        {
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

            foreach (FamilyInstance sro in sroList)
            {
                Wall hostWall = sro.Host as Wall;
                if (hostWall == null) continue;

                XYZ centerPt = (sro.Location as LocationPoint)?.Point;
                if (centerPt == null) continue;

                double width = sro.Symbol.LookupParameter("Width")?.AsDouble() ?? 0;
                if (width <= 0) continue;

                LocationCurve wallLocCurve = hostWall.Location as LocationCurve;
                Line wallLine = wallLocCurve?.Curve as Line;
                if (wallLine == null) continue;

                XYZ wallDir = wallLine.Direction;
                XYZ leftEdge  = centerPt - wallDir * (width / 2.0);
                XYZ rightEdge = centerPt + wallDir * (width / 2.0);

                // Ensure left/right are ordered along the wall's positive direction
                double leftParam  = wallLine.Project(leftEdge).Parameter;
                double rightParam = wallLine.Project(rightEdge).Parameter;
                if (leftParam > rightParam)
                {
                    double tmp = leftParam; leftParam = rightParam; rightParam = tmp;
                }

                // Delete the SRO — wall fills back in
                curDoc.Delete(sro.Id);

                // Split at left edge: hostWall → left segment; newWall1 → leftEdge..end
                ElementId newWallId1 = wallLocCurve.Split(leftParam);
                if (newWallId1 == ElementId.InvalidElementId) continue;

                Wall newWall1 = curDoc.GetElement(newWallId1) as Wall;
                if (newWall1 == null) continue;

                // Split at right edge: newWall1 → stem segment; newWall2 → rightEdge..end
                LocationCurve newWall1Loc = newWall1.Location as LocationCurve;
                Line newWall1Line = newWall1Loc?.Curve as Line;
                if (newWall1Line == null) continue;

                double rightParamLocal = newWall1Line.Project(rightEdge).Parameter;
                ElementId newWallId2 = newWall1Loc.Split(rightParamLocal);
                if (newWallId2 == ElementId.InvalidElementId) continue;

                // newWall1 is now the middle wall segment (where the SRO was) — delete it
                curDoc.Delete(newWallId1);
            }
        }

        // Item 4 -------------------------------------------------------------------------

        private void UpdateFrontDoors(Document curDoc)
        {
            string newFamilyName = "LD_DR_Ext_Single 3_4 Lite_1 Panel";
            Utils.LoadFamilyFromLibrary(curDoc, DoorFamilyPath, newFamilyName);

            FamilySymbol type80 = Utils.FindFamilySymbol(curDoc, newFamilyName, "36\"x80\" DL");
            FamilySymbol type96 = Utils.FindFamilySymbol(curDoc, newFamilyName, "36\"x96\" DL");

            if (type80 != null && !type80.IsActive) type80.Activate();
            if (type96 != null && !type96.IsActive) type96.Activate();

            // Find all 36"-wide doors whose type Description contains "Exterior Entry"
            List<FamilyInstance> frontDoors = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(d =>
                {
                    string desc = d.Symbol.LookupParameter("Description")?.AsString() ?? string.Empty;
                    if (desc.IndexOf("Exterior Entry", StringComparison.OrdinalIgnoreCase) < 0)
                        return false;

                    double width = d.Symbol.LookupParameter("Width")?.AsDouble() ?? 0;
                    return Math.Abs(width - 3.0) < 0.01; // 36" = 3 ft
                })
                .ToList();

            foreach (FamilyInstance door in frontDoors)
            {
                double height = door.Symbol.LookupParameter("Height")?.AsDouble() ?? 0;

                // 80" = 6.6667 ft, 96" = 8 ft
                if (Math.Abs(height - (80.0 / 12.0)) < 0.01 && type80 != null)
                    door.ChangeTypeId(type80.Id);
                else if (Math.Abs(height - (96.0 / 12.0)) < 0.01 && type96 != null)
                    door.ChangeTypeId(type96.Id);
            }
        }

        // Item 2 -------------------------------------------------------------------------

        private bool IsBathRoom(string name)
        {
            return name.IndexOf("Bath", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Powder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Pwdr", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private ViewPlan GetFloorPlanForLevel(Document curDoc, Level level)
        {
            return new FilteredElementCollector(curDoc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan &&
                                     !v.IsTemplate &&
                                     v.GenLevel != null &&
                                     v.GenLevel.Id == level.Id);
        }

        private void DuplicateSwitchAndCleanWiring(Document curDoc, FamilyInstance existingSwitch, FamilySymbol newSwitchType)
        {
            Wall hostWall = existingSwitch.Host as Wall;
            if (hostWall == null)
                return;

            XYZ swPt = (existingSwitch.Location as LocationPoint)?.Point;
            if (swPt == null)
                return;

            Line wallLine = (hostWall.Location as LocationCurve)?.Curve as Line;
            if (wallLine == null)
                return;

            XYZ newPt = swPt + wallLine.Direction * (4.0 / 12.0);

            if (!newSwitchType.IsActive)
                newSwitchType.Activate();

            curDoc.Create.NewFamilyInstance(newPt, newSwitchType, hostWall, StructuralType.NonStructural);

            Room switchRoom = FindRoomContainingPoint(curDoc, swPt);
            if (switchRoom != null)
                DeleteWiringLinesInRoom(curDoc, switchRoom);
        }

        private Room FindRoomContainingPoint(Document curDoc, XYZ point)
        {
            return new FilteredElementCollector(curDoc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .FirstOrDefault(r => r.Location != null &&
                    r.IsPointInRoom(new XYZ(point.X, point.Y, r.Level.Elevation + 1.0)));
        }

        private void DeleteWiringLinesInRoom(Document curDoc, Room room)
        {
            GraphicsStyle wiringStyle = new FilteredElementCollector(curDoc)
                .OfClass(typeof(GraphicsStyle))
                .Cast<GraphicsStyle>()
                .FirstOrDefault(gs => gs.Name == "Wiring");

            if (wiringStyle == null)
                return;

            List<ElementId> toDelete = new FilteredElementCollector(curDoc)
                .OfClass(typeof(CurveElement))
                .OfType<DetailLine>()
                .Where(dl => dl.LineStyle.Id == wiringStyle.Id)
                .Where(dl =>
                {
                    XYZ mid = dl.GeometryCurve.Evaluate(0.5, true);
                    return room.IsPointInRoom(new XYZ(mid.X, mid.Y, room.Level.Elevation + 1.0));
                })
                .Select(dl => dl.Id)
                .ToList();

            foreach (ElementId id in toDelete)
                curDoc.Delete(id);
        }

        // Helpers ------------------------------------------------------------------------

        private double GetParamValueInFeet(Element elem, string paramName)
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p != null && p.StorageType == StorageType.Double)
                return p.AsDouble();
            return 0.0;
        }

        private void SetParamValueInFeet(Element elem, string paramName, double valueInFeet)
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                p.Set(valueInFeet);
        }

        private void SetParamInt(Element elem, string paramName, int value)
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Integer)
                p.Set(value);
        }
    }
}

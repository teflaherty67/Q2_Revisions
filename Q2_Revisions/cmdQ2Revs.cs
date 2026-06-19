namespace Q2_Revisions
{
    [Transaction(TransactionMode.Manual)]
    public class cmdQ2Revs : IExternalCommand
    {
        private const string ShelvingFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Generic Model\Interior";
        private const string SwitchFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Lighting\Devices";
        private const string DoorFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Doors";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document cuDoc = uidoc.Document;

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

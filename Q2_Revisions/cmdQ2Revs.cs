namespace Q2_Revisions
{
    [Transaction(TransactionMode.Manual)]
    public class cmdQ2Revs : IExternalCommand
    {
        private const string LibraryPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026";
        private const string ShelvingFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Generic Model\Interior";
        private const string SwitchFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Lighting\Devices";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Revit application and document variables
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document cuDoc = uidoc.Document;

            using (Transaction t = new Transaction(cuDoc, "Q2 Revisions"))
            {
                t.Start();

                // Item 1: Replace Shelving / 5 Shelves with LD_GM_Shelving / 4 Shelves
                UpdateShelving(cuDoc);

                // Item 2: Add second switch in all bath rooms
                UpdateBathSwitches(cuDoc);

                t.Commit();
            }

            return Result.Succeeded;
        }

        private void UpdateShelving(Document curDoc)
        {
            // Load new shelving family if not already in the document
            Utils.LoadFamilyFromLibrary(curDoc, ShelvingFamilyPath, "LD_GM_Shelving");

            FamilySymbol newShelvingType = Utils.FindFamilySymbol(curDoc, "LD_GM_Shelving", "4 Shelves");
            if (newShelvingType == null)
                return;

            if (!newShelvingType.IsActive)
                newShelvingType.Activate();

            // Collect all Shelving / 5 Shelves instances
            List<FamilyInstance> oldInstances = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName == "Shelving" && fi.Symbol.Name == "5 Shelves")
                .ToList();

            foreach (FamilyInstance oldInstance in oldInstances)
            {
                // Save depth values from old instance
                double depth1 = GetParamValueInFeet(oldInstance, "Depth1");
                double depth2 = GetParamValueInFeet(oldInstance, "Depth2");
                double depth3 = GetParamValueInFeet(oldInstance, "Depth3");
                double depth4 = GetParamValueInFeet(oldInstance, "Depth4");
                double depth5 = GetParamValueInFeet(oldInstance, "Depth5");

                bool shallowUppers = depth4 < depth3 || depth5 < depth3;

                // Swap to new family type
                oldInstance.ChangeTypeId(newShelvingType.Id);

                // Apply saved depth values to new instance
                SetParamValueInFeet(oldInstance, "Depth1", depth1);
                SetParamValueInFeet(oldInstance, "Depth2", depth2);
                SetParamValueInFeet(oldInstance, "Depth3", depth3);
                SetParamValueInFeet(oldInstance, "Depth4", depth4);

                if (shallowUppers)
                    SetParamInt(oldInstance, "Shallow Uppers", 1);
            }
        }

        private void UpdateBathSwitches(Document curDoc)
        {
            // Load new switch family
            Utils.LoadFamilyFromLibrary(curDoc, SwitchFamilyPath, "LD_LD_Switch-Wall");

            FamilySymbol newSwitchType = Utils.FindFamilySymbol(curDoc, "LD_LD_Switch-Wall", "Switch");
            if (newSwitchType == null)
                return;

            if (!newSwitchType.IsActive)
                newSwitchType.Activate();

            // Get all bath rooms
            List<Room> bathRooms = new FilteredElementCollector(curDoc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .Where(r => r.Location != null && r.Name.IndexOf("Bath", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // Get all doors
            List<FamilyInstance> allDoors = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            // Get all existing EL-Wall Base / Switch instances
            List<FamilyInstance> allSwitches = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_ElectricalFixtures)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName == "EL-Wall Base" && fi.Symbol.Name == "Switch")
                .ToList();

            // Get the Wiring line style for cleanup
            GraphicsStyle wiringStyle = new FilteredElementCollector(curDoc)
                .OfClass(typeof(GraphicsStyle))
                .Cast<GraphicsStyle>()
                .FirstOrDefault(gs => gs.Name == "Wiring");

            foreach (Room bathRoom in bathRooms)
            {
                // Find all doors that border this bath room
                List<FamilyInstance> roomDoors = allDoors
                    .Where(d => IsDoorOnRoom(d, bathRoom))
                    .ToList();

                if (roomDoors.Count == 0)
                    continue;

                // Find all switches on walls that border this bath room
                List<FamilyInstance> roomSwitches = allSwitches
                    .Where(s => IsSwitchInRoom(s, bathRoom))
                    .ToList();

                if (roomSwitches.Count == 0)
                    continue;

                // Find the switch-door pair with minimum distance — that is the entry switch
                FamilyInstance entrySwitch = null;
                FamilyInstance entryDoor = null;
                double minDist = double.MaxValue;

                foreach (FamilyInstance door in roomDoors)
                {
                    XYZ doorPt = (door.Location as LocationPoint)?.Point;
                    if (doorPt == null)
                        continue;

                    foreach (FamilyInstance sw in roomSwitches)
                    {
                        XYZ swPt = (sw.Location as LocationPoint)?.Point;
                        if (swPt == null)
                            continue;

                        double dist = doorPt.DistanceTo(swPt);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            entrySwitch = sw;
                            entryDoor = door;
                        }
                    }
                }

                if (entrySwitch == null || entryDoor == null)
                    continue;

                // Delete wiring detail lines in this bath room
                if (wiringStyle != null)
                    DeleteWiringLinesInRoom(curDoc, bathRoom, wiringStyle);

                // Place second switch 4" from existing switch, away from door
                PlaceSecondSwitch(curDoc, entrySwitch, entryDoor, newSwitchType);
            }
        }

        private bool IsDoorOnRoom(FamilyInstance door, Room room)
        {
            Room toRoom = door.ToRoom;
            Room fromRoom = door.FromRoom;
            return (toRoom != null && toRoom.Id == room.Id) ||
                   (fromRoom != null && fromRoom.Id == room.Id);
        }

        private bool IsSwitchInRoom(FamilyInstance sw, Room room)
        {
            LocationPoint lp = sw.Location as LocationPoint;
            if (lp == null)
                return false;

            // Test a point slightly above the floor at the switch's XY position
            XYZ testPt = new XYZ(lp.Point.X, lp.Point.Y, room.Level.Elevation + 1.0);
            return room.IsPointInRoom(testPt);
        }

        private void DeleteWiringLinesInRoom(Document curDoc, Room bathRoom, GraphicsStyle wiringStyle)
        {
            List<ElementId> toDelete = new FilteredElementCollector(curDoc)
                .OfClass(typeof(CurveElement))
                .OfType<DetailLine>()
                .Where(dl => dl.LineStyle.Id == wiringStyle.Id)
                .Where(dl =>
                {
                    XYZ mid = dl.GeometryCurve.Evaluate(0.5, true);
                    XYZ testPt = new XYZ(mid.X, mid.Y, bathRoom.Level.Elevation + 1.0);
                    return bathRoom.IsPointInRoom(testPt);
                })
                .Select(dl => dl.Id)
                .ToList();

            foreach (ElementId id in toDelete)
                curDoc.Delete(id);
        }

        private void PlaceSecondSwitch(Document curDoc, FamilyInstance existingSwitch, FamilyInstance entryDoor, FamilySymbol newSwitchType)
        {
            Wall hostWall = existingSwitch.Host as Wall;
            if (hostWall == null)
                return;

            XYZ swPt = (existingSwitch.Location as LocationPoint)?.Point;
            XYZ doorPt = (entryDoor.Location as LocationPoint)?.Point;
            if (swPt == null || doorPt == null)
                return;

            // Get the wall's direction vector
            Line wallLine = (hostWall.Location as LocationCurve)?.Curve as Line;
            if (wallLine == null)
                return;

            XYZ wallDir = wallLine.Direction;

            // Determine which direction along the wall is away from the door
            XYZ toDoor = doorPt - swPt;
            XYZ offsetDir = toDoor.DotProduct(wallDir) > 0 ? wallDir.Negate() : wallDir;

            // New switch location 4" along the wall from the existing switch
            XYZ newPt = swPt + offsetDir * (4.0 / 12.0);

            curDoc.Create.NewFamilyInstance(newPt, newSwitchType, hostWall, StructuralType.NonStructural);
        }

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

        private void UpdateShelving(Document curDoc)
        {
            // Load new shelving family if not already in the document
            Utils.LoadFamilyFromLibrary(curDoc, ShelvingFamilyPath, "LD_GM_Shelving");

            FamilySymbol newShelvingType = Utils.FindFamilySymbol(curDoc, "LD_GM_Shelving", "4 Shelves");
            if (newShelvingType == null)
                return;

            if (!newShelvingType.IsActive)
                newShelvingType.Activate();

            // Collect all Shelving / 5 Shelves instances
            List<FamilyInstance> oldInstances = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName == "Shelving" && fi.Symbol.Name == "5 Shelves")
                .ToList();

            foreach (FamilyInstance oldInstance in oldInstances)
            {
                // Save depth values from old instance
                double depth1 = GetParamValueInFeet(oldInstance, "Depth1");
                double depth2 = GetParamValueInFeet(oldInstance, "Depth2");
                double depth3 = GetParamValueInFeet(oldInstance, "Depth3");
                double depth4 = GetParamValueInFeet(oldInstance, "Depth4");
                double depth5 = GetParamValueInFeet(oldInstance, "Depth5");

                bool shallowUppers = depth4 < depth3 || depth5 < depth3;

                // Swap to new family type
                oldInstance.ChangeTypeId(newShelvingType.Id);

                // Apply saved depth values to new instance
                SetParamValueInFeet(oldInstance, "Depth1", depth1);
                SetParamValueInFeet(oldInstance, "Depth2", depth2);
                SetParamValueInFeet(oldInstance, "Depth3", depth3);
                SetParamValueInFeet(oldInstance, "Depth4", depth4);

                if (shallowUppers)
                    SetParamInt(oldInstance, "Shallow Uppers", 1);
            }
        }

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

namespace Q2_Revisions
{
    [Transaction(TransactionMode.Manual)]
    public class cmdQ2Revs : IExternalCommand
    {
        private const string LibraryPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026";
        private const string ShelvingFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Generic Model\Interior";

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

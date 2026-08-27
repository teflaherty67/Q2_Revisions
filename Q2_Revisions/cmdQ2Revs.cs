using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Q2_Revisions.Common;
using System.Windows.Media.TextFormatting;

namespace Q2_Revisions
{
    [Transaction(TransactionMode.Manual)]
    public class cmdQ2Revs : IExternalCommand
    {
        // set variables for file paths
        private const string ShelvingFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Generic Model\Interior";
        private const string CeilingItemsPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Generic Model\Interior";
        private const string DoorFamilyPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Doors";
        private const string VanityCabinetPath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Casework\Bath";
        private const string ViewsFilePath = @"S:\Shared Folders\Lifestyle USA Design\Library 2026\Template\Views.rvt";

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

            // create notification message
            string shelvingMsg = shelfCount == 0
                ? "No shelf stacks were found in the project."
                : $"{shelfCount} shelf {(shelfCount == 1 ? "stack was" : "stacks were")} updated to 4 shelves.";

            // notify the user of shelving update
            Utils.TaskDialogInformation("Q2 Revisions", "Update Shelving", shelvingMsg);

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

                // create notification message
                string flooringMsg = updatedRooms.Count == 0
                    ? "The flooring in all First Floor rooms is already HS."
                    : $"The flooring was changed in the following {updatedRooms.Count} {(updatedRooms.Count == 1 ? "room" : "rooms")}:\n" +
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

            // create notification message
            string clgItemsMsg = dispStairCount == 0
                ? "No Disp Stair types were found in the --Ceiling Items-- family."
                : $"The --Ceiling Items-- family was updated to the new LD_GM_Ceiling_Items family. " +
                  $"{dispStairCount} Disp Stair {(dispStairCount == 1 ? "type was" : "types were")} replaced with the new family type.";

            // notify the user of ceiling items update
            Utils.TaskDialogInformation("Q2 Revisions", "Update Ceiling Items", clgItemsMsg);


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

            // create notification message
            string sroMsg = countSRO == 0
                ? "No SROs were found in the project."
                : $"{countSRO} SRO{(countSRO == 1 ? " was" : "s were")} removed from the project.";

            // notify the user of SRO removal
            Utils.TaskDialogInformation("Q2 Revisions", "Remove SROs", sroMsg);

            #endregion

            #endregion

            #region Exterior Entry Revisions

            #region Revision 5:Front Door Revisions

            // check value of specLevel and only update front door if not Terrata
            if (specLevel != "Terrata")
            {
                // create a transaction
                using (Transaction t5 = new Transaction(curDoc, "Update Front Door"))
                {
                    // start the transaction
                    t5.Start();

                    // call the method to update the front door
                    UpdateFrontDoor(curDoc, specLevel);

                    // commit the transaction
                    t5.Commit();
                }               
            }

            #endregion

            #region Revision 6:Rear Door Revisions

            // check value of specLevel and only update rear door if not Terrata
            if (specLevel != "Terrata")
            {
                // create a transaction
                using (Transaction t6 = new Transaction(curDoc, "Update Rear Door"))
                {
                    // start the transaction
                    t6.Start();

                    // call the method to update the rear door
                    UpdateRearDoor(curDoc, specLevel);

                    // commit the transaction
                    t6.Commit();
                }
            }

            #endregion

            // notify the user of front and rear door updates
            if (specLevel != "Terrata")
            {
                // switch to the door schedule view so the user can see the changes
                View doorSchedule = Utils.GetScheduleByNameContains(curDoc, "Door Schedule");
                if (doorSchedule != null)
                    uidoc.ActiveView = doorSchedule;

                // create notification message
                string doorMsg = "The front and rear door families were updated, and types set per the selected spec level.";

                // notify the user of door updates
                Utils.TaskDialogInformation("Q2 Revisions", "Update Exterior Entry Doors", doorMsg);
            }


            #endregion

            #region Electrical Revisions

            #region Revision 7: Remove all TV & Phone Jacks

            // create variable for deleted outlet count
            int outletCount = 0;

            // set the active view to the First Floor Electrical view
            View electricalView = GetFirstFloorElectricalView(curDoc);
            if (electricalView != null)
                uidoc.ActiveView = electricalView;

            // create a transaction
            using (Transaction t7 = new Transaction(curDoc, "Remove PH/TV Outlets"))
            {
                // start the transaction
                t7.Start();

                // call the method to remove telephone and television outlets
                outletCount = RemovePHTVOutlets(curDoc);

                // commit the transaction
                t7.Commit();
            }

            // create notification message
            string outletMsg = outletCount == 0
                ? "No telephone or television jacks were found in the project."
                : outletCount == 1
                    ? "There was 1 telephone and/or television jack removed from the project."
                    : $"There were {outletCount} telephone and/or television jacks removed from the project.";

            // notify the user of outlet removal
            Utils.TaskDialogInformation("Q2 Revisions", "Remove PH & TV Outlets", outletMsg);

            #endregion

            #region Revision 8: Remove WH Tstat

            // create variable for WH-Tstat count
            int whTstatCount = 0;

            // create a transaction
            using (Transaction t8 = new Transaction(curDoc, "Remove WH-Tstat"))
            {
                // start the transaction
                t8.Start();

                // call the method to remove WH-Tstat instances
                whTstatCount = RemoveWHTstat(curDoc);

                // commit the transaction
                t8.Commit();
            }

            // create notification message
            string whTstatMsg = whTstatCount == 0
                ? "No WH-Tstat instances were found in the project."
                : $"{whTstatCount} WH-Tstat {(whTstatCount == 1 ? "instance was" : "instances were")} removed from the project.";

            // notify the user of WH-Tstat removal
            Utils.TaskDialogInformation("Q2 Revisions", "Remove WH-Tstat", whTstatMsg);

            #endregion

            #region Revision 9: Update Family/Living data drops to Dual Cat6

            // create a transaction
            using (Transaction t9 = new Transaction(curDoc, "Rename Dual Data Type"))
            {
                // start the transaction
                t9.Start();

                // call the method to rename the Outlet-Dual Cat5e-Cat6 type
                RenameDualDataType(curDoc);

                // commit the transaction
                t9.Commit();
            }

            // create notification message
            string dualDataMsg = "The EL-Wall Base type 'Outlet-Dual Cat5e-Cat6' was renamed to 'Outlet-Dual Cat6' and Type Comments set to 'Dual Cat6'.";

            // notify the user of the type rename
            Utils.TaskDialogInformation("Q2 Revisions", "Rename Dual Data Type", dualDataMsg);

            #endregion

            #region Revision 10: Changed WP LED fixtures at wet areas to standard LED

            // create variable for swapped fixture rooms
            List<string> wpLedRooms = new List<string>();

            // create a transaction
            using (Transaction t10 = new Transaction(curDoc, "Swap WP LED to LED in Bathrooms"))
            {
                // start the transaction
                t10.Start();

                // call the method to swap WP LED fixtures in bathrooms
                wpLedRooms = SwapWPLEDInBathrooms(curDoc);

                // commit the transaction
                t10.Commit();
            }

            // create notification message
            List<string> uniqueWpLedRooms = wpLedRooms.Distinct().ToList();
            string wpLedMsg = wpLedRooms.Count == 0
                ? "No WP LED fixtures were found in bathrooms."
                : $"{wpLedRooms.Count} WP LED {(wpLedRooms.Count == 1 ? "fixture was" : "fixtures were")} replaced with {(wpLedRooms.Count == 1 ? "a standard" : "standard")} LED {(wpLedRooms.Count == 1 ? "fixture" : "fixtures")} in the following {(uniqueWpLedRooms.Count == 1 ? "room" : "rooms")}:\n" +
                  string.Join("\n", uniqueWpLedRooms.Select(r => $"• {r}"));

            // notify the user of WP LED swap results
            Utils.TaskDialogInformation("Q2 Revisions", "Swap WP LED Fixtures", wpLedMsg);

            #endregion

            // create notification message
            string userInputMsg = "The next 3 revisions require user input. Please follow the prompts in the lower-left corner of the Revit window.";

            // notify the user that input is required
            Utils.TaskDialogInformation("Q2 Revisions", "User Input Required", userInputMsg);

            #region Revision 11: Add 6 LED fixtures at Family/Living

            // prompt the user to select the ceiling fan; click Finish without selecting to skip
            FamilyInstance ceilingFan = null;
            try
            {
                IList<Reference> fanRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new CeilingFanSelectionFilter(),
                    "Select the ceiling fan in the Family/Living Room, then click Finish. Click Finish without selecting to skip.");

                // use the first selected fan if any were picked
                if (fanRefs != null && fanRefs.Count > 0)
                    ceilingFan = curDoc.GetElement(fanRefs[0]) as FamilyInstance;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // user pressed Esc - skip this revision
            }

            // only place fixtures if the user selected a fan
            if (ceilingFan != null)
            {
                // create a transaction
                using (Transaction t11 = new Transaction(curDoc, "Add LED Fixtures"))
                {
                    // start the transaction
                    t11.Start();

                    // call the method to place 6 LED fixtures around the ceiling fan
                    AddLEDFixtures(curDoc, uidoc.ActiveView, ceilingFan);

                    // commit the transaction
                    t11.Commit();
                }

                // create notification message
                string ledMsg = "6 LED fixtures were added around the selected ceiling fan.";

                // notify the user that LED fixtures were placed
                Utils.TaskDialogInformation("Q2 Revisions", "Add LED Fixtures", ledMsg);
            }

            #endregion

            #region Revision 12: Separate switches for bath lights & exhaust fans

            // get all bath and powder rooms ordered by level elevation then by room name
            List<SpatialElement> bathRooms = new FilteredElementCollector(curDoc)
                .OfClass(typeof(SpatialElement))
                .Cast<SpatialElement>()
                .Where(r => r.Location != null)
                .Where(r =>
                {
                    string name = r.LookupParameter("Name")?.AsString() ?? string.Empty;
                    return name.IndexOf("Bath", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("Powder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("Pwdr", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("Utility", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("Laundry", StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .OrderBy(r => (curDoc.GetElement(r.LevelId) as Level)?.Elevation ?? 0)
                .ThenBy(r => r.LookupParameter("Name")?.AsString() ?? string.Empty)
                .ToList();

            // track total copied switches
            int switchCopyCount = 0;

            // process each bath and powder room individually
            foreach (SpatialElement bathRoom in bathRooms)
            {
                // get the room name and level
                string roomName = bathRoom.LookupParameter("Name")?.AsString() ?? "Bath/Powder Room";
                Level roomLevel = curDoc.GetElement(bathRoom.LevelId) as Level;

                // switch to the electrical view for this room's level
                View elecView = null;
                if (roomLevel != null)
                {
                    elecView = GetElectricalViewForLevel(curDoc, roomLevel.Name);
                    if (elecView != null)
                        uidoc.ActiveView = elecView;
                }

                // prompt the user to select the switch in this specific room
                IList<Reference> switchRefs = null;
                try
                {
                    switchRefs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        new SwitchSelectionFilter(),
                        $"Select switch to copy at {roomName}. Click Finish without selecting to skip.");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // user pressed Esc - skip this room
                    continue;
                }

                // skip if no switch was selected
                if (switchRefs == null || switchRefs.Count == 0) continue;

                // copy the selected switch in a transaction
                using (Transaction tSwitch = new Transaction(curDoc, $"Copy Switch – {roomName}"))
                {
                    // start the transaction
                    tSwitch.Start();

                    // copy each selected switch to the side opposite its nearest door
                    // and delete all room wiring except the wire attached to the original switch
                    foreach (Reference switchRef in switchRefs)
                    {
                        FamilyInstance switchInst = curDoc.GetElement(switchRef) as FamilyInstance;
                        if (switchInst == null) continue;

                        switchCopyCount += CopySwitchOppositeDoor(curDoc, switchInst);

                        if (elecView != null)
                            DeleteRoomWiringExceptSwitchWire(curDoc, elecView, bathRoom, switchInst);
                    }

                    // commit the transaction
                    tSwitch.Commit();
                }
            }

            // create notification message
            string switchMsg = $"{switchCopyCount} {(switchCopyCount == 1 ? "switch was" : "switches were")} duplicated to separate lights and exhaust fans at wet areas. Verify placement and update circuits as needed.";

            // notify the user of switch duplication results
            Utils.TaskDialogInformation("Q2 Revisions", "Separate Switches", switchMsg);


            #endregion

            #region Revision 13: Move data distribution panel to Utility Room

            // determine which level the distribution panel is on
            View distBoxElecView = GetDistributionBoxElectricalView(curDoc);

            // determine which level the Utility Room is on
            View utilityElecView = GetUtilityRoomElectricalView(curDoc);

            // switch to the electrical view for the level of the distribution panel
            if (distBoxElecView != null)
                uidoc.ActiveView = distBoxElecView;

            // prompt the user to select the distribution panel and all associated elements
            IList<Reference> panelRefs = null;
            try
            {
                panelRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    "Select the distribution panel and all associated elements, then click Finish.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }

            if (panelRefs != null && panelRefs.Count > 0)
            {
                // transaction 1: group the selected elements, always including the device itself
                FamilyInstance distBoxDevice = GetDistributionBoxDevice(curDoc);
                Group panelGroup = null;
                using (Transaction t13a = new Transaction(curDoc, "Group Distribution Panel"))
                {
                    t13a.Start();
                    List<ElementId> groupIds = panelRefs.Select(r => r.ElementId).ToList();
                    if (distBoxDevice != null && !groupIds.Contains(distBoxDevice.Id))
                        groupIds.Add(distBoxDevice.Id);
                    panelGroup = GroupDistributionPanel(curDoc, groupIds);
                    t13a.Commit();
                }

                // switch to the electrical view for the level of the Utility Room
                if (utilityElecView != null)
                    uidoc.ActiveView = utilityElecView;

                // prompt the user to pick the wall where the panel will go
                Wall targetWall = null;
                try
                {
                    Reference wallRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        new WallSelectionFilter(),
                        "Pick the wall in the Utility Room where the distribution panel will be placed.");
                    targetWall = curDoc.GetElement(wallRef) as Wall;
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }

                // prompt the user to pick the exact location on that wall
                XYZ insertPoint = null;
                if (targetWall != null)
                {
                    try
                    {
                        insertPoint = uidoc.Selection.PickPoint(
                            "Pick the location on the wall for the distribution panel.");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                }

                if (panelGroup != null && targetWall != null && insertPoint != null)
                {
                    // transaction 2: place a new instance of the group at the picked point and show the detail group
                    using (Transaction t13b = new Transaction(curDoc, "Place Distribution Panel"))
                    {
                        t13b.Start();
                        PlaceDistributionPanel(curDoc, uidoc, panelGroup, insertPoint, targetWall);
                        t13b.Commit();
                    }

                    // create notification message
                    string panelMsg = "The data distribution panel was placed in the Utility Room. Verify the position and ungroup if needed.";

                    // notify the user of panel placement
                    Utils.TaskDialogInformation("Q2 Revisions", "Move Distribution Panel", panelMsg);
                }
            }

            #endregion

            #region Revision 14: WH Outlet Note Revision

            // set the active view to the first floor electrical view
            View elecViewForNote = GetFirstFloorElectricalView(curDoc);
            if (elecViewForNote != null)
                uidoc.ActiveView = elecViewForNote;

            // create a transaction
            using (Transaction t14 = new Transaction(curDoc, "Add WH Outlet Note"))
            {
                // start the transaction
                t14.Start();

                // call the method to add the WH outlet note
                if (elecViewForNote != null)
                    AddWHOutletNote(curDoc, elecViewForNote);

                // commit the transaction
                t14.Commit();
            }

            // create notification message
            string whNoteMsg = "WH outlet note added to the electrical plan.";

            // notify the user of WH outlet note addition
            Utils.TaskDialogInformation("Q2 Revisions", "Add WH Outlet Note", whNoteMsg);

            #endregion

            #endregion

            #region Interior Elevation Revisions

            #region Revision 15: Raise Vanity Counter Height to 3'-0"

            // switch to the interior elevations sheet that contains the Master Bath
            ViewSheet interiorSheet = GetMasterBathInteriorSheet(curDoc);
            if (interiorSheet != null)
                uidoc.ActiveView = interiorSheet;

            // create a transaction
            using (Transaction t15 = new Transaction(curDoc, "Update Vanity Heights"))
            {
                // start the transaction
                t15.Start();

                // call the method to update vanity counter and cabinet heights
                UpdateVanityHeights(curDoc);

                // commit the transaction
                t15.Commit();
            }

            // create notification message
            string vanityMsg = "Vanity cabinets and counters were raised to 3'-0\" AFF.";

            // notify the user of vanity height update results
            Utils.TaskDialogInformation("Q2 Revisions", "Update Vanity Heights", vanityMsg);


            #endregion

            #region Revision 16: Check Master Bath Vanity Length

            // check the master bath vanity counter length and build conditional checklist line
            double mbCounterLength = GetMasterBathVanityCounterLength(curDoc);

            // if the vanity is 60" or longer, load the new vanity cabinet families
            if (mbCounterLength >= 5.0 - 0.001)
            {
                // create a transaction
                using (Transaction t16 = new Transaction(curDoc, "Load Vanity Cabinet Families"))
                {
                    // start the transaction
                    t16.Start();

                    // call the method to load all vanity cabinet families
                    LoadVanityCabinetFamilies(curDoc);

                    // commit the transaction
                    t16.Commit();
                }

                // create notification message
                string vanityCabMsg = "Vanity cabinet families loaded for Master Bath vanity revision.";

                // notify the user that vanity cabinet families were loaded
                Utils.TaskDialogInformation("Q2 Revisions", "Load Vanity Cabinet Families", vanityCabMsg);
            }

            #endregion

            #endregion

            #region Detail Items Revisions

            // pre-step: open Views.rvt in the background and copy the 3 detail legends
            int legendsCopied = CopyDetailLegends(curDoc, uiapp.Application);

            // create notification message
            string legendsMsg = legendsCopied == 0
                ? "No detail legends were copied. They may already exist in the project or Views.rvt could not be opened."
                : $"{legendsCopied} detail {(legendsCopied == 1 ? "legend was" : "legends were")} loaded into the project.";

            // notify the user of detail legends copy results
            Utils.TaskDialogInformation("Q2 Revisions", "Load Detail Legends", legendsMsg);

            #region Revision 17: Place Water Shut-Off Legend on Foundation Plan Sheets

            // create variable for shut-off legend placement count
            int shutOffPlaced = 0;

            // create a transaction
            using (Transaction t17 = new Transaction(curDoc, "Place Water Shut-Off Legend"))
            {
                // start the transaction
                t17.Start();

                // call the method to place the Water Shut-Off legend on all Foundation Plan sheets
                shutOffPlaced = PlaceWaterShutOffLegend(curDoc);

                // commit the transaction
                t17.Commit();
            }

            #endregion

            #region Revision 18: Replace Siding Eave Detail Legend on Exterior Elevation Sheets

            // create variable for siding eave detail legend replacement count
            int sidingReplaced = 0;

            // create a transaction
            using (Transaction t18 = new Transaction(curDoc, "Replace Siding Eave Detail Legend"))
            {
                // start the transaction
                t18.Start();

                // call the method to replace the siding eave detail legend on all Exterior Elevation sheets
                sidingReplaced = ReplaceEaveDetailLegend(curDoc, "siding", "Eave Detail @ Siding w/ Spray Foam");

                // commit the transaction
                t18.Commit();
            }

            #endregion

            #region Revision 19: Replace Brick Eave Detail Legend on Exterior Elevation Sheets (if present)

            // create variable for brick eave detail legend replacement count
            int brickReplaced = 0;

            // create a transaction
            using (Transaction t19 = new Transaction(curDoc, "Replace Brick Eave Detail Legend"))
            {
                // start the transaction
                t19.Start();

                // call the method to replace the brick eave detail legend on all Exterior Elevation sheets
                brickReplaced = ReplaceEaveDetailLegend(curDoc, "brick", "Eave Detail @ Brick w/ Spray Foam");

                // commit the transaction
                t19.Commit();
            }

            #endregion

            // create notification message
            string detailLegendsMsg = $"Water Shut-Off detail was added to {shutOffPlaced} {(shutOffPlaced == 1 ? "Foundation Plan sheet" : "Foundation Plan sheets")}, " +
                $"and {sidingReplaced + brickReplaced} {(sidingReplaced + brickReplaced == 1 ? "eave detail was" : "eave details were")} updated on the Exterior Elevation sheets.";

            // notify the user of detail legend updates
            Utils.TaskDialogInformation("Q2 Revisions", "Update Detail Legends", detailLegendsMsg);

            #endregion

            #region Manual Checklist Items

            // build the list of manual checklist items for the .txt file
            string txtFilePath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(curDoc.PathName), $"{planName} Q2 Revisions.txt");

            // build the checklist lines dynamically so conditional items can be included
            List<string> txtLines = new List<string>
            {
                $"{planName} – Q2 Revisions: Items to Complete Manually",
                string.Empty,
                "1. Review client redlines for all plan specific revisions.",
                "2. Review client redlines for SRO stem wall removal. Stem walls that contain electrical elements shall remain regardless of client redlines.",
                "3. Review & finalize location of new LED fixtures, if added, at Family ceiling fan. Add CL dimensions as required.",
                "4. Review location of LED fixtures at tubs & showers & move if required.",
                "5. Verify switch for coach lights is located in Garage.",
                "6. Verify switch for Covered Porch lights is located in Entry/Foyer.",
                "7. Rework wiring at powder and bathrooms.",
                "8. Remove all rain diverters located above A/C units.",
                "9. Verify location and justification of WH outlet note, and adjust leader to point to outlet.",
                "10. Revise siding trim at windows & doors to have 90-degree miters.",
                "11. Add crown molding to upper cabinets, and add call-out note.",
            };

            // add Master Bath cabinet revision note based on counter length (in feet; 60" = 5.0')
            // item number is derived from the current list count so new static lines don't require renumbering
            int nextItem = txtLines.Count - 1; // subtract 1 to account for the blank line
            if (mbCounterLength >= 5.0 - 0.001 && mbCounterLength <= 5.0 + 0.001)
                txtLines.Add($"{nextItem}. Revise Master Bath cabinets to: VSB24 | VDB12 (3-drawer) | VSB24.");
            else if (mbCounterLength > 5.0 + 0.001)
                txtLines.Add($"{nextItem}. Revise Master Bath cabinets to: VSB | min 12\" VDB (3-drawer) | VSB.");

            // write the manual checklist items to the .txt file
            System.IO.File.WriteAllLines(txtFilePath, txtLines);

            #endregion

            // create notification message
            string completionMsg = $"Q2 Revisions completed. Refer to {planName}.txt file for revisions to complete manually.";

            // notify the user that all revisions are complete
            Utils.TaskDialogInformation("Q2 Revisions", "Q2 Revisions Complete", completionMsg);


            // launch the .txt file automatically after the user closes the dialog
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(txtFilePath)
            {
                UseShellExecute = true
            });

            return Result.Succeeded;
        }       
        
        #region Floor Plan Revisions Methods

        /// <summary>
        /// method to set active view to the First Floor Plan annotation view, if it exists.
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
            // only load the new shelving family if it is not already in the project
            bool familyExists = new FilteredElementCollector(curDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Any(f => f.Name.Equals("LD_GM_Shelving", StringComparison.OrdinalIgnoreCase));

            if (!familyExists)
                Utils.LoadFamilyFromLibrary(curDoc, ShelvingFamilyPath, "LD_GM_Shelving");

            // find the "4 Shelves" type in the new family
            FamilySymbol newType = Utils.FindFamilySymbol(curDoc, "LD_GM_Shelving", "4 Shelves");

            // return 0 if the new type is not found
            if (newType == null) return 0;

            // activate the new type if it is not already active
            if (!newType.IsActive) newType.Activate();

            // collect all instances of the old shelf types in either family name
            List<FamilyInstance> oldInstances = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => (fi.Symbol.FamilyName == "Shelving" || fi.Symbol.FamilyName == "LD_GM_Shelving")
                          && (fi.Symbol.Name == "5 Shelves" || fi.Symbol.Name == "12\"/18\""))
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
        /// method to update --Ceiliing Items-- family in the current document
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

                // flatten center point to the wall's Z so edge points are coplanar with the wall curve
                XYZ centerFlat = new XYZ(centerPt.X, centerPt.Y, wallStart.Z);

                // calculate the left and right edges of the SRO opening
                XYZ leftEdge = centerFlat - wallDir * (width / 2.0);
                XYZ rightEdge = centerFlat + wallDir * (width / 2.0);

                // delete the SRO so the wall heals
                curDoc.Delete(sro.Id);

                // minimum segment length Revit will accept for wall creation (1")
                double minLength = 1.0 / 12.0;

                // shorten the host wall to stop at the left edge of the opening
                // only if the resulting segment would be long enough
                if (wallStart.DistanceTo(leftEdge) > minLength)
                    wallLocCurve.Curve = Line.CreateBound(wallStart, leftEdge);

                // create a new wall from the right edge of the opening to the original wall end
                // only if the resulting segment would be long enough
                if (rightEdge.DistanceTo(wallEnd) > minLength)
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

        #region Exterior Entry Revisions Methods

        /// <summary>
        /// method to update the front door to the new LD_DR_Ext_Single 3_4 Lite_1 Panel family.
        /// finds doors that are 36" wide with "Exterior Entry" in the Description parameter
        /// and swaps to the correct type based on spec level.
        /// </summary>
        private void UpdateFrontDoor(Document curDoc, string specLevel)
        {
            // load the new front door family from the library
            Utils.LoadFamilyFromLibrary(curDoc, DoorFamilyPath, "LD_DR_Ext_Single 3_4 Lite_1 Panel");

            // determine the correct type name based on spec level
            string typeName = specLevel == "Complete Home Plus" ? "36\"x96\" DL" : "36\"x80\" DL";

            // find the new door type in the loaded family
            FamilySymbol newType = Utils.FindFamilySymbol(curDoc, "LD_DR_Ext_Single 3_4 Lite_1 Panel", typeName);

            // return if the new type is not found
            if (newType == null) return;

            // activate the new type if it is not already active
            if (!newType.IsActive) newType.Activate();

            // collect all door instances that are 36" wide with "Exterior Entry" in Description
            List<FamilyInstance> frontDoors = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(d =>
                {
                    // check width is 36" (3.0 feet)
                    double width = d.Symbol.LookupParameter("Width")?.AsDouble() ?? 0;
                    if (Math.Abs(width - 3.0) > 0.01) return false;

                    // check Description contains "Exterior Entry"
                    string desc = d.Symbol.LookupParameter("Description")?.AsString() ?? string.Empty;
                    return desc.IndexOf("Exterior Entry", StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            // loop through each front door and swap to the new type
            foreach (FamilyInstance door in frontDoors)
            {
                // swap the door to the new type
                door.ChangeTypeId(newType.Id);
            }
        }

        /// <summary>
        /// method to update the rear door to the correct new family based on spec level.
        /// finds doors that are 32" wide with "Exterior Entry" in the Description parameter.
        /// CH: LD_DR_Ext_Single_Half Lite_2 Panel / 32"x80"
        /// CHP: LD_DR_Ext_Single_Full Lite / 32"x80" w/ Blinds
        /// </summary>
        private void UpdateRearDoor(Document curDoc, string specLevel)
        {
            // determine the correct family and type name based on spec level
            string familyName;
            string typeName;

            if (specLevel == "Complete Home Plus")
            {
                familyName = "LD_DR_Ext_Single_Full Lite";
                typeName = "32\"x80\" w/ Blinds";
            }
            else
            {
                familyName = "LD_DR_Ext_Single_Half Lite_2 Panel";
                typeName = "32\"x80\"";
            }

            // load the new rear door family from the library
            Utils.LoadFamilyFromLibrary(curDoc, DoorFamilyPath, familyName);

            // find the new door type in the loaded family
            FamilySymbol newType = Utils.FindFamilySymbol(curDoc, familyName, typeName);

            // return if the new type is not found
            if (newType == null) return;

            // activate the new type if it is not already active
            if (!newType.IsActive) newType.Activate();

            // collect all door instances that are 32" wide with "Exterior Entry" in Description
            List<FamilyInstance> rearDoors = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(d =>
                {
                    // check width is 32" (2.667 feet)
                    double width = d.Symbol.LookupParameter("Width")?.AsDouble() ?? 0;
                    if (Math.Abs(width - (32.0 / 12.0)) > 0.01) return false;

                    // check Description contains "Exterior Entry"
                    string desc = d.Symbol.LookupParameter("Description")?.AsString() ?? string.Empty;
                    return desc.IndexOf("Exterior Entry", StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            // loop through each rear door and swap to the new type
            foreach (FamilyInstance door in rearDoors)
            {
                // swap the door to the new type
                door.ChangeTypeId(newType.Id);
            }
        }

        #endregion

        #region Electrical Plan Revisions Methods

        /// <summary>
        /// method to find the First Floor Electrical view in the current document.
        /// searches for a ViewPlan associated with the First Floor level whose name contains "Electrical".
        /// </summary>
        private View GetFirstFloorElectricalView(Document curDoc)
        {
            // find the level named "First Floor"
            Level firstFloor = new FilteredElementCollector(curDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals("First Floor", StringComparison.OrdinalIgnoreCase));

            // return null if the level is not found
            if (firstFloor == null) return null;

            // find and return a ViewPlan associated with First Floor whose name contains "Electrical"
            return new FilteredElementCollector(curDoc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v => v.GenLevel?.Id == firstFloor.Id &&
                                     v.Name.IndexOf("Electrical", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// method to find the electrical ViewPlan for any named level.
        /// searches for a ViewPlan associated with the given level whose name contains "Electrical".
        /// </summary>
        private View GetElectricalViewForLevel(Document curDoc, string levelName)
        {
            // find the level by name
            Level level = new FilteredElementCollector(curDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));

            // return null if the level is not found
            if (level == null) return null;

            // find and return a ViewPlan associated with that level whose name contains "Electrical"
            return new FilteredElementCollector(curDoc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v => v.GenLevel?.Id == level.Id &&
                                     v.Name.IndexOf("Electrical", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// method to remove all telephone and television outlet instances from the current project.
        /// finds electrical fixture instances in the El-Wall Base or El-No Base families
        /// whose type name is Outlet-Television, Outlet-Telephone, or Outlet-Telephone/Television.
        /// </summary>
        private int RemovePHTVOutlets(Document curDoc)
        {
            // define the type names to search for
            List<string> targetTypeNames = new List<string>
            {
                "Outlet-Television",
                "Outlet-Telephone",
                "Outlet-Telephone/Television"
            };

            // collect all electrical fixture instances in the El-Wall Base or El-No Base families
            // whose type name matches one of the target type names
            List<ElementId> toDelete = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi =>
                {
                    // check if the family name contains "El-Wall Base" or "El-No Base"
                    string famName = fi.Symbol.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? string.Empty;
                    if (!famName.Contains("EL-Wall Base") && !famName.Contains("EL-No Base"))
                        return false;

                    // check if the type name matches one of the target type names
                    return targetTypeNames.Any(t => fi.Symbol.Name.Equals(t, StringComparison.OrdinalIgnoreCase));
                })
                .Select(fi => fi.Id)
                .ToList();

            // delete each outlet instance
            foreach (ElementId id in toDelete)
                curDoc.Delete(id);

            // return the number of outlets deleted
            return toDelete.Count;
        }

        /// <summary>
        /// method to remove all WH-Tstat instances from the project.
        /// finds electrical fixture instances in the El-Wall Base
        /// family whose type name is WH-Tstat. 
        /// </summary>
        private int RemoveWHTstat(Document curDoc)
        {
            // collect all EL-Wall Base / WH-Tstat instances
            List<ElementId> toDelete = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi =>
                {
                    // check if the family name is "EL-Wall Base" or "EL-No Base"
                    string famName = fi.Symbol.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? string.Empty;
                    if (!famName.Contains("EL-Wall Base") && !famName.Contains("EL-No Base")) return false;

                    // check if the type name is "WH-Tstat"
                    return fi.Symbol.Name.Equals("WH-Tstat", StringComparison.OrdinalIgnoreCase);
                })
                .Select(fi => fi.Id)
                .ToList();

            // delete each instance
            foreach (ElementId id in toDelete)
                curDoc.Delete(id);

            // return the number of instances deleted
            return toDelete.Count;
        }

        /// <summary>
        /// method to rename the EL-Wall Base FamilySymbol "Outlet-Dual Cat5e-Cat6" to "Outlet-Dual Cat6"
        /// and set its Type Comments parameter to "Dual Cat6".
        /// </summary>
        private void RenameDualDataType(Document curDoc)
        {
            // find the FamilySymbol for EL-Wall Base / Outlet-Dual Cat5e-Cat6
            FamilySymbol target = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs =>
                {
                    // check that the family name contains "EL-Wall Base" or "EL-No Base"
                    string famName = fs.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? string.Empty;
                    return (famName.Contains("EL-Wall Base") || famName.Contains("EL-No Base")) &&
                           fs.Name.Equals("Outlet-Dual Cat5e-Cat6", StringComparison.OrdinalIgnoreCase);
                });

            // return if the type is not found
            if (target == null) return;

            // rename the type
            target.Name = "Outlet-Dual Cat6";

            // set the Type Comments parameter to "Dual Cat6"
            Parameter typeComments = target.LookupParameter("Type Comments");
            if (typeComments != null && !typeComments.IsReadOnly)
                typeComments.Set("Dual Cat6");
        }

        /// <summary>
        /// method to find all LT-No Base / LED-WP fixtures located in rooms whose name contains "Bath",
        /// swap each to LT-No Base / LED, and delete the accompanying Lighting Fixture tag.
        /// Standard LED fixtures are not tagged, so the WP tag is removed without replacement.
        /// </summary>
        private List<string> SwapWPLEDInBathrooms(Document curDoc)
        {
            // find the standard LED type to swap to
            FamilySymbol ledSymbol = Utils.FindFamilySymbol(curDoc, "LT-No Base", "LED");

            // return if the standard LED type is not found
            if (ledSymbol == null) return new List<string>();

            // activate the symbol if it is not already active
            if (!ledSymbol.IsActive) ledSymbol.Activate();

            // collect all LT-No Base / LED-WP instances project-wide
            List<FamilyInstance> wpFixtures = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_LightingFixtures)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi =>
                {
                    // check family name and type name
                    string famName = fi.Symbol.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? string.Empty;
                    return famName.Contains("LT-No Base") &&
                           fi.Symbol.Name.Equals("LED-WP", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            // collect all lighting fixture tags upfront for efficient lookup
            List<IndependentTag> allTags = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_LightingFixtureTags)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            // track the rooms where fixtures were swapped
            List<string> swappedRooms = new List<string>();

            foreach (FamilyInstance fi in wpFixtures)
            {
                // get the fixture's location point
                XYZ loc = (fi.Location as LocationPoint)?.Point;
                if (loc == null) continue;

                // try 1' above the floor plane first, then 1' below as a fallback
                // (face-hosted ceiling fixtures are at Elevation from Level = 0', so the room
                // interior is 1' above that reference plane)
                Room room = curDoc.GetRoomAtPoint(new XYZ(loc.X, loc.Y, loc.Z + 1.0))
                         ?? curDoc.GetRoomAtPoint(new XYZ(loc.X, loc.Y, loc.Z - 1.0));

                // skip if no room was found or the room name does not contain "Bath"
                if (room == null) continue;
                string roomName = room.LookupParameter("Name")?.AsString() ?? string.Empty;
                if (roomName.IndexOf("Bath", StringComparison.OrdinalIgnoreCase) < 0) continue;

                // delete any Lighting Fixture tags associated with this fixture
                List<ElementId> tagIds = allTags
                    .Where(t => t.GetTaggedElementIds().Any(id => id.HostElementId == fi.Id))
                    .Select(t => t.Id)
                    .ToList();

                foreach (ElementId tagId in tagIds)
                    curDoc.Delete(tagId);

                // swap the fixture type to standard LED
                fi.ChangeTypeId(ledSymbol.Id);

                swappedRooms.Add(roomName);
            }

            // return the list of rooms where fixtures were swapped
            return swappedRooms;
        }


        /// <summary>
        /// method to place 6 LT-No Base / LED fixtures around the selected ceiling fan.
        /// 2 fixtures are placed directly in line with the fan (left and right along the view's
        /// RightDirection) at 3'-0" from center. 4 fixtures are placed diagonally at 3'-0" in
        /// both axes simultaneously (upper-left, upper-right, lower-left, lower-right).
        /// Uses the active view's RightDirection and UpDirection to handle rotated viewports.
        /// </summary>
        private void AddLEDFixtures(Document curDoc, View activeView, FamilyInstance ceilingFan)
        {
            // find the LT-No Base / LED family symbol
            FamilySymbol ledSymbol = Utils.FindFamilySymbol(curDoc, "LT-No Base", "LED");

            // return if the symbol is not found
            if (ledSymbol == null) return;

            // activate the symbol if it is not already active
            if (!ledSymbol.IsActive) ledSymbol.Activate();

            // get the fan's center point in model coordinates
            XYZ fanCenter = (ceilingFan.Location as LocationPoint)?.Point;

            // return if the fan location is not found
            if (fanCenter == null) return;

            // get the view's orientation vectors to handle rotated viewports
            XYZ right = activeView.RightDirection;
            XYZ up = activeView.UpDirection;

            // distance from fan center to each fixture in feet
            double d = 3.0;

            // calculate the 6 fixture positions relative to the fan center
            List<XYZ> positions = new List<XYZ>
            {
                fanCenter + d * right,                   // directly right (in line with fan)
                fanCenter - d * right,                   // directly left  (in line with fan)
                fanCenter + d * right + d * up,          // upper-right diagonal
                fanCenter - d * right + d * up,          // upper-left diagonal
                fanCenter + d * right - d * up,          // lower-right diagonal
                fanCenter - d * right - d * up,          // lower-left diagonal
            };

            // get the fan's host level for placement
            Level hostLevel = curDoc.GetElement(ceilingFan.LevelId) as Level;

            // read the fan's elevation offset from its host level so fixtures match exactly
            double fanOffset = ceilingFan.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)?.AsDouble() ?? 0.0;

            // place each LED fixture at the calculated position
            foreach (XYZ pos in positions)
            {
                // flatten to the level plane; elevation will be set explicitly after placement
                XYZ placePoint = new XYZ(pos.X, pos.Y, hostLevel.Elevation);

                // place the fixture on the level
                FamilyInstance ledInst = curDoc.Create.NewFamilyInstance(placePoint, ledSymbol, hostLevel, StructuralType.NonStructural);

                // set the elevation offset to match the fan so the fixture sits at the same height
                Parameter offsetParam = ledInst.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                if (offsetParam != null && !offsetParam.IsReadOnly)
                    offsetParam.Set(fanOffset);
            }
        }

        /// <summary>
        /// method to delete wiring attached to exisitng switch at wet areas.
        /// </summary>
        private void DeleteRoomWiringExceptSwitchWire(Document curDoc, View elecView, SpatialElement room, FamilyInstance switchInst)
        {
            Room revitRoom = room as Room;
            if (revitRoom == null) return;

            XYZ switchPt = (switchInst.Location as LocationPoint)?.Point;
            if (switchPt == null) return;

            // use the room's level elevation for IsPointInRoom checks
            Level roomLevel = curDoc.GetElement(room.LevelId) as Level;
            double levelElev = roomLevel?.Elevation ?? 0;

            // find all detail lines with a "Wiring" line style in the electrical view
            List<CurveElement> roomWiring = new FilteredElementCollector(curDoc, elecView.Id)
                .OfClass(typeof(CurveElement))
                .Cast<CurveElement>()
                .Where(ce =>
                {
                    string lsName = ce.LineStyle?.Name ?? string.Empty;
                    if (lsName.IndexOf("Wiring", StringComparison.OrdinalIgnoreCase) < 0) return false;

                    // check if either endpoint falls inside the room
                    Curve curve = ce.GeometryCurve;
                    if (curve == null) return false;
                    XYZ p0 = new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, levelElev + 1);
                    XYZ p1 = new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, levelElev + 1);
                    return revitRoom.IsPointInRoom(p0) || revitRoom.IsPointInRoom(p1);
                })
                .ToList();

            // keep the single wire whose nearest endpoint is closest to the switch insertion point;
            // this works regardless of switch orientation or exact gap distance
            if (roomWiring.Count == 0) return;

            CurveElement switchWire = roomWiring
                .OrderBy(ce =>
                {
                    Curve curve = ce.GeometryCurve;
                    return Math.Min(
                        curve.GetEndPoint(0).DistanceTo(switchPt),
                        curve.GetEndPoint(1).DistanceTo(switchPt));
                })
                .First();

            // delete all other wiring in the room
            foreach (CurveElement ce in roomWiring)
            {
                if (ce.Id != switchWire.Id)
                    curDoc.Delete(ce.Id);
            }
        }

        /// <summary>
        /// method to copy a wall-hosted light switch 4" away on the side of the switch
        /// that is opposite the nearest door. uses the wall direction to determine which
        /// side the door is on relative to the switch, then offsets in the opposing direction.
        /// </summary>
        private int CopySwitchOppositeDoor(Document curDoc, FamilyInstance switchInst)
        {
            // get the host wall; skip if not wall-hosted
            Wall hostWall = switchInst.Host as Wall;
            if (hostWall == null) return 0;

            // get the switch's location point in model coordinates
            XYZ switchPt = (switchInst.Location as LocationPoint)?.Point;
            if (switchPt == null) return 0;

            // get the wall's direction vector
            Line wallLine = (hostWall.Location as LocationCurve)?.Curve as Line;
            if (wallLine == null) return 0;
            XYZ wallDir = wallLine.Direction;

            // find the door nearest to the switch within 10 feet
            FamilyInstance nearestDoor = new FilteredElementCollector(curDoc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(d =>
                {
                    XYZ doorPt = (d.Location as LocationPoint)?.Point;
                    return doorPt != null && switchPt.DistanceTo(doorPt) <= 10.0;
                })
                .OrderBy(d => switchPt.DistanceTo((d.Location as LocationPoint).Point))
                .FirstOrDefault();

            // determine which direction along the wall is OPPOSITE the door
            XYZ copyDir = wallDir;

            if (nearestDoor != null)
            {
                XYZ doorPt = (nearestDoor.Location as LocationPoint)?.Point;
                if (doorPt != null)
                {
                    // project the vector from switch to door onto the wall direction
                    double dot = (doorPt - switchPt).DotProduct(wallDir);

                    // if dot > 0, door is in the positive wall direction — copy goes negative
                    // if dot < 0, door is in the negative wall direction — copy goes positive
                    copyDir = dot > 0 ? wallDir.Negate() : wallDir;
                }
            }

            // offset 4 inches (4/12 feet) along the wall in the opposite direction from the door
            XYZ newPt = switchPt + copyDir.Multiply(4.0 / 12.0);

            // create the new switch instance on the same host wall at the offset position
            FamilyInstance newSwitch = curDoc.Create.NewFamilyInstance(
                newPt, switchInst.Symbol, hostWall, switchInst.LookupParameter("Level") != null
                    ? curDoc.GetElement(switchInst.LevelId) as Level
                    : null,
                StructuralType.NonStructural);

            return newSwitch != null ? 1 : 0;
        }

        /// <summary>
        /// returns the electrical view for the level where the Leviton 49605-14P distribution box is located.
        /// returns null if the box or its electrical view cannot be found.
        /// </summary>
        private View GetDistributionBoxElectricalView(Document curDoc)
        {
            FamilyInstance distBox = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .FirstOrDefault(fi =>
                    fi.Symbol.FamilyName.Equals("Leviton 49605-14P", StringComparison.OrdinalIgnoreCase) ||
                    fi.Symbol.FamilyName.Equals("Medicine Cabinet-Framed", StringComparison.OrdinalIgnoreCase));

            if (distBox == null) return null;

            Level boxLevel = curDoc.GetElement(distBox.LevelId) as Level;
            return boxLevel == null ? null : GetElectricalViewForLevel(curDoc, boxLevel.Name);
        }

        /// <summary>
        /// returns the electrical view for the level that contains a room named "Utility" or "Laundry".
        /// returns null if no such room or electrical view is found.
        /// </summary>
        private View GetUtilityRoomElectricalView(Document curDoc)
        {
            SpatialElement utilityRoom = new FilteredElementCollector(curDoc)
                .OfClass(typeof(SpatialElement))
                .Cast<SpatialElement>()
                .FirstOrDefault(r =>
                {
                    string name = r.LookupParameter("Name")?.AsString() ?? string.Empty;
                    return name.IndexOf("Utility", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Laundry", StringComparison.OrdinalIgnoreCase) >= 0;
                });

            if (utilityRoom == null) return null;

            Level utilityLevel = curDoc.GetElement(utilityRoom.LevelId) as Level;
            return utilityLevel == null ? null : GetElectricalViewForLevel(curDoc, utilityLevel.Name);
        }

        /// <summary>
        /// creates a named group from the selected element ids and returns it.
        /// </summary>
        private Group GroupDistributionPanel(Document curDoc, List<ElementId> elementIds)
        {
            Group panelGroup = curDoc.Create.NewGroup(elementIds);
            if (panelGroup != null)
                panelGroup.GroupType.Name = "Data Distribution Panel";
            return panelGroup;
        }

        /// <summary>
        /// places a new instance of the distribution panel group at the picked insertion point,
        /// rotates it to face the target wall, and shows the attached detail group in the active view.
        /// </summary>
        private void PlaceDistributionPanel(Document curDoc, UIDocument uidoc, Group panelGroup, XYZ insertPoint, Wall targetWall)
        {
            // get the original facing direction from the family instance in the group
            XYZ originalFacing = GetGroupFacingDirection(curDoc, panelGroup);

            // get the inward normal of the target wall (facing into the room from the picked side)
            XYZ targetNormal = GetWallInwardNormal(targetWall, insertPoint);

            // place a new instance of the group type at the picked point
            Group newInstance = curDoc.Create.PlaceGroup(insertPoint, panelGroup.GroupType);

            // rotate the new instance so it faces the target wall
            double angle = originalFacing.AngleOnPlaneTo(targetNormal, XYZ.BasisZ);
            if (Math.Abs(angle) > 0.001)
                ElementTransformUtils.RotateElement(curDoc, newInstance.Id,
                    Line.CreateBound(insertPoint, insertPoint + XYZ.BasisZ), angle);

            // show the attached detail group on the new instance in the active view
            newInstance.ShowAllAttachedDetailGroups(uidoc.ActiveView);

            // delete the original group instance left at the old location
            curDoc.Delete(panelGroup.Id);
        }

        /// <summary>
        /// returns the Leviton 49605-14P distribution box FamilyInstance, or null if not found.
        /// </summary>
        private FamilyInstance GetDistributionBoxDevice(Document curDoc)
        {
            return new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .FirstOrDefault(fi =>
                    fi.Symbol.FamilyName.Equals("Leviton 49605-14P", StringComparison.OrdinalIgnoreCase) ||
                    fi.Symbol.FamilyName.Equals("Medicine Cabinet-Framed", StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// returns the facing orientation of the first family instance found in the group.
        /// falls back to XYZ.BasisX if none is found.
        /// </summary>
        private XYZ GetGroupFacingDirection(Document curDoc, Group group)
        {
            foreach (ElementId id in group.GetMemberIds())
            {
                FamilyInstance fi = curDoc.GetElement(id) as FamilyInstance;
                if (fi != null && fi.FacingOrientation.GetLength() > 0.001)
                    return fi.FacingOrientation;
            }
            return XYZ.BasisX;
        }

        /// <summary>
        /// returns the wall's inward normal on the side of nearPoint.
        /// </summary>
        private XYZ GetWallInwardNormal(Wall wall, XYZ nearPoint)
        {
            Line wallLine = (wall.Location as LocationCurve)?.Curve as Line;
            if (wallLine == null) return XYZ.BasisX;

            XYZ wallDir = wallLine.Direction.Normalize();
            XYZ normal1 = new XYZ(-wallDir.Y, wallDir.X, 0);
            XYZ wallMid = wallLine.Evaluate(0.5, true);

            // pick whichever normal points toward nearPoint
            return (nearPoint - wallMid).DotProduct(normal1) >= 0 ? normal1 : normal1.Negate();
        }

        /// <summary>
        /// finds water heater instances (Specialty Equipment or Mechanical Equipment) whose type
        /// comments contain "WH" and places a TextNote "110V DED for tankless WH @ 48" FFF"
        /// 3' to the right of each instance in the given electrical view.
        /// returns the number of notes added.
        /// </summary>
        private void AddWHOutletNote(Document curDoc, View electricalView)
        {
            // find the STANDARD text note type
            TextNoteType noteType = new FilteredElementCollector(curDoc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault(t => t.Name.Equals("STANDARD", StringComparison.OrdinalIgnoreCase));

            if (noteType == null) return;

            // find the WH instance in Specialty Equipment or Mechanical Equipment
            // where Type Comments contains "WH"
            FamilyInstance whInst = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .WherePasses(new LogicalOrFilter(
                    new ElementCategoryFilter(BuiltInCategory.OST_SpecialityEquipment),
                    new ElementCategoryFilter(BuiltInCategory.OST_MechanicalEquipment)))
                .Cast<FamilyInstance>()
                .FirstOrDefault(fi =>
                {
                    string typeComments = fi.Symbol.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)?.AsString() ?? string.Empty;
                    return typeComments.IndexOf("WH", StringComparison.OrdinalIgnoreCase) >= 0;
                });

            if (whInst == null || !(whInst.Location is LocationPoint lp)) return;

            // set text note options: left horizontal alignment
            TextNoteOptions options = new TextNoteOptions(noteType.Id)
            {
                HorizontalAlignment = HorizontalTextAlignment.Left
            };

            // offset 3' to the right in the view's right direction
            XYZ noteOrigin = lp.Point + electricalView.RightDirection * 3.0;

            TextNote note = TextNote.Create(curDoc, electricalView.Id, noteOrigin,
                "110V DED for tankless WH @ 48\" AFF", options);

            // add a leader from the left side of the note pointing to the WH location
            Leader leader = note.AddLeader(TextNoteLeaderTypes.TNLT_STRAIGHT_L);
            leader.End = lp.Point;
        }


        #endregion

        #region Interior Elevation Revisions Methods

        /// <summary>
        /// method to find the sheet containing interior elevations for the Master Bath.
        /// searches all sheets for a viewport whose view name contains "Master Bath".
        /// falls back to any sheet whose name contains "Interior" if no Master Bath view is found.
        /// </summary>
        private ViewSheet GetMasterBathInteriorSheet(Document curDoc)
        {
            // collect all views whose name contains "Bath", preferring "Master Bath"
            List<View> bathViews = new FilteredElementCollector(curDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.Name.IndexOf("Bath", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(v => v.Name.IndexOf("Master Bath", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            foreach (View view in bathViews)
            {
                string sheetNumber = view.LookupParameter("Sheet Number")?.AsString();
                if (string.IsNullOrEmpty(sheetNumber)) continue;

                ViewSheet sheet = Utils.GetSheetsByNumber(curDoc, sheetNumber).FirstOrDefault();
                if (sheet != null)
                    return sheet;
            }

            return null;
        }

        /// <summary>
        /// method to update the Counter Height on all --Vanity Counter-- instances to 3'-0"
        /// and the Cabinet Height on all families containing "Vanity Cabinet" to 2'-10½".
        /// </summary>
        private void UpdateVanityHeights(Document curDoc)
        {
            // collect all family instances project-wide
            List<FamilyInstance> allInstances = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            foreach (FamilyInstance fi in allInstances)
            {
                // get the family name using the reliable built-in parameter
                string famName = fi.Symbol.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? string.Empty;

                // check for --Vanity Counter-- family and update Counter Height to 3'-0"
                if (famName.Contains("--Vanity Counter--"))
                {
                    Parameter counterHeight = fi.LookupParameter("Counter Height");
                    if (counterHeight != null && !counterHeight.IsReadOnly)
                    {
                        // calculate how much the counter is being raised and apply the same delta to Mirror Height
                        double delta = 3.0 - counterHeight.AsDouble();
                        counterHeight.Set(3.0);

                        Parameter mirrorHeight = fi.LookupParameter("Mirror Height");
                        if (mirrorHeight != null && !mirrorHeight.IsReadOnly)
                            mirrorHeight.Set(mirrorHeight.AsDouble() + delta);
                    }
                }

                // check for any family containing "Vanity Cabinet" and update Cabinet Height to 2'-10½"
                else if (famName.IndexOf("Vanity Cabinet", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Parameter cabinetHeight = fi.LookupParameter("Cabinet Height");
                    if (cabinetHeight != null && !cabinetHeight.IsReadOnly)
                        cabinetHeight.Set(2.0 + 10.5 / 12.0);
                }
            }
        }

        /// <summary>
        /// finds the --Vanity Counter-- instance in the Master Bath room and returns its length in feet.
        /// returns -1 if no counter is found.
        /// </summary>
        private double GetMasterBathVanityCounterLength(Document curDoc)
        {
            FamilyInstance counter = new FilteredElementCollector(curDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => (fi.Symbol.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? string.Empty)
                    .Contains("--Vanity Counter--"))
                .FirstOrDefault(fi =>
                {
                    // try LocationPoint first, then midpoint of LocationCurve for line-based families
                    XYZ loc = (fi.Location as LocationPoint)?.Point;
                    if (loc == null)
                    {
                        Curve curve = (fi.Location as LocationCurve)?.Curve;
                        loc = curve?.Evaluate(0.5, true);
                    }
                    if (loc == null) return false;

                    Room room = curDoc.GetRoomAtPoint(new XYZ(loc.X, loc.Y, loc.Z + 1.0))
                             ?? curDoc.GetRoomAtPoint(new XYZ(loc.X, loc.Y, loc.Z - 1.0));

                    return (room?.LookupParameter("Name")?.AsString() ?? string.Empty)
                        .IndexOf("Master Bath", StringComparison.OrdinalIgnoreCase) >= 0;
                });

            return counter?.LookupParameter("Length")?.AsDouble() ?? -1.0;
        }

        /// <summary>
        /// loads all vanity cabinet families from the Bath casework library folder into the project.
        /// </summary>
        private void LoadVanityCabinetFamilies(Document curDoc)
        {
            List<string> familyNames = new List<string>
            {
                "LD_CW_Vanity_2-Dr_1-Drwr_Flush",
                "LD_CW_Vanity_2-Dr_2-Drwr_Flush",
                "LD_CW_Vanity_3-Drwr_Flush",
                "LD_CW_Vanity_3-Drwr_Recess",
                "LD_CW_Vanity_4-Drwr_Recess",
                "LD_CW_Vanity_Sink_1-Dr_Recess",
                "LD_CW_Vanity_Sink_2-Dr_Flush",
                "LD_CW_Vanity_Sink_2-Dr_Recess",
                "LD_CW_Vanity_Filler",
                "LD_CW_Vanity_Filler_Sizes",
            };

            foreach (string familyName in familyNames)
                Utils.LoadFamilyFromLibrary(curDoc, VanityCabinetPath, familyName);
        }


        #endregion

        #region Detail Items Revisions Methods

        /// <summary>
        /// opens Views.rvt in the background, copies the 3 detail legends into curDoc
        /// (skipping any that already exist), then closes the source document.
        /// returns the number of legends copied.
        /// </summary>
        private int CopyDetailLegends(Document curDoc, Autodesk.Revit.ApplicationServices.Application app)
        {
            // names of the legends to copy
            List<string> legendNames = new List<string>
            {
                "Water Shut-Off",
                "Eave Detail @ Brick w/ Spray Foam",
                "Eave Detail @ Siding w/ Spray Foam",
            };

            // collect names already present in the current document so we can skip duplicates
            HashSet<string> existingNames = new HashSet<string>(
                new FilteredElementCollector(curDoc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Select(v => v.Name),
                StringComparer.OrdinalIgnoreCase);

            // open Views.rvt in the background
            Document sourceDoc = null;
            try
            {
                OpenOptions openOptions = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DoNotDetach };
                ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(ViewsFilePath);
                sourceDoc = app.OpenDocumentFile(modelPath, openOptions);
            }
            catch { return 0; }

            if (sourceDoc == null) return 0;

            int count = 0;
            try
            {
                // find the requested legends in the source document, skipping ones already in curDoc
                List<ElementId> toCopy = new FilteredElementCollector(sourceDoc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.ViewType == ViewType.Legend
                             && legendNames.Any(n => n.Equals(v.Name, StringComparison.OrdinalIgnoreCase))
                             && !existingNames.Contains(v.Name))
                    .Select(v => v.Id)
                    .ToList();

                if (toCopy.Count > 0)
                {
                    using (Transaction t = new Transaction(curDoc, "Copy Detail Legends"))
                    {
                        t.Start();
                        ElementTransformUtils.CopyElements(sourceDoc, toCopy, curDoc, Transform.Identity, new CopyPasteOptions());
                        t.Commit();
                    }
                    count = toCopy.Count;
                }
            }
            finally
            {
                sourceDoc.Close(false);
            }

            return count;
        }

        /// <summary>
        /// finds the Water Shut-Off legend in curDoc and places it on every sheet
        /// whose name contains "Foundation Plan". returns the number of sheets it was placed on.
        /// </summary>
        private int PlaceWaterShutOffLegend(Document curDoc)
        {
            // find the Water Shut-Off legend
            View legend = new FilteredElementCollector(curDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => v.ViewType == ViewType.Legend
                                  && v.Name.Equals("Water Shut-Off", StringComparison.OrdinalIgnoreCase));

            if (legend == null) return 0;

            // find the "No Title" viewport type
            ElementId noTitleTypeId = GetNoTitleViewportTypeId(curDoc);

            // find all sheets whose name contains "Foundation Plan"
            List<ViewSheet> foundationSheets = Utils.GetAllSheets(curDoc)
                .Where(s => s.Name.IndexOf("Form", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (foundationSheets.Count == 0) return 0;

            int count = 0;
            foreach (ViewSheet sheet in foundationSheets)
            {
                // skip if the legend is already on this sheet
                bool alreadyPlaced = sheet.GetAllViewports()
                    .Select(id => curDoc.GetElement(id) as Viewport)
                    .Any(vp => vp != null && vp.ViewId == legend.Id);

                if (alreadyPlaced) continue;

                // place at a default position — user should verify and move as needed
                Viewport vp = Viewport.Create(curDoc, sheet.Id, legend.Id, new XYZ(0.5, 0.5, 0));

                // set viewport type to No Title
                if (noTitleTypeId != null && vp != null)
                    vp.ChangeTypeId(noTitleTypeId);

                count++;
            }

            return count;
        }

        /// <summary>
        /// finds Exterior Elevation sheets that have an existing eave detail legend whose name
        /// starts with "Eave Detail" and contains the given keyword ("siding" or "brick"),
        /// removes the old viewport, and places the new legend at the same center position.
        /// returns the number of sheets where a replacement was made.
        /// </summary>
        private int ReplaceEaveDetailLegend(Document curDoc, string keyword, string newLegendName)
        {
            // find the new legend in the current document
            View newLegend = new FilteredElementCollector(curDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => v.ViewType == ViewType.Legend
                                  && v.Name.Equals(newLegendName, StringComparison.OrdinalIgnoreCase));

            if (newLegend == null) return 0;

            // find all Exterior Elevation sheets
            List<ViewSheet> exteriorSheets = Utils.GetAllSheets(curDoc)
                .Where(s => s.Name.IndexOf("Exterior Elevation", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (exteriorSheets.Count == 0) return 0;

            // find the "No Title" viewport type once for all placements
            ElementId noTitleTypeId = GetNoTitleViewportTypeId(curDoc);

            int count = 0;
            foreach (ViewSheet sheet in exteriorSheets)
            {
                // find a viewport on this sheet whose view is an eave detail matching the keyword
                Viewport oldVp = sheet.GetAllViewports()
                    .Select(id => curDoc.GetElement(id) as Viewport)
                    .FirstOrDefault(vp =>
                    {
                        if (vp == null) return false;
                        View v = curDoc.GetElement(vp.ViewId) as View;
                        if (v == null || v.ViewType != ViewType.Legend) return false;
                        string name = v.Name;
                        return name.StartsWith("Eave Detail", StringComparison.OrdinalIgnoreCase)
                            && name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                    });

                if (oldVp == null) continue;

                // record the center of the existing viewport before deleting it
                XYZ center = oldVp.GetBoxCenter();

                // remove the old viewport
                curDoc.Delete(oldVp.Id);

                // place the new legend at the same position
                Viewport newVp = Viewport.Create(curDoc, sheet.Id, newLegend.Id, center);

                // set viewport type to No Title
                if (noTitleTypeId != null && newVp != null)
                    newVp.ChangeTypeId(noTitleTypeId);

                count++;
            }

            return count;
        }

        /// <summary>
        /// returns the ElementId of the "No Title" viewport type, or null if not found.
        /// </summary>
        private ElementId GetNoTitleViewportTypeId(Document curDoc)
        {
            return new FilteredElementCollector(curDoc)
                .OfClass(typeof(ElementType))
                .Cast<ElementType>()
                .FirstOrDefault(t => t.FamilyName.Equals("Viewport", StringComparison.OrdinalIgnoreCase)
                                  && t.Name.Equals("No Title", StringComparison.OrdinalIgnoreCase))
                ?.Id;
        }

        #endregion

        #region Selection Filters

        /// <summary>
        /// selection filter that restricts element picking to EL-Wall Base / Switch instances only.
        /// </summary>
        private class SwitchSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                FamilyInstance fi = elem as FamilyInstance;
                if (fi == null) return false;

                // must be in the Electrical Fixtures category
                if (fi.Category?.Id.Value != (long)BuiltInCategory.OST_ElectricalFixtures)
                    return false;

                string famName = fi.Symbol.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? string.Empty;
                return (famName.Equals("EL-Wall Base", StringComparison.OrdinalIgnoreCase) ||
                        famName.Equals("EL-No Base", StringComparison.OrdinalIgnoreCase))
                    && fi.Symbol.Name.Equals("Switch", StringComparison.OrdinalIgnoreCase);
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        /// <summary>
        /// selection filter that restricts element picking to wall-hosted family instances only,
        /// used for selecting light switches mounted on walls.
        /// </summary>
        private class WallHostedSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                FamilyInstance fi = elem as FamilyInstance;
                return fi?.Host is Wall;
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }


        /// <summary>
        /// selection filter that restricts element picking to LT-No Base / Ceiling Fan instances only.
        /// </summary>
        private class CeilingFanSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                FamilyInstance fi = elem as FamilyInstance;
                if (fi == null) return false;

                // check that the family name contains "LT-No Base" and the type is "Ceiling Fan"
                string famName = fi.Symbol.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString() ?? string.Empty;
                return famName.Contains("LT-No Base") &&
                       fi.Symbol.Name.Equals("Ceiling Fan", StringComparison.OrdinalIgnoreCase);
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        /// <summary>
        /// selection filter that restricts element picking to walls only.
        /// </summary>
        private class WallSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Wall;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        #endregion
    }
}
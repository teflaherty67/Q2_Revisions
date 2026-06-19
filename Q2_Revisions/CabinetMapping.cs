namespace Q2_Revisions
{
    internal class CabinetMapping
    {
        internal string OldFamilyName  { get; }
        internal string NewFamilyName  { get; }
        internal string LibrarySubfolder { get; } // "Kitchen" or "Bath"

        internal CabinetMapping(string oldName, string newName, string subfolder)
        {
            OldFamilyName    = oldName;
            NewFamilyName    = newName;
            LibrarySubfolder = subfolder;
        }

        // ── Kitchen base cabinets ─────────────────────────────────────────────
        // ── Kitchen upper cabinets ────────────────────────────────────────────
        // ── Bath vanity cabinets ──────────────────────────────────────────────
        // Add entries as: new CabinetMapping("Old Family Name", "New Family Name", "Kitchen|Bath")
        internal static readonly List<CabinetMapping> AllMappings = new List<CabinetMapping>
        {
            // Kitchen – Base Cabinets
            new CabinetMapping("Base Cabinet-Double Door & 2 Drawer", "LD_CW_Base_2-Dr_2-Drwr_Flush", "Kitchen"),

            // Kitchen – Upper Cabinets
            // new CabinetMapping("Upper Cabinet-...", "LD_CW_Upper_...", "Kitchen"),

            // Bath – Vanity Cabinets
            // new CabinetMapping("Vanity Cabinet-...", "LD_CW_Bath_...", "Bath"),
        };
    }
}

using UnityEngine;

namespace CarWash.UI
{
    /// <summary>
    /// References to the UI sprites from Art/UI (docs/ART_IMPORT.md). Built by ArtBuilder, registered by Bootstrap,
    /// read by UiFactory/Hud. Referencing them here also guarantees they ship in builds.
    /// </summary>
    public class UiSkin : ScriptableObject
    {
        public Sprite panel, card, toast;
        public Sprite btnPrimary, btnHover, btnPressed, btnDisabled;
        public Sprite crosshair, crosshairHit;
        public Sprite slot, slotSelected, barBg, barFill;
        [Tooltip("White rounded built-in UI sprite (UISprite) for tinted bar fills — ui_bar_fill is baked green")]
        public Sprite barFillWhite;
        public Color buttonText = new Color32(0x1B, 0x23, 0x40, 0xFF);
        public Color panelText = Color.white;
        [Tooltip("All Art/UI/Icons/** sprites (docs/ICONS.md), looked up by file name via Icon().")]
        public Sprite[] icons = new Sprite[0];

        System.Collections.Generic.Dictionary<string, Sprite> _iconMap;
        void OnEnable() => _iconMap = null;
        void OnValidate() => _iconMap = null;
        public void ResetIconCache() => _iconMap = null;

        /// <summary>Icon by file name without extension, e.g. "ico_tool_washer"; null when missing.</summary>
        public Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_iconMap == null)
            {
                _iconMap = new System.Collections.Generic.Dictionary<string, Sprite>();
                foreach (var sp in icons) if (sp != null) _iconMap[sp.name] = sp;
            }
            return _iconMap.TryGetValue(name, out var r) ? r : null;
        }

        /// <summary>Tool id (T01..T08) → inventory icon name.</summary>
        public static string ToolIconName(string toolId)
        {
            switch (toolId)
            {
                case "T01": return "ico_tool_washer";
                case "T02": return "ico_tool_foam";
                case "T03": return "ico_tool_towel";
                case "T04": return "ico_tool_vac";
                case "T05": return "ico_tool_wax";
                case "T06": return "ico_tool_net";
                case "T07": return "ico_tool_extinguisher";
                case "T08": return "ico_tool_blower";
                default: return null;
            }
        }

        public Sprite ToolIcon(string toolId) => Icon(ToolIconName(toolId));

        /// <summary>
        /// Vehicle → icon. Only 4 vehicle icons exist (docs/ICONS.md); other ids use the nearest silhouette by size,
        /// bosses use ico_boss_monstertruck (Critters folder).
        /// </summary>
        public static string VehicleIconName(Data.VehicleData v)
        {
            if (v == null) return "ico_vehicle_sedan";
            switch (v.id)
            {
                case "V01": return "ico_vehicle_hatchback";
                case "V02": return "ico_vehicle_sedan";
                case "V03": return "ico_vehicle_pickup";
                case "V04": return "ico_vehicle_minivan";
            }
            if (v.isBoss) return "ico_boss_monstertruck";
            switch (v.Size)
            {
                case Data.VehicleSize.S: return "ico_vehicle_hatchback";
                case Data.VehicleSize.M: return "ico_vehicle_sedan";
                case Data.VehicleSize.L: return "ico_vehicle_minivan";
                default: return "ico_vehicle_pickup";
            }
        }

        public Sprite VehicleIcon(Data.VehicleData v) => Icon(VehicleIconName(v));

        /// <summary>Critter id → icon (docs/ICONS.md; ids without an icon → null).</summary>
        public static string CritterIconName(string critterId)
        {
            switch (critterId)
            {
                case "C01": return "ico_critter_raccoon";
                case "C02": return "ico_critter_bees";
                case "C03": return "ico_critter_slime";
                case "C04": return "ico_critter_pigeon";
                case "C05": return "ico_critter_possum";
                default: return null;
            }
        }

        public Sprite CritterIcon(string critterId) => Icon(CritterIconName(critterId));
    }
}

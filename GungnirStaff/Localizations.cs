using System.Collections.Generic;
using Jotunn.Managers;

namespace GungnirStaff
{
    /// <summary>
    ///     Translation tokens. Anything user-visible goes through a $token so it can be
    ///     translated later; Jotunn merges these into Valheim's own table.
    /// </summary>
    internal static class Localizations
    {
        internal static void Register()
        {
            // GetLocalization() returns this mod's own CustomLocalization instance,
            // created and owned by Jotunn - safe to call repeatedly across reloads.
            LocalizationManager.Instance.GetLocalization().AddTranslation("English",
                new Dictionary<string, string>
                {
                    { "gungnir_item_name", "Gungnir" },
                    {
                        "gungnir_item_desc",
                        "Odin's own spear, that never misses its mark and never breaks its oath. "
                        + "The shaft thrums with a magic too vast to spend on a single form, so it "
                        + "takes the power of other staffs into itself and wields them as one."
                    },
                    { "gungnir_bar_title", "Staff rack" },
                    { "gungnir_msg_only_staffs", "Only magic staffs fit in the rack" },
                    { "gungnir_msg_rack_full", "The staff rack is full" },
                });
        }
    }
}

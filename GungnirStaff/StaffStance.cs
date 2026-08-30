using System.Linq;
using System.Runtime.CompilerServices;

namespace GungnirStaff
{
    /// <summary>
    ///     Keeps Gungnir held the same way whichever staff is selected.
    ///
    ///     Selecting a staff points Gungnir's <c>m_shared</c> at that staff's own
    ///     SharedData, which is what makes every vanilla system behave correctly for
    ///     free - and it brings the staff's hold pose along with it. That is right for
    ///     the staffs that are staff-shaped and wrong for the ones that are not: the
    ///     Dead Raiser is a skull, and vanilla gives it a posture to match, so a Gungnir
    ///     impersonating it was suddenly being carried like a skull.
    ///
    ///     Only <c>m_animationState</c> is touched, and it is worth knowing how little
    ///     that field does: the whole game reads it in exactly two places -
    ///     <c>Humanoid.SetupAnimationState</c>, which picks the animator pose, and
    ///     <c>KeyHints.UpdateHints</c>, which picks which key hints to show. It has no
    ///     bearing on damage, attacks, casting, eitr or which hand the item goes in.
    /// </summary>
    internal static class StaffStance
    {
        /// <summary>
        ///     Fallback pose when the stance cannot be worked out from the staffs that
        ///     are installed - the game's own "holding a staff" state.
        /// </summary>
        private const ItemDrop.ItemData.AnimationState DefaultStance =
            ItemDrop.ItemData.AnimationState.Staves;

        /// <summary>
        ///     Adjusted copies, keyed on the SharedData they were made from.
        ///
        ///     A copy is essential rather than convenient: the SharedData handed to us is
        ///     the one instance every item of that type shares, so writing the stance
        ///     straight into it would re-pose every Dead Raiser in the game, including
        ///     the real one in the player's inventory, for the rest of the session.
        ///
        ///     Caching keeps the copy stable across re-selections and re-equips, so the
        ///     item does not appear to change identity every time it is projected.
        /// </summary>
        private static readonly ConditionalWeakTable<ItemDrop.ItemData.SharedData,
            ItemDrop.ItemData.SharedData> Adjusted =
            new ConditionalWeakTable<ItemDrop.ItemData.SharedData,
                ItemDrop.ItemData.SharedData>();

        private static ItemDrop.ItemData.AnimationState? _common;

        /// <summary>
        ///     The SharedData to project onto Gungnir for this staff: the staff's own
        ///     when its stance already matches, otherwise an adjusted copy.
        /// </summary>
        internal static ItemDrop.ItemData.SharedData Normalise(
            ItemDrop.ItemData.SharedData staff)
        {
            if (staff == null || !ModConfig.NormaliseStaffStance.Value)
            {
                return staff;
            }

            var wanted = CommonStance();
            if (staff.m_animationState == wanted)
            {
                // The overwhelmingly common case. No copy, so the projection keeps
                // sharing the staff's own data exactly as it did before.
                return staff;
            }

            if (Adjusted.TryGetValue(staff, out var existing))
            {
                return existing;
            }

            var copy = new ItemDrop.ItemData.SharedData();
            GungnirItem.CopyFields(staff, copy);
            copy.m_animationState = wanted;
            Adjusted.Add(staff, copy);

            ModConfig.Trace(
                $"Holding '{staff.m_name}' as {wanted} instead of {staff.m_animationState}.");
            return copy;
        }

        /// <summary>
        ///     The stance most installed staffs use.
        ///
        ///     Measured rather than hard-coded, so this still does the right thing for a
        ///     modded staff set where the majority is not the vanilla one - and so the
        ///     odd one out is always the thing that gets moved, which is exactly what
        ///     "hold it like the others" means.
        /// </summary>
        private static ItemDrop.ItemData.AnimationState CommonStance()
        {
            if (_common.HasValue)
            {
                return _common.Value;
            }

            var staffs = StaffRegistry.AllStaffPrefabs();
            if (staffs.Count == 0)
            {
                // ObjectDB is not populated yet. Deliberately not cached, so the real
                // answer is still worked out once the staffs are known.
                return DefaultStance;
            }

            var winner = staffs
                .Select(p => p.GetComponent<ItemDrop>()?.m_itemData?.m_shared)
                .Where(s => s != null)
                .GroupBy(s => s.m_animationState)
                .OrderByDescending(g => g.Count())
                .Select(g => (ItemDrop.ItemData.AnimationState?)g.Key)
                .FirstOrDefault();

            _common = winner ?? DefaultStance;
            ModConfig.Trace($"Common staff stance is {_common.Value}.");
            return _common.Value;
        }

        /// <summary>Forces a fresh measurement, e.g. after a hot reload or an ObjectDB change.</summary>
        internal static void Reset()
        {
            _common = null;
        }
    }
}

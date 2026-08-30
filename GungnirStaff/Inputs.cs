namespace GungnirStaff
{
    /// <summary>
    ///     Keyboard handling.
    ///
    ///     Every binding is a BepInEx <c>KeyboardShortcut</c>, so modifiers are part of
    ///     the binding itself and the whole set is rebindable from Configuration Manager
    ///     (F1) at runtime, with no restart and no code changes.
    /// </summary>
    internal static class Inputs
    {
        /// <summary>
        ///     True while the game is accepting gameplay keys - i.e. not typing in chat,
        ///     not in a menu, not in the console.
        /// </summary>
        private static bool GameplayInput()
        {
            var player = Player.m_localPlayer;
            return player != null && player.TakeInput();
        }

        /// <summary>Diagnostic key. Intentionally not gated: it must work anywhere.</summary>
        internal static bool StatusPressed()
        {
            return ModConfig.StatusKey.Value.IsDown();
        }

        internal static bool HolsterPressed()
        {
            return GameplayInput() && ModConfig.HolsterKey.Value.IsDown();
        }

        /// <summary>
        ///     Returns the slot whose shortcut fired this frame, or -1.
        ///     Only slots within the configured SlotCount are considered.
        /// </summary>
        internal static int PressedSlot()
        {
            if (!GameplayInput())
            {
                return -1;
            }

            var count = System.Math.Min(ModConfig.SlotCount.Value, ModConfig.MaxSlots);
            for (var i = 0; i < count; i++)
            {
                if (ModConfig.SlotKeys[i].Value.IsDown())
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///     True if a slot shortcut is firing right now. Used to suppress the vanilla
        ///     hotbar so Ctrl+3 selects a staff instead of also using hotbar item 3.
        /// </summary>
        internal static bool SlotShortcutActive()
        {
            var count = System.Math.Min(ModConfig.SlotCount.Value, ModConfig.MaxSlots);
            for (var i = 0; i < count; i++)
            {
                if (ModConfig.SlotKeys[i].Value.IsDown())
                {
                    return true;
                }
            }

            return ModConfig.HolsterKey.Value.IsDown();
        }
    }
}

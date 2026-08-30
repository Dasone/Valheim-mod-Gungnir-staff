using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

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

            // Every bound key, not SlotCount of them: SlotCount is 0 when slots follow
            // the staff's upgrade level, which silently disabled every shortcut.
            // Selecting an empty or non-existent slot is already handled downstream.
            for (var i = 0; i < ModConfig.MaxSlots; i++)
            {
                if (ModConfig.SlotKeys[i].Value.IsDown())
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///     A binding written short enough to sit under a hotbar slot - "Alt+1" rather
        ///     than BepInEx's own "Alpha1 + LeftAlt".
        ///
        ///     Built from the shortcut rather than from a hard-coded string so a rebind in
        ///     Configuration Manager is reflected on the bar, and so the label cannot drift
        ///     out of step with the key that actually works.
        /// </summary>
        internal static string ShortLabel(KeyboardShortcut shortcut)
        {
            var main = KeyName(shortcut.MainKey);
            if (string.IsNullOrEmpty(main))
            {
                return string.Empty;
            }

            var modifiers = shortcut.Modifiers?
                .Select(ModifierName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToArray();

            return modifiers == null || modifiers.Length == 0
                ? main
                : string.Join("+", modifiers) + "+" + main;
        }

        /// <summary>Left and right of a modifier read the same on a label.</summary>
        private static string ModifierName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                case KeyCode.AltGr:
                    return "Alt";
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return "Ctrl";
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "Shift";
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                    return "Cmd";
                default:
                    return KeyName(key);
            }
        }

        /// <summary>
        ///     The printable name of a key. The number row matters most here - its enum
        ///     names are "Alpha1" and "Keypad1", neither of which is what is printed on
        ///     the key the player is looking for.
        /// </summary>
        private static string KeyName(KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return string.Empty;
            }

            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return ((int)(key - KeyCode.Alpha0)).ToString();
            }

            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            {
                return "#" + (int)(key - KeyCode.Keypad0);
            }

            return key.ToString();
        }

        /// <summary>
        ///     True if a slot shortcut is firing right now. Used to suppress the vanilla
        ///     hotbar so Ctrl+3 selects a staff instead of also using hotbar item 3.
        /// </summary>
        internal static bool SlotShortcutActive()
        {
            for (var i = 0; i < ModConfig.MaxSlots; i++)
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

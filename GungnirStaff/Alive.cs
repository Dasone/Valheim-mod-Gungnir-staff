using System.Linq;
using HarmonyLib;

namespace GungnirStaff
{
    /// <summary>
    ///     "Is the mod alive?" reporting. Writes the same status line to every place
    ///     you might be looking: the BepInEx console window, Valheim's own F5 console,
    ///     and the on-screen message area.
    /// </summary>
    internal static class Alive
    {
        /// <summary>
        ///     One line describing exactly which build is running. The timestamp is
        ///     baked in at compile time, so after a hot reload you can tell at a glance
        ///     whether the new DLL actually took.
        /// </summary>
        internal static string StatusLine(Harmony harmony)
        {
            var patches = harmony == null ? 0 : harmony.GetPatchedMethods().Count();
            return $"{GungnirStaffPlugin.ModName} v{GungnirStaffPlugin.ModVersion} | " +
                   $"{BuildInfo.Configuration} build {BuildInfo.BuildTime} | " +
                   $"{patches} patched method(s)";
        }

        /// <summary>
        ///     Announces the mod everywhere at once.
        /// </summary>
        /// <param name="reason">What triggered this, e.g. "loaded" or "reloaded".</param>
        internal static void Announce(string reason, Harmony harmony)
        {
            var line = $"[{reason}] {StatusLine(harmony)}";

            // 1. BepInEx console window + LogOutput.log. LogMessage sits above Info,
            //    so it shows even if you filter the console down later.
            GungnirStaffPlugin.Log.LogMessage(line);

            // 2. Valheim's in-game console (F5). Null until a game is running, which
            //    is exactly how we tell a hot reload apart from a cold start.
            ToGameConsole(line);

            // 3. On-screen, centre of the HUD. Also null-safe before a world loads.
            ToHud(line);
        }

        /// <summary>True once a world is actually running - i.e. this is a hot reload, not startup.</summary>
        internal static bool InGame => Console.m_instance != null || Player.m_localPlayer != null;

        internal static void ToGameConsole(string text)
        {
            // Console.m_instance is private in the game; reachable because the csproj
            // publicizes assembly_valheim.
            Console.m_instance?.Print(text);
        }

        internal static void ToHud(string text)
        {
            MessageHud.m_instance?.ShowMessage(MessageHud.MessageType.Center, text);
        }
    }
}

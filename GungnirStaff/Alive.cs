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
                   $"loaded from {LoadedFrom()} | " +
                   $"{patches} patched method(s)";
        }

        /// <summary>
        ///     Which copy of the DLL is actually running.
        ///
        ///     The version number cannot answer this on its own: a development build
        ///     carries the same ModVersion as the release it was branched from, so
        ///     "v1.0.3" is true of both the packaged mod and a local build sitting on top
        ///     of it. The folder name is the part that differs, and ScriptEngine loads
        ///     from memory rather than from a path - which is itself the tell that a
        ///     hot-reloaded build is the one in charge.
        /// </summary>
        private static string LoadedFrom()
        {
            try
            {
                var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(location))
                {
                    return "scripts (hot reload, in memory)";
                }

                var folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(location));
                return string.IsNullOrEmpty(folder) ? location : folder;
            }
            catch (System.Exception)
            {
                return "unknown";
            }
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

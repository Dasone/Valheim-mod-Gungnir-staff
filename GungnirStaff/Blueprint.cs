using System.Linq;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     One-shot dump of a vanilla item prefab's construction.
    ///
    ///     Building a standalone item means reproducing the component set Valheim expects
    ///     - miss one and the item is unpickupable, invisible on the ground, or fails to
    ///     replicate. Reading a real one is the only reliable way to know what that set
    ///     is; guessing at it is how you get a subtly broken prefab.
    /// </summary>
    internal static class Blueprint
    {
        private static bool _dumped;

        internal static void DumpVanillaItem(string prefabName)
        {
            if (_dumped || !ModConfig.VerboseLogging.Value)
            {
                return;
            }

            var db = ObjectDB.instance;
            var prefab = db != null ? db.GetItemPrefab(prefabName) : null;
            if (prefab == null)
            {
                return;
            }

            _dumped = true;
            var log = GungnirStaffPlugin.Log;

            log.LogInfo($"BP ROOT '{prefab.name}' layer={LayerMask.LayerToName(prefab.layer)} "
                        + $"components=[{Components(prefab)}]");

            foreach (Transform child in prefab.transform)
            {
                log.LogInfo($"BP   child '{child.name}' "
                            + $"layer={LayerMask.LayerToName(child.gameObject.layer)} "
                            + $"components=[{Components(child.gameObject)}] "
                            + $"children={child.childCount}");
            }

            var drop = prefab.GetComponent<ItemDrop>();
            if (drop != null)
            {
                log.LogInfo($"BP ItemDrop autoPickup={drop.m_autoPickup} "
                            + $"stack={drop.m_itemData.m_stack} "
                            + $"quality={drop.m_itemData.m_quality}");
            }

            var nview = prefab.GetComponent<ZNetView>();
            if (nview != null)
            {
                log.LogInfo($"BP ZNetView persistent={nview.m_persistent} "
                            + $"distant={nview.m_distant} type={nview.m_type} "
                            + $"syncInitialScale={nview.m_syncInitialScale}");
            }

            var body = prefab.GetComponent<Rigidbody>();
            if (body != null)
            {
                log.LogInfo($"BP Rigidbody mass={body.mass} "
                            + $"useGravity={body.useGravity} "
                            + $"constraints={body.constraints} interpolation={body.interpolation}");
            }

            foreach (var col in prefab.GetComponentsInChildren<Collider>(true))
            {
                log.LogInfo($"BP Collider on '{col.gameObject.name}' type={col.GetType().Name} "
                            + $"trigger={col.isTrigger} "
                            + $"size={(col is BoxCollider box ? box.size.ToString("F3") : "-")} "
                            + $"radius={(col is SphereCollider sph ? sph.radius.ToString("F3") : "-")}");
            }

            var shared = drop?.m_itemData?.m_shared;
            if (shared != null)
            {
                log.LogInfo($"BP shared maxStack={shared.m_maxStackSize} weight={shared.m_weight} "
                            + $"value={shared.m_value} teleportable={shared.m_teleportable} "
                            + $"questItem={shared.m_questItem} useDurability={shared.m_useDurability} "
                            + $"maxDur={shared.m_maxDurability} durPerLevel={shared.m_durabilityPerLevel} "
                            + $"equipDur={shared.m_equipDuration} "
                            + $"animState={shared.m_animationState} "
                            + $"attackAnim='{shared.m_attack?.m_attackAnimation}'");
            }
        }

        private static string Components(GameObject go)
        {
            return string.Join(", ", go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetType().Name));
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GungnirStaff
{
    /// <summary>
    ///     The staff rack: a one-row inventory strip that behaves like a second hotbar.
    ///
    ///     Built by cloning vanilla's own player <see cref="InventoryGrid"/> rather than
    ///     by assembling UI from scratch: the clone inherits Valheim's slot art,
    ///     tooltips, durability bars, gamepad handling and drag visuals, and vanilla's
    ///     <c>UpdateGui</c> rebuilds the slots to match whatever inventory we bind, so a
    ///     one-row 7-wide grid needs no layout code of our own.
    ///
    ///     It is parented to the canvas root rather than the inventory panel, so it can
    ///     stay on screen while the inventory is closed and still receive clicks.
    /// </summary>
    internal static class GungnirBar
    {
        /// <summary>Name given to the shortcut labels, so they can be found again.</summary>
        private const string SlotKeyLabelName = "GungnirSlotKey";

        private static GameObject _root;
        private static InventoryGrid _grid;
        private static Inventory _bound;

        /// <summary>The shortcut labels currently on the bar.</summary>
        private static readonly List<TMP_Text> _slotKeyLabels = new List<TMP_Text>();

        /// <summary>
        ///     How many slots those labels were built for. Tracked separately from the
        ///     list length because a slot bound to nothing gets no label, and comparing
        ///     the list length to the slot count would then rebuild on every frame.
        /// </summary>
        private static int _labelledSlots = -1;

        private static bool _dragging;
        private static Vector2 _dragGrabPoint;
        private static Vector2 _dragStartPosition;
        private static Vector2 _dragPosition;

        /// <summary>
        ///     Set while the drag writes its result back to the config, so the bar is not
        ///     torn down and rebuilt underneath the cursor. Every other layout setting
        ///     still rebuilds normally.
        /// </summary>
        private static bool _writingOwnPosition;

        /// <summary>The live grid, or null when the bar does not exist right now.</summary>
        internal static InventoryGrid Grid => _grid;

        /// <summary>The container currently displayed, or null.</summary>
        internal static StaffContainer Container { get; private set; }

        /// <summary>
        ///     Called every frame from the plugin's own Update, so the bar keeps working
        ///     with the inventory closed. Cheap when there is nothing to do.
        /// </summary>
        internal static void Refresh(Player player)
        {
            var gui = InventoryGui.m_instance;
            if (gui == null || player == null)
            {
                Hide();
                return;
            }

            var gungnir = GungnirItem.Active(player);
            if (!ShouldShow(gungnir))
            {
                Hide();
                return;
            }

            if (_grid == null && !Build(gui))
            {
                return;
            }

            Container = gungnir != null ? StaffContainer.For(gungnir) : null;
            if (Container == null)
            {
                // Visibility is Always but there is no Gungnir to show the contents of.
                Hide();
                return;
            }

            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }

            // Before ApplyLayout, which reads whatever position the drag has reached.
            UpdateDrag();
            ApplyLayout();

            _bound = Container.Inventory;
            _grid.UpdateInventory(_bound, player, gui.m_dragItem);
            SyncSlotKeyLabels();
            HighlightSelected();
        }

        private static bool ShouldShow(ItemDrop.ItemData gungnir)
        {
            switch (ModConfig.Visibility.Value)
            {
                case BarVisibility.Always:
                    return true;
                case BarVisibility.InventoryOnly:
                    return gungnir != null && InventoryGui.IsVisible();
                default:
                    return gungnir != null;
            }
        }

        private static void Hide()
        {
            EndDrag(false);
            Container = null;
            if (_root != null && _root.activeSelf)
            {
                _root.SetActive(false);
            }
        }

        /// <summary>Tears the bar down. Called on hot reload and on layout changes.</summary>
        internal static void Teardown()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }

            _dragging = false;
            _root = null;
            _grid = null;
            _bound = null;
            Container = null;

            // The labels lived on the slot widgets, which have just gone with the root.
            _slotKeyLabels.Clear();
            _labelledSlots = -1;
        }

        /// <summary>
        ///     Puts each slot's shortcut under it, the way the vanilla hotbar numbers its
        ///     own slots.
        ///
        ///     Rebuilt rather than refreshed when the row changes shape: vanilla's
        ///     <c>UpdateGui</c> destroys and recreates every slot widget whenever the
        ///     inventory's width changes - an upgrade widening the rack, say - taking our
        ///     labels with them. It leaves the widgets alone otherwise, which is why this
        ///     is not doing work on every frame.
        /// </summary>
        private static void SyncSlotKeyLabels()
        {
            var elements = _grid.m_elements;

            if (!ModConfig.ShowSlotKeys.Value || elements == null)
            {
                if (_slotKeyLabels.Count > 0)
                {
                    ClearSlotKeyLabels();
                }

                return;
            }

            if (LabelsAreCurrent(elements.Count))
            {
                return;
            }

            ClearSlotKeyLabels();

            for (var i = 0; i < elements.Count; i++)
            {
                var label = BuildSlotKeyLabel(elements[i], i);
                if (label != null)
                {
                    _slotKeyLabels.Add(label);
                }
            }

            _labelledSlots = elements.Count;
            ModConfig.Trace($"Built {_slotKeyLabels.Count} shortcut label(s) for {elements.Count} slot(s).");
        }

        /// <summary>
        ///     True when the labels still match the row. A destroyed label reads as null
        ///     through Unity's own equality, which is how a rebuilt row is detected
        ///     without hooking anything.
        /// </summary>
        private static bool LabelsAreCurrent(int slots)
        {
            if (_labelledSlots != slots)
            {
                return false;
            }

            foreach (var label in _slotKeyLabels)
            {
                if (label == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ClearSlotKeyLabels()
        {
            foreach (var label in _slotKeyLabels)
            {
                if (label != null)
                {
                    Object.Destroy(label.gameObject);
                }
            }

            _slotKeyLabels.Clear();
            _labelledSlots = -1;
        }

        /// <summary>
        ///     One label, cloned from the slot's own stack-count text so it inherits
        ///     Valheim's font, outline and material rather than needing any of that built
        ///     from scratch.
        /// </summary>
        private static TMP_Text BuildSlotKeyLabel(InventoryGrid.Element element, int slot)
        {
            var source = element?.m_amount;
            if (source == null || element.m_go == null || slot >= ModConfig.MaxSlots)
            {
                return null;
            }

            var text = Inputs.ShortLabel(ModConfig.SlotKeys[slot].Value);
            if (string.IsNullOrEmpty(text))
            {
                // Unbound slot. No label rather than an empty box.
                return null;
            }

            var go = Object.Instantiate(source.gameObject, element.m_go.transform);
            go.name = SlotKeyLabelName;
            go.SetActive(true);

            var label = go.GetComponent<TMP_Text>();
            if (label == null)
            {
                Object.Destroy(go);
                return null;
            }

            // Sized from the slot rather than from a fixed number, so the labels track
            // the bar at any BarScale and at any slot size the game's UI settings give.
            var slotHeight = SlotHeight(element);

            var rect = go.transform as RectTransform;
            if (rect != null)
            {
                // Pinned to the bottom edge of the slot with a top pivot, so the text
                // hangs underneath instead of overlapping the icon.
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -slotHeight * 0.04f);
                rect.sizeDelta = new Vector2(slotHeight * 1.6f, slotHeight * 0.4f);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            // THE reason the labels did not show up. Cloning copies the component's
            // enabled flag, and vanilla disables the stack-count text on any slot whose
            // item cannot stack - UpdateGui does m_amount.enabled = maxStackSize > 1, and
            // a staff's max stack is 1, so every slot in the rack has it switched off.
            // SetActive(true) activates the GameObject and leaves the Behaviour disabled,
            // so the label existed, was positioned correctly, and drew nothing.
            label.enabled = true;

            label.text = text;
            label.fontSize = slotHeight * 0.26f * ModConfig.SlotKeyScale.Value;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Top;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = new Color(1f, 1f, 1f, 0.7f);

            // Never let a label eat a click meant for the slot above it.
            label.raycastTarget = false;

            return label;
        }

        /// <summary>The slot's own height, with a sane fallback if the rect is missing.</summary>
        private static float SlotHeight(InventoryGrid.Element element)
        {
            var rect = element.m_go.transform as RectTransform;
            var height = rect != null ? rect.rect.height : 0f;
            return height > 1f ? height : 64f;
        }

        private static bool Build(InventoryGui gui)
        {
            var source = gui.m_playerGrid;
            var parent = FindAlwaysActiveUiRoot(gui);
            if (source == null || parent == null)
            {
                return false;
            }

            try
            {
                // Parented to the ROOT canvas. Not the inventory panel (disabled the
                // moment the inventory closes) and not the HUD (its canvas has no
                // raycaster, so slots parented there receive no clicks at all - which is
                // why placing staffs silently did nothing). Last sibling so it draws over
                // the rest of the UI.
                _root = Object.Instantiate(source.gameObject, parent);
                _root.name = "GungnirStaffBar";
                _root.transform.SetAsLastSibling();

                _grid = _root.GetComponent<InventoryGrid>();
                if (_grid == null)
                {
                    Teardown();
                    return false;
                }

                MakeSelfSufficient();
                ClearClonedSlots();

                // Drop whatever vanilla wired to the clone and put our own handlers on.
                _grid.m_onSelected = null;
                _grid.m_onRightClick = null;
                _grid.m_onSelected += OnSelected;
                _grid.m_onRightClick += OnRightClick;

                ModConfig.Trace($"Staff bar created under '{parent.name}'.");
                return true;
            }
            catch (System.Exception ex)
            {
                GungnirStaffPlugin.Log.LogError($"Could not build the staff bar: {ex}");
                Teardown();
                return false;
            }
        }

        /// <summary>
        ///     A parent that stays active with the inventory closed.
        ///
        ///     Inventory_screen turns out to be its own root Canvas, and it is switched
        ///     off the moment the inventory closes - so it can never host an
        ///     always-visible bar. The HUD root stays up during play (and correctly
        ///     disappears when the player hides the HUD), which is what we want; input is
        ///     handled by our own raycaster rather than by an ancestor's.
        /// </summary>
        private static Transform FindAlwaysActiveUiRoot(InventoryGui gui)
        {
            var hud = Hud.m_instance != null ? Hud.m_instance.m_rootObject : null;
            if (hud != null)
            {
                return hud.transform;
            }

            var canvas = gui.GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.rootCanvas : null;
            return root != null ? root.transform : gui.transform.root;
        }

        /// <summary>
        ///     Gives the bar its own Canvas and GraphicRaycaster.
        ///
        ///     Valheim puts the only nearby raycaster on Inventory_screen, so a grid that
        ///     relies on an ancestor for input can either be clickable or survive the
        ///     inventory closing, but not both. Owning them makes the bar independent of
        ///     whatever its ancestors do, and confines the change to our object rather
        ///     than adding a raycaster to a shared vanilla canvas.
        /// </summary>
        private static void MakeSelfSufficient()
        {
            var canvas = _root.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = _root.AddComponent<Canvas>();
            }

            // Raycasters are prioritised by canvas sorting order, so the bar has to
            // outrank Inventory_screen or the open inventory's "dropped outside" catcher
            // wins the click and throws the staff on the ground. Read the inventory's
            // actual order rather than guessing a number.
            var guiCanvas = InventoryGui.m_instance != null
                ? InventoryGui.m_instance.GetComponentInParent<Canvas>()
                : null;
            var inventoryOrder = guiCanvas != null ? guiCanvas.sortingOrder : 0;

            canvas.overrideSorting = true;
            canvas.sortingOrder = inventoryOrder + 5;
            ModConfig.Trace($"Rack canvas sortingOrder {canvas.sortingOrder} "
                            + $"(inventory canvas is {inventoryOrder}).");

            if (_root.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                _root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
        }

        /// <summary>
        ///     Instantiating the player grid also copies the slot widgets vanilla had
        ///     already built into it. Those copies are not in the clone's
        ///     <c>m_elements</c> list, so <c>UpdateGui</c> never destroys them and they
        ///     hang around drawing a ghost of the player inventory on top of our row.
        ///     Wipe the grid root by hand before the first update.
        /// </summary>
        private static void ClearClonedSlots()
        {
            _grid.m_elements?.Clear();

            var gridRoot = _grid.m_gridRoot;
            if (gridRoot == null)
            {
                return;
            }

            for (var i = gridRoot.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(gridRoot.GetChild(i).gameObject);
            }
        }

        /// <summary>
        ///     Layout changes rebuild the bar - except the one this file makes itself.
        ///
        ///     Position is applied from config every frame, so a drag needs no rebuild at
        ///     all, and tearing the bar down the instant the mouse is released would blink
        ///     it off screen at exactly the wrong moment.
        /// </summary>
        internal static void OnLayoutSettingChanged()
        {
            if (_writingOwnPosition)
            {
                return;
            }

            Teardown();
        }

        /// <summary>
        ///     Middle-drag the bar to reposition it, while the inventory is open and the
        ///     cursor is free. The result is written back to PositionX/PositionY, so it
        ///     survives a relog and stays editable from F1.
        /// </summary>
        private static void UpdateDrag()
        {
            var button = ModConfig.MoveBarMouseButton.Value;

            // The cursor is only free with the inventory up; dragging while it is
            // captured by the camera would fling the bar around as the player looks.
            if (button < 0 || !InventoryGui.IsVisible())
            {
                EndDrag(false);
                return;
            }

            if (!_dragging)
            {
                if (ZInput.GetMouseButtonDown(button)
                    && PointerIsOverSlots()
                    && TryPointerInParent(out var grab))
                {
                    _dragging = true;
                    _dragGrabPoint = grab;
                    _dragStartPosition = ConfiguredPosition();
                    _dragPosition = _dragStartPosition;
                }

                return;
            }

            // The delta between two points in the SAME space, which is why it does not
            // matter that the parent's local coordinates and the bar's own anchored ones
            // have different origins.
            if (TryPointerInParent(out var now))
            {
                _dragPosition = ClampToRange(_dragStartPosition + (now - _dragGrabPoint));
            }

            if (!ZInput.GetMouseButton(button))
            {
                EndDrag(true);
            }
        }

        private static void EndDrag(bool commit)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            if (!commit)
            {
                return;
            }

            _writingOwnPosition = true;
            try
            {
                ModConfig.BarOffsetX.Value = Mathf.Round(_dragPosition.x);
                ModConfig.BarOffsetY.Value = Mathf.Round(_dragPosition.y);
            }
            finally
            {
                _writingOwnPosition = false;
            }

            ModConfig.Trace(
                $"Staff bar moved to {ModConfig.BarOffsetX.Value}, {ModConfig.BarOffsetY.Value}.");
        }

        /// <summary>
        ///     The cursor in the bar's PARENT space, which is the space anchoredPosition
        ///     is measured in. Goes through RectTransformUtility rather than using raw
        ///     pixels, so the drag tracks the cursor at any UI scale or resolution.
        /// </summary>
        private static bool TryPointerInParent(out Vector2 point)
        {
            point = Vector2.zero;

            var parent = _root != null ? _root.transform.parent as RectTransform : null;
            if (parent == null)
            {
                return false;
            }

            var canvas = _root.GetComponent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, ZInput.mousePosition, cam, out point);
        }

        private static Vector2 ConfiguredPosition()
        {
            return new Vector2(ModConfig.BarOffsetX.Value, ModConfig.BarOffsetY.Value);
        }

        /// <summary>
        ///     Held to the same bounds the config accepts, so what it is dragged to is
        ///     what gets saved rather than something BepInEx silently clamps afterwards.
        /// </summary>
        private static Vector2 ClampToRange(Vector2 position)
        {
            return new Vector2(
                Mathf.Clamp(position.x, -2000f, 2000f),
                Mathf.Clamp(position.y, -2000f, 2000f));
        }

        private static void ApplyLayout()
        {
            var rect = _root.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            // Anchored to the bottom-centre of the screen, like the vanilla hotbar, so
            // the configured position means the same thing at any resolution.
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = _dragging ? _dragPosition : ConfiguredPosition();
            rect.localScale = Vector3.one * ModConfig.BarScale.Value;
        }

        /// <summary>
        ///     Lights up the active slot with vanilla's own "this is equipped" highlight.
        ///     Driven straight onto the widget rather than by setting m_equipped on the
        ///     stored item, because that flag is persisted and would make the game try to
        ///     equip a staff that is sitting inside another item.
        /// </summary>
        private static void HighlightSelected()
        {
            var slot = Container?.SelectedSlot ?? -1;
            if (slot < 0 || _bound == null)
            {
                return;
            }

            if (_grid.m_elements == null || slot >= _grid.m_elements.Count)
            {
                return;
            }

            var element = _grid.GetElement(slot, 0, _bound.m_width);
            if (element?.m_equiped != null)
            {
                element.m_equiped.enabled = true;
            }
        }

        /// <summary>
        ///     True when the mouse is over one of the rack's actual SLOTS.
        ///
        ///     Deliberately not the same test as <see cref="PointerIsOverBar"/>. The bar
        ///     is a clone of the player's 8x4 grid, so its own rectangle is far taller
        ///     than the one visible row - generous is what you want when catching a
        ///     dropped item, and wrong for starting a drag, where it would let a
        ///     middle-click in empty space several rows above the bar pick it up.
        /// </summary>
        private static bool PointerIsOverSlots()
        {
            var elements = _grid != null ? _grid.m_elements : null;
            if (elements == null || elements.Count == 0)
            {
                return false;
            }

            var canvas = _root.GetComponent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            foreach (var element in elements)
            {
                var rect = element?.m_go != null ? element.m_go.transform as RectTransform : null;
                if (rect != null
                    && RectTransformUtility.RectangleContainsScreenPoint(
                        rect, ZInput.mousePosition, cam))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the mouse is inside the rack's rectangle.</summary>
        internal static bool PointerIsOverBar()
        {
            if (_root == null || !_root.activeInHierarchy)
            {
                return false;
            }

            var rect = _root.transform as RectTransform;
            if (rect == null)
            {
                return false;
            }

            var canvas = _root.GetComponent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.RectangleContainsScreenPoint(
                rect, ZInput.mousePosition, cam);
        }

        /// <summary>
        ///     Places the item vanilla is about to throw on the ground into the rack
        ///     instead.
        ///
        ///     The rack lives on its own canvas, so whether its slots or the inventory's
        ///     "dropped outside" catcher win a click comes down to raycast ordering. This
        ///     makes placing work either way, and means a staff can never end up on the
        ///     floor because the click landed a pixel off a slot.
        /// </summary>
        internal static bool TryPlaceDraggedItem(InventoryGui gui)
        {
            var item = gui.m_dragItem;
            if (item == null || _grid == null || Container == null || _bound == null)
            {
                return false;
            }

            var player = Player.m_localPlayer;
            if (!StaffRegistry.IsStaff(item))
            {
                player?.Message(MessageHud.MessageType.Center, "$gungnir_msg_only_staffs");
                return true; // handled: refuse, but do not drop it on the ground
            }

            // Vanilla's own hit test, so the slot matches what the player sees.
            var hovered = _grid.GetHoveredElement();
            var slot = hovered?.m_pos ?? FirstFreeSlot();
            if (slot.x < 0)
            {
                player?.Message(MessageHud.MessageType.Center, "$gungnir_msg_rack_full");
                return true;
            }

            if (!_grid.DropItem(gui.m_dragInventory, item, gui.m_dragAmount, slot))
            {
                return true;
            }

            gui.SetupDragItem(null, null, 0);
            Container.SaveToItem();
            ModConfig.Trace($"Placed {item.m_shared.m_name} into rack slot {slot.x + 1}.");
            return true;
        }

        private static Vector2i FirstFreeSlot()
        {
            for (var x = 0; x < _bound.m_width; x++)
            {
                if (_bound.GetItemAt(x, 0) == null)
                {
                    return new Vector2i(x, 0);
                }
            }

            return new Vector2i(-1, 0);
        }

        /// <summary>Right click on a slot: make that staff the active one.</summary>
        private static void OnRightClick(InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos)
        {
            ModConfig.Trace($"Rack right-click at slot {pos.x + 1}, item={item?.m_shared?.m_name ?? "empty"}.");

            var player = Player.m_localPlayer;
            var gungnir = GungnirItem.Active(player);
            if (gungnir == null || item == null)
            {
                return;
            }

            StaffSwitcher.Select(player, gungnir, pos.x);
        }

        /// <summary>
        ///     Left click: hand off to vanilla so dragging, stacking and the split dialog
        ///     all behave exactly as they do in any other inventory.
        ///     Ctrl+left click (Modifier.Move): send the staff back to the player inventory.
        /// </summary>
        private static void OnSelected(
            InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos, InventoryGrid.Modifier mod)
        {
            var gui = InventoryGui.m_instance;
            var player = Player.m_localPlayer;
            ModConfig.Trace($"Rack left-click at slot {pos.x + 1}, mod={mod}, "
                            + $"slotItem={item?.m_shared?.m_name ?? "empty"}, "
                            + $"dragging={gui?.m_dragItem?.m_shared?.m_name ?? "nothing"}.");

            if (gui == null || player == null)
            {
                return;
            }

            if (mod == InventoryGrid.Modifier.Move)
            {
                MoveToPlayerInventory(player, item);
                return;
            }

            // Vanilla's Move handling picks a target from m_playerGrid/m_containerGrid,
            // which our bar is neither of - every other modifier is safe to delegate.
            gui.OnSelectedItem(grid, item, pos, mod);
        }

        private static void MoveToPlayerInventory(Player player, ItemDrop.ItemData item)
        {
            if (item == null || Container == null)
            {
                return;
            }

            var inv = player.m_inventory;
            if (inv == null)
            {
                return;
            }

            if (!inv.CanAddItem(item))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_noroom");
                return;
            }

            inv.MoveItemToThis(Container.Inventory, item);
            Container.SaveToItem();

            // The staff we were impersonating may have just left the bar.
            var gungnir = GungnirItem.Active(player);
            if (gungnir != null)
            {
                StaffSwitcher.Reapply(player, gungnir);
            }
        }
    }
}

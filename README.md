# CavesOfQud-QudUX
Some minor tweaks and improvements to Qud menus/etc.

Implementation overview:
* The entry point is `Egcb_UILoader`, which is loaded via `[HasModSensitiveStaticCache]` and `[ModSensitiveCacheInit]` attributes.
* `Egcb_UILoader` initializes `Egcb_UIMonitor`, which uses the Unity `Coroutine()` and `Update()` frameworks to monitor for when the user opens certain UI screens, such as the Journal screen, Inventory screen, or Character Creation - Complete screen. `Egcb_UIMonitor` also adds the `Egcb_PlayerUIHelper` part to the player, which does some additional event-based monitoring (such as listening for the PlayerBeginConversation event).
* When a tracked UI screen is opened by the user, `Egcb_UIMonitor` instantiates a new object of type `Egcb_JournalExtender`, `Egcb_InventoryExtender`, `Egcb_ReviewCharExtender`, or `Egcb_AbilityManagerExtender`.
* `Egcb_UIMonitor` calls `FrameCheck()` each frame on the \*Extender object, which then evaluates whether the menu screen needs to be updated with the mod's changes.
* Because all of the game's screens are internal classes and there are no events available to use to hook into UI actions, this mod works by parsing UI screen characters directly from the TextConsole screen buffer while the user is in the menu, and then overwriting parts of the screen using the game's ScreenBuffer infrastructure. The mod also uses `Unity.Input` to listen for keypresses. It would be more ideal to use the game's native `Keyboard` functions which account better for custom keymapping, but I don't think it's generally possible or easy to do that in the context of this mod.
* `Egcb_PlayerUIHelper`
  * Handles most of the conversation-related features (such as applying a visual effect to quest-givers) by listening for `"PlayerBeginConversation"` event and responding appropriately.
  * Handles other event-based features, such as the animated text when running into a wall during a screen transition ("ObjectEnteringCellBlockedBySolid" event)
  * Stores some data that needs to be serialized (for `Egcb_AbilityManagerExtender`) and includes the requisite serialization functions.

![cover image](QudUX_Cover.png)

Available for download here:

https://steamcommunity.com/sharedfiles/filedetails/?id=1804499742

# CavesOfQud-QudUX
Some minor tweaks and improvements to Qud menus/etc.

Implementation overview:
* The entry point is `Egcb_UILoader`, which is loaded as an `IPart` of the dummy object defined in `ObjectBlueprints.xml`.
* `Egcb_UILoader` initializes `Egcb_UIMonitor`, which uses the Unity `Coroutine()` and `Update()` frameworks to monitor for when the user opens the Journal or Inventory screen.
* When either of those screens are opened by the user, `Egcb_UIMonitor` instantiates a new object of type `Egcb_JournalExtender` or `Egcb_InventoryExtender`.
* `Egcb_UIMonitor` calls the `FrameCheck()` each frame on the \*Extender object, which then evaluates whether the menu screen needs to be updated with the mod's changes.
* Because all of the game's screens are internal classes and there are no events available to use to hook into UI actions, this mod works by parsing the characters on the screen while the user is in the menu, and then overwriting parts of the screen directly using the game's ScreenBuffer infrastructure.

![cover image](QudUX_Cover.png)

Available for download here:

https://steamcommunity.com/sharedfiles/filedetails/?id=1804499742

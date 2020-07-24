using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XRL.Core;
using XRL.World.Parts;
using Egocarib.Console;

namespace Egocarib.Code
{
    public class Egcb_UIMonitor : MonoBehaviour
    {
        [NonSerialized] public static Egcb_UIMonitor Instance;
        private static bool bInitialized;
        private string UIMode;
        private int waitForUIFrames = 0;
        private string UISubmode = string.Empty;
        //private bool pasteKeysPressed = false;
        private Egcb_JournalExtender JournalExtender;
        private Egcb_InventoryExtender InventoryExtender;
        private Egcb_ReviewCharExtender ReviewCharExtender;
        private Egcb_AbilityManagerExtender AbilityManagerExtender;
        private Egcb_LookTiler LookTiler;
        private Egcb_ConversationTiler ConversationTiler;
        private List<XRL.World.GameObject> gameObjsWithTrackingPart = new List<XRL.World.GameObject>();
        private XRL.World.GameObject latestGoWithTrackingPart = null;
        private const float coroutineShortYield = 0.1f;
        private const float coroutineLongYield = 0.75f;
        private readonly Dictionary<string, float> coroutineYieldTimes = new Dictionary<string, float>()
        {
            //these are the GameViews used during normal gameplay (primarily just the "Game" view). We use a longer
            //yield time for these views to reduce processing load while the player in playing. Other views (which are
            //all menu/pop-up views) use a shorter coroutine yield to allow processing stuff quickly while menus are open
            { "FireMissileWeapon" , coroutineLongYield },
            { "Game"              , coroutineLongYield },
            { "Stage"             , coroutineLongYield },
            { "Looker"            , coroutineLongYield },
            { "PickDirection"     , coroutineLongYield },
            { "PickField"         , coroutineLongYield },
            { "PickTarget"        , coroutineLongYield }
            //everything else that's not defined in here uses coroutineShortYield
        };

        public bool Initialize()
        {
            if (!Egcb_UIMonitor.bInitialized)
            {
                Egcb_UIMonitor.bInitialized = true;
                Egcb_UIMonitor.Instance = this;
                this.enabled = false;
                base.StartCoroutine(this.UIMonitorLoop());
                return true;
            }
            return false;
        }

        public static bool IsActive
        {
            get
            {
                return Egcb_UIMonitor.bInitialized;
            }
        }

        public static void DirectEnableUIMode(string uiMode, string subMode, XRL.World.GameObject obj1)
        {
            Egcb_UIMonitor.Instance.UIMode = uiMode;
            Egcb_UIMonitor.Instance.waitForUIFrames = 2; //allow a frame or two to pass until popup GameView transition occurs
            Egcb_UIMonitor.Instance.enabled = true;
            Egcb_UIMonitor.Instance.UISubmode = subMode;
            if (subMode == "LookTiler")
            {
                Egcb_UIMonitor.Instance.LookTiler = new Egcb_LookTiler(obj1);
            }
            if (subMode == "ConversationTiler")
            {
                Egcb_UIMonitor.Instance.ConversationTiler = new Egcb_ConversationTiler(obj1);
            }
        }

        private void Update() //runs only when this.enabled = true
        {
            bool popUpMode = (this.UIMode == "Popup" && (GameManager.Instance.CurrentGameView.StartsWith("Popup") || GameManager.Instance.CurrentGameView == "TwiddleObject"));
            if (!popUpMode && GameManager.Instance.CurrentGameView != this.UIMode)
            {
                if (this.waitForUIFrames > 0)
                {
                    this.waitForUIFrames--;
                    return;
                }
                this.enabled = false;
                this.JournalExtender = null;
                this.InventoryExtender = null;
                this.ReviewCharExtender = null;
                this.AbilityManagerExtender = null;
                this.LookTiler = null;
                this.ConversationTiler = null;
                this.waitForUIFrames = 0;
                this.UISubmode = string.Empty;
                if (this.UIMode == "WorldCreationProgress")
                {
                    Egcb_ReviewCharExtender.ApplyCustomTile(); //leaving world creation progress screen - apply custom tile now before player is inserted into starting village
                }
                return;
            }

            if (this.waitForUIFrames > 0)
            {
                this.waitForUIFrames = 0;
            }

            if (this.UIMode == "Inventory")
            {
                if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt) || Input.GetKeyDown(KeyCode.AltGr))
                {
                    this.InventoryExtender.ReactToAltKeyRequest();
                }
                else
                {
                    this.InventoryExtender.FrameCheck();
                }
            }
            else if (this.UIMode == "Journal")
            {
                this.JournalExtender.FrameCheck();
            }
            else if (this.UIMode == "Popup")
            {
                if (this.UISubmode == "LookTiler")
                {
                    this.LookTiler.FrameCheck();
                }
            }
            else if (this.UIMode == "Popup:AskString")
            {
                //technically this "works" except that Popup:AskString doesn't care about whatever I send to Keyboard.PushKey for some reason I haven't been able to decipher.

                //if (XRL.Rules.Stat.Random(1,150) == 1)
                //{
                //    Debug.Log("QudUX Mod: DEBUG - pushed 'f' key.");
                //    ConsoleLib.Console.Keyboard.PushKey(KeyCode.F);
                //    ConsoleLib.Console.Keyboard.vkCode = ConsoleLib.Console.Keys.F;
                //    ConsoleLib.Console.Keyboard.Char = (int)'f';
                //}
                //if (((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKey(KeyCode.V))
                //    || ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKey(KeyCode.Insert)))
                //{
                //    if (this.pasteKeysPressed)
                //    {
                //        return; //already processed this keypress
                //    }
                //    this.pasteKeysPressed = true;
                //    bool cachedInputBufferState = GameManager.bCapInputBuffer;
                //    try
                //    {
                //        string clipText = XRL.UI.CreateCharacter.ClipboardHelper.clipBoard;
                //        if (!string.IsNullOrEmpty(clipText) && clipText.Length <= 999)
                //        {
                //            StringKeyCoder codes = new StringKeyCoder(clipText);
                //            GameManager.bCapInputBuffer = false;
                //            foreach (KeyCode kc in codes.StringKeyCodes)
                //            {
                //                ConsoleLib.Console.Keyboard.PushKey(kc);
                //            }
                //            GameManager.bCapInputBuffer = cachedInputBufferState;
                //        }
                //    }
                //    catch (Exception ex)
                //    {
                //        Debug.Log("QudUX Mod: Encountered exception while trying to paste into Popup.\nException: " + ex.ToString());
                //        GameManager.bCapInputBuffer = cachedInputBufferState;
                //    }
                //}
                //else
                //{
                //    this.pasteKeysPressed = false;
                //}
            }
            else if (this.UIMode == "Conversation")
            {
                if (this.UISubmode == "ConversationTiler")
                {
                    this.ConversationTiler.FrameCheck();
                }
            }
            else if (this.UIMode == "ReviewCharacter")
            {
                if (Input.GetKeyDown(KeyCode.M))
                {
                    this.ReviewCharExtender.ReactToKeyRequest("M");
                }
                //else if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                //{
                //    this.ReviewCharExtender.ReactToKeyRequest("ENTER");
                //}
                else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.Alpha4)) //(+ include letter directional bind?) -- WOULD be better to use game's Keyboard.getvk for mappings for all of these, but I'm not sure that's possible...
                {
                    this.ReviewCharExtender.ReactToKeyRequest("LEFT");
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.Alpha6))
                {
                    this.ReviewCharExtender.ReactToKeyRequest("RIGHT");
                }
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Alpha8))
                {
                    this.ReviewCharExtender.ReactToKeyRequest("UP");
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Alpha2))
                {
                    this.ReviewCharExtender.ReactToKeyRequest("DOWN");
                }
                else
                {
                    this.ReviewCharExtender.FrameCheck();
                }
            }
        }

        //private string DEBUG_LAST_GAME_VIEW = string.Empty; //DEBUG ONLY
        private IEnumerator UIMonitorLoop()
        {
            Debug.Log("QudUX Mod: UI Monitor Activated.");
            for (;;)
            {
                while (this.enabled == true)
                {
                    yield return new WaitForSeconds(coroutineShortYield);
                }

                ////DEBUG ONLY
                //if (GameManager.Instance.CurrentGameView != this.DEBUG_LAST_GAME_VIEW)
                //{
                //    this.DEBUG_LAST_GAME_VIEW = GameManager.Instance.CurrentGameView;
                //    Debug.Log("QudUX Debug: CurrentGameView == " + GameManager.Instance.CurrentGameView);
                //}
                ////DEBUG ONLY
                try
                {
                    if (XRLCore.Core.Game?.Player?.Body != this.latestGoWithTrackingPart
                        && XRLCore.Core.Game?.Player?.Body != null
                        && !gameObjsWithTrackingPart.CleanContains(XRLCore.Core.Game.Player.Body)
                        && !XRLCore.Core.Game.Player.Body.IsNowhere()) //IsNowhere forces us to wait for player to be initialized (otherwise duplicate part won't be loaded from serialization yet and we erroneously add another)
                    {
                        XRL.World.GameObject player = XRLCore.Core.Game.Player.Body;
                        this.latestGoWithTrackingPart = player;
                        gameObjsWithTrackingPart.Add(player);
                        //add part to player (or dominated entity, whatever, etc)
                        if (!player.HasPart("Egcb_PlayerUIHelper")) //may already have the part if it was serialized on the player
                        {
                            player.AddPart<Egcb_PlayerUIHelper>(true);
                        }
                        new NalathniAppraiseConnector(); //initialize a new NalathniAppraiseExtender object to complete initial analysis and set up static values
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("QudUX Mod: Encountered exception in coroutine segment 1.\nException: " + ex.ToString() + "\nAttempting to resume...");
                }
                if (GameManager.Instance.CurrentGameView == "Inventory"
                    || GameManager.Instance.CurrentGameView == "Journal"
                    || GameManager.Instance.CurrentGameView == "ReviewCharacter"
                    || GameManager.Instance.CurrentGameView == "WorldCreationProgress"
                    || GameManager.Instance.CurrentGameView == "AbilityManager"
                    || GameManager.Instance.CurrentGameView == "Trade"
                    || GameManager.Instance.CurrentGameView == "Popup:AskString")
                {
                    this.UIMode = GameManager.Instance.CurrentGameView;
                    //TODO: should check whether the overlay inventory option is enabled, and don't do anything if it is.
                    try
                    {
                        if (this.UIMode == "Journal")
                        {
                            this.JournalExtender = new Egcb_JournalExtender();
                        }
                        else if (this.UIMode == "Inventory")
                        {
                            this.InventoryExtender = new Egcb_InventoryExtender();
                        }
                        else if (this.UIMode == "Trade")
                        {
                            Egcb_PlayerUIHelper.SetTraderInteraction();
                            //one time call - don't need to monitor the menu itself
                        }
                        else if (this.UIMode == "ReviewCharacter")
                        {
                            this.ReviewCharExtender = new Egcb_ReviewCharExtender();
                        }
                        else if (this.UIMode == "WorldCreationProgress")
                        {
                            //Do nothing here (we're only tracking for when this state is removed)
                        }
                        else if (this.UIMode == "Popup:AskString")
                        {
                            //Do nothing here - we'll listen for keypresses in Egcb_UIMonitor.Update() [not actually implemented]
                        }
                        else if (this.UIMode == "AbilityManager")
                        {
                            this.AbilityManagerExtender = new Egcb_AbilityManagerExtender();
                            this.AbilityManagerExtender.UpdateAbilityDescriptions();
                            //this is all we need to do for this one - a single update on menu open. No active monitoring/changes in the menu itself.
                        }
                        this.enabled = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("QudUX Mod: Encountered exception in coroutine segment 2.\nException: " + ex.ToString() + "\nAttempting to resume...");
                    }
                    do { yield return new WaitForSeconds(coroutineShortYield); } while (this.enabled == true);
                }
                else
                {
                    if (!this.coroutineYieldTimes.ContainsKey(GameManager.Instance.CurrentGameView))
                    {
                        this.coroutineYieldTimes.Add(GameManager.Instance.CurrentGameView, coroutineShortYield);
                    }
                    yield return new WaitForSeconds(this.coroutineYieldTimes[GameManager.Instance.CurrentGameView]);
                }
            }
        }
    }
}
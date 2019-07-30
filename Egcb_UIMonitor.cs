using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XRL.Core;
using XRL.World.Parts;

namespace Egocarib.Code
{
    public class Egcb_UIMonitor : MonoBehaviour
    {
        [NonSerialized] public static Egcb_UIMonitor Instance;
        private static bool bInitialized;
        private string UIMode;
        private Egcb_JournalExtender JournalExtender;
        private Egcb_InventoryExtender InventoryExtender;
        private Egcb_ReviewCharExtender ReviewCharExtender;
        private List<XRL.World.GameObject> gameObjsWithTrackingPart = new List<XRL.World.GameObject>();
        private XRL.World.GameObject latestGoWithTrackingPart = null;
        private readonly Dictionary<string, float> coroutineYieldTimes = new Dictionary<string, float>()
        {
            //full list of GameViews used from main menu through character creation
            //we use a value of 0.1 seconds during the main menus to ensure that Sprite option appears in a timely manner
            //once the game starts, the coroutine frequency slows down to 1 second to reduce processing load
            { "MainMenu"              , 0.1f },
            { "NewGame"               , 0.1f },
            { "PickGameType"          , 0.1f },
            { "PickGenotype"          , 0.1f },
            { "PickStatistics"        , 0.1f },
            { "PickMutations"         , 0.1f },
            { "PickSubtype"           , 0.1f },
            { "PickCybernetics"       , 0.1f },
            { "ReviewCharacter"       , 0.1f },
            { "Embark"                , 0.1f },
            { "WorldCreationProgress" , 0.1f },
            { "Popup:AskString"       , 0.1f },
            { "Popup:MessageBox"      , 0.1f }
            //everything else that's not defined in here (i.e. the main game) is treated as 1.0f
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

        private void Update() //runs only when this.enabled = true
        {
            if (GameManager.Instance.CurrentGameView != this.UIMode)
            {
                this.enabled = false;
                this.JournalExtender = null;
                this.InventoryExtender = null;
                this.ReviewCharExtender = null;
                if (this.UIMode == "WorldCreationProgress")
                {
                    Egcb_ReviewCharExtender.ApplyCustomTile(); //leaving world creation progress screen - apply custom tile now before player is inserted into starting village
                }
                return;
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

        private IEnumerator UIMonitorLoop()
        {
            Debug.Log("QudUX Mod: UI Monitor Activated.");
            for (;;)
            {
                if (XRLCore.Core.Game?.Player?.Body != this.latestGoWithTrackingPart && XRLCore.Core.Game?.Player?.Body != null && !gameObjsWithTrackingPart.CleanContains(XRLCore.Core.Game.Player.Body))
                {
                    XRL.World.GameObject player = XRLCore.Core.Game.Player.Body;
                    this.latestGoWithTrackingPart = player;
                    gameObjsWithTrackingPart.Add(player);
                    //add part to player (or dominated entity, whatever, etc)
                    if (!player.HasPart("Egcb_PlayerUIHelper")) //may already have the part if it was serialized on the player
                    {
                        player.AddPart<Egcb_PlayerUIHelper>(true);
                    }
                    NalathniAppraiseConnector appraiseLoader = new NalathniAppraiseConnector(); //initialize a new NalathniAppraiseExtender object to complete initial analysis and set up static values
                }
                if (GameManager.Instance.CurrentGameView == "Inventory"
                    || GameManager.Instance.CurrentGameView == "Journal"
                    || GameManager.Instance.CurrentGameView == "ReviewCharacter"
                    || GameManager.Instance.CurrentGameView == "WorldCreationProgress")
                {
                    this.UIMode = GameManager.Instance.CurrentGameView;
                    //TODO: should check whether the overlay inventory option is enabled, and don't do anything if it is.
                    if (this.UIMode == "Journal")
                    {
                        this.JournalExtender = new Egcb_JournalExtender();
                    }
                    else if (this.UIMode == "Inventory")
                    {
                        this.InventoryExtender = new Egcb_InventoryExtender();
                    }
                    else if (this.UIMode == "ReviewCharacter")
                    {
                        this.ReviewCharExtender = new Egcb_ReviewCharExtender();
                    }
                    else if (this.UIMode == "WorldCreationProgress")
                    {
                        //Do nothing here (we're only tracking for when this state is removed)
                    }
                    this.enabled = true;
                    do { yield return new WaitForSeconds(0.1f); } while (this.enabled == true);
                }
                if (!this.coroutineYieldTimes.ContainsKey(GameManager.Instance.CurrentGameView))
                {
                    this.coroutineYieldTimes.Add(GameManager.Instance.CurrentGameView, 1f);
                }
                yield return new WaitForSeconds(this.coroutineYieldTimes[GameManager.Instance.CurrentGameView]);
            }
        }
    }
}
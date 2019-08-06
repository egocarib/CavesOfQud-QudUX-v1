using UnityEngine;
using System.Collections.Generic;
using Qud.API;
using XRL.Language;
using XRL.Core;
using XRL.Rules;
using XRL.World.Parts.Effects;
using XRL.UI;
using System;
using System.Text.RegularExpressions;
using XRL.World.Encounters.EncounterObjectBuilders;

namespace XRL.World.Parts
{
    [Serializable]
    public class Egcb_PlayerUIHelper : IPart
    {
        public static GameObject PlayerBody = null;
        public static GameObject ConversationPartner = null;
        private GameObject LookTarget = null;
        public static List<GameObject> NewQuestHolders = new List<GameObject>();
        public static List<GameObject> ActiveQuestHolders = new List<GameObject>();
        public static List<GameObject> ZoneTradersTradedWith = new List<GameObject>();
        public static string CurrentInteractionZoneID = string.Empty;
        public Dictionary<Guid, string> DefaultAbilityDescriptions = null;
        private string _colorMatchPattern = string.Empty;

        public string ColorMatchPattern
        {
            get
            {
                if (string.IsNullOrEmpty(this._colorMatchPattern))
                {
                    this._colorMatchPattern = "&[";
                    foreach (KeyValuePair<char, ushort> color in ConsoleLib.Console.ColorUtility.CharToColorMap)
                    {
                        this._colorMatchPattern += color.Key;
                    }
                    this._colorMatchPattern += "]";
                }
                return this._colorMatchPattern;
            }
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        public override void SaveData(SerializationWriter Writer)
        {
            base.SaveData(Writer);
            Writer.Write(this.DefaultAbilityDescriptions == null ? 0 : this.DefaultAbilityDescriptions.Count);
            if (this.DefaultAbilityDescriptions != null)
            {
                foreach (KeyValuePair<Guid, string> kv in this.DefaultAbilityDescriptions)
                {
                    Writer.Write(kv.Key);
                    Writer.Write(kv.Value);
                }
            }
        }

        // Load data is called when loading the save game, we also need to override this
        public override void LoadData(SerializationReader Reader)
        {
            base.LoadData(Reader);
            int count = Reader.ReadInt32();
            if (count <= 0)
            {
                this.DefaultAbilityDescriptions = null;
                return;
            }
            this.DefaultAbilityDescriptions = new Dictionary<Guid, string>();
            for (int i = 0; i < count; i++)
            {
                Guid guid = Reader.ReadGuid();
                string description = Reader.ReadString();
                this.DefaultAbilityDescriptions.Add(guid, description);
            }
        }

        public override void Register(GameObject Object)
        {
            Object.RegisterPartEvent(this, "PlayerBeginConversation");
            Object.RegisterPartEvent(this, "ObjectEnteringCellBlockedBySolid");
            Object.RegisterPartEvent(this, "OwnerGetInventoryActions");
            base.Register(Object);
        }

        public override bool FireEvent(Event E)
        {

            if (E.ID == "OwnerGetInventoryActions")
            {
                //EventParameterGetInventoryActions actionList = E.GetParameter("Actions") as EventParameterGetInventoryActions;
                GameObject targetObject = E.GetGameObjectParameter("Object");
                //Debug.Log("QudUX Debug: OwnerGetInventoryActions\n    object = " + targetObject.DisplayName);
                //Debug.Log("QudUX Debug: Attempting to enable Popup UIMode...");
                Egocarib.Code.Egcb_UIMonitor.DirectEnableUIMode("Popup", "LookTiler", targetObject); //enable popup mode to draw tiles while this item's popup menus (interact / description) are open
            }

            if (E.ID == "PlayerBeginConversation")
            {
                Egcb_PlayerUIHelper.PlayerBody = XRLCore.Core.Game.Player.Body;
                GameObject speaker = E.GetGameObjectParameter("Speaker");
                Egcb_PlayerUIHelper.ConversationPartner = speaker;
                if (Egcb_PlayerUIHelper.CurrentInteractionZoneID != speaker.CurrentCell.ParentZone.ZoneID)
                {
                    Egcb_PlayerUIHelper.ZoneTradersTradedWith.Clear();
                    Egcb_PlayerUIHelper.CurrentInteractionZoneID = speaker.CurrentCell.ParentZone.ZoneID;
                }
                string questID = speaker.GetStringProperty("GivesDynamicQuest", string.Empty);
                Conversation convo = E.GetParameter<Conversation>("Conversation");
                if (speaker.HasPart("GenericInventoryRestocker") || speaker.HasPart("Restocker"))
                {
                    try
                    {
                        Egcb_PlayerUIHelper.AddChoiceToRestockers(convo, speaker);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("QudUX Mod: Encountered exception while adding conversation choice to merchant to ask about restock duration.\nException details: \n" + ex.ToString());
                    }
                }
                if (questID == string.Empty || XRLCore.Core.Game.FinishedQuests.ContainsKey(questID)) //speaker has no dynamic quests
                {
                    try
                    {
                        this.AddChoiceToIdentifyQuestGivers(convo, speaker);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("QudUX Mod: Encountered exception while adding conversation choices to identify village quest givers.\nException details: \n" + ex.ToString());
                    }
                }
                else //speaker does have dynamic quest
                {
                    try
                    {
                        this.UpdateForgottenRuinQuestAndConversationText(convo, questID);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("QudUX Mod: Encountered exception while translating quest/conversation references to 'some forgotten ruins'.\nException details: \n" + ex.ToString());
                    }
                }
                Egocarib.Code.Egcb_UIMonitor.DirectEnableUIMode("Conversation", "ConversationTiler", speaker); //enable conversation mode to draw tiles while the conversationUI is open
            }
            else if (E.ID == "ObjectEnteringCellBlockedBySolid")
            {
                GameObject barrier = E.GetGameObjectParameter("Object");
                if (barrier.CurrentCell.ParentZone.ZoneID != this.ParentObject.CurrentCell.ParentZone.ZoneID) //ran into a wall or object in another zone while trying to leave the zone
                {
                    Cell centerCell = this.ParentObject.CurrentCell.ParentZone.GetCell(40, 12);
                    string direction = this.ParentObject.CurrentCell.GetDirectionFromCell(centerCell);
                    Cell effectOriginCell = this.ParentObject.CurrentCell.GetCellFromDirection(direction, true) ?? this.ParentObject.CurrentCell;
                    int effectX = effectOriginCell.X;
                    int effectY = effectOriginCell.Y;
                    int rand = Stat.Random(1, 3);
                    string text;
                    if (rand == 1)
                    {
                        text = "&wOUCH!";
                    }
                    else if (rand == 2)
                    {
                        text = barrier.IsWall() ? "&wWall!" : "&wBlocked!";
                    }
                    else
                    {
                        text = "&wBlocked!";
                    }
                    //when moving eastward into a new cell, most of the phrase is cut off the edge of the screen, so we need to shift it left to accomodate the string size
                    int phraseLength = ConsoleLib.Console.ColorUtility.StripFormatting(text).Length;
                    if ((80 - effectX) < phraseLength)
                    {
                        effectX = Math.Max(10, 80 - phraseLength);
                    }
                    float num = (float)this.GetDegreesForVisualEffect(direction) / 58f;
                    float xDel = (float)Math.Sin((double)num) / 4f;
                    float yDel = (float)Math.Cos((double)num) / 4f;
                    XRLCore.ParticleManager.Add(text, (float)effectX, (float)effectY, xDel, yDel, 22, 0f, 0f);
                }
            }
            return base.FireEvent(E);
        }

        public static void SetTraderInteraction()
        {
            Egcb_PlayerUIHelper.ZoneTradersTradedWith.Add(Egcb_PlayerUIHelper.ConversationPartner);
            Egcb_PlayerUIHelper.AddChoiceToRestockers();
        }

        public override bool BeforeRender(Event E)
        {
            //This function updates MessageQueue messages so that colors appear appropriately in the Sidebar message log (and the fullscreen message log)
            //It works by applying color to every word so that linebreaks don't reset words to the default color.
            if (XRLCore.Core.Game.Player.Messages.Cache_0_12Valid)
            {
                //do nothing if cache is valid
            }
            else
            {
                try
                {
                    //update the most recent 12 lines in the MessageQueue to ensure that color gets applied to every word.
                    List<string> messages = XRLCore.Core.Game.Player.Messages.Messages;
                    for (int i = Math.Max(messages.Count - 12, 0); i < messages.Count; i++)
                    {
                        string message = messages[i];
                        if (!message.Contains("&"))
                        {
                            continue;
                        }
                        string newMessage = string.Empty;
                        string colorString = string.Empty;
                        //iterate through words split by spaces. Technically this doesn't account properly for linebreak '\n' characters in a
                        //message, but those are rare and every example I've seen uses the default &y at the beginning of the new line anyway
                        foreach (string word in message.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (!string.IsNullOrEmpty(newMessage))
                            {
                                newMessage += " ";
                            }
                            string newWord = word;
                            if (!newWord.Contains("&"))
                            {
                                newWord = colorString + newWord;
                            }
                            else
                            {
                                if (newWord.IndexOf("&") != 0)
                                {
                                    newWord = colorString + newWord;
                                }
                                Match colorMatch = Regex.Match(word, this.ColorMatchPattern, RegexOptions.RightToLeft);
                                if (colorMatch.Success)
                                {
                                    colorString = colorMatch.Value;
                                }
                            }
                            newMessage += newWord;
                        }
                        messages[i] = newMessage;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("QudUX Mod: Error trying to update color formatting in message log. (Exception: " + ex.ToString() + ")");
                }
            }
            return base.BeforeRender(E);
        }

        public static bool AddChoiceToRestockers(Conversation convo = null, GameObject speaker = null)
        {
            if (speaker == null)
            {
                if (Egcb_PlayerUIHelper.ConversationPartner == null)
                {
                    return false;
                }
                speaker = Egcb_PlayerUIHelper.ConversationPartner;
                convo = speaker.GetPart<ConversationScript>().customConversation;
                if (convo == null)
                {
                    return false;
                }
            }

            //you must view a trader's goods before the new conversation options become available.
            if (!Egcb_PlayerUIHelper.ZoneTradersTradedWith.Contains(speaker))
            {
                return false;
            }

            //clean up old versions of the conversation if they exist
            if (convo.NodesByID.ContainsKey("*Egcb_RestockDiscussionNode"))
            {
                convo.NodesByID.Remove("*Egcb_RestockDiscussionNode");
                if (convo.NodesByID.ContainsKey("Start"))
                {
                    for (int i = 0; i < convo.NodesByID["Start"].Choices.Count; i++)
                    {
                        if (convo.NodesByID["Start"].Choices[i].ID == "*Egcb_RestockerDiscussionStartChoice")
                        {
                            convo.NodesByID["Start"].Choices.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            long ticksRemaining;
            bool bChanceBasedRestock = false;
            if (speaker.HasPart("Restocker"))
            {
                Restocker r = speaker.GetPart<Restocker>();
                ticksRemaining = r.NextRestockTick - ZoneManager.Ticker;
            }
            else if (speaker.HasPart("GenericInventoryRestocker"))
            {
                GenericInventoryRestocker r = speaker.GetPart<GenericInventoryRestocker>();
                ticksRemaining = r.RestockFrequency - (ZoneManager.Ticker - r.LastRestockTick);
                bChanceBasedRestock = true;
            }
            else
            {
                return false;
            }

            //build some dialog based on the time until restock and related parameters. TraderDialogGenData ensures the dialog options
            //stay the same for a single trader during the entire time that trader is waiting for restock
            TraderDialogGenData dialogGen = TraderDialogGenData.GetData(speaker, ticksRemaining);
            double daysTillRestock = (double)ticksRemaining / 1200.0;
            string restockDialog;
            if (daysTillRestock >= 9)
            {
                if (speaker.Blueprint == "Sparafucile")
                {
                    restockDialog = "\n&w*Sparafucile pokes at a few of =pronouns.possessive= wares and then gazes up at you, squinting, as if to question the basis of your inquiry.*&y\n ";
                }
                else
                {
                    restockDialog = (dialogGen.Random2 == 1)
                        ? "Business is booming, friend.\n\nI'm pretty satisfied with what I've got for sale right now; maybe you should look for another "
                            + "vendor if you need something I'm not offering. I'll think about acquiring more goods eventually, but it won't be anytime soon."
                        : "Don't see anything that catches your eye?\n\nWell, you're in the minority. My latest shipment has been selling well and "
                            + "it'll be a while before I think about rotating my stock.";
                }
            }
            else
            {
                if (speaker.Blueprint == "Sparafucile")
                {
                    if (daysTillRestock < 0.5)
                    {
                        restockDialog = "\n&w*Sparafucile nods eagerly, as if to convey that =pronouns.subjective= is expecting something very soon.*&y\n ";
                    }
                    else
                    {
                        restockDialog = "\n&w*Smiling, Sparafucile gives a slight nod.*&y\n\n"
                            + "&w*=pronouns.Subjective= purses =pronouns.possessive= lips thoughtfully for a moment, then raises " + Math.Max(1, (int)daysTillRestock) + " thin fingers.*&y\n ";
                    }
                }
                else
                {
                    string daysTillRestockPhrase = (daysTillRestock < 0.5) ? "in a matter of hours"
                                : (daysTillRestock < 1) ? "by this time tomorrow"
                                : (daysTillRestock < 1.8) ? "within a day or two"
                                : (daysTillRestock < 2.5) ? "in about two days' time"
                                : (daysTillRestock < 3.5) ? "in about three days' time"
                                : (daysTillRestock < 5.5) ? "in four or five days"
                                : "in about a week, give or take";
                    string pronounObj = (dialogGen.Random3 == 1 ? "him" : (dialogGen.Random3 == 2 ? "her" : "them"));
                    string pronounSubj = (dialogGen.Random3 == 1 ? "he" : (dialogGen.Random3 == 2 ? "she" : "they"));
                    restockDialog =
                          (dialogGen.Random4 == 1) ? "There are rumors of a well-stocked dromad caravan moving through the area.\n\nMy sources tell me the caravan "
                                                + "should be passing through " + daysTillRestockPhrase + ". I'll likely able to pick up some new trinkets at that time."
                                                + (bChanceBasedRestock ? "\n\nOf course, they are only rumors, and dromads tend to wander. I can't make any guarantees." : string.Empty)
                        : (dialogGen.Random4 == 2) ? "My friend, a water baron is coming to visit this area soon. I sent " + pronounObj + " a list of my requests and should "
                                                + "have some new stock available after " + pronounSubj + " arrive" + (pronounSubj == "they" ? "" : "s") + ".\n\n"
                                                + "By the movements of the Beetle Moon, I predict " + pronounSubj + " should be here " + daysTillRestockPhrase + "."
                                                + (bChanceBasedRestock ? "\n\nIn honesty, though, " + pronounSubj + (pronounSubj == "they" ? " are" : " is") + " not the most "
                                                + "reliable friend. I can't make any guarantees." : string.Empty)
                        : (dialogGen.Random4 == 3) ? "It just so happens my apprentice has come upon a new source of inventory, and is negotiating with the merchant in a "
                                                + "nearby village.\n\nThose talks should wrap up soon and I expect to have some new stock " + daysTillRestockPhrase + "."
                                                + (bChanceBasedRestock ? "\n\nOf course, negotiations run like water through the salt. I can't make any guarantees." : string.Empty)
                        : "I'm glad you asked, friend. Arconauts have been coming in droves from a nearby ruin that was recently unearthed. "
                                                + "They've been selling me trinkets faster than I can sort them, to be honest. After I manage to get things organized "
                                                + "I'll have more inventory to offer.\n\nCheck back with me " + daysTillRestockPhrase + ", and I'll show you what I've got."
                                                + (bChanceBasedRestock ? "\n\nThat is... assuming any of the junk is actually resellable. I can't make any guarantees." : string.Empty);
                }
            }

            /* //DEBUG ONLY

            string sDEBUG = string.Empty;
            if (speaker.HasPart("GenericInventoryRestocker"))
            {
                sDEBUG += "\n    *GenericInventoryRestocker";
                sDEBUG += "\n        IsPlayerLed()? = " + speaker.IsPlayerLed();
                GenericInventoryRestocker r = speaker.GetPart<GenericInventoryRestocker>();
                long countdown = r.RestockFrequency - (ZoneManager.Ticker - r.LastRestockTick);
                sDEBUG += "\n        " + countdown + " ticks until restock (" + Math.Round((double)countdown / 1200.0, 2) + " days)";
            }
            if (speaker.HasPart("Restocker"))
            {
                sDEBUG += "\n    *Restocker";
                sDEBUG += "\n        IsPlayerLed()? = " + speaker.IsPlayerLed();
                Restocker r = speaker.GetPart<Restocker>();
                long countdown = r.NextRestockTick - ZoneManager.Ticker;
                sDEBUG += "\n        " + countdown + " ticks until restock (" + Math.Round((double)countdown / 1200.0, 2) + " days)";
            }
            if (!string.IsNullOrEmpty(sDEBUG))
            {
                sDEBUG = "Restocker data:" + sDEBUG;
                Debug.Log("QudUX Mod: " + sDEBUG);
            }

            //DEBUG ONLY */

            //add options to ask location of quest givers for whom the quest has already started
            if (convo.NodesByID.ContainsKey("Start"))
            {
                //create node with info about trading
                string restockNodeID = "*Egcb_RestockDiscussionNode";
                ConversationNode restockNode = ConversationsAPI.newNode(restockDialog, restockNodeID);
                restockNode.AddChoice("I have more to ask.", "Start", null);
                restockNode.AddChoice("Live and drink.", "End", null);
                convo.AddNode(restockNode);
                ConversationNode startNode = convo.NodesByID["Start"];
                int rand = Stat.Random(1, 3);
                ConversationChoice askRestockChoice = new ConversationChoice
                {
                    ID = "*Egcb_RestockerDiscussionStartChoice",
                    Text = (rand == 1) ? "Any new wares on the way?"
                        : (rand == 2) ? "Do you have anything else to sell?"
                        : "Can you let me know if you get any new stock?",
                    GotoID = restockNodeID,
                    ParentNode = startNode,
                    Ordinal = 991 //set to make this appear immediately after the trade option
                };
                startNode.Choices.Add(askRestockChoice);
                startNode.SortEndChoicesToEnd();
            }
            return true;
        }

        public bool AddChoiceToIdentifyQuestGivers(Conversation convo, GameObject speaker)
        {
            NewQuestHolders.Clear();
            ActiveQuestHolders.Clear();

            //determine which quest givers are in the area, using similar logic to DynamicQuestSignpostConversation.cs
            foreach (GameObject go in speaker.CurrentCell.ParentZone.GetObjectsWithProperty("GivesDynamicQuest"))
            {
                if (go != speaker && !go.HasEffect("Egcb_QuestGiverVision"))
                {
                    string questID = go.GetStringProperty("GivesDynamicQuest", null);
                    if (questID != null)
                    {
                        if (!XRLCore.Core.Game.HasQuest(questID))
                        {
                            NewQuestHolders.Add(go);
                        }
                        else if (!XRLCore.Core.Game.FinishedQuests.ContainsKey(questID))
                        {
                            ActiveQuestHolders.Add(go);
                        }
                    }
                }
            }
            if ((NewQuestHolders.Count + ActiveQuestHolders.Count) < 1) //No quest givers
            {
                return false;
            }

            //add options to ask location of quest givers for whom the quest has already started
            if (ActiveQuestHolders.Count > 0 && convo.NodesByID.ContainsKey("Start"))
            {
                string nameList = this.BuildQuestGiverNameList(ActiveQuestHolders);
                ConversationNode cNode = convo.NodesByID["Start"];
                this.RemoveOldEgcbChoices(cNode);
                ConversationChoice cChoice = new ConversationChoice
                {
                    Text = this.StatementLocationOf(nameList),
                    GotoID = "End",
                    ParentNode = cNode,
                    Execute = "XRL.World.Parts.Egcb_PlayerUIHelper:ApplyActiveQuestGiverEffect" //function to execute when this choice is selected.
                };
                cNode.Choices.Add(cChoice);
            }
            if (NewQuestHolders.Count > 0 && convo.NodesByID.ContainsKey("*DynamicQuestSignpostConversationIntro"))
            {
                string nameList = this.BuildQuestGiverNameList(NewQuestHolders);
                ConversationNode cNode = convo.NodesByID["*DynamicQuestSignpostConversationIntro"];
                this.RemoveOldEgcbChoices(cNode);
                ConversationChoice cChoice = new ConversationChoice
                {
                    Text = this.QuestionLocationOf(nameList, NewQuestHolders.Count > 1),
                    GotoID = "End",
                    ParentNode = cNode,
                    Execute = "XRL.World.Parts.Egcb_PlayerUIHelper:ApplyNewQuestGiverEffect" //function to execute when this choice is selected.
                };
                cNode.Choices.Add(cChoice);
            }
            return true;
        }

        public void RemoveOldEgcbChoices(ConversationNode cNode)
        {
            if (cNode == null || cNode.Choices == null)
            {
                return;
            }
            for (int i = cNode.Choices.Count - 1; i >= 0; i--)
            {
                ConversationChoice cChoice = cNode.Choices[i];
                if (cChoice != null && cChoice.Execute != null && cChoice.Execute.Contains(":"))
                {
                    string executeType = cChoice.Execute.Split(':')[0];
                    if (executeType == "XRL.World.Parts.Egcb_PlayerUIHelper")
                    {
                        cNode.Choices.RemoveAt(i);
                    }
                }
            }
        }

        public string StatementLocationOf(string nameList)
        {
            return "I'm looking for " + nameList + ".";
        }
        public string QuestionLocationOf(string nameList, bool multiple)
        {
            int randVal = Stat.Random(1, 3);
            string qText = randVal == 1 ? "How can I find " + nameList + "?"
                         : randVal == 2 ? "Can you help me track down " + nameList + "?"
                         : "Do you know where " + nameList + (multiple ? " are" : " is") + " located?";
            return qText;
        }

        public string BuildQuestGiverNameList(List<GameObject> questGiverList, string conjunction = "or")
        {
            //build quest giver name list
            string nameList = string.Empty;
            for (int i = 0; i < questGiverList.Count; i++)
            {
                if (i > 0)
                {
                    nameList += (i == questGiverList.Count - 1) ? (" " + conjunction + " ") : ", ";
                }
                nameList += Grammar.ShortenName(questGiverList[i].DisplayNameOnly);
            }
            return ConsoleLib.Console.ColorUtility.StripFormatting(nameList);
        }

        public static bool ApplyNewQuestGiverEffect()
        {
            //Debug.Log("QudUX Mod: ApplyNewQuestGiverEffect()");
            return ApplyQuestGiverEffect(Egcb_PlayerUIHelper.NewQuestHolders);
        }
        public static bool ApplyActiveQuestGiverEffect()
        {
            //Debug.Log("QudUX Mod: ApplyActiveQuestGiverEffect()");
            return ApplyQuestGiverEffect(Egcb_PlayerUIHelper.ActiveQuestHolders);
        }

        public static bool ApplyQuestGiverEffect(List<GameObject> QuestGivers)
        {
            //Debug.Log("QudUX Mod: ApplyQuestGiverEffect() [ConversationPartner is null? = " + (Egcb_PlayerUIHelper.ConversationPartner == null) + "   Playerbody is null? = " + (Egcb_PlayerUIHelper.PlayerBody == null) + "   Playerbody == XRL body? = " + (Egcb_PlayerUIHelper.PlayerBody == XRLCore.Core.Game.Player.Body) + "]");
            if (Egcb_PlayerUIHelper.ConversationPartner != null)
            {
                int randNum = Stat.Random(1, 3);
                Popup.Show(Egcb_PlayerUIHelper.ConversationPartner.The
                          + Egcb_PlayerUIHelper.ConversationPartner.DisplayNameOnly  + "&y "
                          + ((randNum == 1) ? "points you in the right direction."
                            : (randNum == 2) ? "gives you a rough layout of the area."
                            : "gestures disinterestedly, sending you on your way.") );
            }
            if (Egcb_PlayerUIHelper.PlayerBody != null && Egcb_PlayerUIHelper.PlayerBody == XRLCore.Core.Game.Player.Body)
            {
                string playerZoneID = Egcb_PlayerUIHelper.PlayerBody.CurrentCell.ParentZone.ZoneID;
                foreach (GameObject questGiver in QuestGivers)
                {
                    if (questGiver.CurrentCell.ParentZone.ZoneID == playerZoneID)
                    {
                        if (questGiver.HasEffect("Egcb_QuestGiverVision"))
                        {
                            questGiver.RemoveEffect("Egcb_QuestGiverVision");
                        }
                        questGiver.ApplyEffect(new Egcb_QuestGiverVision(Egcb_PlayerUIHelper.PlayerBody));
                    }
                }
            }
            return true;
        }

        public bool UpdateForgottenRuinQuestAndConversationText(Conversation convo, string questID)
        {
            //find any nodes that mention "[Ss]ome forgotten ruins" - if the ConversationNode.Text doesn't mention it,
            //no ConversationChoice in that node will mention it either, so we can skip those nodes.
            Dictionary<string, ConversationNode> cNodes = new Dictionary<string, ConversationNode>();
            foreach (KeyValuePair<string, ConversationNode> cNodeDef in convo.NodesByID)
            {
                if (cNodeDef.Value.Text != null && cNodeDef.Value.Text.Contains("ome forgotten ruins"))
                {
                    cNodes.Add(cNodeDef.Key, cNodeDef.Value);
                }
            }
            foreach (ConversationNode cNode in convo.StartNodes)
            {
                if (cNode.Text != null && cNode.Text.Contains("ome forgotten ruins") && !cNodes.ContainsKey(cNode.ID))
                {
                    cNodes.Add(cNode.ID, cNode);
                }
            }
            if (cNodes.Count <= 0)
            {
                return false;
            }

            //for each node that mentions "some forgotten ruins", identify the quest target location and the landmark location
            //all dynamic quests should have a landmark location. Target location is only for "find a location" type quests
            string targetLocationID = string.Empty;
            string landmarkLocationID = string.Empty;
            foreach (KeyValuePair<string, ConversationNode> cNodeDef in cNodes)
            {
                ConversationNode cNode = cNodeDef.Value;
                foreach (ConversationChoice cChoice in cNode.Choices)
                {
                    if (targetLocationID == string.Empty && cChoice.SpecialRequirement != null && (cChoice.SpecialRequirement.StartsWith("IsMapNoteRevealed:") || cChoice.SpecialRequirement.StartsWith("!IsMapNoteRevealed:")))
                    {
                        targetLocationID = cChoice.SpecialRequirement.Split(':')[1];
                    }
                    if (landmarkLocationID == string.Empty && cChoice.RevealMapNoteId != null && cChoice.RevealMapNoteId.Length > 0)
                    {
                        landmarkLocationID = cChoice.RevealMapNoteId;
                    }
                }
                if (targetLocationID != string.Empty && landmarkLocationID != string.Empty)
                {
                    break;
                }
            }

            //retrieve JournalMapNote data for locations
            JournalMapNote targetLocation = (targetLocationID == string.Empty) ? null : JournalAPI.GetMapNote(targetLocationID);
            JournalMapNote landmarkLocation = JournalAPI.GetMapNote(landmarkLocationID);
            if ((targetLocation != null && targetLocation.text == "some forgotten ruins") || landmarkLocation == null || landmarkLocation.text == "some forgotten ruins")
            {
                Debug.Log("QudUX Mod: Error interpreting quest location text."
                         + "\n    targetLocation = " + (targetLocation == null ? "<NULL>" : targetLocation.text)
                         + "\n    landmarkLocation = " + (landmarkLocation == null ? "<NULL>" : landmarkLocation.text));
                return false;
            }

            //update all mentions of "some forgotten ruins" in the entire conversation node & associated conversation choices
            foreach (KeyValuePair<string, ConversationNode> cNodeDef in cNodes)
            {
                while (cNodeDef.Value.Text.Contains("place, some forgotten ruins"))
                {
                    if (targetLocation == null)
                    {
                        Debug.Log("QudUX Mod: Error updating ConversationNode text - target location for quest is undefined");
                        break;
                    }
                    else
                    {
                        cNodeDef.Value.Text = cNodeDef.Value.Text.Replace("place, some forgotten ruins", "place, " + targetLocation.text); //this is the only phrase where target location appears in the ConversationNode
                    }
                }
                while (cNodeDef.Value.Text.Contains("some forgotten ruins"))
                {
                    cNodeDef.Value.Text = cNodeDef.Value.Text.Replace("some forgotten ruins", landmarkLocation.text); //Other instances of "some forgotten ruins" in ConversationNode always refer to the landmark location
                }
                while (cNodeDef.Value.Text.Contains("Some forgotten ruins"))
                {
                    cNodeDef.Value.Text = cNodeDef.Value.Text.Replace("Some forgotten ruins", Grammar.InitCap(landmarkLocation.text));
                }
                foreach (ConversationChoice cChoice in cNodeDef.Value.Choices)
                {
                    while (cChoice.Text.Contains("some forgotten ruins"))
                    {
                        if (targetLocation == null)
                        {
                            Debug.Log("QudUX Mod: Error updating ConversationChoice text - target location for quest is undefined");
                            break;
                        }
                        else
                        {
                            cChoice.Text = cChoice.Text.Replace("some forgotten ruins", targetLocation.text); //instances of "some forgotten ruins" in ConversationChoice.Text always refer to the target location
                        }
                    }
                }
            }

            //update all references to "some forgotten ruins" in associated QuestStep descriptions
            Quest quest = null;
            foreach (KeyValuePair<string, Quest> qDef in XRLCore.Core.Game.Quests)
            {
                if (qDef.Value.ID == questID)
                {
                    quest = qDef.Value;
                    break;
                }
            }
            if (quest == null)
            {
                Debug.Log("QudUX Mod: unexpectedly failed to load quest for a conversation: questID = " + questID);
                return false;
            }
            foreach (KeyValuePair<string, QuestStep> qStepDef in quest.StepsByID)
            {
                QuestStep qStep = qStepDef.Value;
                if (qStep.Name != null && qStep.Name.ToLower().Contains("some forgotten ruins"))
                {
                    string oldName = qStep.Name;
                    int startPos = qStep.Name.ToLower().IndexOf("some forgotten ruins");
                    int length = 20; //length of "some forgotten ruins" string
                    qStep.Name = qStep.Name.Remove(startPos, length);
                    qStep.Name = qStep.Name.Insert(startPos, Grammar.MakeTitleCase(ConsoleLib.Console.ColorUtility.StripFormatting(targetLocation.text))); //QuestStep.Name always refers to the target location
                    Debug.Log("QudUX Mod: updating quest name:`n    OLD => " + oldName + "\n    NEW => " + qStep.Name);
                }
                if (qStep.Text != null && qStep.Text.ToLower().Contains("some forgotten ruins"))
                {
                    string oldText = qStep.Text;
                    if (qStep.Text.Substring(0, 7) == "Locate ")
                    {
                        if (qStep.Text.Contains(", located")) //QuestStep.Text format:   "Locate TARGET_LOCATION, located ... LANDMARK_LOCATION."
                        {
                            int startPos = 7; //starting position of targetLocation
                            int midPos = qStep.Text.IndexOf(", located "); //position of first character after targetLocation
                            int length = midPos - startPos;
                            if (qStep.Text.Substring(7, length).ToLower().Contains("some forgotten ruins"))
                            {
                                //replace target location
                                qStep.Text = qStep.Text.Remove(startPos, length);
                                qStep.Text = qStep.Text.Insert(startPos, targetLocation.text + "&y");
                            }
                            if (qStep.Text.Substring(midPos + 10).ToLower().Contains("some forgotten ruins"))
                            {
                                //replace landmark location
                                startPos = qStep.Text.ToLower().IndexOf("some forgotten ruins");
                                length = 20; //length of "some forgotten ruins"
                                qStep.Text = qStep.Text.Remove(startPos, length);
                                qStep.Text = qStep.Text.Insert(startPos, landmarkLocation.text + "&y");
                            }
                        }
                        else //QuestStep.Text format:   "Locate ... at LANDMARK_LOCATION."
                        {
                            qStep.Text = qStep.Text.Replace(" at some forgotten ruins.", " at " + landmarkLocation.text + ".");
                        }
                    }
                    else //QuestStep.Text format:   "Travel to LANDMARK_LOCATION and ...."
                    {
                        qStep.Text = qStep.Text.Replace("Travel to some forgotten ruins", "Travel to " + landmarkLocation.text);
                    }
                    Debug.Log("QudUX Mod: updating quest text:`n    OLD => " + oldText + "\n    NEW => " + qStep.Text);
                }
            }
            return true;
        }

        private int GetDegreesForVisualEffect(string direction, int degreeVariance = 30)
        {
            int startDegree = direction == "S" ? 0
                            : direction == "SW" ? 315
                            : direction == "W" ? 270
                            : direction == "NW" ? 225
                            : direction == "N" ? 180
                            : direction == "NE" ? 135
                            : direction == "E" ? 90
                            : direction == "SE" ? 45
                            : (degreeVariance = 359); //random if direction didn't match
            int boundA = (startDegree - degreeVariance);
            int boundB = (startDegree + degreeVariance);
            return Stat.RandomCosmetic(boundA, boundB) % 360;
        }
    }

    public class TraderDialogGenData
    {
        private static readonly Dictionary<GameObject, TraderDialogGenData> _Data = new Dictionary<GameObject, TraderDialogGenData>();
        readonly long ExpirationTick;
        readonly public int Random2;
        readonly public int Random3;
        readonly public int Random4;

        public TraderDialogGenData(long ticksRemaining)
        {
            this.ExpirationTick = ZoneManager.Ticker + ticksRemaining;
            this.Random2 = Stat.Random(1, 2);
            this.Random3 = Stat.Random(1, 3);
            this.Random4 = Stat.Random(1, 4);
        }

        public static TraderDialogGenData GetData(GameObject trader, long ticksRemaining)
        {
            if (!_Data.ContainsKey(trader) || _Data[trader].ExpirationTick <= ZoneManager.Ticker)
            {
                _Data.Remove(trader);
                _Data.Add(trader, new TraderDialogGenData(ticksRemaining));
            }
            return _Data[trader];
        }
    }
}

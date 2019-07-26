﻿using UnityEngine;
using System.Collections.Generic;
using Qud.API;
using XRL.Language;
using XRL.Core;
using XRL.Rules;
using XRL.World.Parts.Effects;
using XRL.UI;
using System;

namespace XRL.World.Parts
{
    public class Egcb_PlayerUIHelper : IPart
    {
        public static GameObject PlayerBody = null;
        public static GameObject ConversationPartner = null;
        public static List<GameObject> NewQuestHolders = new List<GameObject>();
        public static List<GameObject> ActiveQuestHolders = new List<GameObject>();

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        public override void Register(GameObject Object)
        {
            Object.RegisterPartEvent(this, "PlayerBeginConversation");
            base.Register(Object);
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == "PlayerBeginConversation")
            {
                Egcb_PlayerUIHelper.PlayerBody = XRLCore.Core.Game.Player.Body;
                GameObject speaker = E.GetGameObjectParameter("Speaker");
                Egcb_PlayerUIHelper.ConversationPartner = speaker;
                string questID = speaker.GetStringProperty("GivesDynamicQuest", string.Empty);
                Conversation convo = E.GetParameter<Conversation>("Conversation");
                if (questID == string.Empty || XRLCore.Core.Game.FinishedQuests.ContainsKey(questID)) //speaker has no dynamic quests
                {
                    this.AddChoiceToIdentifyQuestGivers(convo, speaker);
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
            }
            return base.FireEvent(E);
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
            Debug.Log("QudUX Mod: ApplyNewQuestGiverEffect()");
            return ApplyQuestGiverEffect(Egcb_PlayerUIHelper.NewQuestHolders);
        }
        public static bool ApplyActiveQuestGiverEffect()
        {
            Debug.Log("QudUX Mod: ApplyActiveQuestGiverEffect()");
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
    }
}

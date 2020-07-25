using System;
using System.Collections.Generic;
using XRL.UI;
using XRL;
using XRL.Core;
using XRL.World.Parts;
using GameObject = XRL.World.GameObject;
using UnityEngine;
using XRL.World.Parts.Mutation;
using XRL.World.Skills;

namespace Egocarib.Code
{
    public class Egcb_AbilityDataEntry
    {
        public string Name;
        public string Class;
        public string Command;
        public string MutationName;
        public string SkillName;
        public string BaseCooldown;
        public string CustomDescription;
        public string DeleteLines;
        public string DeletePhrases;
    }

    public class Egcb_AbilityData
    {
        private static Dictionary<string, List<Egcb_AbilityDataEntry>> _Categories = new Dictionary<string, List<Egcb_AbilityDataEntry>>();
        private static readonly HashSet<Guid> AbilitiesWithoutRealDescriptions = new HashSet<Guid>();
        private readonly Dictionary<Guid, string> VanillaDescriptionText;
        private static bool _bStaticInitialized = false;
        private static bool? _bLoadedXMLData = null;
        public readonly List<BaseMutation> PlayerMutations;
        public Dictionary<string, List<Egcb_AbilityDataEntry>> Categories
        {
            get
            {
                Egcb_AbilityData.InitializeStaticData();
                return Egcb_AbilityData._Categories;
            }
        }

        public Egcb_AbilityData() //instantiated when the Manage Abilities UI window opens
        {
            //load mutations from player
            Mutations playerMutationData = XRLCore.Core?.Game?.Player?.Body?.GetPart<Mutations>();
            if (playerMutationData != null)
            {
                this.PlayerMutations = playerMutationData.ActiveMutationList;
            }
            else
            {
                this.PlayerMutations = new List<BaseMutation>(0);
            }

            //load vanilla descriptions from player
            this.VanillaDescriptionText = Egcb_AbilityData.LoadAbilityData();
        }

        public static bool InitializeStaticData()
        {
            if (!Egcb_AbilityData._bStaticInitialized)
            {
                Egcb_AbilityData._bStaticInitialized = true;
                Egcb_AbilityData._Categories.Clear();
                Egcb_AbilityData._Categories = Egcb_QudUXFileHandler.LoadCategorizedAbilityDataEntries(); //load data from XML file
                Egcb_AbilityData._bLoadedXMLData = (Egcb_AbilityData._Categories.Count > 0);
            }
            return (bool)Egcb_AbilityData._bLoadedXMLData;
        }

        public static Dictionary<Guid, string> LoadAbilityData()
        {
            Dictionary<Guid, string> originalAbilityDescriptions = new Dictionary<Guid, string>();
            GameObject playerBody = XRLCore.Core?.Game?.Player?.Body;
            if (playerBody != null)
            {
                if (!playerBody.HasPart("Egcb_PlayerUIHelper"))
                {
                    Debug.Log("QudUX Mod: Unexpectedly failed to load ability data from the player body. QudUX might be unable to show improved descriptions on the activated ability screen.");
                }
                else
                {
                    originalAbilityDescriptions = playerBody.GetPart<Egcb_PlayerUIHelper>().DefaultAbilityDescriptions;
                }
            }
            return originalAbilityDescriptions;
        }

        public bool SaveAbilityData()
        {
            //add original descriptions to Egcb_PlayerUIHelper IPart so that they'll get serialized.
            if (!XRLCore.Core.Game.Player.Body.HasPart("Egcb_PlayerUIHelper"))
            {
                Debug.Log("QudUX Mod: Unexpectedly failed to save ability data to the player, so that data won't be serialized. This might affect Activated Ability description generation.");
                return false;
            }
            XRLCore.Core.Game.Player.Body.GetPart<Egcb_PlayerUIHelper>().DefaultAbilityDescriptions = this.VanillaDescriptionText;
            return true;
        }

        public void TryUpdate(string category, ActivatedAbilityEntry ability)
        {
            if (ability == null || string.IsNullOrEmpty(category))
            {
                return;
            }

            bool hasMeaningfulDescription;
            if (Egcb_AbilityData.AbilitiesWithoutRealDescriptions.Contains(ability.ID))
            {
                hasMeaningfulDescription = false;
            }
            else
            {
                string vanillaDescription = this.VanillaDescriptionText.ContainsKey(ability.ID) ? this.VanillaDescriptionText[ability.ID] : ability.Description;
                string simplifiedName = this.SimplifiedAbilityName(ability.DisplayName);
                hasMeaningfulDescription = !string.IsNullOrEmpty(vanillaDescription)
                    && ability.DisplayName != vanillaDescription
                    && simplifiedName != vanillaDescription
                    && !simplifiedName.StartsWith(vanillaDescription);
            }
            if (!hasMeaningfulDescription)
            {
                Egcb_AbilityData.AbilitiesWithoutRealDescriptions.Add(ability.ID);
                if (category == "Mental Mutation" || category == "Mutation" || category == "Physical Mutation")
                {
                    this.UpdateMutationAbilityDescription(category, ability);
                }
                else
                {
                    this.UpdateNonMutationAbilityDescription(category, ability);
                }
            }
            else //already has a meaningful description. Append cooldown info to the existing description.
            {
                this.AddCooldownToPrexistingAbilityDescription(category, ability);
            }
        }

        public void AddCooldownToPrexistingAbilityDescription(string category, ActivatedAbilityEntry ability)
        {
            this.UpdateNonMutationAbilityDescription(category, ability, true);
        }

        public void UpdateNonMutationAbilityDescription(string category, ActivatedAbilityEntry ability, bool bAddCooldownOnly = false)
        {
            if (!this.Categories.ContainsKey(category))
            {
                Debug.Log("QudUX Mod: Couldn't find any data for activated ability category '" + category + "'. Activated ability description for " + this.SimplifiedAbilityName(ability.DisplayName) + " won't be updated.");
                return;
            }
            List<Egcb_AbilityDataEntry> abilityData = this.Categories[category];
            string deleteLines = null;
            string deletePhrases = null;
            foreach (Egcb_AbilityDataEntry abilityDataEntry in abilityData)
            {
                if (abilityDataEntry.Name == this.SimplifiedAbilityName(ability.DisplayName))
                {
                    string description = string.Empty;
                    deleteLines = abilityDataEntry.DeleteLines;
                    deletePhrases = abilityDataEntry.DeletePhrases;
                    if (!string.IsNullOrEmpty(abilityDataEntry.CustomDescription))
                    {
                        description = abilityDataEntry.CustomDescription;
                    }
                    else if (bAddCooldownOnly)
                    {
                        description = this.VanillaDescriptionText.ContainsKey(ability.ID) ? this.VanillaDescriptionText[ability.ID] : ability.Description;
                    }
                    else if (!string.IsNullOrEmpty(abilityDataEntry.SkillName))
                    {
                        if (SkillFactory.Factory.PowersByClass.ContainsKey(abilityDataEntry.SkillName))
                        {
                            PowerEntry skill = SkillFactory.Factory.PowersByClass[abilityDataEntry.SkillName];
                            description = skill.Description;
                        }
                    }
                    description = description.TrimEnd('\r', '\n', ' ');
                    if (!string.IsNullOrEmpty(description))
                    {
                        if (!string.IsNullOrEmpty(abilityDataEntry.BaseCooldown))
                        {
                            string adjustedCooldownString = this.GetCooldownString(abilityDataEntry.BaseCooldown);
                            if (!string.IsNullOrEmpty(adjustedCooldownString))
                            {
                                description += "\n\n" + adjustedCooldownString;
                            }
                        }
                        if (!this.VanillaDescriptionText.ContainsKey(ability.ID))
                        {
                            this.VanillaDescriptionText.Add(ability.ID, ability.Description);
                        }
                        ability.Description = description;
                    }
                    break;
                }
            }
            if (!this.VanillaDescriptionText.ContainsKey(ability.ID))
            {
                this.VanillaDescriptionText.Add(ability.ID, ability.Description);
            }
            ability.Description = Egcb_AbilityData.SpecialFormatDescription(ability.Description, deleteLines, deletePhrases);
        }

        public void UpdateMutationAbilityDescription(string category, ActivatedAbilityEntry ability)
        {
            if (!this.Categories.ContainsKey(category))
            {
                Debug.Log("QudUX Mod: Couldn't find any data for activated ability category '" + category + "'. Activated ability description for " + this.SimplifiedAbilityName(ability.DisplayName) + " won't be updated.");
                return;
            }
            List<Egcb_AbilityDataEntry> abilityData = this.Categories[category];
            foreach (Egcb_AbilityDataEntry abilityDataEntry in abilityData)
            {
                if (abilityDataEntry.Name == this.SimplifiedAbilityName(ability.DisplayName))
                {
                    //match AbilityDataEntry to the Ability name
                    BaseMutation abilitySourceMutation = null;
                    BaseMutation secondaryMatch = null;
                    foreach (BaseMutation playerMutation in this.PlayerMutations)
                    {
                        MutationEntry mutationEntry = playerMutation.GetMutationEntry();
                        if (mutationEntry != null && mutationEntry.DisplayName == abilityDataEntry.MutationName)
                        {
                            abilitySourceMutation = playerMutation;
                            break;
                        }
                        if (playerMutation.DisplayName == abilityDataEntry.MutationName)
                        {
                            secondaryMatch = playerMutation; //less desirable match method, but necessary for some NPC mutations that don't have a MutationEntry
                        }
                    }
                    if (abilitySourceMutation == null && secondaryMatch != null)
                    {
                        abilitySourceMutation = secondaryMatch;
                    }
                    if (abilitySourceMutation == null)
                    {
                        Debug.Log("QudUX Mod: Unexpectedly failed to load mutation description data for '" + this.SimplifiedAbilityName(ability.DisplayName) + "' activated ability.");
                        continue;
                    }
                    if (!this.VanillaDescriptionText.ContainsKey(ability.ID))
                    {
                        this.VanillaDescriptionText.Add(ability.ID, ability.Description);
                    }
                    ability.Description = abilitySourceMutation.GetDescription() + "\n\n" + abilitySourceMutation.GetLevelText(abilitySourceMutation.Level);
                    ability.Description = ability.Description.TrimEnd('\r', '\n', ' ');
                    //updated Cooldown based on wisdom:
                    if (ability.Description.Contains("Cooldown:") || !string.IsNullOrEmpty(abilityDataEntry.BaseCooldown))
                    {
                        string updatedDescription = string.Empty;
                        string extractedCooldownString = !string.IsNullOrEmpty(abilityDataEntry.BaseCooldown) ? this.GetCooldownString(abilityDataEntry.BaseCooldown) : string.Empty;
                        string[] descriptionParts = ability.Description.Split('\n');
                        foreach (string descriptionPart in descriptionParts)
                        {
                            if (descriptionPart.Contains("Cooldown:"))
                            {
                                string[] words = descriptionPart.Split(' ');
                                foreach (string word in words)
                                {
                                    int o;
                                    if (int.TryParse(word, out o))
                                    {
                                        extractedCooldownString = this.GetCooldownString(word);
                                        break;
                                    }
                                }
                                if (string.IsNullOrEmpty(extractedCooldownString))
                                {
                                    updatedDescription += (updatedDescription != string.Empty ? "\n" : string.Empty) + descriptionPart; //restore line in case we didn't find the number (should never happen)
                                }
                            }
                            else
                            {
                                updatedDescription += (updatedDescription != string.Empty ? "\n" : string.Empty) + descriptionPart;
                            }
                        }
                        ability.Description = updatedDescription + (!string.IsNullOrEmpty(extractedCooldownString) ? "\n\n" + extractedCooldownString : string.Empty);
                    }
                    ability.Description = Egcb_AbilityData.SpecialFormatDescription(ability.Description, abilityDataEntry.DeleteLines, abilityDataEntry.DeletePhrases);
                    break;
                }
            }
        }

        public string GetCooldownString(string baseCooldown)
        {
            string cooldownString = string.Empty;
            int number;
            bool isNumber = int.TryParse(baseCooldown, out number);
            if (isNumber)
            {
                int newCooldown = this.GetAdjustedCooldown(number);
                string changePhrase = (newCooldown > number) ? " (increased due to your Willpower)" : (newCooldown < number) ? " (decreased due to your Willpower)" : string.Empty;
                cooldownString = "Cooldown: &C" + newCooldown.ToString() + "&y";
                if (!string.IsNullOrEmpty(changePhrase))
                {
                    cooldownString += changePhrase + "\n&KBase Cooldown: " + baseCooldown;
                }
            }
            return cooldownString;
        }

        public int GetAdjustedCooldown(int baseCooldown)
        {
            GameObject player = XRLCore.Core.Game.Player.Body;
            if (player == null || !player.HasStat("Willpower"))
            {
                return baseCooldown;
            }
            int internalCooldown = baseCooldown * 10;
            int val = (int)((double)internalCooldown * (100.0 - (double)((player.Stat("Willpower", 0) - 16) * 5))) / 100;
            int calculatedCooldown = Math.Max(val, ActivatedAbilities.MinimumValueForCooldown(internalCooldown));
            baseCooldown = (int)Math.Ceiling((double)((float)calculatedCooldown / 10f));
            return baseCooldown;
        }

        public string SimplifiedAbilityName(string name)
        {
            //strips off extra info in parentheses or brackets. For example, converts "Lase [4 charges]" to "Lase"
            if (name == null)
            {
                return string.Empty;
            }
            if (name.IndexOf('(') >= 0)
            {
                name = name.Split('(')[0].Trim();
            }
            if (name.IndexOf('[') >= 0)
            {
                name = name.Split('[')[0].Trim();
            }
            return name;
        }

        public static string SpecialFormatDescription(string description, string lineDeletionClues = null, string phraseDeletionClues = null)
        {
            if (description.StartsWith("&K\u00b3&y"))
            {
                return description; //avoid double-applying this format to things
            }
            if (!string.IsNullOrEmpty(lineDeletionClues) || !string.IsNullOrEmpty(phraseDeletionClues) || description.Contains(" reputation "))
            {
                string cleansedDescription = string.Empty;
                foreach (string _line in description.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                {
                    string line = _line;
                    bool shouldDeleteLine = false;
                    //remove any custom lines from the description that were configured in AbilityExtenderData.xml
                    if (!string.IsNullOrEmpty(lineDeletionClues))
                    {
                        foreach (string deletionClue in lineDeletionClues.Split('~'))
                        {
                            if (line.StartsWith(deletionClue))
                            {
                                shouldDeleteLine = true;
                                break;
                            }
                        }
                    }
                    //remove any custom phrases from the description that were configured in AbilityExtenderData.xml
                    if (!string.IsNullOrEmpty(phraseDeletionClues))
                    {
                        foreach (string deletionClue in phraseDeletionClues.Split('~'))
                        {
                            if (line.Contains(deletionClue))
                            {
                                line = line.Replace(deletionClue, "");
                            }
                        }
                    }
                    //remove reputation lines, because they aren't relevant to activated ability descriptions
                    if (!shouldDeleteLine && line.Contains(" reputation "))
                    {
                        if (line[0] == '+' || line[0] == '-')
                        {
                            string[] words = line.Split(' ');
                            if (words.Length >= 2 && words[1] == "reputation")
                            {
                                shouldDeleteLine = true;
                            }
                        }
                    }
                    if (shouldDeleteLine == false)
                    {
                        cleansedDescription += (string.IsNullOrEmpty(cleansedDescription) ? "" : "\n") + line;
                    }
                }
                description = cleansedDescription;
            }
            //format into text block and add dark gray border line
            string formattedDescription = string.Empty;
            TextBlock textBlock = new TextBlock(description, 29, 20, false);
            for (int i = 0; i < Math.Min(20, textBlock.Lines.Count); i++)
            {
                formattedDescription += ((i > 0) ? "\n" : "") + "&K\u00b3&y" + textBlock.Lines[i].PadRight(27);
            }
            formattedDescription = formattedDescription.TrimEnd('\r', '\n', ' ');
            formattedDescription = (formattedDescription.EndsWith("\n&K\u00b3&y")) ? formattedDescription.Remove(formattedDescription.Length - 6) : formattedDescription;
            return formattedDescription;
        }
    }

    public class Egcb_AbilityManagerExtender
    {
        public readonly Egcb_AbilityData AbilityData = new Egcb_AbilityData();
        public readonly Dictionary<string, List<ActivatedAbilityEntry>> PlayerAbilities = new Dictionary<string, List<ActivatedAbilityEntry>>();

        public void UpdateAbilityDescriptions()
        {
            if (Egcb_AbilityData.InitializeStaticData())
            {
                this.LoadPlayerAbilities();
                foreach (KeyValuePair<string, List<ActivatedAbilityEntry>> playerAbilityCategories in this.PlayerAbilities)
                {
                    string category = playerAbilityCategories.Key;
                    List<ActivatedAbilityEntry> abilities = playerAbilityCategories.Value;
                    foreach (ActivatedAbilityEntry ability in abilities)
                    {
                        AbilityData.TryUpdate(category, ability);
                    }
                }
                AbilityData.SaveAbilityData();
            }
        }

        public void LoadPlayerAbilities()
        {
            this.PlayerAbilities.Clear();
            ActivatedAbilities activatedAbilities = XRLCore.Core.Game.Player.Body.GetPart("ActivatedAbilities") as ActivatedAbilities;
            if (activatedAbilities == null || activatedAbilities.AbilityLists == null)
            {
                return;
            }
            foreach (KeyValuePair<string, List<ActivatedAbilityEntry>> abilityList in activatedAbilities.AbilityLists)
            {
                List<ActivatedAbilityEntry> entries = new List<ActivatedAbilityEntry>();
                foreach (ActivatedAbilityEntry entry in abilityList.Value)
                {
                    entries.Add(entry);
                }
                if (entries.Count > 0)
                {
                    this.PlayerAbilities.Add(abilityList.Key, entries);
                }
            }
        }
    }
}

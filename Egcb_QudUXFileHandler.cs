using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
//using System.Text;
//using System.Globalization;
//using System.Text.RegularExpressions;
using XRL;
using UnityEngine;

namespace Egocarib.Code
{
    public static class Egcb_QudUXFileHandler
    {
        private static string _modDirectory;
        public static string ModDirectory //I haven't found a more convenient way to do this so far
        {
            get
            {
                if (string.IsNullOrEmpty(Egcb_QudUXFileHandler._modDirectory))
                {
                    //loop through the mod manager to get our mod's directory path
                    ModManager.ForEachMod(delegate (ModInfo mod)
                    {
                        foreach (string filePath in mod.ScriptFiles)
                        {
                            if (Path.GetFileName(filePath) == "Egcb_QudUXFileHandler.cs")
                            {
                                Egcb_QudUXFileHandler._modDirectory = Path.GetDirectoryName(filePath);
                                return; //we found our mod
                            }
                        }
                    });
                }
                return Egcb_QudUXFileHandler._modDirectory;
            }
        }

        public static Dictionary<string, List<Egcb_AbilityDataEntry>> LoadCategorizedAbilityDataEntries()
        {
            Dictionary<string, List<Egcb_AbilityDataEntry>> CategorizedData = new Dictionary<string, List<Egcb_AbilityDataEntry>>();
            try
            {
                using (XmlTextReader stream = new XmlTextReader(Path.Combine(Egcb_QudUXFileHandler.ModDirectory, "AbilityExtenderData.xml"))) //this file is packaged with the mod and should always exist
                {
                    stream.WhitespaceHandling = WhitespaceHandling.None;
                    while (stream.Read())
                    {
                        if (stream.Name == "abilityEntries")
                        {
                            while (stream.Read())
                            {
                                if (stream.Name == "category")
                                {
                                    string categoryName = stream.GetAttribute("Name");
                                    List<Egcb_AbilityDataEntry> categoryEntries = new List<Egcb_AbilityDataEntry>();
                                    while (stream.Read())
                                    {
                                        if (stream.Name == "abilityEntry")
                                        {
                                            Egcb_AbilityDataEntry thisEntry = new Egcb_AbilityDataEntry
                                            {
                                                Name = stream.GetAttribute("Name"),
                                                Class = stream.GetAttribute("Class"),
                                                Command = stream.GetAttribute("Command"),
                                                MutationName = stream.GetAttribute("MutationName"),
                                                SkillName = stream.GetAttribute("SkillName"),
                                                BaseCooldown = stream.GetAttribute("BaseCooldown"),
                                                CustomDescription = stream.GetAttribute("CustomDescription"),
                                                DeleteLines = stream.GetAttribute("DeleteLines"),
                                                DeletePhrases = stream.GetAttribute("DeletePhrases")
                                            };
                                            categoryEntries.Add(thisEntry);
                                        }
                                        if (stream.NodeType == XmlNodeType.EndElement && (stream.Name == string.Empty || stream.Name == "category"))
                                        {
                                            break;
                                        }
                                    }
                                    if (categoryEntries.Count > 0)
                                    {
                                        if (CategorizedData.ContainsKey(categoryName))
                                        {
                                            CategorizedData[categoryName].AddRange(categoryEntries);
                                        }
                                        else
                                        {
                                            CategorizedData.Add(categoryName, categoryEntries);
                                        }
                                    }
                                }
                                if (stream.NodeType == XmlNodeType.EndElement && (stream.Name == string.Empty || stream.Name == "abilityEntries"))
                                {
                                    break;
                                }
                            }
                        }
                        if (stream.NodeType == XmlNodeType.EndElement && (stream.Name == string.Empty || stream.Name == "abilityEntries"))
                        {
                            break;
                        }
                    }
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.Log("QudUX Mod: Error trying to load data from AbilityExtenderData.xml (" + ex.ToString() + ")");
                CategorizedData.Clear();
            }
            return CategorizedData;
        }
    }
}
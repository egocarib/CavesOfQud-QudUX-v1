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

        //Old method below - used to save and load this data to file, but that was not ideal. Switched to native game serialization instead.

        //public static bool SaveOriginalAbilityDescriptions(Dictionary<Guid, string> abilityDescriptions)
        //{
        //    try
        //    {
        //        using (StreamWriter xmlStream = new StreamWriter(Path.Combine(Egcb_QudUXFileHandler.ModDirectory, "AbilityExtenderChanges.xml"), false))
        //        {
        //            XmlWriterSettings xmlSettings = new XmlWriterSettings { Indent = true };
        //            using (XmlWriter xmlWriter = XmlWriter.Create(xmlStream, xmlSettings))
        //            {
        //                xmlWriter.WriteStartDocument();
        //                xmlWriter.WriteStartElement("abilityChanges");
        //                foreach (KeyValuePair<Guid, string> abilityDescription in abilityDescriptions)
        //                {
        //                    xmlWriter.WriteStartElement("abilityChangeEntry");
        //                    xmlWriter.WriteAttributeString("Guid", abilityDescription.Key.ToString());
        //                    xmlWriter.WriteAttributeString("OriginalDescription", Egcb_QudUXFileHandler.EncodeNonAsciiCharacters(abilityDescription.Value));
        //                    xmlWriter.WriteFullEndElement();
        //                }
        //                xmlWriter.WriteEndDocument();
        //                xmlWriter.Flush();
        //                xmlWriter.Close();
        //            }
        //            xmlStream.Flush();
        //            xmlStream.Close();
        //        }
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.Log("QudUX Mod: Failed to serialize ability description data to AbilityExtenderChanges.xml (" + ex.ToString() + ")");
        //        return false;
        //    }
        //}

        //public static Dictionary<Guid, string> LoadOriginalAbilityDescriptions()
        //{
        //    Dictionary<Guid, string> abilityDescriptions = new Dictionary<Guid, string>();
        //    if (File.Exists(Path.Combine(Egcb_QudUXFileHandler.ModDirectory, "AbilityExtenderChanges.xml"))) //may not exist if we haven't created it yet
        //    {
        //        try
        //        {
        //            using (XmlTextReader stream = new XmlTextReader(Path.Combine(Egcb_QudUXFileHandler.ModDirectory, "AbilityExtenderChanges.xml")))
        //            {
        //                stream.WhitespaceHandling = WhitespaceHandling.None;
        //                while (stream.Read())
        //                {
        //                    if (stream.Name == "abilityChanges")
        //                    {
        //                        while (stream.Read())
        //                        {
        //                            if (stream.NodeType == XmlNodeType.Element && stream.Name == "abilityChangeEntry")
        //                            {
        //                                Guid guid = new Guid(stream.GetAttribute("Guid"));
        //                                string description = Egcb_QudUXFileHandler.DecodeEncodedNonAsciiCharacters(stream.GetAttribute("OriginalDescription"));
        //                                if (!abilityDescriptions.ContainsKey(guid))
        //                                {
        //                                    abilityDescriptions.Add(guid, description);
        //                                }
        //                            }
        //                            if (stream.NodeType == XmlNodeType.EndElement && (stream.Name == string.Empty || stream.Name == "abilityChanges"))
        //                            {
        //                                break;
        //                            }
        //                        }
        //                    }
        //                    if (stream.NodeType == XmlNodeType.EndElement && (stream.Name == string.Empty || stream.Name == "abilityChanges"))
        //                    {
        //                        break;
        //                    }
        //                }
        //                stream.Close();
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.Log("QudUX Mod: Error trying to load data from AbilityExtenderChanges.xml (" + ex.ToString() + ")");
        //            abilityDescriptions.Clear();
        //        }
        //    }
        //    return abilityDescriptions;
        //}

        //public static string EncodeNonAsciiCharacters(string value)
        //{
        //    StringBuilder sb = new StringBuilder();
        //    foreach (char c in value)
        //    {
        //        if (c > 127)
        //        {
        //            // This character is too big for ASCII
        //            string encodedValue = "\\u" + ((int)c).ToString("x4");
        //            sb.Append(encodedValue);
        //        }
        //        else
        //        {
        //            sb.Append(c);
        //        }
        //    }
        //    return sb.ToString();
        //}

        //public static string DecodeEncodedNonAsciiCharacters(string value)
        //{
        //    return Regex.Replace(
        //        value,
        //        @"\\u(?<Value>[a-zA-Z0-9]{4})",
        //        m => {
        //            return ((char)int.Parse(m.Groups["Value"].Value, NumberStyles.HexNumber)).ToString();
        //        });
        //}
    }
}
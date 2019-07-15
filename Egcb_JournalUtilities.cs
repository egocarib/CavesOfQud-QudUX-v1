using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XRL.Core;
using Qud.API;
using System.Text;
using XRL.Rules;
using XRL.Language;

namespace Egocarib.Code
{
    public class Egcb_JournalUtilities
    {
        private static string GameIDAssociation; //used to ensure static data is re-evaluated after player saves / reloads a new character with different journal data
        private static Dictionary<string, bool> NameCheckDictionary = new Dictionary<string, bool>();
        private static bool bRenamedForgottenRuins = false;

        public static void ResetZoneNameForMapNote(JournalMapNote jnote)
        {
            string newLocationName;
            do
            {
                newLocationName = Egcb_JournalUtilities.GenerateName();
            } while (!Egcb_JournalUtilities.ValidateUniqueJournalName(newLocationName));
            jnote.text = newLocationName;
            XRLCore.Core.Game.ZoneManager.SetZoneDisplayName(jnote.zoneid, newLocationName);
            XRLCore.Core.Game.ZoneManager.SetZoneHasProperName(jnote.zoneid, true);
            jnote.Updated();
        }

        public static void RenameForgottenRuins()
        {
            if (Egcb_JournalUtilities.bRenamedForgottenRuins)
            {
                return;
            }
            Egcb_JournalUtilities.bRenamedForgottenRuins = true;
            //string debugVal = String.Empty;
            List<JournalMapNote> mapNotes = JournalAPI.MapNotes;
            foreach (JournalMapNote jnote in mapNotes)
            {
                if (jnote.text == "some forgotten ruins")
                {
                    //debugVal += "\n  some forgotten ruins - " + XRL.LoreGenerator.GenerateLandmarkDirectionsTo(jnote.zoneid, !string.IsNullOrEmpty(XRLCore.Core.Game.GetStringGameState("villageZeroName", string.Empty)));
                    Egcb_JournalUtilities.ResetZoneNameForMapNote(jnote);
                    //Debug.Log("QudUX Mod: renamed zone " + jnote.zoneid + " from 'some forgotten ruins' to '" + jnote.text + "'\n        full data: " + jnote.GetDisplayText());
                }
            }
            //Debug.Log("QudUX Mod: Forgotten Ruins Test...\n" + debugVal);
        }

        public static bool ValidateUniqueJournalName(string name)
        {
            name = name.ToLower();
            if (Egcb_JournalUtilities.NameCheckDictionary.Count <= 0 || Egcb_JournalUtilities.GameIDAssociation != XRLCore.Core.Game.GameID)
            {
                Egcb_JournalUtilities.NameCheckDictionary.Clear();
                Egcb_JournalUtilities.GameIDAssociation = XRLCore.Core.Game.GameID; //Not 100% sure if this is necessary, but better to be safe than sorry. (not sure if this would persist through save/reload of different character with different journal data)
                foreach (JournalMapNote place in JournalAPI.MapNotes)
                {
                    if (!Egcb_JournalUtilities.NameCheckDictionary.ContainsKey(place.text.ToLower()))
                    {
                        Egcb_JournalUtilities.NameCheckDictionary.Add(place.text.ToLower(), true);
                    }
                }
            }
            if (Egcb_JournalUtilities.NameCheckDictionary.ContainsKey(name))
            {
                return false;
            }
            Egcb_JournalUtilities.NameCheckDictionary.Add(name, true);
            return true;
        }

        public static string MakeName(string[] Prefixes, int PrefixAmount, string[] Infixes, int InfixAmount, string[] Postfixes, int PostfixAmount, int HyphenationChance, int TwoNameChance, bool TitleCase = true)
        {
            string text;
            do
            {
                StringBuilder stringBuilder = new StringBuilder();
                short num = 0;
                if (Stat.Random(1, 100) < TwoNameChance)
                {
                    num = 1;
                }
                for (int i = 0; i <= (int)num; i++)
                {
                    for (int j = 0; j < PrefixAmount; j++)
                    {
                        stringBuilder.Append(Prefixes.GetRandomElement(null));
                        if (Stat.Random(1, 100) < HyphenationChance)
                        {
                            stringBuilder.Append("-");
                        }
                    }
                    for (int k = 0; k < InfixAmount; k++)
                    {
                        stringBuilder.Append(Infixes.GetRandomElement(null));
                        if (Stat.Random(1, 100) < HyphenationChance)
                        {
                            stringBuilder.Append("-");
                        }
                    }
                    for (int l = 0; l < PostfixAmount; l++)
                    {
                        stringBuilder.Append(Postfixes.GetRandomElement(null));
                        if (Stat.Random(1, 100) < HyphenationChance)
                        {
                            stringBuilder.Append("-");
                        }
                    }
                    if (stringBuilder[stringBuilder.Length - 1] == '-')
                    {
                        stringBuilder.Remove(stringBuilder.Length - 1, 1);
                    }
                    stringBuilder.Append(' ');
                }
                if (stringBuilder[stringBuilder.Length - 1] == ' ')
                {
                    stringBuilder.Length--;
                }
                if (stringBuilder[stringBuilder.Length - 1] == '-')
                {
                    stringBuilder.Length--;
                }
                text = stringBuilder.ToString();
            }
            while (Grammar.ContainsBadWords(text));
            return (!TitleCase) ? text : Grammar.MakeTitleCase(text);
        }

        public static string GenerateName()
        {
            string text;
            text = Egcb_JournalUtilities.MakeName(new string[]
            {
                "U",
                "Ma",
                "Ka",
                "Mi",
                "Shu",
                "Ha",
                "Ala",
                "A",
                "Da",
                "Bi",
                "Ta",
                "Te",
                "Tu",
                "Sa",
                "Du",
                "Na",
                "She",
                "Sha",
                "Eka",
                "Ki",
                "I",
                "Su",
                "Qa"
            }, 1, new string[]
            {
                "rche",
                "ga",
                "rva",
                "mri",
                "azo",
                "arra",
                "ili",
                "ba",
                "gga",
                "rqa",
                "rqu",
                "by",
                "rsi",
                "ra",
                "ne"
            }, Stat.Random(0, 1), new string[]
            {
                "ppur",
                "ppar",
                "ppir",
                "sh",
                "d",
                "mish",
                "kh",
                "mur",
                "bal",
                "mas",
                "zor",
                "mor",
                "nip",
                "lep",
                "pad",
                "kesh",
                "war",
                "tum",
                "mmu",
                "mrod",
                "shur",
                "nna",
                "kish",
                "ruk",
                "r",
                "ppa",
                "wan",
                "shan",
                "tara",
                "vah",
                "vuh",
                "lil"
            }, 1, 5, 7, true);
            if (Stat.Random(0, 100) < 50)
            {
                int num = Stat.Random(0, 22);
                if (num == 0)
                {
                    text += " Schism"; //
                }
                else if (num == 1)
                {
                    text = "the Sliver of " + text; //
                }
                else if (num == 2)
                {
                    text = "the Glyph at " + text; //
                }
                else if (num == 3)
                {
                    text += " Roof"; //
                }
                else if (num == 4)
                {
                    text += " Fist"; //
                }
                else if (num == 5)
                {
                    text = "Ethereal " + text; //
                }
                else if (num == 6)
                {
                    text = "Aged " + text; //
                }
                else if (num == 7)
                {
                    text += " Sluice"; //
                }
                else if (num == 8)
                {
                    text += " Aperture"; //
                }
                else if (num == 9)
                {
                    text += " Disc"; //
                }
                else if (num == 10)
                {
                    text += " Clearing"; //
                }
                else if (num == 11)
                {
                    text += " Pass"; //
                }
                else if (num == 12)
                {
                    text += " Seam"; //
                }
                else if (num == 13)
                {
                    text += " Twinge"; //
                }
                else if (num == 14)
                {
                    text += " Grist"; //
                }
                else if (num == 15)
                {
                    text = "Dilapidated " + text;
                }
                else if (num == 16)
                {
                    text = "Forgotten " + text;
                }
                else if (num == 17)
                {
                    text = "Crumbling " + text;
                }
                else if (num == 18)
                {
                    text = "Tangential " + text;
                }
                else if (num == 19)
                {
                    text += " Vents";
                }
                else if (num == 20)
                {
                    text += " Lookabout";
                }
                else if (num == 21)
                {
                    text += " Furrow";
                }
                else if (num == 22)
                {
                    text = "Distasteful " + text;
                }
            }
            return text;
        }

        public static bool FrozenZoneDataExists(string zoneID) //Unfortunately need this function because ZoneManager.FrozenZones is private
        {
            try
            {
                string compressedZoneFile = Path.Combine(Path.Combine(XRLCore.Core.Game.GetCacheDirectory(), "ZoneCache"), zoneID + ".zone.gz");
                if (File.Exists(compressedZoneFile))
                {
                    return true;
                }
                string pureZoneFile = Path.Combine(Path.Combine(XRLCore.Core.Game.GetCacheDirectory(), "ZoneCache"), zoneID + ".zone");
                if (File.Exists(pureZoneFile))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("QudUX Mod: Encountered exception while checking for frozen zone: " + ex.ToString());
            }
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using ConsoleLib.Console;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.Core;

namespace Egocarib.Code
{
    public class Egcb_JournalExtender
    {
        public readonly char ExploredLocationSymbol = '\u00fb'; //checkmark
        public readonly char UnexploredLocationSymbol = '?';
        private ConsoleChar AppliedSymbol = null; //use to track if the screen has been updated - check each frame
        private readonly ConsoleChar LocationTabChar = TextConsole.CurrentBuffer[3, 2]; //reference preserved for efficiency
        private readonly ushort WChar = 22; //22 = W  (there's no easy map to read from for this)
        private Dictionary<string, List<JournalFacts>> CachedRelevantJournalNotesByName = new Dictionary<string, List<JournalFacts>>();
        private List<string> ErroredJournalScreenStrings = new List<string>();

        public void FrameCheck() //called each frame
        {
            if (AppliedSymbol != null && (AppliedSymbol.Char == this.ExploredLocationSymbol || AppliedSymbol.Char == this.UnexploredLocationSymbol))
            {
                return; //last applied symbol is still visible, no update needed
            }
            else if (!this.JournalLocationTabActive())
            {
                return; //not on location tab - (no current functionality on other tabs)
            }
            else
            {
                this.UpdateScreen();
            }
        }

        public void UpdateScreen()
        {
            object bufferCS = TextConsole.BufferCS;
            lock (bufferCS) //acquire a lock, otherwise we get weird screens due to buffer contention
            {
                ScreenBuffer scrapBuffer = ScreenBuffer.GetScrapBuffer2(true);
                for (int i = 4; i < 24; i++) //4 is the first row any entry can appear on
                {
                    if (scrapBuffer[3, i].Char == '$') //indicates a discrete location entry
                    {
                        char[] locationChars = new char[70];
                        for (int j = 9; j < 79; j++)
                        {
                            locationChars[j - 9] = scrapBuffer[j, i].Char;
                        }
                        string locationName = new string(locationChars).Trim().ToLower();

                        this.UpdateJournalNoteDictionary();

                        if (!this.CachedRelevantJournalNotesByName.ContainsKey(locationName))
                        {
                            if (!this.ErroredJournalScreenStrings.Contains(locationName))
                            {
                                Debug.Log("QudUX Mod: Error trying to parse location from journal screen. [locationName = "
                                    + locationName + "]\nKNOWN LOCATIONS:\n" + DebugGetKnownLocationsList());
                                this.ErroredJournalScreenStrings.Add(locationName);
                            }
                            continue;
                        }

                        JournalFacts jFacts = new JournalFacts();
                        bool bFactsIdentified = false;
                        if (this.CachedRelevantJournalNotesByName[locationName].Count == 1)
                        {
                            //only one matching entry, no need to parse the directions
                            jFacts = this.CachedRelevantJournalNotesByName[locationName][0];
                            bFactsIdentified = true;
                        }
                        else if (i < 23) //verify there's at least one line left for directoins to appear on screen
                        {
                            List<JournalFacts> jFactsArray = this.CachedRelevantJournalNotesByName[locationName];
                            char[] directionChars = new char[76];
                            for (int j = 3; j < 79; j++)
                            {
                                directionChars[j - 3] = scrapBuffer[j, i + 1].Char;
                            }
                            string directionString = new string(directionChars).Trim();

                            int count = 0;
                            int index = -1;
                            for (int j = 0; j < jFactsArray.Count; j++)
                            {
                                if (jFactsArray[j].directionsTo == directionString)
                                {
                                    count++;
                                    index = j;
                                }
                            }
                            if (count == 1)
                            {
                                jFacts = jFactsArray[index];
                                bFactsIdentified = true;
                            }
                        }
                        if (bFactsIdentified) //draw the appropriate character to represent if this location was previously visited
                        {
                            string locationIndicator = jFacts.hasBeenVisited ? "&G" + this.ExploredLocationSymbol : "&K" + this.UnexploredLocationSymbol;
                            this.AppliedSymbol = TextConsole.CurrentBuffer[4, i];
                            scrapBuffer.Goto(4, i);
                            scrapBuffer.Write(locationIndicator);
                        }
                    }
                }
                Popup._TextConsole.DrawBuffer(scrapBuffer, null, false);
            }
        }

        public void UpdateJournalNoteDictionary()
        {
            if (this.CachedRelevantJournalNotesByName.Count > 0)
            {
                return; //already cached in this instance (only one instance per time the journal menu is open, so that should be fine)
            }
            List<JournalMapNote> mapNotes = JournalAPI.MapNotes;
            foreach (JournalMapNote jMapNote in mapNotes)
            {
                if (jMapNote.revealed) //player knows about it
                {
                    string mapTextPlain = ConsoleLib.Console.ColorUtility.StripFormatting(jMapNote.text).ToLower();
                    if (!this.CachedRelevantJournalNotesByName.ContainsKey(mapTextPlain))
                    {
                        this.CachedRelevantJournalNotesByName.Add(mapTextPlain, new List<JournalFacts>());
                    }
                    this.CachedRelevantJournalNotesByName[mapTextPlain].Add(new JournalFacts(jMapNote));
                }
            }
        }

        private struct JournalFacts
        {
            public JournalFacts(JournalMapNote jmn)
            {
                this.entry = jmn;
                this._directionsTo = String.Empty;
                this._hasBeenVisited = null;
            }
            public readonly JournalMapNote entry;
            public string directionsTo
            {
                get
                {
                    if (this._directionsTo == String.Empty)
                    {
                        this._directionsTo = LoreGenerator.GenerateLandmarkDirectionsTo(this.entry.zoneid,
                            !string.IsNullOrEmpty(XRLCore.Core.Game.GetStringGameState("villageZeroName", string.Empty)));
                    }
                    return this._directionsTo;
                }
            }
            public bool hasBeenVisited
            {
                get
                {
                    if (this._hasBeenVisited == null)
                    {
                        this._hasBeenVisited = XRLCore.Core.Game.ZoneManager.CachedZones.ContainsKey(this.entry.zoneid)
                            || Egcb_JournalUtilities.FrozenZoneDataExists(this.entry.zoneid);
                    }
                    return (bool)this._hasBeenVisited;
                }
            }
            private string _directionsTo;
            private bool? _hasBeenVisited;
        }

        public bool JournalLocationTabActive()
        {
            if (this.LocationTabChar.Char == 'L')
            {
                ushort fontColorCode = ConsoleLib.Console.ColorUtility.GetForeground(this.LocationTabChar.Attributes);
                if (fontColorCode == this.WChar)
                {
                    return true; //yellow "L" appears, meaning the "Locations" tab is highlighted in yellow in the journal and active
                }
            }
            return false;
        }

        public string DebugGetKnownLocationsList()
        {
            string retVal = String.Empty;
            List<JournalMapNote> mapNotes = JournalAPI.MapNotes;
            foreach (JournalMapNote jmn in mapNotes)
            {
                if (jmn.revealed) //player knows about it
                {
                    bool zoneWasVisited = (XRLCore.Core.Game.ZoneManager.CachedZones.ContainsKey(jmn.zoneid)
                        || Egcb_JournalUtilities.FrozenZoneDataExists(jmn.zoneid));
                    retVal += "\n  " + jmn.text + "  [visited? = " + zoneWasVisited + "]";
                    retVal += " [formatStripped = " + ConsoleLib.Console.ColorUtility.StripFormatting(jmn.text) + "]";
                }
            }
            return retVal;
        }
    }
}

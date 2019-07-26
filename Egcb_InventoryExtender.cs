using System;
using System.Collections.Generic;
using XRL.UI;
using XRL.Core;
using XRL.World.Parts;
using ConsoleLib.Console;
using GameObject = XRL.World.GameObject;

namespace Egocarib.Code
{
    public class Egcb_InventoryExtender
    {
        private Dictionary<Coords, ConsoleChar> CachedConsoleChars = new Dictionary<Coords, ConsoleChar>();
        private Dictionary<Coords, char> CachedOverwriteChars = new Dictionary<Coords, char>();
        private Dictionary<string, string> CachedValuePerLbStrings = new Dictionary<string, string>();
        private string DisplayMode = "Default";
        private List<GameObject> InventoryList = new List<GameObject>();
        private NalathniAppraiseConnector NalathniAppraiser = new NalathniAppraiseConnector();

        public class Coords
        {
            private readonly int x;
            public int X { get { return x; } }

            private readonly int y;
            public int Y { get { return y; } }

            public Coords(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }


        public void FrameCheck() //called each frame
        {
            if (this.DisplayMode == "Default")
            {
                return; //no custom updates needed in default mode
            }
            else if (this.LastCustomOverwritePersists()) //TODO: improve the efficiency of this function (maybe read directly from TextConsole insteady of copying scrapBuffer.
            {
                return; //last applied text is still visible, no update needed
            }
            else
            {
                this.UpdateScreen();
            }
        }

        public void UpdateScreen()
        {
            if (this.NalathniAppraiser.PreventPlayerAppraisal)
            {
                return;
            }
            this.LoadInventoryData();
            object bufferCS = TextConsole.BufferCS;
            lock (bufferCS) //acquire a lock on the screenbuffer to avoid interwoven rendering with other processes
            {
                //TODO: do something fun if character is confused and tries to do this in inventory
                this.CachedConsoleChars.Clear();
                this.CachedOverwriteChars.Clear();
                ScreenBuffer scrapBuffer = ScreenBuffer.GetScrapBuffer2(true);
                for (int i = 1; i < 24; i++)
                {
                    if (scrapBuffer[5, i].Char == ')') //this row represents an individual item (ignore other rows, such as category titles)
                    {
                        bool selectedRow = (scrapBuffer[1, i].Char == '>');
                        char[] itemChars = new char[60];
                        for (int j = 7; j < 67; j++)
                        {
                            itemChars[j - 7] = scrapBuffer[j, i].Char;
                        }
                        string itemName = new string(itemChars);
                        if (this.CachedValuePerLbStrings.ContainsKey(itemName))
                        {
                            string thisVal = this.PadColorStringLeft(this.CachedValuePerLbStrings[itemName], 11);
                            if (selectedRow)
                            {
                                thisVal = thisVal.Replace("&b$&c", "&B$&C").Replace("&y", "&Y");
                            }
                            string thisValStripped = ConsoleLib.Console.ColorUtility.StripFormatting(thisVal);
                            for (int k = 79 - 11, charCt = 0; k < 79; k++, charCt++)
                            {
                                //save off the ConsoleChar for each coordinate in an array of some sort so I can restore them easily when user presses alt again
                                ConsoleChar cc = new ConsoleChar();
                                cc.Copy(scrapBuffer[k, i]);
                                this.CachedConsoleChars.Add(new Coords(k, i), cc);
                                this.CachedOverwriteChars.Add(new Coords(k, i), thisValStripped[charCt]);
                            }
                            scrapBuffer.Goto(79 - 11, i);
                            scrapBuffer.Write(thisVal);
                        }
                    }
                }
                Popup._TextConsole.DrawBuffer(scrapBuffer, null, false);
            }
        }

        public void ReactToAltKeyRequest()  //74th tile
        {
            if (this.DisplayMode == "Default")
            {
                if (this.NalathniAppraiser.PreventPlayerAppraisal)
                {
                    return;
                }
                this.DisplayMode = "ValuePerPound";
            }
            else if (this.DisplayMode == "ValuePerPound")
            {
                this.DisplayMode = "Default";
                if (this.LastCustomOverwritePersists())
                {
                    this.RestoreGameConsole();
                }
            }
        }

        public void LoadInventoryData()
        {
            //This assumes items can't be added to the inventory while the inventory is open. It's probably possible that a mod could add items while
            //the inventory is open. If it turns out to be an issue, this code can be updated to retrieve the entire list every frame, but that seems
            //overkill for now - this should probably work for most realistic cases.
            if (this.InventoryList.Count <= 0)
            {
                GameObject playerCurrentBody = XRLCore.Core.Game?.Player?.Body;
                Inventory inventory = playerCurrentBody?.GetPart("Inventory") as Inventory;
                if (inventory == null)
                {
                    return;
                }
                List<GameObject> objects = inventory.GetObjectsDirect();
                for (int i = 0; i < objects.Count; i++)
                {
                    GameObject item = objects[i];
                    if (!item.HasTag("HiddenInInventory"))
                    {
                        //removed filter string handling, because if the player removes the filter after we do this, we won't have all the items saved in our List<GameObject>

                        //if (InventoryScreen.filterString == string.Empty || ConsoleLib.Console.ColorUtility.StripFormatting(item.DisplayName).ToLower().Contains(InventoryScreen.filterString.ToLower()))
                        //{
                            this.InventoryList.Add(item);
                        //}
                    }
                }
                this.InventoryList.Sort(InventoryScreen.displayNameSorter);

                foreach (GameObject item in this.InventoryList)
                {
                    string strippedConstrainedName = item.GetCachedDisplayNameStripped().Substring(0, Math.Min(item.GetCachedDisplayNameStripped().Length, 60)).PadRight(60);
                    if (!this.CachedValuePerLbStrings.ContainsKey(strippedConstrainedName))
                    {
                        this.CachedValuePerLbStrings.Add(strippedConstrainedName, this.GetItemValueString(item));
                    }
                }
            }
        }

        public string GetItemValueString(GameObject item)
        {
            string valueString = String.Empty;
            int weight = (item.pPhysics != null) ? item.pPhysics.Weight : 0;
            if (weight <= 0)
            {
                valueString = "&Kweightless";
            }
            else
            {
                double itemValue = this.GetItemPricePer(item) * (double)item.Count;
                double perPoundValue = itemValue / (double)weight;
                int finalValue = this.NalathniAppraiser.Approximate(perPoundValue); //Try to use the Nalathni Approximate method first, in case the player has NalathniDragon's Appraisal mod installed.
                if (finalValue < 0) //Fall back to normal integer rounding if the player doesn't have that mod installed (or if it returned a negative [intended to represent fractional] value)
                {
                    finalValue = (int)Math.Round(perPoundValue, MidpointRounding.AwayFromZero);
                }
                valueString = "&b$&c" + finalValue + "&y / lb.";
            }
            return valueString;
        }

        public string PadColorStringLeft(string colorString, int desiredLength)
        {
            int currentLength = ConsoleLib.Console.ColorUtility.StripFormatting(colorString).Length;
            return (currentLength >= desiredLength) ? colorString : colorString.PadLeft(colorString.Length + (desiredLength - currentLength));
        }

        public void RestoreGameConsole()
        {
            ScreenBuffer scrapBuffer = ScreenBuffer.GetScrapBuffer2(true);
            foreach (KeyValuePair<Coords, ConsoleChar> gameBlock in this.CachedConsoleChars)
            {
                scrapBuffer[gameBlock.Key.X, gameBlock.Key.Y].Copy(gameBlock.Value);
            }
            Popup._TextConsole.DrawBuffer(scrapBuffer, null, false);
        }

        public bool LastCustomOverwritePersists()
        {
            if (this.CachedOverwriteChars.Count <= 0)
            {
                return false;
            }
            foreach (KeyValuePair<Coords, char> customBlock in this.CachedOverwriteChars)
            {
                if (TextConsole.CurrentBuffer[customBlock.Key.X, customBlock.Key.Y].Char != customBlock.Value)
                {
                    return false;
                }
            }
            return true;
        }

        public double GetItemPricePer(GameObject item)
        {
            return (item.GetIntProperty("Currency", 0) != 0) ? item.ValueEach : item.ValueEach * (double)this.CopyOfInternalMethod_TradeUI_GetMultiplier();
        }

        public float CopyOfInternalMethod_TradeUI_GetMultiplier()
        {
            GameObject body = XRLCore.Core.Game.Player.Body;
            if (!body.Statistics.ContainsKey("Ego"))
            {
                return 0.25f;
            }
            float num = (float)body.Statistics["Ego"].Modifier;
            if (body.HasPart("Persuasion_SnakeOiler"))
            {
                num += 2f;
            }
            if (body.HasEffect("Glotrot"))
            {
                num = -3f;
            }
            float num2 = 0.35f + 0.07f * num;
            if (body.HasPart("SociallyRepugnant"))
            {
                num2 /= 5f;
            }
            if (num2 > 0.95f)
            {
                num2 = 0.95f;
            }
            else if (num2 < 0.05f)
            {
                num2 = 0.05f;
            }
            return num2;
        }
    }
}

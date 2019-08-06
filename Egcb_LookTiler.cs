using System.Collections.Generic;
using XRL.UI;
using ConsoleLib.Console;
using GameObject = XRL.World.GameObject;
using Egocarib.Console;

namespace Egocarib.Code
{
    public class Egcb_LookTiler
    {
        private readonly GameObject LookTarget;
        private readonly TileMaker LookTargetInfo;
        private readonly string LookTargetName;
        private readonly bool bLookTargetValid;
        private readonly List<Coords> LastTileCoordsList;

        public Egcb_LookTiler(GameObject target)
        {
            this.LookTarget = target;
            this.LookTargetName = ConsoleLib.Console.ColorUtility.StripFormatting(target.DisplayName);
            this.LookTargetInfo = new TileMaker(target);
            this.bLookTargetValid = this.LookTarget != null
                && this.LookTarget.IsValid()
                && this.LookTargetInfo.IsValid()
                && !string.IsNullOrEmpty(this.LookTargetName);
            this.LastTileCoordsList = new List<Coords>();
        }

        public Egcb_LookTiler()
        {
            this.bLookTargetValid = false;
        }

        public void FrameCheck() //called each frame
        {
            if (this.bLookTargetValid)
            {
                this.UpdateScreen();
            }
        }

        public void UpdateScreen()
        {
            //check if previously drawn tiles are still rendered in the same spot(s) on screen - if so, no update needed
            if (this.LastTileCoordsList.Count > 0)
            {
                bool tilesValid = true;
                foreach (Coords coords in this.LastTileCoordsList)
                {
                    if (!this.LookTargetInfo.IsTileOnScreen(coords))
                    {
                        tilesValid = false;
                    }
                }
                if (tilesValid)
                {
                    return;
                }
            }
            this.LastTileCoordsList.Clear();
            string description = this.LookTargetName;
            ScreenBuffer scrapBuffer = ScreenBuffer.GetScrapBuffer2(true);
            UnityEngine.Color color_y = ColorUtility.usfColorMap[7];
            UnityEngine.Color color_k = ColorUtility.usfColorMap[0];
            bool bDidDraw = false;
            for (int y = 1; y < 24; y++)
            {
                for (int x = 0; x < 39; x++)
                {
                    if (scrapBuffer[x, y].Char == 'Ý' && scrapBuffer[x, y].Foreground == color_y && scrapBuffer[x, y].Background == color_k) //this character is the left thick border line of a popup dialog
                    {
                        if (scrapBuffer[x + 2, y + 1].Char == description[0]) //item name found in expected spot on the first line of the pop-up box.
                        {
                            int targetCol = 0;
                            int charIdx = 0;
                            int targetRow = y + 1;
                            for (int letterPos = x + 3; letterPos < 80; letterPos++)
                            {
                                charIdx++;
                                if (charIdx < description.Length)
                                {
                                    if (scrapBuffer[letterPos, targetRow].Char != description[charIdx])
                                    {
                                        if (charIdx > 30 && scrapBuffer[letterPos, targetRow].Char == ' ')
                                        {
                                            //matched at least 30 characters and we now have unexpected blank spaces - this likely means the
                                            //display name wrapped to the next line, and we can add the tile here after the wrap point
                                            if (scrapBuffer[letterPos - 1, targetRow].Char == ' ')
                                            {
                                                targetCol = letterPos;
                                            }
                                            else if (scrapBuffer[letterPos + 1, targetRow].Char == ' ')
                                            {
                                                targetCol = letterPos + 1;
                                            }
                                        }
                                        break;
                                    }
                                }
                                else
                                {
                                    if (scrapBuffer[letterPos, targetRow].Char == ' ' && scrapBuffer[letterPos + 1, targetRow].Char == ' ')
                                    {
                                        targetCol = letterPos + 1;
                                    }
                                    break;
                                }
                            }
                            if (targetCol > 0) //identified a spot to put the item's tile.
                            {
                                //draw tile
                                this.LookTargetInfo.WriteTileToBuffer(scrapBuffer, targetCol, targetRow);
                                this.LastTileCoordsList.Add(new Coords(targetCol, targetRow));
                                bDidDraw = true;
                            }
                        }
                    }
                }
            }
            if (bDidDraw)
            {
                Popup._TextConsole.DrawBuffer(scrapBuffer, null, false);
            }
        }
    }
}

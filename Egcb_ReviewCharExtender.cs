using System;
using System.Collections.Generic;
using XRL.UI;
using XRL.Core;
using XRL.World.Parts;
using ConsoleLib.Console;
using GameObject = XRL.World.GameObject;
using UnityEngine;
using XRL;

namespace Egocarib.Code
{
    public class Egcb_ReviewCharExtender
    {
        public static string TileToApply = string.Empty;
        public static string TileToApplyDetailColor;
        public static GameObject TileTarget;
        private static int cachedTileIndex = -999;
        private static int cachedColorIndex = -999;
        public readonly char previewTileForegroundColor;
        private bool bTileSelectorActive = false;
        private bool bInitialized = false;
        private bool bDisabled = false;
        private bool bWasShown = false;
        private bool bProcessingKey = false;
        private bool bAnimating = false;
        private int AnimationFrame = 0;
        private string AnimationDirection;
        public readonly int tileBoxWidth = 11;
        public readonly int tileBoxHeight = 7;
        public readonly int tileBoxStartX = 29;
        public readonly int tileBoxStartY = 2;
        public List<Tuple<string, string>> TileData;
        private int tileDataIndex;
        private int detailColorListIndex = -1;
        private readonly List<string> detailColorListStrings = new List<string>() { "b", "B", "c", "C", "g", "G", "w", "W", "o", "O", "r", "R", "m", "M", "Y", "y", "K", "k" };
        private readonly List<Color> detailColorList = new List<Color>()
        {
            ConsoleLib.Console.ColorUtility.ColorMap['b'],
            ConsoleLib.Console.ColorUtility.ColorMap['B'],
            ConsoleLib.Console.ColorUtility.ColorMap['c'],
            ConsoleLib.Console.ColorUtility.ColorMap['C'],
            ConsoleLib.Console.ColorUtility.ColorMap['g'],
            ConsoleLib.Console.ColorUtility.ColorMap['G'],
            ConsoleLib.Console.ColorUtility.ColorMap['w'],
            ConsoleLib.Console.ColorUtility.ColorMap['W'],
            ConsoleLib.Console.ColorUtility.ColorMap['o'],
            ConsoleLib.Console.ColorUtility.ColorMap['O'],
            ConsoleLib.Console.ColorUtility.ColorMap['r'],
            ConsoleLib.Console.ColorUtility.ColorMap['R'],
            ConsoleLib.Console.ColorUtility.ColorMap['m'],
            ConsoleLib.Console.ColorUtility.ColorMap['M'],
            ConsoleLib.Console.ColorUtility.ColorMap['Y'],
            ConsoleLib.Console.ColorUtility.ColorMap['y'],
            ConsoleLib.Console.ColorUtility.ColorMap['K'],
            ConsoleLib.Console.ColorUtility.ColorMap['k']
        };

        public bool Busy
        {
            get
            {
                return (this.bAnimating || this.bProcessingKey);
            }
        }

        public Egcb_ReviewCharExtender()
        {
            List<SubtypeEntry> gameSubtypes = new List<SubtypeEntry>();
            foreach (SubtypeClass subtypeClass in SubtypeFactory.Classes)
            {
                gameSubtypes.AddRange(subtypeClass.GetAllSubtypes());
            }
            this.TileData = new List<Tuple<string, string>>(gameSubtypes.Count);
            this.tileDataIndex = 0;
            foreach (SubtypeEntry subtype in gameSubtypes)
            {
                this.TileData.Add(new Tuple<string, string>(subtype.Tile, subtype.DetailColor));
                if (CreateCharacter.Template.Subtype == subtype.Name)
                {
                    this.tileDataIndex = this.TileData.Count - 1;
                }
            }
            //determine foreground color for tiles ('y' unless player has Photosynthetic skin, then 'g')
            this.previewTileForegroundColor = 'y';
            Mutations mutations = CreateCharacter.Template.PlayerBody.GetPart<Mutations>();
            if (mutations != null && mutations.MutationList.Count > 0)
            {
                for (int i = 0; i < mutations.MutationList.Count; i++)
                {
                    if (mutations.MutationList[i].DisplayName == "Photosynthetic Skin")
                    {
                        this.previewTileForegroundColor = 'g';
                        break;
                    }
                }
            }
            //restore cached tile indices if still relevant (this is used to preserve the player's choice after entering a world seed,
            //because QudUX interprets that as a screen transition (to Popup:AskString) and re-instantiates a new version of this class.
            if (Egcb_ReviewCharExtender.TileTarget == CreateCharacter.Template.PlayerBody)
            {
                if (Egcb_ReviewCharExtender.cachedTileIndex != -999)
                {
                    this.tileDataIndex = Egcb_ReviewCharExtender.cachedTileIndex;
                    Egcb_ReviewCharExtender.cachedTileIndex = -999;
                    this.detailColorListIndex = Egcb_ReviewCharExtender.cachedColorIndex != -999 ? Egcb_ReviewCharExtender.cachedColorIndex : -1;
                    Egcb_ReviewCharExtender.cachedColorIndex = -999;
                    this.SaveCustomTile();
                    this.bWasShown = true;
                }
                
            }
        }

        public void FrameCheck() //called each frame
        {
            if (this.bAnimating)
            {
                this.AnimateTiles();
            }
            else
            {
                this.UpdateScreen();
            }
        }

        public void UpdateScreen()
        {
            ScreenBuffer scrapBuffer = null;
            if (this.bDisabled)
            {
                return;
            }
            if (!this.bInitialized)
            {
                this.bInitialized = true;
                string errorMessage = string.Empty;
                scrapBuffer = ScreenBuffer.GetScrapBuffer2(true);
                for (int i = 17; i < 23; i++)
                {
                    if (scrapBuffer[17, i].Char == 'M')
                    {
                        this.bDisabled = true; //"M" key is already taken - could happen after a game update where devs add some new "M"-associated option. In that case we won't override the core game.
                        errorMessage = "Character 'M' already reserved by another action on the [ Character Creation - Complete ] screen.";
                    }
                }
                //acceptible options (empty or we already wrote it to the buffer before a popup appeared/etc)
                string expectedModifyLineString = "                                   ";
                if (scrapBuffer[17, 23].Char == 'M')
                {
                    if (scrapBuffer[21, 23].Char == 'M')
                    {
                        expectedModifyLineString = "M - Modify character sprite        ";
                    }
                    else if (scrapBuffer[21, 23].Char == 'C')
                    {
                        expectedModifyLineString = "M - Confirm & save sprite          ";
                    }
                }
                for (int i = 17; i < 50; i++)
                {
                    if (scrapBuffer[i, 23].Char != expectedModifyLineString[i - 17]) //something unexpected is already in the row we were planning on adding this option to
                    {
                        this.bDisabled = true;
                        errorMessage = "Screen text already present in location where Character Tile option was going to be added on the [ Character Creation - Complete ] screen.";
                    }
                }
                if (expectedModifyLineString[0] == ' ') //only check if we didn't already add the option (i.e. after Enter World Seed popup, we already drew this stuff)
                {
                    for (int x = this.tileBoxStartX; x < this.tileBoxStartX + this.tileBoxWidth; x++)
                    {
                        for (int y = this.tileBoxStartY; y < this.tileBoxStartY + this.tileBoxHeight; y++)
                        {
                            if (scrapBuffer[x, y].Char != ' ')
                            {
                                this.bDisabled = true;
                                errorMessage = "Character Tile option can't be added because unexpected text found at coordinates " + x + ", " + y + " on the [ Character Creation - Complete ] screen.";
                            }
                        }
                    }
                }
                if (this.bDisabled)
                {
                    Debug.Log("QudUX Mod: Character Tile Option Error:\n    " + errorMessage);
                    return;
                }
            }
            if (scrapBuffer == null)
            {
                scrapBuffer = ScreenBuffer.GetScrapBuffer2(true);
            }
            if (this.bTileSelectorActive)
            {
                this.bWasShown = true;
                scrapBuffer.Goto(17, 23);
                scrapBuffer.Write("&WM&y - Confirm && save sprite  ");
                scrapBuffer.Goto(this.tileBoxStartX + 1, this.tileBoxStartY);
                scrapBuffer.Write("[ &GSprite&y ]");
                this.ClearTileBox(scrapBuffer, true);
                this.DrawTiles(scrapBuffer);
                scrapBuffer.Goto(this.tileBoxStartX, this.tileBoxStartY + 4);
                scrapBuffer.Write("&W\u001b4&y &Kimage &W6\u001a");
                scrapBuffer.Goto(this.tileBoxStartX, this.tileBoxStartY + 5);
                scrapBuffer.Write("&W\u00188&y &Kcolor &W2\u0019");
                //scrapBuffer.Goto(this.tileBoxStartX, this.tileBoxStartY + 6);
                //scrapBuffer.Write("&y &WM &yto save");
            }
            else
            {
                scrapBuffer.Goto(17, 23);
                scrapBuffer.Write("&WM&y - Modify character sprite        ");
                this.ClearTileBox(scrapBuffer);
                if (this.bWasShown)
                {
                    scrapBuffer.Goto(this.tileBoxStartX + 1, this.tileBoxStartY);
                    scrapBuffer.Write("[ &GSprite&y ]");
                    this.DrawTiles(scrapBuffer, true);
                }
            }
            Popup._TextConsole.DrawBuffer(scrapBuffer, null, false);
        }

        public void ClearTileBox(ScreenBuffer scrapBuffer = null, bool bTileRowOnly = false, bool bDraw = false)
        {
            if (scrapBuffer == null)
            {
                scrapBuffer = ScreenBuffer.GetScrapBuffer2(true);
            }
            for (int x = this.tileBoxStartX; x < (this.tileBoxStartX + this.tileBoxWidth); x++)
            {
                for (int y = this.tileBoxStartY + 1; y < (this.tileBoxStartY + (bTileRowOnly ? 3 : this.tileBoxHeight)); y++)
                {
                    scrapBuffer.Goto(x, y);
                    scrapBuffer.Write("&y^k ");
                }
            }
            if (bDraw)
            {
                Popup._TextConsole.DrawBuffer(scrapBuffer, null, false);
            }
        }

        public void AnimateTiles(string direction = null)
        {
            if (this.TileData.Count < 4) //minimum # tiles needed to animate
            {
                this.bAnimating = false;
                return;
            }
            bool inProgress = string.IsNullOrEmpty(direction);
            if (inProgress)
            {
                if (string.IsNullOrEmpty(this.AnimationDirection))
                {
                    this.bAnimating = false;
                    return;
                }
                direction = this.AnimationDirection;
            }
            else
            {
                this.bAnimating = true;
                this.AnimationFrame = 0;
                this.AnimationDirection = direction;
            }
            if (this.AnimationFrame < 2)
            {
                int animateOffset = (this.AnimationFrame + 1) * ((direction == "RIGHT") ? 1 : -1);
                ScreenBuffer scrapBuffer = ScreenBuffer.GetScrapBuffer2(true);
                this.ClearTileBox(scrapBuffer, true);
                this.DrawTiles(scrapBuffer, false, animateOffset, true);
            }
            this.AnimationFrame++;
            if (this.AnimationFrame == 2)
            {
                this.tileDataIndex -= ((direction == "RIGHT") ? 1 : -1);
                this.AnimationDirection = null;
                this.bAnimating = false;
                this.SaveCustomTile();
            }
        }

        public void DrawTiles(ScreenBuffer scrapBuffer, bool centerTileOnly = false, int animateOffset = 0, bool bDraw = false)
        {
            if (this.TileData.Count < 1)
            {
                return; //no tile data has been loaded
            }
            if (string.IsNullOrEmpty(CreateCharacter.Template.PlayerBody.pRender.DetailColor))
            {
                return; //unexpected missing tile data
            }
            this.ConstrainTileIndex();
            int x = this.tileBoxStartX + ((this.tileBoxWidth - 1) / 2);
            int y = this.tileBoxStartY + 2;

            //draw main tile
            //--------------------------------------
            scrapBuffer[x + animateOffset, y].SetBackground('k');
            scrapBuffer[x + animateOffset, y].SetForeground(this.previewTileForegroundColor);
            scrapBuffer[x + animateOffset, y].TileLayerBackground[0] = (this.detailColorListIndex >= 0)
                                                     ? this.detailColorList[this.detailColorListIndex] //custom detail color
                                                     : ConsoleLib.Console.ColorUtility.ColorMap[this.TileData[this.tileDataIndex].Item2[0]]; //default detail color for subtype
            scrapBuffer[x + animateOffset, y].TileLayerForeground[0] = ConsoleLib.Console.ColorUtility.ColorMap[this.previewTileForegroundColor];
            scrapBuffer[x + animateOffset, y].Tile = this.TileData[this.tileDataIndex].Item1;
            if (animateOffset != 0) //fade main color to 'K' while animating away from/toward center.
            {
                scrapBuffer[x + animateOffset, y].SetForeground('K');
                scrapBuffer[x + animateOffset, y].TileLayerForeground[0] = ConsoleLib.Console.ColorUtility.ColorMap['K'];
            }
            if (Math.Abs(animateOffset) == 2) //also fade detail color on second frame (furthest from center)
            {
                scrapBuffer[x + animateOffset, y].TileLayerBackground[0] = ConsoleLib.Console.ColorUtility.ColorMap['K']; 
            }

            if (!centerTileOnly)
            {
                //draw second tile (left-hand side)
                //--------------------------------------
                if (this.TileData.Count >= 3 || (this.TileData.Count == 2 && this.tileDataIndex == 1))
                {
                    this.tileDataIndex--; //temporarily shift index left 1
                    this.ConstrainTileIndex();
                    char foregroundColor, detailColor;
                    if (animateOffset <= 0) //0, -1, or -2 (static or moving leftward off screen)
                    {
                        detailColor = (animateOffset != -1) ? 'K' : 'k'; //black out detail color at animationOffset 1
                        foregroundColor = (animateOffset != -2) ? 'K' : 'k'; //black out foreground color at animationOffset 2 (allow detail color back in so it's not totally black)
                    }
                    else //1 or 2 (moving rightward toward center)
                    {
                        foregroundColor = 'K'; //foreground color darkened for both frames
                        detailColor = (animateOffset != 2) ? 'K' : this.TileData[this.tileDataIndex].Item2[0]; //detail color only on second frame (closest to center)
                    }
                    scrapBuffer[x + animateOffset - 3, y].SetBackground('k');
                    scrapBuffer[x + animateOffset - 3, y].SetForeground(foregroundColor);
                    scrapBuffer[x + animateOffset - 3, y].TileLayerBackground[0] = ConsoleLib.Console.ColorUtility.ColorMap[detailColor];
                    scrapBuffer[x + animateOffset - 3, y].TileLayerForeground[0] = ConsoleLib.Console.ColorUtility.ColorMap[foregroundColor];
                    scrapBuffer[x + animateOffset - 3, y].Tile = this.TileData[this.tileDataIndex].Item1;
                    this.tileDataIndex++; //restore index
                }
                //draw third tile (right-hand side)
                //--------------------------------------
                if (this.TileData.Count >= 3 || (this.TileData.Count == 2 && this.tileDataIndex == 0))
                {
                    this.tileDataIndex++; //temporarily shift index right 1
                    this.ConstrainTileIndex();
                    char foregroundColor, detailColor;
                    if (animateOffset >= 0) //0, 1, or 2 (static or moving rightward offscreen)
                    {
                        detailColor = (animateOffset != 1) ? 'K' : 'k'; //black out detail color at animationOffset 1
                        foregroundColor = (animateOffset != 2) ? 'K' : 'k'; //black out foreground color at animationOffset 2 (allow detail color back in so it's not totally black)
                    }
                    else //-1 or -2 (moving leftward toward center)
                    {
                        foregroundColor = 'K'; //foreground color darkened for both frames
                        detailColor = (animateOffset != -2) ? 'K' : this.TileData[this.tileDataIndex].Item2[0]; //detail color only on second frame (closest to center)
                    }
                    scrapBuffer[x + animateOffset + 3, y].SetBackground('k');
                    scrapBuffer[x + animateOffset + 3, y].SetForeground(foregroundColor);
                    scrapBuffer[x + animateOffset + 3, y].TileLayerBackground[0] = ConsoleLib.Console.ColorUtility.ColorMap[detailColor];
                    scrapBuffer[x + animateOffset + 3, y].TileLayerForeground[0] = ConsoleLib.Console.ColorUtility.ColorMap[foregroundColor];
                    scrapBuffer[x + animateOffset + 3, y].Tile = this.TileData[this.tileDataIndex].Item1;
                    this.tileDataIndex--; //restore index
                }
            }
            if (bDraw)
            {
                Popup._TextConsole.DrawBuffer(scrapBuffer, null, false);
            }
        }

        public void ConstrainTileIndex()
        {
            //assumes TileData.Count > 0 (will get division by zero error if it's not)
            if (this.tileDataIndex >= this.TileData.Count || this.tileDataIndex < 0)
            {
                this.tileDataIndex %= this.TileData.Count;
            }
            if (this.tileDataIndex < 0)
            {
                this.tileDataIndex = this.TileData.Count + this.tileDataIndex;
            }
        }

        public void ReactToKeyRequest(string key)
        {
            if (this.Busy)
            {
                return;
            }
            this.bProcessingKey = true;
            if (key == "M")
            {
                if (!this.bTileSelectorActive)
                {
                    this.bTileSelectorActive = true;
                    this.UpdateScreen();
                }
                else if (this.bTileSelectorActive)
                {
                    this.bTileSelectorActive = false;
                    //save tile data (will be set by Egcb_UIMonitor after the WorldCreationProgress GUI, which is where the player tile actually gets initialized by the game)
                    this.SaveCustomTile();
                    this.UpdateScreen();
                }
            }
            else if (this.bTileSelectorActive) //only resond to other keys when Tile Selection screen is active
            {
                if (key == "RIGHT")
                {
                    this.AnimateTiles(key);
                    this.detailColorListIndex = -1;
                }
                else if (key == "LEFT")
                {
                    this.AnimateTiles(key);
                    this.detailColorListIndex = -1;
                }
                else if (key == "UP")
                {
                    this.detailColorListIndex--;
                    if (this.detailColorListIndex < 0)
                    {
                        this.detailColorListIndex = this.detailColorList.Count - 1;
                    }
                    this.UpdateScreen();
                    this.SaveCustomTile();
                }
                else if (key == "DOWN")
                {
                    this.detailColorListIndex++;
                    if (this.detailColorListIndex >= this.detailColorList.Count)
                    {
                        this.detailColorListIndex = 0;
                    }
                    this.UpdateScreen();
                    this.SaveCustomTile();
                }
            }
            this.bProcessingKey = false;
        }

        public void SaveCustomTile()
        {
            if (this.tileDataIndex >= 0)
            {
                Egcb_ReviewCharExtender.TileTarget = CreateCharacter.Template.PlayerBody;
                Egcb_ReviewCharExtender.TileToApply = this.TileData[this.tileDataIndex].Item1;
                Egcb_ReviewCharExtender.TileToApplyDetailColor = (this.detailColorListIndex >= 0) ? this.detailColorListStrings[this.detailColorListIndex] : this.TileData[this.tileDataIndex].Item2;
                Egcb_ReviewCharExtender.cachedColorIndex = (this.detailColorListIndex >= 0) ? this.detailColorListIndex : -999;
                Egcb_ReviewCharExtender.cachedTileIndex = this.tileDataIndex;
            }
        }

        public static void ApplyCustomTile()
        {
            if (Egcb_ReviewCharExtender.TileTarget == XRLCore.Core.Game.Player.Body && !string.IsNullOrEmpty(Egcb_ReviewCharExtender.TileToApply))
            {
                Egcb_ReviewCharExtender.TileTarget.pRender.Tile = Egcb_ReviewCharExtender.TileToApply;
                Egcb_ReviewCharExtender.TileTarget.pRender.DetailColor = Egcb_ReviewCharExtender.TileToApplyDetailColor;
                Egcb_ReviewCharExtender.TileTarget = null;
                Egcb_ReviewCharExtender.TileToApply = string.Empty;
                Egcb_ReviewCharExtender.cachedColorIndex = -999;
                Egcb_ReviewCharExtender.cachedTileIndex = -999;
            }
        }
    }
}

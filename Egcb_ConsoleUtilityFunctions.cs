using XRL.Core;
using XRL.World;
using XRL.World.Parts;
using ConsoleLib.Console;
using GameObject = XRL.World.GameObject;
using Color = UnityEngine.Color;

namespace Egocarib.Console
{
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

    public class TileMaker
    {
        public string Tile;
        public string RenderString;
        public string BackgroundString;
        public char DetailColorChar;
        public char ForegroundColorChar;
        public char BackgroundColorChar;
        public ushort Attributes;
        public Color DetailColor;
        public Color ForegroundColor;
        public Color BackgroundColor;

        public bool IsValid()
        {
            return (!string.IsNullOrEmpty(this.Tile) || !string.IsNullOrEmpty(this.RenderString));
        }

        public TileMaker(string blueprintString)
        {
            GameObject go = null;
            if (GameObjectFactory.Factory.Blueprints.ContainsKey(blueprintString))
            {
                go = GameObjectFactory.Factory.CreateObject(blueprintString);
            }
            this.Initialize(go, false);
            if (go != null)
            {
                go.Destroy();
            }
        }

        public TileMaker(GameObject go)
        {
            this.Initialize(go);
        }

        //writes a tile to a ScreenBuffer. You'll still need to draw the buffer yourself
        public bool WriteTileToBuffer(ScreenBuffer scrapBuffer, int x, int y)
        {
            if (scrapBuffer == null || x < 0 || x >= scrapBuffer.Width || y < 0 || y >= scrapBuffer.Height)
            {
                return false;
            }
            scrapBuffer[x, y].Attributes = this.Attributes;
            if (!string.IsNullOrEmpty(this.Tile))
            {
                scrapBuffer[x, y].TileLayerBackground[0] = this.DetailColor;
                scrapBuffer[x, y].TileLayerForeground[0] = this.ForegroundColor;
                scrapBuffer[x, y].Tile = this.Tile;
            }
            else if (!string.IsNullOrEmpty(this.RenderString))
            {
                scrapBuffer[x, y].ClearTileLayers();
                scrapBuffer[x, y].Char = this.RenderString[0];
            }
            else
            {
                return false;
            }
            return true;
        }

        //returns true if the tile is already applied at the specified screen coordinates
        public bool IsTileOnScreen(int x, int y)
        {
            if (x < 0 || x >= TextConsole.CurrentBuffer.Width || y < 0 || y >= TextConsole.CurrentBuffer.Height)
            {
                return false;
            }
            ConsoleChar screenChar = TextConsole.CurrentBuffer[x, y];
            bool tileApplied = screenChar != null
                && screenChar.Attributes == this.Attributes
                && ((!string.IsNullOrEmpty(this.Tile)
                        && screenChar.Tile == this.Tile
                        && screenChar.TileLayerBackground[0] == this.DetailColor
                        && screenChar.TileLayerForeground[0] == this.ForegroundColor)
                    || (!string.IsNullOrEmpty(this.RenderString)
                        && screenChar.Char == this.RenderString[0]));
            return tileApplied;
        }
        public bool IsTileOnScreen(Coords coords)
        {
            return this.IsTileOnScreen(coords.X, coords.Y);
        }

        private void Initialize(GameObject go, bool renderOK = true)
        {
            this.Tile = string.Empty;
            this.RenderString = string.Empty;
            this.BackgroundString = string.Empty;
            this.DetailColorChar = 'k';
            this.ForegroundColorChar = 'y';
            this.BackgroundColorChar = 'k';
            this.DetailColor = ConsoleLib.Console.ColorUtility.ColorMap['k'];
            this.ForegroundColor = ConsoleLib.Console.ColorUtility.ColorMap['y'];
            this.BackgroundColor = ConsoleLib.Console.ColorUtility.ColorMap['k'];

            //gather render data for GameObject similar to how the game does it in Cell.cs
            Render pRender = go?.pRender;
            //if (pRender == null || pRender.Tile == null || !pRender.Visible || Globals.RenderMode != RenderModeType.Tiles)
            if (pRender == null || !pRender.Visible || Globals.RenderMode != RenderModeType.Tiles)
            {
                return;
            }
            RenderEvent renderData = new RenderEvent();
            Examiner examinerPart = go.GetPart<Examiner>();
            if (examinerPart != null && !string.IsNullOrEmpty(examinerPart.UnknownTile) && !go.Understood())
            {
                renderData.Tile = examinerPart.UnknownTile;
            }
            else
            {
                renderData.Tile = go.pRender.Tile;
            }
            if (!string.IsNullOrEmpty(pRender.TileColor))
            {
                renderData.ColorString = pRender.TileColor;
            }
            else
            {
                renderData.ColorString = pRender.ColorString;
            }
            if (renderOK) //we can't render blueprint-created objects, because the game will throw errors trying to check their current cell
            {
                go.Render(renderData);
            }

            //renderData.Tile can be null if something has a temporary character replacement, like the up arrow from flying
            this.Tile = !string.IsNullOrEmpty(renderData.Tile) ? renderData.Tile : pRender.Tile;
            this.RenderString = !string.IsNullOrEmpty(renderData.RenderString) ? renderData.RenderString : pRender.RenderString;
            this.BackgroundString = renderData.BackgroundString;

            ////DEBUG
            //UnityEngine.Debug.Log("Render data from GameObject.Render() for " + go.DisplayName + ":\n    Tile=" + renderData.Tile
            //    + "\n    ColorString=" + renderData.ColorString
            //    + "\n    DetailColor=" + renderData.DetailColor
            //    + "\n    RenderString=" + renderData.RenderString
            //    + "\n    BackgroundString=" + renderData.BackgroundString
            //    + "\nRender data from object itself:"
            //    + "\n    Tile=" + pRender.Tile
            //    + "\n    RenderString=" + pRender.RenderString
            //    + "\n    TileColor=" + pRender.TileColor
            //    + "\n    ColorString=" + pRender.ColorString
            //    + "\n    DetailColor=" + pRender.DetailColor);
            ////DEBUG

            //save render data in our custom TileColorData format, using logic similar to QudItemListElement.InitFrom()
            if (!string.IsNullOrEmpty(pRender.DetailColor))
            {
                this.DetailColor = ConsoleLib.Console.ColorUtility.ColorMap[pRender.DetailColor[0]];
                this.DetailColorChar = pRender.DetailColor[0];
            }
            string colorString = renderData.ColorString + (string.IsNullOrEmpty(this.Tile) ? this.BackgroundString : string.Empty);
            if (!string.IsNullOrEmpty(colorString))
            {
                for (int j = 0; j < colorString.Length; j++)
                {
                    if (colorString[j] == '&' && j < colorString.Length - 1)
                    {
                        if (colorString[j + 1] == '&')
                        {
                            j++;
                        }
                        else
                        {
                            this.ForegroundColor = ConsoleLib.Console.ColorUtility.ColorMap[colorString[j + 1]];
                            this.ForegroundColorChar = colorString[j + 1];
                        }
                    }
                    if (colorString[j] == '^' && j < colorString.Length - 1)
                    {
                        if (colorString[j + 1] == '^')
                        {
                            j++;
                        }
                        else
                        {
                            this.BackgroundColor = ConsoleLib.Console.ColorUtility.ColorMap[colorString[j + 1]];
                            this.BackgroundColorChar = colorString[j + 1];
                        }
                    }
                }
            }
            this.Attributes = ColorUtility.MakeColor(ColorUtility.CharToColorMap[this.ForegroundColorChar], ColorUtility.CharToColorMap[this.BackgroundColorChar]);
        }
    }
}
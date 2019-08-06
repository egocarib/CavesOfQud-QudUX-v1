using XRL.UI;
using ConsoleLib.Console;
using Egocarib.Console;
using GameObject = XRL.World.GameObject;

namespace Egocarib.Code
{
    public class Egcb_ConversationTiler
    {
        private readonly GameObject ConversationTarget;
        private readonly TileMaker ConversationTargetInfo;
        private readonly string ConversationTargetName;
        private readonly bool bConversationTargetValid;
        private Coords LastTileCoords = null;

        public Egcb_ConversationTiler(GameObject target)
        {
            this.ConversationTarget = target;
            this.ConversationTargetName = ConsoleLib.Console.ColorUtility.StripFormatting(target.DisplayName);
            this.ConversationTargetInfo = new TileMaker(target);
            this.bConversationTargetValid = this.ConversationTarget != null
                && this.ConversationTarget.IsValid()
                && this.ConversationTargetInfo.IsValid()
                && !string.IsNullOrEmpty(this.ConversationTargetName)
                && this.ConversationTargetName.Length <= 74; //no room to render tile if name is greater than 74 characters (it'll overflow off the side of the screen)
        }

        public Egcb_ConversationTiler() //default constructor, not intended to be used.
        {
            this.bConversationTargetValid = false;
        }

        public void FrameCheck() //called each frame
        {
            if (this.bConversationTargetValid)
            {
                this.UpdateScreen();
            }
        }

        public void UpdateScreen()
        {
            if (this.LastTileCoords != null && this.ConversationTargetInfo.IsTileOnScreen(this.LastTileCoords))
            {
                return; //tile persists where we last drew it, no need to update this frame
            }
            string description = this.ConversationTargetName;
            ScreenBuffer scrapBuffer = ScreenBuffer.GetScrapBuffer2(true);
            //check if the upper left corner of the screen represents the typical conversation screenbox, i.e. ┌─[ name ]─
            ushort screenBoxAttributes = ConsoleLib.Console.ColorUtility.MakeColor(TextColor.Grey, TextColor.Black);
            bool looksLikeDefaultScreenBox = scrapBuffer[0, 0].Char == 'Ú' && scrapBuffer[0, 0].Attributes == screenBoxAttributes
                && scrapBuffer[1, 0].Char == 'Ä' && scrapBuffer[1, 0].Attributes == screenBoxAttributes
                && scrapBuffer[2, 0].Char == '[' && scrapBuffer[2, 0].Attributes == screenBoxAttributes
                && scrapBuffer[3, 0].Char == ' ';
            if (!looksLikeDefaultScreenBox)
            {
                return;
            }

            int x;
            //verify the name of the person we're conversing with
            for (x = 4; x < 4 + description.Length; x++)
            {
                if (scrapBuffer[x, 0].Char != description[x - 4])
                {
                    return; //ConversationUI title didn't match the expected name; don't draw tile
                }
            }

            //draw tile now that we've verified the name
            x = 4 + description.Length;
            scrapBuffer[x++, 0].Char = ' ';
            this.ConversationTargetInfo.WriteTileToBuffer(scrapBuffer, x, 0);
            this.LastTileCoords = new Coords(x, 0);
            if (x++ < 79)
            {
                scrapBuffer[x, 0].Char = ' ';
            }
            if (x++ < 79)
            {
                scrapBuffer[x, 0].Char = ']';
            }
            
            //draw the updated buffer to the screen
            Popup._TextConsole.DrawBuffer(scrapBuffer, null, false);
        }
    }
}

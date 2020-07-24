using UnityEngine;
using Egocarib.Code;

namespace XRL.World.Parts
{
    [HasModSensitiveStaticCache]
    public static class Egcb_UILoader
    {
        private static UnityEngine.GameObject monitorObject; //used for activating monobehavior (different from Qud's typical GameObject type)
        private static bool bStarted = false;

        /// <summary>
        /// Called on game startup. Can potentially be called multiple times if mod loadout changes.
        /// </summary>
        [ModSensitiveCacheInit]
        public static void Bootup()
        {
            if (Egcb_UILoader.bStarted == true)
            {
                return;
            }
            Egcb_UILoader.bStarted = true;
            Debug.Log("QudUX Mod: Successfully Initialized.");
            Egcb_UILoader.StartOptionsMonitor();
        }

        private static void StartOptionsMonitor()
        {
            if (!Egcb_UIMonitor.IsActive)
            {
                //using UnityEngine.GameObject.AddComponent is the only way that I know of to "instantiate" an instance of a
                //class that derives from MonoBehavior. We need MonoBehavior's Coroutine functionality to spin off a separate
                //"thread" to poll for the Options menu, because there is no event available in the game API for a mod to hook into.
                Egcb_UILoader.monitorObject = new UnityEngine.GameObject();
                Egcb_UIMonitor taskManager = monitorObject.AddComponent<Egcb_UIMonitor>();
                taskManager.Initialize();
            }
            return;
        }
    }
}

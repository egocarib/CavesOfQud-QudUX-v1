using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HistoryKit;
using XRL;
using XRL.Core;

namespace Egocarib.Code
{
    [HasGameBasedStaticCache]
    public static class Patch_Cache
    {
        [GameBasedStaticCache] //ensure this gets reset for each new game
        public static HashSet<string> UsedRuinsNames = new HashSet<string>();
    }

    [HarmonyPatch]
    public class Patch_XRL_Annals_QudHistoryFactory
    {
        static Type QudHistoryFactoryType = AccessTools.TypeByName("XRL.Annals.QudHistoryFactory");
        static MethodInfo Method_StatRandom = SymbolExtensions.GetMethodInfo(() => XRL.Rules.Stat.Random(0, 80));

        /// <summary>
        /// Calculate target method. This is necessary because QudHistoryFactory is an internal type, so we
        /// can't simply use typeof() on it and put it in the HarmonyPatch attribute.
        /// </summary>
        [HarmonyTargetMethod]
        static MethodBase TargetMethod()
        {
            return QudHistoryFactoryType.GetMethod("NameRuinsSite", new Type[] { typeof(History), typeof(bool).MakeByRefType() });
        }

        /// <summary>
        /// Postfix patch. Prevents duplicate ruins names from being generated - tries to generate a new name if
        /// the name has already been used. This is surprisingly common (especially after we remove "some
        /// forgotten ruins" from the picture). Ultimately this ensures all ruins names are unique.
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(ref string __result)
        {
            int ct = 0;
            while (Patch_Cache.UsedRuinsNames.Contains(__result))
            {
                if (ct++ > 10)
                {
                    //In practice, I've never seen this take more than 2 attempts, but adding a short circuit just in case.
                    XRLCore.Log("QudUX: (Warning) Failed to find a suitable name for Ruins location after >10 attempts. Allowing duplicate name: " + __result);
                    break;
                }
                __result = Egcb_JournalUtilities.GenerateName();
            }
            Patch_Cache.UsedRuinsNames.Add(__result);
            //XRLCore.Log("NameRuinsSite result: " + __result);
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //QudHistoryFactory.NameRuinsSite takes Stat.Random(0, 80) and then if the value is not less than
            //60, it returns "some forgotten ruins" as the name of a ruins site. To patch this function, we
            //will find the "Stat.Random(0, 80)" code, and replace it with "Stat.Random(0, 59)" which will
            //ensure that "some forgotten ruins" is never returned from the function.
            
            //Specifically, we need to find and overwrite the following sequence of IL instructions
            //	  IL_0321: ldc.i4.0
            //    IL_0322: ldc.i4.s  80
            //    IL_0324: call      int32 XRL.Rules.Stat::Random(int32, int32)

            var ins = new List<CodeInstruction>(instructions);
            for (var i = 0; i < ins.Count; i++)
            {
                if (ins[i].opcode == OpCodes.Ldc_I4_0 && (i + 2) < ins.Count)
                {
                    if (ins[i + 1].opcode == OpCodes.Ldc_I4_S && Convert.ToInt32(ins[i + 1].operand) == 80)
                    {
                        if (ins[i + 2].opcode == OpCodes.Call)
                        {
                            MethodInfo callMethodInfo = (MethodInfo)(ins[i + 2].operand);
                            if (callMethodInfo.MetadataToken == Method_StatRandom.MetadataToken)
                            {
                                //XRLCore.Log("QudUX: Patched QudHistoryFactory to remove \"some forgotten ruins\" as a naming option.");
                                //We have found our target triplet of IL instructions
                                ins[i + 1].operand = 59; //make the modification
                                return ins.AsEnumerable(); //return the modified instructions
                            }
                        }

                    }
                }
            }
            XRLCore.Log("QudUX: (Warning) Failed to patch QudHistoryFactory; \"some forgotten ruins\" may still be used to name locations.");
            return ins.AsEnumerable();
        }
    }
}

using HarmonyLib;
using ShinyShoe;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Patty_SoundChanger_MOD
{
    internal class TranspilerFix
    {
        // This patch is needed to fix BattleMusicChanged not using currentTrackName... thanks dev...
        [HarmonyTranspiler, HarmonyPatch(typeof(SoundManager), "PlayBattleMusic")]
        public static IEnumerable<CodeInstruction> OnBattleMusicChanged(IEnumerable<CodeInstruction> instructions)
        {
            var modifiedInstructions = new List<CodeInstruction>(instructions);
            int insertIdx = modifiedInstructions.FindIndex(codeInstruction =>
                                                           codeInstruction.opcode == OpCodes.Ldsfld &&
                                                           codeInstruction.operand.ToString().Contains("BattleMusicChanged"));

            if (insertIdx <= 0)
            {
                return instructions;
            }

            // Get the amount of instruction needed to be removed
            var removeCount = 1; // Start with current line
            for (var i = insertIdx; i < modifiedInstructions.Count; i++)
            {
                var code = modifiedInstructions[i];
                if (code.opcode == OpCodes.Callvirt &&
                    code.operand?.ToString()?.Contains("Dispatch") == true)
                {
                    break;
                }
                else
                {
                    removeCount++;
                }
            }
            modifiedInstructions.RemoveRange(insertIdx, removeCount);
            var newCodes = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.DeclaredField(typeof(SoundManager), "BattleMusicChanged")),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(SoundManager), "currentTrackName")),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.DeclaredMethod(typeof(Signal<string>), "Dispatch")),
            };
            modifiedInstructions.InsertRange(insertIdx, newCodes);

            /* Before
             * SoundManager.BattleMusicChanged.Dispatch(battleMusicTrack.publicTrackNameKey.Localize());
             * 
             * After
             * SoundManager.BattleMusicChanged.Dispatch(this.currentTrackName);
             */
            return modifiedInstructions;
        }

    }
}

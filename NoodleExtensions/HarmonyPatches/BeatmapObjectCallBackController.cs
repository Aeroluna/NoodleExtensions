﻿namespace NoodleExtensions.HarmonyPatches
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using CustomJSONData.CustomBeatmap;
    using HarmonyLib;
    using Heck;
    using static NoodleExtensions.NoodleObjectDataManager;

    [HeckPatch(typeof(BeatmapObjectCallbackController))]
    [HeckPatch("LateUpdate")]
    internal static class BeatmapObjectCallBackControllerLateUpdate
    {
        private static readonly MethodInfo _getAheadTime = SymbolExtensions.GetMethodInfo(() => GetAheadTime(null, null, 0));
        private static readonly MethodInfo _beatmapObjectSpawnControllerCallback = typeof(BeatmapObjectSpawnController).GetMethod("HandleBeatmapObjectCallback", BindingFlags.Public | BindingFlags.Instance);

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            bool foundAheadTime = false;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (!foundAheadTime &&
                    instructionList[i].opcode == OpCodes.Ldfld &&
                    ((FieldInfo)instructionList[i].operand).Name == "aheadTime")
                {
                    foundAheadTime = true;

                    instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Call, _getAheadTime));
                    instructionList.Insert(i - 1, new CodeInstruction(OpCodes.Ldloc_3));
                    instructionList.Insert(i - 1, new CodeInstruction(OpCodes.Ldloc_1));
                }
            }

            if (!foundAheadTime)
            {
                Plugin.Logger.Log("Failed to find aheadTime ldfld!", IPA.Logging.Logger.Level.Error);
            }

            return instructionList.AsEnumerable();
        }

        private static float GetAheadTime(BeatmapObjectCallbackData beatmapObjectCallbackData, BeatmapObjectData beatmapObjectData, float @default)
        {
            if (beatmapObjectCallbackData.callback.Method == _beatmapObjectSpawnControllerCallback &&
                (beatmapObjectData is CustomObstacleData || beatmapObjectData is CustomNoteData))
            {
                NoodleObjectData noodleData = TryGetObjectData<NoodleObjectData>(beatmapObjectData);
                if (noodleData != null)
                {
                    float? aheadTime = noodleData.AheadTimeInternal;
                    if (aheadTime.HasValue)
                    {
                        return aheadTime.Value;
                    }
                }
            }

            return @default;
        }
    }
}

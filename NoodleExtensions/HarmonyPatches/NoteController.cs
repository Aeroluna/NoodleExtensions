﻿namespace NoodleExtensions.HarmonyPatches
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;
    using Heck;
    using Heck.Animation;
    using IPA.Utilities;
    using NoodleExtensions.Animation;
    using UnityEngine;
    using static NoodleExtensions.NoodleObjectDataManager;

    [HeckPatch(typeof(NoteController))]
    [HeckPatch("Init")]
    internal static class NoteControllerInit
    {
        internal static readonly FieldAccessor<NoteMovement, NoteJump>.Accessor _noteJumpAccessor = FieldAccessor<NoteMovement, NoteJump>.GetAccessor("_jump");
        internal static readonly FieldAccessor<NoteMovement, NoteFloorMovement>.Accessor _noteFloorMovementAccessor = FieldAccessor<NoteMovement, NoteFloorMovement>.GetAccessor("_floorMovement");

        internal static readonly FieldAccessor<NoteFloorMovement, Quaternion>.Accessor _worldRotationFloorAccessor = FieldAccessor<NoteFloorMovement, Quaternion>.GetAccessor("_worldRotation");
        internal static readonly FieldAccessor<NoteFloorMovement, Quaternion>.Accessor _inverseWorldRotationFloorAccessor = FieldAccessor<NoteFloorMovement, Quaternion>.GetAccessor("_inverseWorldRotation");

        internal static readonly FieldAccessor<NoteJump, Quaternion>.Accessor _worldRotationJumpAccessor = FieldAccessor<NoteJump, Quaternion>.GetAccessor("_worldRotation");
        internal static readonly FieldAccessor<NoteJump, Quaternion>.Accessor _inverseWorldRotationJumpAccessor = FieldAccessor<NoteJump, Quaternion>.GetAccessor("_inverseWorldRotation");
        private static readonly FieldAccessor<NoteJump, Quaternion>.Accessor _endRotationAccessor = FieldAccessor<NoteJump, Quaternion>.GetAccessor("_endRotation");
        private static readonly FieldAccessor<NoteJump, Quaternion>.Accessor _middleRotationAccessor = FieldAccessor<NoteJump, Quaternion>.GetAccessor("_middleRotation");
        private static readonly FieldAccessor<NoteJump, Vector3[]>.Accessor _randomRotationsAccessor = FieldAccessor<NoteJump, Vector3[]>.GetAccessor("_randomRotations");
        private static readonly FieldAccessor<NoteJump, int>.Accessor _randomRotationIdxAccessor = FieldAccessor<NoteJump, int>.GetAccessor("_randomRotationIdx");

        private static readonly MethodInfo _getFlipYSide = SymbolExtensions.GetMethodInfo(() => GetFlipYSide(null, 0));

        private static void Postfix(NoteController __instance, NoteData noteData, NoteMovement ____noteMovement, Vector3 moveStartPos, Vector3 moveEndPos, Vector3 jumpEndPos, float endRotation)
        {
            NoodleNoteData noodleData = TryGetObjectData<NoodleNoteData>(noteData);
            if (noodleData == null)
            {
                return;
            }

            Quaternion? cutQuaternion = noodleData.CutQuaternion;

            NoteJump noteJump = _noteJumpAccessor(ref ____noteMovement);
            NoteFloorMovement floorMovement = _noteFloorMovementAccessor(ref ____noteMovement);

            if (cutQuaternion.HasValue)
            {
                Quaternion quatVal = cutQuaternion.Value;
                _endRotationAccessor(ref noteJump) = quatVal;
                Vector3 vector = quatVal.eulerAngles;
                vector += _randomRotationsAccessor(ref noteJump)[_randomRotationIdxAccessor(ref noteJump)] * 20;
                Quaternion midrotation = Quaternion.Euler(vector);
                _middleRotationAccessor(ref noteJump) = midrotation;
            }

            Quaternion? worldRotationQuaternion = noodleData.WorldRotationQuaternion;
            Quaternion? localRotationQuaternion = noodleData.LocalRotationQuaternion;

            Transform transform = __instance.transform;

            Quaternion localRotation = Quaternion.identity;
            if (worldRotationQuaternion.HasValue || localRotationQuaternion.HasValue)
            {
                if (localRotationQuaternion.HasValue)
                {
                    localRotation = localRotationQuaternion.Value;
                }

                if (worldRotationQuaternion.HasValue)
                {
                    Quaternion quatVal = worldRotationQuaternion.Value;
                    Quaternion inverseWorldRotation = Quaternion.Inverse(quatVal);
                    _worldRotationJumpAccessor(ref noteJump) = quatVal;
                    _inverseWorldRotationJumpAccessor(ref noteJump) = inverseWorldRotation;
                    _worldRotationFloorAccessor(ref floorMovement) = quatVal;
                    _inverseWorldRotationFloorAccessor(ref floorMovement) = inverseWorldRotation;

                    quatVal *= localRotation;

                    transform.localRotation = quatVal;
                }
                else
                {
                    transform.localRotation *= localRotation;
                }
            }

            if (transform.localScale != Vector3.one)
            {
                transform.localScale = Vector3.one; // This is a fix for animation due to notes being recycled
            }

            Track track = noodleData.Track;
            if (track != null && ParentObject.Controller != null)
            {
                ParentObject parentObject = ParentObject.Controller.GetParentObjectTrack(track);
                if (parentObject != null)
                {
                    parentObject.ParentToObject(transform);
                }
                else
                {
                    ParentObject.ResetTransformParent(transform);
                }
            }
            else
            {
                ParentObject.ResetTransformParent(transform);
            }

            noodleData.EndRotation = endRotation;
            noodleData.MoveStartPos = moveStartPos;
            noodleData.MoveEndPos = moveEndPos;
            noodleData.JumpEndPos = jumpEndPos;
            noodleData.WorldRotation = __instance.worldRotation;
            noodleData.LocalRotation = localRotation;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            bool foundFlipYSide = false;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (!foundFlipYSide &&
                    instructionList[i].opcode == OpCodes.Callvirt &&
                    ((MethodInfo)instructionList[i].operand).Name == "get_flipYSide")
                {
                    foundFlipYSide = true;

                    instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Call, _getFlipYSide));
                    instructionList.Insert(i - 1, new CodeInstruction(OpCodes.Ldarg_0));
                    instructionList.Insert(i, new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(NoteController), "_noteData")));
                }
            }

            if (!foundFlipYSide)
            {
                Plugin.Logger.Log("Failed to find Get_flipYSide call!", IPA.Logging.Logger.Level.Error);
            }

            return instructionList.AsEnumerable();
        }

        private static float GetFlipYSide(NoteData noteData, float @default)
        {
            float output = @default;

            NoodleNoteData noodleData = TryGetObjectData<NoodleNoteData>(noteData);
            if (noodleData != null)
            {
                float? flipYSide = noodleData.FlipYSideInternal;
                if (flipYSide.HasValue)
                {
                    output = flipYSide.Value;
                }
            }

            return output;
        }
    }

    [HeckPatch(typeof(NoteController))]
    [HeckPatch("ManualUpdate")]
    internal static class NoteControllerUpdate
    {
        internal static readonly FieldAccessor<NoteFloorMovement, Vector3>.Accessor _floorEndPosAccessor = FieldAccessor<NoteFloorMovement, Vector3>.GetAccessor("_endPos");
        private static readonly FieldAccessor<NoteFloorMovement, Vector3>.Accessor _floorStartPosAccessor = FieldAccessor<NoteFloorMovement, Vector3>.GetAccessor("_startPos");
        private static readonly FieldAccessor<NoteJump, Vector3>.Accessor _jumpStartPosAccessor = FieldAccessor<NoteJump, Vector3>.GetAccessor("_startPos");
        private static readonly FieldAccessor<NoteJump, Vector3>.Accessor _jumpEndPosAccessor = FieldAccessor<NoteJump, Vector3>.GetAccessor("_endPos");

        private static readonly FieldAccessor<NoteJump, IAudioTimeSource>.Accessor _audioTimeSyncControllerAccessor = FieldAccessor<NoteJump, IAudioTimeSource>.GetAccessor("_audioTimeSyncController");
        private static readonly FieldAccessor<NoteJump, float>.Accessor _jumpDurationAccessor = FieldAccessor<NoteJump, float>.GetAccessor("_jumpDuration");

        private static readonly FieldAccessor<GameNoteController, BoxCuttableBySaber[]>.Accessor _gameNoteBigCuttableAccessor = FieldAccessor<GameNoteController, BoxCuttableBySaber[]>.GetAccessor("_bigCuttableBySaberList");
        private static readonly FieldAccessor<GameNoteController, BoxCuttableBySaber[]>.Accessor _gameNoteSmallCuttableAccessor = FieldAccessor<GameNoteController, BoxCuttableBySaber[]>.GetAccessor("_smallCuttableBySaberList");
        private static readonly FieldAccessor<BombNoteController, CuttableBySaber>.Accessor _bombNoteCuttableAccessor = FieldAccessor<BombNoteController, CuttableBySaber>.GetAccessor("_cuttableBySaber");

        internal static NoodleObjectData NoodleData { get; private set; }

        private static void Prefix(NoteController __instance, NoteData ____noteData, NoteMovement ____noteMovement)
        {
            NoodleData = TryGetObjectData<NoodleObjectData>(____noteData);
            if (NoodleData == null)
            {
                return;
            }

            NoodleNoteData noodleData = (NoodleNoteData)NoodleData;

            Track track = noodleData.Track;
            NoodleObjectData.AnimationObjectData animationObject = noodleData.AnimationObject;
            if (track != null || animationObject != null)
            {
                NoteJump noteJump = NoteControllerInit._noteJumpAccessor(ref ____noteMovement);
                NoteFloorMovement floorMovement = NoteControllerInit._noteFloorMovementAccessor(ref ____noteMovement);

                // idk i just copied base game time
                float jumpDuration = _jumpDurationAccessor(ref noteJump);
                float elapsedTime = _audioTimeSyncControllerAccessor(ref noteJump).songTime - (____noteData.time - (jumpDuration * 0.5f));
                elapsedTime = NoteJumpManualUpdate.NoteJumpTimeAdjust(elapsedTime, jumpDuration);
                float normalTime = elapsedTime / jumpDuration;

                Animation.AnimationHelper.GetObjectOffset(animationObject, track, normalTime, out Vector3? positionOffset, out Quaternion? rotationOffset, out Vector3? scaleOffset, out Quaternion? localRotationOffset, out float? dissolve, out float? dissolveArrow, out float? cuttable);

                if (positionOffset.HasValue)
                {
                    Vector3 moveStartPos = noodleData.MoveStartPos;
                    Vector3 moveEndPos = noodleData.MoveEndPos;
                    Vector3 jumpEndPos = noodleData.JumpEndPos;

                    Vector3 offset = positionOffset.Value;
                    _floorStartPosAccessor(ref floorMovement) = moveStartPos + offset;
                    _floorEndPosAccessor(ref floorMovement) = moveEndPos + offset;
                    _jumpStartPosAccessor(ref noteJump) = moveEndPos + offset;
                    _jumpEndPosAccessor(ref noteJump) = jumpEndPos + offset;
                }

                Transform transform = __instance.transform;

                if (rotationOffset.HasValue || localRotationOffset.HasValue)
                {
                    Quaternion worldRotation = noodleData.WorldRotation;
                    Quaternion localRotation = noodleData.LocalRotation;

                    Quaternion worldRotationQuatnerion = worldRotation;
                    if (rotationOffset.HasValue)
                    {
                        worldRotationQuatnerion *= rotationOffset.Value;
                        Quaternion inverseWorldRotation = Quaternion.Inverse(worldRotationQuatnerion);
                        NoteControllerInit._worldRotationJumpAccessor(ref noteJump) = worldRotationQuatnerion;
                        NoteControllerInit._inverseWorldRotationJumpAccessor(ref noteJump) = inverseWorldRotation;
                        NoteControllerInit._worldRotationFloorAccessor(ref floorMovement) = worldRotationQuatnerion;
                        NoteControllerInit._inverseWorldRotationFloorAccessor(ref floorMovement) = inverseWorldRotation;
                    }

                    worldRotationQuatnerion *= localRotation;

                    if (localRotationOffset.HasValue)
                    {
                        worldRotationQuatnerion *= localRotationOffset.Value;
                    }

                    transform.localRotation = worldRotationQuatnerion;
                }

                if (scaleOffset.HasValue)
                {
                    transform.localScale = scaleOffset.Value;
                }

                if (dissolve.HasValue)
                {
                    if (CutoutManager.NoteCutoutEffects.TryGetValue(__instance, out CutoutEffectWrapper cutoutEffect))
                    {
                        cutoutEffect.SetCutout(dissolve.Value);
                    }
                }

                if (dissolveArrow.HasValue && __instance.noteData.colorType != ColorType.None)
                {
                    if (CutoutManager.NoteDisappearingArrowWrappers.TryGetValue(__instance, out DisappearingArrowWrapper disappearingArrowWrapper))
                    {
                        disappearingArrowWrapper.SetCutout(dissolveArrow.Value);
                    }
                }

                if (cuttable.HasValue)
                {
                    bool enabled = cuttable.Value >= 1;

                    switch (__instance)
                    {
                        case GameNoteController gameNoteController:
                            BoxCuttableBySaber[] bigCuttableBySaberList = _gameNoteBigCuttableAccessor(ref gameNoteController);
                            foreach (BoxCuttableBySaber bigCuttableBySaber in bigCuttableBySaberList)
                            {
                                if (bigCuttableBySaber.canBeCut != enabled)
                                {
                                    bigCuttableBySaber.canBeCut = enabled;
                                }
                            }

                            BoxCuttableBySaber[] smallCuttableBySaberList = _gameNoteSmallCuttableAccessor(ref gameNoteController);
                            foreach (BoxCuttableBySaber smallCuttableBySaber in smallCuttableBySaberList)
                            {
                                if (smallCuttableBySaber.canBeCut != enabled)
                                {
                                    smallCuttableBySaber.canBeCut = enabled;
                                }
                            }

                            break;

                        case BombNoteController bombNoteController:
                            CuttableBySaber boxCuttableBySaber = _bombNoteCuttableAccessor(ref bombNoteController);
                            if (boxCuttableBySaber.canBeCut != enabled)
                            {
                                boxCuttableBySaber.canBeCut = enabled;
                            }

                            break;
                    }
                }
            }
        }
    }
}

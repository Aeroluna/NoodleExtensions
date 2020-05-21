﻿using CustomJSONData;
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static NoodleExtensions.HarmonyPatches.SpawnDataHelper;
using static NoodleExtensions.HarmonyPatches.SpawnDataHelper.BeatmapObjectSpawnMovementDataVariables;
using static NoodleExtensions.Plugin;

namespace NoodleExtensions.HarmonyPatches
{
    [HarmonyPatch(typeof(BeatmapObjectSpawnMovementData))]
    [HarmonyPatch("Init")]
    internal class BeatmapObjectSpawnMovementDataInit
    {
        private static void Postfix(BeatmapObjectSpawnMovementData __instance)
        {
            InitBeatmapObjectSpawnController(__instance);
        }
    }

    [NoodlePatch(typeof(BeatmapObjectSpawnMovementData))]
    [NoodlePatch("GetObstacleSpawnMovementData")]
    internal class BeatmapObjectSpawnMovementDataGetObstacleSpawnMovementData
    {
        private static void Postfix(ObstacleData obstacleData, ref Vector3 moveStartPos, ref Vector3 moveEndPos, ref Vector3 jumpEndPos, ref float obstacleHeight)
        {
            if (obstacleData is CustomObstacleData customData)
            {
                dynamic dynData = customData.customData;
                IEnumerable<float?> _position = ((List<object>)Trees.at(dynData, POSITION))?.Select(n => n.ToNullableFloat());
                IEnumerable<float?> _scale = ((List<object>)Trees.at(dynData, SCALE))?.Select(n => n.ToNullableFloat());
                float? _njs = (float?)Trees.at(dynData, NOTEJUMPSPEED);
                float? _spawnoffset = (float?)Trees.at(dynData, SPAWNOFFSET);

                float? _startX = _position?.ElementAtOrDefault(0);
                float? _startY = _position?.ElementAtOrDefault(1);

                float? _height = _scale?.ElementAtOrDefault(1);

                // Actual wall stuff
                if (_startX.HasValue || _startY.HasValue || _njs.HasValue || _spawnoffset.HasValue)
                {
                    GetNoteJumpValues(_njs, _spawnoffset, out float _, out float _, out Vector3 _localMoveStartPos, out Vector3 _localMoveEndPos, out Vector3 _localJumpEndPos);

                    // Ripped from base game
                    Vector3 noteOffset = GetNoteOffset(obstacleData, _startX, null);
                    noteOffset.y = _startY.HasValue ? _verticalObstaclePosY + _startY.GetValueOrDefault(0) * _noteLinesDistance : ((obstacleData.obstacleType == ObstacleType.Top)
                        ? (_topObstaclePosY + _jumpOffsetY) : _verticalObstaclePosY); // If _startY(_startHeight) is set, put wall on floor
                    moveStartPos = _localMoveStartPos + noteOffset;
                    moveEndPos = _localMoveEndPos + noteOffset;
                    jumpEndPos = _localJumpEndPos + noteOffset;
                }
                if (_height.HasValue) obstacleHeight = _height.Value * _noteLinesDistance;
            }
        }
    }

    [NoodlePatch(typeof(BeatmapObjectSpawnMovementData))]
    [NoodlePatch("GetNoteSpawnMovementData")]
    internal class BeatmapObjectSpawnMovementDataGetNoteSpawnMovementData
    {
        private static void Postfix(NoteData noteData, ref Vector3 moveStartPos, ref Vector3 moveEndPos, ref Vector3 jumpEndPos, ref float jumpGravity)
        {
            if (noteData is CustomNoteData customData)
            {
                dynamic dynData = customData.customData;
                IEnumerable<float?> _position = ((List<object>)Trees.at(dynData, POSITION))?.Select(n => n.ToNullableFloat());
                float? flipLineIndex = (float?)Trees.at(dynData, "flipLineIndex");
                float? _njs = (float?)Trees.at(dynData, NOTEJUMPSPEED);
                float? _spawnoffset = (float?)Trees.at(dynData, SPAWNOFFSET);

                float? _startRow = _position?.ElementAtOrDefault(0);
                float? _startHeight = _position?.ElementAtOrDefault(1);

                if (_position != null || flipLineIndex != null || _njs.HasValue || _spawnoffset.HasValue)
                {
                    GetNoteJumpValues(_njs, _spawnoffset, out float _, out float _localJumpDistance, out Vector3 _localMoveStartPos, out Vector3 _localMoveEndPos, out Vector3 _localJumpEndPos);

                    float _localNoteJumpMovementSpeed = _njs ?? _noteJumpMovementSpeed;

                    Vector3 noteOffset = GetNoteOffset(noteData, _startRow, _startHeight);

                    float lineYPos = LineYPosForLineLayer(noteData, _startHeight);
                    // Magic numbers below found with linear regression y=mx+b using existing HighestJumpPosYForLineLayer values
                    float highestJump = _startHeight.HasValue ? ((0.875f * lineYPos) + 0.639583f) + _jumpOffsetY :
                        beatmapObjectSpawnMovementData.HighestJumpPosYForLineLayer(noteData.noteLineLayer);
                    jumpGravity = 2f * (highestJump - lineYPos) /
                        Mathf.Pow(_localJumpDistance / _localNoteJumpMovementSpeed * 0.5f, 2f);

                    jumpEndPos = _localJumpEndPos + noteOffset;

                    // IsBasicNote() check is skipped so bombs can flip too
                    Vector3 noteOffset2 = GetNoteOffset(noteData, flipLineIndex ?? _startRow, _startHeight);
                    moveStartPos = _localMoveStartPos + noteOffset2;
                    moveEndPos = _localMoveEndPos + noteOffset2;
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelDefinition", menuName = "Kingdom Rush/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [Header("General Info")]
    public string levelId = "Level_1";
    public string displayName = "Cửa Ải 1: Thung Lũng Goblin";
    [Range(1, 8)] public int levelNumber = 1;
    [Range(1, 5)] public int difficulty = 1;

    [Header("Economy & Rules")]
    public int startingGold = 250;
    public int startingLives = 20;
    public int waveCount = 10;

    [Header("Procedural Map Meta")]
    public string themeName = "Cỏ (Grass)";
    public int seed = 12345;
    public int pathCount = 1;
    public int pathLength = 25;
    public int plotCount = 8;
    public float qualityScore = 85f;

    [Header("Grid Data")]
    public List<Vector2Int> path1Waypoints = new List<Vector2Int>();
    public List<Vector2Int> path2Waypoints = new List<Vector2Int>();
    public List<Vector2Int> plotCoordinates = new List<Vector2Int>();
}

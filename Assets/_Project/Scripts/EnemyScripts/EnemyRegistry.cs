using System.Collections.Generic;
using UnityEngine;

// Central registry for all living enemies in the scene.
// Enables O(1) registration and zero-GC targeting queries for Towers, HUD, Spells, and Hero.
public static class EnemyRegistry
{
    private static readonly List<EnemyHealth> activeEnemies = new List<EnemyHealth>(64);

    public static IReadOnlyList<EnemyHealth> ActiveEnemies => activeEnemies;
    public static int ActiveCount => activeEnemies.Count;

    public static void Register(EnemyHealth enemy)
    {
        if (enemy != null && !activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
        }
    }

    public static void Unregister(EnemyHealth enemy)
    {
        if (enemy != null)
        {
            activeEnemies.Remove(enemy);
        }
    }

    public static void Clear()
    {
        activeEnemies.Clear();
    }
}

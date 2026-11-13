using System;
using UnityEngine;

[Serializable]
public class Tower
{
    public string name;
    public int cost;
    public GameObject prefab;
    public Sprite iconSprite;

    // Thuộc tính tương thích (Compatibility properties cho Editor / Scripts)
    public string Name => name;
    public GameObject TowerPrefab => prefab;

    public Tower(string _name, int _cost, GameObject _prefab, Sprite _iconSprite = null)
    {
        name = _name;
        cost = _cost;
        prefab = _prefab;
        iconSprite = _iconSprite;
    }
}


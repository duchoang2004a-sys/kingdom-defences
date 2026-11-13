using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    public static ObjectPool instance;
    
    private Dictionary<GameObject, Queue<GameObject>> poolDict = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, GameObject> instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

    private void Awake()
    {
        instance = this;
    }

    public void Preload(GameObject prefab, int count)
    {
        if (prefab == null) return;
        
        if (!poolDict.ContainsKey(prefab))
        {
            poolDict[prefab] = new Queue<GameObject>();
        }

        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            poolDict[prefab].Enqueue(obj);
            instanceToPrefabMap[obj] = prefab;
        }
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        
        if (!poolDict.ContainsKey(prefab))
        {
            poolDict[prefab] = new Queue<GameObject>();
        }

        GameObject obj;
        if (poolDict[prefab].Count > 0)
        {
            obj = poolDict[prefab].Dequeue();
        }
        else
        {
            obj = Instantiate(prefab, transform);
            instanceToPrefabMap[obj] = prefab;
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);
        return obj;
    }

    public void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;
        
        if (instanceToPrefabMap.TryGetValue(obj, out GameObject prefab))
        {
            obj.SetActive(false);
            if (poolDict.ContainsKey(prefab))
            {
                poolDict[prefab].Enqueue(obj);
            }
        }
        else
        {
            Destroy(obj); // Fallback
        }
    }
}

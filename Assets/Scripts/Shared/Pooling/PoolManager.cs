using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bloodrush.Shared.Pooling
{
// Merkezi nesne havuzu — sık Instantiate/Destroy edilen prefab'lar (mermi,
// grenade, hit/kan efekti) buradan alınır/bırakılır (bkz. mimari kurallar §3,
// BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md Faz 6).
//
// Tek gerekçe (yeni singleton kuralı): havuzlar sahne çapında paylaşılan,
// tek mantıksal kaynak olmalı (Weapons/Enemy/Player aynı havuza erişir);
// doğal bir "sahip" yok, bu yüzden ilk kullanımda kendini lazy olarak
// oluşturan bir singleton. DontDestroyOnLoad KULLANILMIYOR — her sahne
// kendi havuzuyla başlar/biter, sahne değişince pooled objeler de doğal
// olarak temizlenir (bölümler arası bayat referans riski yok).
public class PoolManager : MonoBehaviour
{
    static PoolManager instance;
    static PoolManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("PoolManager");
                instance = go.AddComponent<PoolManager>();
            }
            return instance;
        }
    }

    class Pool
    {
        public GameObject        prefab;
        public Transform         holder;
        public Stack<GameObject> inactive = new Stack<GameObject>();
    }

    // Alınan her klonun hangi havuza ait olduğunu hatırlar (Release için).
    class PoolMember : MonoBehaviour { public Pool pool; }

    readonly Dictionary<int, Pool>  poolsByPrefab = new Dictionary<int, Pool>();
    readonly Dictionary<Type, Pool> poolsByType   = new Dictionary<Type, Pool>();

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    // Prefab-tabanlı havuz — LauncherProjectile/EnemyProjectile gibi Inspector'da
    // atanmış bir prefab'ı olan nesneler için.
    public static T Get<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        if (prefab == null) return null;
        var pool = Instance.GetOrCreatePrefabPool(prefab.gameObject);
        return Instance.Spawn<T>(pool, position, rotation);
    }

    // Prefab'sız (kod içinde prosedürel kurulan) nesneler için — ilk çağrıda
    // gizli, deaktif bir şablon oluşturulur, sonraki her Get<T>() o şablonun
    // klonunu döndürür. HitEffect/BloodEffect gibi "new GameObject() +
    // AddComponent" ile kurulan efektler için.
    public static T Get<T>(Vector3 position, Quaternion rotation) where T : Component
    {
        var pool = Instance.GetOrCreateTemplatePool<T>();
        return Instance.Spawn<T>(pool, position, rotation);
    }

    public static void Release(GameObject obj)
    {
        if (obj == null) return;

        var member = obj.GetComponent<PoolMember>();
        if (member == null || member.pool == null)
        {
            // Havuza ait değil (tek seferlik/elle oluşturulmuş bir nesne) — güvenli varsayılan.
            Destroy(obj);
            return;
        }

        foreach (var poolable in obj.GetComponents<IPoolable>())
            poolable.OnDespawned();

        obj.SetActive(false);
        obj.transform.SetParent(member.pool.holder);
        member.pool.inactive.Push(obj);
    }

    public static void Release(Component c)
    {
        if (c != null) Release(c.gameObject);
    }

    Pool GetOrCreatePrefabPool(GameObject prefab)
    {
        int key = prefab.GetInstanceID();
        if (poolsByPrefab.TryGetValue(key, out var pool)) return pool;
        pool = new Pool { prefab = prefab, holder = MakeHolder(prefab.name) };
        poolsByPrefab[key] = pool;
        return pool;
    }

    Pool GetOrCreateTemplatePool<T>() where T : Component
    {
        var type = typeof(T);
        if (poolsByType.TryGetValue(type, out var pool)) return pool;

        var template = new GameObject($"__PoolTemplate_{type.Name}");
        template.transform.SetParent(transform);
        template.SetActive(false);
        template.AddComponent<T>();

        pool = new Pool { prefab = template, holder = MakeHolder(type.Name) };
        poolsByType[type] = pool;
        return pool;
    }

    Transform MakeHolder(string label)
    {
        var holder = new GameObject($"Pool_{label}").transform;
        holder.SetParent(transform);
        return holder;
    }

    T Spawn<T>(Pool pool, Vector3 position, Quaternion rotation) where T : Component
    {
        GameObject go = pool.inactive.Count > 0 ? pool.inactive.Pop() : Instantiate(pool.prefab);

        var member = go.GetComponent<PoolMember>();
        if (member == null) member = go.AddComponent<PoolMember>();
        member.pool = pool;

        go.transform.SetParent(null);
        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);

        foreach (var poolable in go.GetComponents<IPoolable>())
            poolable.OnSpawned();

        return go.GetComponent<T>();
    }
}
}

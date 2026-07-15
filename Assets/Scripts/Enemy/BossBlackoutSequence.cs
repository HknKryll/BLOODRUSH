using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Bloodrush.Flow;

namespace Bloodrush.Enemy
{
// Boss'un faz geçişi karanlığı: sahne ışıklarını söndürüp tam siyah overlay
// gösterir, oyuncunun arkasına ışınlar, süre dolunca her şeyi geri açar.
public class BossBlackoutSequence
{
    readonly NavMeshAgent agent;
    readonly float teleportBehindDist;
    readonly float blackoutDuration;
    readonly Renderer[] renderers;

    readonly List<Light> litLights = new();
    Image blackoutImg;

    public BossBlackoutSequence(NavMeshAgent agent, float teleportBehindDist, float blackoutDuration, Renderer[] renderers)
    {
        this.agent = agent;
        this.teleportBehindDist = teleportBehindDist;
        this.blackoutDuration = blackoutDuration;
        this.renderers = renderers;
    }

    public IEnumerator Run(Transform boss, Transform player, Action facePlayer)
    {
        // Sahne ışıklarını söndür
        litLights.Clear();
        foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>())
            if (l.enabled) { litLights.Add(l); l.enabled = false; }

        // Tam siyah overlay (garanti kör)
        blackoutImg = GameFlow.CreateOverlay(Color.black);
        blackoutImg.color = Color.black;

        // Boss görünmez
        SetRenderers(false);

        yield return null;

        // Oyuncunun arkasına ışınla
        if (player != null)
        {
            Vector3 behind = player.position - player.forward * teleportBehindDist;
            if (NavMesh.SamplePosition(behind, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                agent.enabled = false;
                boss.position = hit.position;
                agent.enabled = true;
            }
            facePlayer?.Invoke();
        }

        yield return new WaitForSecondsRealtime(blackoutDuration);

        // Işıkları geri aç, overlay kaldır, boss görünür
        RestoreLights();
        SetRenderers(true);
        if (blackoutImg) { UnityEngine.Object.Destroy(blackoutImg.canvas.gameObject); blackoutImg = null; }
    }

    public void RestoreLights()
    {
        foreach (var l in litLights) if (l != null) l.enabled = true;
        litLights.Clear();
    }

    // Boss karanlık ortasında ölürse: ışıkları geri ver, overlay'i kaldır.
    public void CleanupOnDeath()
    {
        RestoreLights();
        if (blackoutImg) { UnityEngine.Object.Destroy(blackoutImg.canvas.gameObject); blackoutImg = null; }
    }

    void SetRenderers(bool visible)
    {
        foreach (var r in renderers) if (r != null) r.enabled = visible;
    }
}
}

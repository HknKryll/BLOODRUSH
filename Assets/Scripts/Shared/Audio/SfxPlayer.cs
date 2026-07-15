using UnityEngine;

namespace Bloodrush.Shared.Audio
{
// Bir AudioSource'u sarmalar; her scriptte tekrarlanan
// AddComponent+PlayOneShot boilerplate'ini tek yerde toplar.
public class SfxPlayer
{
    readonly AudioSource source;

    public SfxPlayer(AudioSource source) => this.source = source;

    public static SfxPlayer Create(GameObject host, float spatialBlend = 1f)
    {
        var source = host.AddComponent<AudioSource>();
        source.playOnAwake  = false;
        source.spatialBlend = spatialBlend;
        return new SfxPlayer(source);
    }

    // GetComponent varsa onu kullanır, yoksa ekler (PlayerShoot'un önceki davranışı).
    public static SfxPlayer CreateOrGet(GameObject host, float spatialBlend = 1f)
    {
        var source = host.GetComponent<AudioSource>();
        if (source == null) source = host.AddComponent<AudioSource>();
        source.playOnAwake  = false;
        source.spatialBlend = spatialBlend;
        return new SfxPlayer(source);
    }

    public void Play(AudioClip clip, float volume = 1f)
    {
        if (clip != null && source != null)
            source.PlayOneShot(clip, volume);
    }

    // Sahibi olmayan, tek seferlik 3D ses (ör. AmmoPickup toplanınca).
    public static void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    // Kaynak obje yok edildikten sonra da çalmaya devam etmesi gereken sesler için
    // (ör. düşman öldüğünde ölüm sesi) — geçici bir GameObject oluşturup süresi
    // dolunca kendini yok eder.
    public static void PlayDetached(AudioClip clip, Vector3 position, float volume = 1f, float spatialBlend = 0f, float lifetime = 5f)
    {
        if (clip == null) return;

        var sfxGO = new GameObject("DetachedSFX");
        sfxGO.transform.position = position;
        var source = sfxGO.AddComponent<AudioSource>();
        source.spatialBlend = spatialBlend;
        source.volume = volume;
        source.clip = clip;
        source.Play();
        Object.Destroy(sfxGO, lifetime);
    }
}
}

using UnityEngine;
using UnityEngine.Audio;

namespace Bloodrush.Shared.Audio
{
// Projedeki seslerin hangi AudioMixer grubuna gidecegini tek yerden cozer.
//
// NEDEN Resources: RoomMusic kendi AudioSource'unu kod icinde kuruyor, SfxPlayer ise
// static fabrika metotlariyla calisiyor — ikisinin de sahne uzerinden bir referans alma
// sansi yok. Projede bunun icin zaten kurulu bir desen var: UITheme.Load() de
// Resources.Load ile kendini buluyor. Ayni yol izleniyor.
//
// MIXER YOKSA HICBIR SEY BOZULMAZ: gruplar null doner, outputAudioMixerGroup = null
// olur ve sesler bugunku gibi dogrudan AudioListener'a gider. Yani mixer olusturmadan
// once de proje calismaya devam eder.
//
// Kurulum (kullanici Editor'da yapar):
//   Assets/Resources/GameMixer.mixer  →  Master altinda "Music" ve "SFX" gruplari.
public static class AudioRouting
{
    const string MixerResourcePath = "GameMixer";

    static AudioMixer      mixer;
    static AudioMixerGroup musicGroup;
    static AudioMixerGroup sfxGroup;
    static AudioMixerGroup uiGroup;
    static bool            resolved;

    public static AudioMixerGroup Music { get { Resolve(); return musicGroup; } }
    public static AudioMixerGroup Sfx   { get { Resolve(); return sfxGroup;   } }
    public static AudioMixerGroup Ui    { get { Resolve(); return uiGroup;    } }
    public static AudioMixer      Mixer { get { Resolve(); return mixer;      } }

    static void Resolve()
    {
        if (resolved) return;
        resolved = true;

        mixer = Resources.Load<AudioMixer>(MixerResourcePath);
        if (mixer == null)
        {
            // Uyari degil bilgi: mixer henuz kurulmamis olabilir, bu gecerli bir durum.
            Debug.Log($"[AudioRouting] Resources/{MixerResourcePath} bulunamadi — " +
                      "sesler dogrudan calacak (mixer yonlendirmesi yok).");
            return;
        }

        musicGroup = First(mixer.FindMatchingGroups("Music"));
        sfxGroup   = First(mixer.FindMatchingGroups("SFX"));
        uiGroup    = First(mixer.FindMatchingGroups("UI"));

        if (musicGroup == null) Debug.LogWarning("[AudioRouting] GameMixer icinde 'Music' grubu yok.");
        if (sfxGroup   == null) Debug.LogWarning("[AudioRouting] GameMixer icinde 'SFX' grubu yok.");
        if (uiGroup    == null) Debug.LogWarning("[AudioRouting] GameMixer icinde 'UI' grubu yok.");
    }

    static AudioMixerGroup First(AudioMixerGroup[] groups)
        => groups != null && groups.Length > 0 ? groups[0] : null;

    // Domain reload kapaliyken statiklerin onceki oturumdan tasinmamasi icin.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        mixer      = null;
        musicGroup = null;
        sfxGroup   = null;
        uiGroup    = null;
        resolved   = false;
    }
}
}

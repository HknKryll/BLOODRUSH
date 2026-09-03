using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bloodrush.EditorTools
{
// HDRP/Lit'in "Mask Map" alanı, indirilen PBR paketlerindeki ayrı AO/Roughness/Metallic
// dosyalarını DOĞRUDAN kabul etmiyor — tek bir RGBA texture'a paketlenmiş olmaları
// gerekiyor (R=Metallic, G=AO, B=boş, A=Smoothness). ambientCG/Poliigon gibi kaynaklar
// bunları hep ayrı dosya verir; bu araç onları birleştirip HDRP'nin beklediği formatta
// bir PNG üretir. Window → Bloodrush → Mask Map Paketleyici.
public class MaskMapPacker : EditorWindow
{
    Texture2D metallicTex;
    Texture2D aoTex;
    Texture2D roughnessTex;
    bool      invertRoughness = true;   // roughness dosyası → smoothness'a çevrilir (1-r)

    [Range(0f, 1f)] float metallicFallback   = 0f;    // metallicTex boşsa
    [Range(0f, 1f)] float aoFallback         = 1f;    // aoTex boşsa (1 = gölgeleme yok)
    [Range(0f, 1f)] float smoothnessFallback = 0.5f;  // roughnessTex boşsa

    [MenuItem("Window/Bloodrush/Mask Map Paketleyici")]
    static void Open() => GetWindow<MaskMapPacker>("Mask Map Paketleyici");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "HDRP Mask Map kanalları: R=Metallic, G=AO, B=boş, A=Smoothness.\n" +
            "Boş bırakılan kanallar aşağıdaki sabit değeri kullanır.\n" +
            "Kaynak texture'ların Import Settings'inde 'Read/Write' AÇIK olmalı.",
            MessageType.Info);

        EditorGUILayout.Space();
        metallicTex  = (Texture2D)EditorGUILayout.ObjectField("Metallic (opsiyonel)", metallicTex, typeof(Texture2D), false);
        if (metallicTex == null) metallicFallback = EditorGUILayout.Slider("  Sabit Metallic", metallicFallback, 0f, 1f);

        aoTex = (Texture2D)EditorGUILayout.ObjectField("Ambient Occlusion", aoTex, typeof(Texture2D), false);
        if (aoTex == null) aoFallback = EditorGUILayout.Slider("  Sabit AO", aoFallback, 0f, 1f);

        roughnessTex = (Texture2D)EditorGUILayout.ObjectField("Roughness", roughnessTex, typeof(Texture2D), false);
        if (roughnessTex == null)
            smoothnessFallback = EditorGUILayout.Slider("  Sabit Smoothness", smoothnessFallback, 0f, 1f);
        else
            invertRoughness = EditorGUILayout.Toggle("  Roughness → Smoothness çevir (1-r)", invertRoughness);

        EditorGUILayout.Space();

        if (metallicTex == null && aoTex == null && roughnessTex == null)
        {
            EditorGUILayout.HelpBox("En az bir texture seç.", MessageType.Warning);
            return;
        }

        if (!AllReadable(out string badName))
        {
            EditorGUILayout.HelpBox(
                $"'{badName}' texture'ının Import Settings'inde Read/Write Enabled kapalı. " +
                "Inspector'da texture'ı seç → Advanced → Read/Write Enabled'ı aç → Apply.",
                MessageType.Error);
            return;
        }

        if (!SameSize(out int w, out int h, out string mismatchInfo))
        {
            EditorGUILayout.HelpBox("Seçili texture'ların boyutları farklı: " + mismatchInfo +
                " — hepsi aynı çözünürlükte olmalı.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Çıktı boyutu", $"{w} x {h}");

        if (GUILayout.Button("Paketle ve Kaydet...", GUILayout.Height(32)))
            PackAndSave(w, h);
    }

    bool AllReadable(out string badName)
    {
        badName = null;
        foreach (var t in new[] { metallicTex, aoTex, roughnessTex })
        {
            if (t == null) continue;
            if (!t.isReadable) { badName = t.name; return false; }
        }
        return true;
    }

    bool SameSize(out int w, out int h, out string mismatchInfo)
    {
        w = h = 0;
        mismatchInfo = "";
        bool first = true;
        foreach (var t in new[] { metallicTex, aoTex, roughnessTex })
        {
            if (t == null) continue;
            if (first) { w = t.width; h = t.height; first = false; continue; }
            if (t.width != w || t.height != h)
            {
                mismatchInfo = $"{t.name} ({t.width}x{t.height}) vs {w}x{h}";
                return false;
            }
        }
        return true;
    }

    void PackAndSave(int w, int h)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Mask Map Kaydet", "MaskMap", "png", "Mask map PNG'sini nereye kaydetmek istersin?");
        if (string.IsNullOrEmpty(path)) return;

        var result = new Texture2D(w, h, TextureFormat.RGBA32, false, true);   // linear=true — mask map renk verisi değil
        var mPix = metallicTex  != null ? metallicTex.GetPixels()  : null;
        var aPix = aoTex        != null ? aoTex.GetPixels()        : null;
        var rPix = roughnessTex != null ? roughnessTex.GetPixels() : null;

        var outPix = new Color[w * h];
        for (int i = 0; i < outPix.Length; i++)
        {
            float m = mPix != null ? mPix[i].grayscale : metallicFallback;
            float ao = aPix != null ? aPix[i].grayscale : aoFallback;
            float s;
            if (rPix != null)
            {
                float r = rPix[i].grayscale;
                s = invertRoughness ? 1f - r : r;
            }
            else
            {
                s = smoothnessFallback;
            }
            outPix[i] = new Color(m, ao, 0f, s);
        }
        result.SetPixels(outPix);
        result.Apply();

        File.WriteAllBytes(path, result.EncodeToPNG());
        DestroyImmediate(result);

        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            // Mask map RENK verisi değil — sRGB AÇIK kalırsa HDRP kanalları gamma-düzeltip bozar.
            importer.sRGBTexture   = false;
            importer.textureType   = TextureImporterType.Default;
            importer.alphaSource   = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

        EditorUtility.DisplayDialog("Mask Map Paketleyici",
            $"Kaydedildi: {path}\n\nHDRP/Lit materyalinde Mask Map alanına sürükle.", "Tamam");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }
}
}

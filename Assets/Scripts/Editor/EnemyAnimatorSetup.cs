using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Bloodrush.EditorTools
{
// Boss ve Ranged dusman icin Animator Controller'larini KOD ILE kurar.
//
// Neden menu: .controller YAML'ini elle yazmak kirilgan (blend tree, layer, mask, gecis
// referanslari ic ic gecmis fileID'ler). UnityEditor.Animations API'si ayni seyi guvenle yapar
// ve menu tekrar tekrar calistirilabilir.
//
// Klipler OLMASA DA calisir: state'ler bos motion ile kurulur, parametreler ve gecisler
// test edilebilir. Klipler geldiginde menuyu tekrar calistir, otomatik baglanir.
public static class EnemyAnimatorSetup
{
    const string ClipFolder  = "Assets/Animations/TPS";
    const string OutFolder   = "Assets/Animations/Controllers";
    const string MaskPath    = "Assets/Animations/UstGovde.mask";

    // Klip dosyasi -> dongu olmali mi? (Idle/Walk/Run donguler, atis ve gecisler degil)
    static readonly Dictionary<string, bool> ClipLoop = new Dictionary<string, bool>
    {
        { "Idle", true }, { "Walk", true }, { "Run", true }, { "Sprint", true },
        { "IdleToRun", false }, { "RunToIdle", false }, { "IdleToWalk", false }, { "WalkToIdle", false },
        { "RevolverAimIdle", true }, { "RevolverAimWalk", true }, { "RevolverAimShoot", false },
        { "ShotgunAimIdle", true }, { "ShotgunAimWalk", true },
    };

    [MenuItem("Bloodrush/Animator/Dusman Controller'larini Kur")]
    public static void BuildAll()
    {
        ImportClips();

        Directory.CreateDirectory(OutFolder);
        AssetDatabase.Refresh();

        var mask = BuildUpperBodyMask();
        BuildController(OutFolder + "/RangedController.controller", mask, dodge: false, runThreshold: 3.5f);
        BuildController(OutFolder + "/BossController.controller",   mask, dodge: true,  runThreshold: 4.5f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EnemyAnimatorSetup] Controller'lar kuruldu: " + OutFolder +
                  " (RangedController, BossController) + " + MaskPath);
    }

    // Dusman MODELINI (klip degil) Humanoid'e cevirir. Humanoid klipler ancak Humanoid bir
    // modele retarget edilir; Generic modelde model bind pozunda (T-poz) kalir.
    [MenuItem("Bloodrush/Animator/Secili Modeli Humanoid Yap")]
    public static void MakeSelectedHumanoid()
    {
        int n = 0;
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) continue;

            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();
            n++;

            bool valid = false;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is Avatar av && av.isValid && av.isHuman) valid = true;

            if (valid) Debug.Log($"[EnemyAnimatorSetup] {Path.GetFileName(path)} Humanoid oldu, avatar gecerli.");
            else Debug.LogWarning($"[EnemyAnimatorSetup] {Path.GetFileName(path)} Humanoid yapildi ama avatar " +
                                  "GECERSIZ — Inspector ▸ Rig ▸ Configure ile kemikleri elle esle.");
        }
        if (n == 0) Debug.LogWarning("[EnemyAnimatorSetup] Project penceresinden bir MODEL (.fbx) sec.");
    }

    // Kokun (non-uniform) olcegini COCUKLARA dagitir, koku (1,1,1) yapar.
    //
    // Neden: eski kutu boss'un koku Y'de 2.38 uzatilmis. Altina bir karakter modeli koyunca
    // model de eziliyor (eni boyu farkli oranda). Skinned bir karakterin ustunde non-uniform
    // olcek her zaman sorun cikarir. Bu arac dunyadaki gorunumu DEGISTIRMEDEN olcegi asagi
    // tasir; sonra modele duzgun, esit bir olcek verilebilir.
    [MenuItem("Bloodrush/Animator/Secili Objenin Olcegini Cocuklara Dagit")]
    public static void FlattenRootScale()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogWarning("[EnemyAnimatorSetup] Hierarchy'den bir obje sec."); return; }

        var root = go.transform;
        Vector3 s = root.localScale;
        if ((s - Vector3.one).sqrMagnitude < 1e-6f)
        { Debug.Log("[EnemyAnimatorSetup] Kok olcegi zaten (1,1,1)."); return; }

        Undo.RegisterFullObjectHierarchyUndo(go, "Olcegi cocuklara dagit");

        foreach (Transform child in root)
        {
            if (Quaternion.Angle(child.localRotation, Quaternion.identity) > 1f)
                Debug.LogWarning($"[EnemyAnimatorSetup] '{child.name}' dondurulmus — non-uniform olcek " +
                                 "dagitimi bu cocukta birebir olmayabilir, gozle kontrol et.", child);

            child.localPosition = Vector3.Scale(child.localPosition, s);
            child.localScale    = Vector3.Scale(child.localScale, s);
        }
        root.localScale = Vector3.one;

        Debug.Log($"[EnemyAnimatorSetup] '{go.name}' kok olcegi {s} cocuklara dagitildi, kok artik (1,1,1). " +
                  "Simdi modele ESIT bir olcek ver (or. 1.5).", go);
    }

    // ── Klip import ayarlari ───────────────────────────────────────────
    // Humanoid + dogru loop + root motion'i poz icine pisir (hareketi NavMeshAgent veriyor).
    static void ImportClips()
    {
        if (!Directory.Exists(ClipFolder))
        {
            Debug.LogWarning($"[EnemyAnimatorSetup] {ClipFolder} yok — klipsiz kuruluyor.");
            return;
        }

        int done = 0;
        foreach (var file in Directory.GetFiles(ClipFolder, "*.fbx"))
        {
            string path = file.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            string name = Path.GetFileNameWithoutExtension(path);
            bool loop = ClipLoop.TryGetValue(name, out bool l) ? l : false;

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            { importer.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
            if (importer.importCameras)   { importer.importCameras   = false; dirty = true; }
            if (importer.importLights)    { importer.importLights    = false; dirty = true; }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    if (i == 0) clips[i].name = name;          // klip adi = dosya adi (Take 001 degil)
                    clips[i].loopTime = loop;
                    clips[i].loopPose = loop;
                    // KOK HAREKETINI POZA PISIR. XZ'yi de pisirmek sart: yoksa Walk/Run klibi
                    // karakteri kendi icinde ILERI YURUTUR, model collider'indan kopup uzaklasir
                    // (hareketi zaten NavMeshAgent veriyor).
                    clips[i].lockRootRotation        = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].lockRootHeightY         = true;
                    clips[i].keepOriginalPositionY   = true;
                    clips[i].lockRootPositionXZ       = true;
                    clips[i].keepOriginalPositionXZ   = true;
                }
                importer.clipAnimations = clips;
                dirty = true;
            }

            if (dirty) { importer.SaveAndReimport(); done++; }
        }
        Debug.Log($"[EnemyAnimatorSetup] {done} klip Humanoid olarak ayarlandi ({ClipFolder}).");

        // Ates/dodge isaretlerini (Animation Event) elle eklemek gerekmesin: klip suresi
        // import sonrasi belli oldugu icin ikinci gecis.
        AddEvent("RevolverAimShoot", "AnimFire",     0.2f);
        AddEvent("ShotgunAimShoot",  "AnimFire",     0.2f);
        AddEvent("Dodge",            "AnimDodgeEnd", 0.9f);
        AddEvent("Dodging",          "AnimDodgeEnd", 0.9f);
    }

    // Klibin normalize edilmis bir noktasina (0-1) Animation Event koyar.
    static void AddEvent(string fileName, string function, float normalizedTime)
    {
        string path = $"{ClipFolder}/{fileName}.fbx";
        if (!File.Exists(path)) return;

        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        var clip     = Clip(fileName);
        if (importer == null || clip == null) return;

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        // Zaten varsa tekrar ekleme (menu tekrar tekrar calistirilabilir olmali).
        if (clips[0].events != null)
            foreach (var e in clips[0].events)
                if (e.functionName == function) return;

        clips[0].events = new[]
        {
            new AnimationEvent { functionName = function, time = clip.length * normalizedTime }
        };
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Debug.Log($"[EnemyAnimatorSetup] {fileName}: '{function}' olayi klibin %{normalizedTime * 100:0}'ine eklendi.");
    }

    // ── Avatar mask: sadece ust govde ──────────────────────────────────
    static AvatarMask BuildUpperBodyMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            Directory.CreateDirectory(Path.GetDirectoryName(MaskPath));
            AssetDatabase.CreateAsset(mask, MaskPath);
        }

        // Ust govde acik, bacaklar kapali: dusman kosarken nisan alip ates edebilsin.
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root,        false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body,        true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head,        true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm,     true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm,    true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg,     false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg,    false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK,  false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK,  false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);
        EditorUtility.SetDirty(mask);
        return mask;
    }

    // ── Controller ─────────────────────────────────────────────────────
    static void BuildController(string path, AvatarMask mask, bool dodge, float runThreshold)
    {
        AssetDatabase.DeleteAsset(path);   // her calistirmada temiz kurulum
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

        ctrl.AddParameter("Speed",    AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Fire",     AnimatorControllerParameterType.Trigger);
        if (dodge) ctrl.AddParameter("Dodge", AnimatorControllerParameterType.Trigger);

        // --- Base layer: tek bir 1D blend tree (Idle -> Walk -> Run) ---
        var locoState = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType      = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.children = new[]
        {
            new ChildMotion { motion = Clip("Idle"), threshold = 0f,            timeScale = 1f },
            new ChildMotion { motion = Clip("Walk"), threshold = 1.6f,          timeScale = 1f },
            new ChildMotion { motion = Clip("Run"),  threshold = runThreshold,  timeScale = 1f },
        };

        var baseSm = ctrl.layers[0].stateMachine;
        baseSm.defaultState = locoState;

        if (dodge)
        {
            var dodgeState = baseSm.AddState("Dodge");
            dodgeState.motion = Clip("Dodge") ?? Clip("Dodging");

            // Any State -> Dodge: hangi state'te olursa olsun girer, kolay kesilmez.
            var any = baseSm.AddAnyStateTransition(dodgeState);
            any.AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
            any.hasExitTime         = false;
            any.duration            = 0.05f;
            any.canTransitionToSelf = false;
            any.interruptionSource  = TransitionInterruptionSource.None;

            var back = dodgeState.AddTransition(locoState);
            back.hasExitTime = true;
            back.exitTime    = 0.9f;
            back.duration    = 0.1f;
        }

        // --- Ust govde layer'i: mask'li, locomotion'i bozmaz ---
        ctrl.AddLayer("UstGovde");
        var layers = ctrl.layers;
        var upper  = layers[1];
        upper.avatarMask    = mask;
        upper.defaultWeight = 1f;
        upper.blendingMode  = AnimatorLayerBlendingMode.Override;
        layers[1] = upper;
        ctrl.layers = layers;

        var sm   = ctrl.layers[1].stateMachine;
        var idle = sm.AddState("Bos");                 // bos: alt govde locomotion'a karismaz
        var aim  = sm.AddState("AimIdle");
        var fire = sm.AddState("Fire");
        sm.defaultState = idle;

        // Boss pompali tasiyor, menzilli dusman tabanca — nisan pozu ona gore.
        aim.motion  = dodge ? (Clip("ShotgunAimIdle")  ?? Clip("RevolverAimIdle"))
                            : (Clip("RevolverAimIdle") ?? Clip("ShotgunAimIdle"));
        fire.motion = Clip("RevolverAimShoot");   // pakette pompali ates klibi yok

        var toAim = idle.AddTransition(aim);
        toAim.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");
        toAim.hasExitTime = false;
        toAim.duration    = 0.15f;

        var toIdle = aim.AddTransition(idle);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming");
        toIdle.hasExitTime = false;
        toIdle.duration    = 0.2f;

        // Ates: trigger ile girer, KLIP BITINCE kendiliginden cikar (exit time 1.0).
        var toFire = aim.AddTransition(fire);
        toFire.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
        toFire.hasExitTime = false;
        toFire.duration    = 0.03f;

        var fireBack = fire.AddTransition(aim);
        fireBack.hasExitTime = true;
        fireBack.exitTime    = 0.95f;
        fireBack.duration    = 0.08f;

        // Nisan birakilirsa ates state'inde asili kalmasin.
        var fireOut = fire.AddTransition(idle);
        fireOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming");
        fireOut.hasExitTime = true;
        fireOut.exitTime    = 0.95f;
        fireOut.duration    = 0.15f;

        EditorUtility.SetDirty(ctrl);
    }

    // FBX'in icindeki ilk animasyon klibini dosya adiyla bulur (klip adi "Take 001" olsa bile).
    static AnimationClip Clip(string fileName)
    {
        string path = $"{ClipFolder}/{fileName}.fbx";
        if (!File.Exists(path)) return null;

        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            if (obj is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }
}
}

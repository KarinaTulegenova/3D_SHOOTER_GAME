#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PlayerRigImportFixer
{
    static readonly string[] GenericOverlayAnimationPaths =
    {
        "Assets/PlayerAnimations/Shooting.fbx",
        "Assets/PlayerAnimations/Gunplay.fbx"
    };

    const string RunningAnimationPath = "Assets/PlayerAnimations/Running.fbx";
    const string ShootRifleAnimationPath = "Assets/PlayerAnimations/Shoot Rifle.fbx";
    const string SoldierAvatarPath = "Assets/Yurowm/YurowmAvatar.FBX";

    [InitializeOnLoadMethod]
    static void ValidateOnLoad()
    {
        EditorApplication.delayCall += EnsureShootRifleUsesSoldierAvatar;
    }

    [MenuItem("Tools/Player/Fix Third Person Rig Setup")]
    public static void EnsureShootRifleUsesSoldierAvatar()
    {
        Avatar soldierAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(SoldierAvatarPath);

        if (soldierAvatar == null)
        {
            Debug.LogError("[RigFix] Missing soldier avatar: " + SoldierAvatarPath);
            return;
        }

        foreach (string animationPath in GenericOverlayAnimationPaths)
            FixGenericOverlayImporter(animationPath, soldierAvatar);

        FixHumanoidSoldierImporter(RunningAnimationPath, soldierAvatar, true);
        FixHumanoidSoldierImporter(ShootRifleAnimationPath, soldierAvatar, false);
    }

    static void FixGenericOverlayImporter(string animationPath, Avatar soldierAvatar)
    {
        ModelImporter importer = AssetImporter.GetAtPath(animationPath) as ModelImporter;

        if (importer == null)
        {
            Debug.LogError("[RigFix] Missing animation importer: " + animationPath);
            return;
        }

        bool changed = false;

        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
            Debug.Log("[RigFix] Set " + animationPath + " Rig Animation Type = Generic (overlay clips).");
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            changed = true;
            Debug.Log("[RigFix] Set " + animationPath + " Avatar Definition = Copy From Other Avatar.");
        }

        if (importer.sourceAvatar != soldierAvatar)
        {
            importer.sourceAvatar = soldierAvatar;
            changed = true;
            Debug.Log("[RigFix] Assigned YurowmAvatar.FBX to " + animationPath + ".");
        }

        if (importer.importAnimation == false)
        {
            importer.importAnimation = true;
            changed = true;
            Debug.Log("[RigFix] Enabled animation import on " + animationPath + ".");
        }

        if (changed)
            importer.SaveAndReimport();
        else
            Debug.Log("[RigFix] " + animationPath + " rig import is already correct.");
    }

    static void FixHumanoidSoldierImporter(string animationPath, Avatar soldierAvatar, bool loopClip)
    {
        ModelImporter importer = AssetImporter.GetAtPath(animationPath) as ModelImporter;

        if (importer == null)
        {
            Debug.LogError("[RigFix] Missing running animation importer: " + animationPath);
            return;
        }

        bool changed = false;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
            Debug.Log("[RigFix] Set " + animationPath + " Rig Animation Type = Humanoid (Yurowm soldier retargeting).");
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            changed = true;
            Debug.Log("[RigFix] Set " + animationPath + " Avatar Definition = Copy From Other Avatar.");
        }

        if (importer.sourceAvatar != soldierAvatar)
        {
            importer.sourceAvatar = soldierAvatar;
            changed = true;
            Debug.Log("[RigFix] Assigned YurowmAvatar.FBX to humanoid clip: " + animationPath + ".");
        }

        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            changed = true;
            Debug.Log("[RigFix] Enabled animation import on " + animationPath + ".");
        }

        ModelImporterClipAnimation[] clipAnimations = importer.defaultClipAnimations;
        if (clipAnimations != null && clipAnimations.Length > 0)
        {
            bool clipChanged = false;
            for (int i = 0; i < clipAnimations.Length; i++)
            {
                if (clipAnimations[i].loopTime != loopClip)
                {
                    clipAnimations[i].loopTime = loopClip;
                    clipChanged = true;
                }

                if (loopClip && !clipAnimations[i].loopPose)
                {
                    clipAnimations[i].loopPose = true;
                    clipChanged = true;
                }
            }

            if (clipChanged)
            {
                importer.clipAnimations = clipAnimations;
                changed = true;
                Debug.Log("[RigFix] Set " + animationPath + " clip loopTime = " + loopClip + ".");
            }
        }

        if (changed)
            importer.SaveAndReimport();
        else
            Debug.Log("[RigFix] " + animationPath + " humanoid soldier import is already correct.");
    }
}
#endif

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class AnimationRebuilder
{
    // ── Sprite sheet GUIDs (from project) ─────────────────────────────────
    const string BowlerGUID  = "6a93a77017d2d0c46943ce94e34f3d68";
    const string BatsmanGUID = "de9cb1a0923b2444ab47be16ebc3845d";
    const string ViratGUID   = "eb9a980596918164a9e4ef68c590dcc3";

    // ── Animation asset paths ──────────────────────────────────────────────
    const string BowlerAnimDir  = "Assets/Animations/Bowler/";
    const string BatsmanAnimDir = "Assets/Animations/Batsman/";

    [MenuItem("Tools/Rebuild Cricket Animations")]
    public static void RebuildAll()
    {
        var bowlerSprites  = LoadSprites(BowlerGUID);
        var batsmanSprites = LoadSprites(BatsmanGUID);
        var viratSprites   = LoadSprites(ViratGUID);

        // ── Bowler animations ──────────────────────────────────────────────
        RebuildBowlerRunup  (bowlerSprites);
        RebuildBowlerDeliver(bowlerSprites);
        RebuildBowlerAppeal (bowlerSprites);

        // ── Batsman animations ─────────────────────────────────────────────
        RebuildBatIdle  (batsmanSprites, viratSprites);
        RebuildBatDrive (batsmanSprites, viratSprites);
        RebuildBatSix   (batsmanSprites, viratSprites);
        RebuildBatMiss  (batsmanSprites, viratSprites);
        RebuildBatCeleb (batsmanSprites, viratSprites);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AnimationRebuilder] All cricket animations rebuilt successfully!");
    }

    // ─── HELPERS ──────────────────────────────────────────────────────────

    static Dictionary<string, Sprite> LoadSprites(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var dict = new Dictionary<string, Sprite>();
        if (string.IsNullOrEmpty(path)) return dict;

        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            if (obj is Sprite sp) dict[sp.name] = sp;
        return dict;
    }

    static AnimationClip LoadClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) Debug.LogError("[AnimationRebuilder] Clip not found at: " + path);
        return clip;
    }

    static EditorCurveBinding SpriteBinding() => new EditorCurveBinding
    {
        type         = typeof(SpriteRenderer),
        path         = "",
        propertyName = "m_Sprite"
    };

    static ObjectReferenceKeyframe KF(float t, Sprite s) =>
        new ObjectReferenceKeyframe { time = t, value = s };

    static Sprite S(Dictionary<string, Sprite> d, string name)
    {
        if (d == null) return null;
        if (d.TryGetValue(name, out var sp)) return sp;
        
        // Soft fallback to check if we loaded with shortened names
        foreach (var pair in d)
        {
            if (pair.Key.EndsWith(name)) return pair.Value;
        }

        Debug.LogWarning("[AnimationRebuilder] Sprite not found: " + name);
        return null;
    }

    static void SetLoop(AnimationClip clip, bool loop, float stopTime)
    {
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = loop;
        s.stopTime = stopTime;
        AnimationUtility.SetAnimationClipSettings(clip, s);
    }

    // ── Sine-wave Y bob for running ──
    static AnimationCurve RunBobY(float duration, float amplitude, int cycles)
    {
        var c = new AnimationCurve();
        int steps = cycles * 8;
        for (int i = 0; i <= steps; i++)
        {
            float t = duration * i / steps;
            float y = amplitude * Mathf.Sin(Mathf.PI * 2f * cycles * (i / (float)steps));
            c.AddKey(new Keyframe(t, y, 0f, 0f));
        }
        return c;
    }

    // ── Squash-on-land Y scale ──
    static AnimationCurve SquashScaleY(float duration, int strides)
    {
        var c = new AnimationCurve();
        c.AddKey(0f, 1f);
        for (int i = 1; i <= strides * 2; i++)
        {
            float t = duration * i / (strides * 2f);
            c.AddKey(new Keyframe(t, i % 2 == 0 ? 0.92f : 1.06f, 0f, 0f));
        }
        c.AddKey(duration, 1f);
        return c;
    }

    // =================================================================
    // BOWLER
    // =================================================================

    static void RebuildBowlerRunup(Dictionary<string, Sprite> spr)
    {
        var clip = LoadClip(BowlerAnimDir + "bowl_runup.anim");
        if (clip == null) return;
        clip.ClearCurves();

        var frames = new[] { "bowler_runup1","bowler_runup2","bowler_runup1","bowler_runup2",
                              "bowler_runup1","bowler_runup2","bowler_runup1","bowler_runup2",
                              "bowler_runup1","bowler_runup2","bowler_runup1","bowler_runup2" };
        var kf = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            kf[i] = KF(i / 12f, S(spr, frames[i]));
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(), kf);

        clip.SetCurve("", typeof(Transform), "localPosition.y", RunBobY(1f, 0.12f, 4));
        clip.SetCurve("", typeof(Transform), "localScale.y", SquashScaleY(1f, 4));
        clip.SetCurve("", typeof(Transform), "localScale.x", SquashScaleY(1f, 4));

        var rot = new AnimationCurve(new Keyframe(0f, -8f), new Keyframe(1f, -8f));
        clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rot);

        SetLoop(clip, true, 1f);
        EditorUtility.SetDirty(clip);
        Debug.Log("  ✓ bowl_runup");
    }

    static void RebuildBowlerDeliver(Dictionary<string, Sprite> spr)
    {
        var clip = LoadClip(BowlerAnimDir + "bowl_deliver.anim");
        if (clip == null) return;
        clip.ClearCurves();

        string[] frameNames = {
            "bowler_loadup", "bowler_loadup", "bowler_leap", "bowler_release", "bowler_release", 
            "bowler_followthru", "bowler_followthru", "bowler_landing", "bowler_landing", "bowler_finish", "bowler_finish"
        };
        float[] times = { 0f, 0.05f, 0.10f, 0.20f, 0.28f, 0.36f, 0.44f, 0.52f, 0.60f, 0.72f, 0.84f };
        var kf = new ObjectReferenceKeyframe[frameNames.Length];
        for (int i = 0; i < frameNames.Length; i++)
            kf[i] = KF(times[i], S(spr, frameNames[i]));
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(), kf);

        var px = new AnimationCurve();
        px.AddKey(new Keyframe(0f,    0f,   0f,  0f));
        px.AddKey(new Keyframe(0.10f,-0.15f,0f,  0f));
        px.AddKey(new Keyframe(0.20f,-0.35f,0f,  0f));
        px.AddKey(new Keyframe(0.44f,-0.5f, 0f,  0f));
        px.AddKey(new Keyframe(0.84f,-0.4f, 0f,  0f));
        clip.SetCurve("", typeof(Transform), "localPosition.x", px);

        var py = new AnimationCurve();
        py.AddKey(0f,    0f);
        py.AddKey(0.10f, 0.28f);
        py.AddKey(0.20f, 0.15f);
        py.AddKey(0.36f,-0.05f);
        py.AddKey(0.52f, 0f);
        py.AddKey(0.84f, 0f);
        clip.SetCurve("", typeof(Transform), "localPosition.y", py);

        var rz = new AnimationCurve();
        rz.AddKey(0f,    0f);
        rz.AddKey(0.08f, 20f);
        rz.AddKey(0.20f,-25f);
        rz.AddKey(0.36f,-10f);
        rz.AddKey(0.84f,  0f);
        clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rz);

        var sy = new AnimationCurve();
        sy.AddKey(0f,    1f);
        sy.AddKey(0.10f, 1.12f);
        sy.AddKey(0.36f, 0.85f);
        sy.AddKey(0.52f, 1f);
        sy.AddKey(0.84f, 1f);
        clip.SetCurve("", typeof(Transform), "localScale.y", sy);

        var sx = new AnimationCurve();
        sx.AddKey(0f,    1f);
        sx.AddKey(0.10f, 0.9f);
        sx.AddKey(0.36f, 1.15f);
        sx.AddKey(0.52f, 1f);
        sx.AddKey(0.84f, 1f);
        clip.SetCurve("", typeof(Transform), "localScale.x", sx);

        SetLoop(clip, false, 0.9f);
        EditorUtility.SetDirty(clip);
        Debug.Log("  ✓ bowl_deliver");
    }

    static void RebuildBowlerAppeal(Dictionary<string, Sprite> spr)
    {
        var clip = LoadClip(BowlerAnimDir + "bowl_appeal.anim");
        if (clip == null) return;
        clip.ClearCurves();

        var kf = new ObjectReferenceKeyframe[]
        {
            KF(0f,   S(spr, "bowler_finish")),
            KF(0.15f,S(spr, "bowler_followthru")),
            KF(0.3f, S(spr, "bowler_finish")),
            KF(0.45f,S(spr, "bowler_followthru")),
            KF(0.6f, S(spr, "bowler_finish")),
        };
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(), kf);

        var py = new AnimationCurve();
        py.AddKey(0f,    0f);
        py.AddKey(0.15f, 0.25f);
        py.AddKey(0.3f,  0f);
        py.AddKey(0.45f, 0.2f);
        py.AddKey(0.6f,  0f);
        clip.SetCurve("", typeof(Transform), "localPosition.y", py);

        var sy = new AnimationCurve();
        sy.AddKey(0f, 1f); sy.AddKey(0.15f, 1.1f); sy.AddKey(0.3f, 0.92f);
        sy.AddKey(0.45f, 1.08f); sy.AddKey(0.6f, 1f);
        clip.SetCurve("", typeof(Transform), "localScale.y", sy);

        var rz = new AnimationCurve();
        rz.AddKey(0f, 0f); rz.AddKey(0.15f, -15f); rz.AddKey(0.3f, 0f);
        rz.AddKey(0.45f, -12f); rz.AddKey(0.6f, 0f);
        clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rz);

        SetLoop(clip, false, 0.65f);
        EditorUtility.SetDirty(clip);
        Debug.Log("  ✓ bowl_appeal");
    }

    // =================================================================
    // BATSMAN
    // =================================================================

    static Sprite BS(Dictionary<string, Sprite> bat, Dictionary<string, Sprite> vir, string virName, string batName)
        => S(vir, virName) ?? S(bat, batName);

    static void RebuildBatIdle(Dictionary<string, Sprite> bat, Dictionary<string, Sprite> vir)
    {
        var clip = LoadClip(BatsmanAnimDir + "bat_idle.anim");
        if (clip == null) return;
        clip.ClearCurves();

        var kf = new ObjectReferenceKeyframe[]
        {
            KF(0f,   BS(bat, vir, "idle",  "Gemini_Generated_Image_jep0fijep0fijep0_idle")),
            KF(0.4f, BS(bat, vir, "ready", "Gemini_Generated_Image_jep0fijep0fijep0_guard")),
            KF(0.8f, BS(bat, vir, "idle",  "Gemini_Generated_Image_jep0fijep0fijep0_idle")),
        };
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(), kf);

        var py = new AnimationCurve();
        py.AddKey(0f, 0f); py.AddKey(0.4f, 0.04f); py.AddKey(0.8f, 0f);
        clip.SetCurve("", typeof(Transform), "localPosition.y", py);

        var px = new AnimationCurve();
        px.AddKey(0f, 0f); px.AddKey(0.4f, 0.02f); px.AddKey(0.8f, 0f);
        clip.SetCurve("", typeof(Transform), "localPosition.x", px);

        var rz = new AnimationCurve();
        rz.AddKey(0f, 0f); rz.AddKey(0.4f, 1.5f); rz.AddKey(0.8f, 0f);
        clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rz);

        SetLoop(clip, true, 0.8f);
        EditorUtility.SetDirty(clip);
        Debug.Log("  ✓ bat_idle");
    }

    static void RebuildBatDrive(Dictionary<string, Sprite> bat, Dictionary<string, Sprite> vir)
    {
        var clip = LoadClip(BatsmanAnimDir + "bat_drive.anim");
        if (clip == null) return;
        clip.ClearCurves();

        var kf = new ObjectReferenceKeyframe[]
        {
            KF(0f,    BS(bat, vir, "ready",     "Gemini_Generated_Image_jep0fijep0fijep0_ready")),
            KF(0.06f, BS(bat, vir, "backlift",  "Gemini_Generated_Image_jep0fijep0fijep0_swing_side")),
            KF(0.14f, BS(bat, vir, "contact",   "Gemini_Generated_Image_jep0fijep0fijep0_swing_up")),
            KF(0.22f, BS(bat, vir, "drive",     "Gemini_Generated_Image_jep0fijep0fijep0_loft")),
            KF(0.32f, BS(bat, vir, "followthru","Gemini_Generated_Image_jep0fijep0fijep0_swing_side")),
            KF(0.44f, BS(bat, vir, "idle",      "Gemini_Generated_Image_jep0fijep0fijep0_idle")),
        };
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(), kf);

        var px = new AnimationCurve();
        px.AddKey(0f,    0f);
        px.AddKey(0.14f,-0.18f);
        px.AddKey(0.32f,-0.22f);
        px.AddKey(0.44f,-0.1f);
        clip.SetCurve("", typeof(Transform), "localPosition.x", px);

        var py = new AnimationCurve();
        py.AddKey(0f,    0f);
        py.AddKey(0.10f,-0.1f);
        py.AddKey(0.22f, 0.05f);
        py.AddKey(0.44f, 0f);
        clip.SetCurve("", typeof(Transform), "localPosition.y", py);

        var rz = new AnimationCurve();
        rz.AddKey(0f,    0f);
        rz.AddKey(0.06f, 18f);
        rz.AddKey(0.14f,-5f);
        rz.AddKey(0.32f,-15f);
        rz.AddKey(0.44f,  0f);
        clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rz);

        var sy = new AnimationCurve();
        sy.AddKey(0f, 1f); sy.AddKey(0.14f, 0.88f); sy.AddKey(0.22f, 1.1f); sy.AddKey(0.44f, 1f);
        clip.SetCurve("", typeof(Transform), "localScale.y", sy);
        
        var sx = new AnimationCurve();
        sx.AddKey(0f, 1f); sx.AddKey(0.14f, 1.12f); sx.AddKey(0.22f, 0.92f); sx.AddKey(0.44f, 1f);
        clip.SetCurve("", typeof(Transform), "localScale.x", sx);

        SetLoop(clip, false, 0.5f);
        EditorUtility.SetDirty(clip);
        Debug.Log("  ✓ bat_drive");
    }

    static void RebuildBatSix(Dictionary<string, Sprite> bat, Dictionary<string, Sprite> vir)
    {
        var clip = LoadClip(BatsmanAnimDir + "bat_six.anim");
        if (clip == null) return;
        clip.ClearCurves();

        var kf = new ObjectReferenceKeyframe[]
        {
            KF(0f,    BS(bat, vir, "ready",    "Gemini_Generated_Image_jep0fijep0fijep0_ready")),
            KF(0.07f, BS(bat, vir, "backlift", "Gemini_Generated_Image_jep0fijep0fijep0_swing_side")),
            KF(0.16f, BS(bat, vir, "sixswing", "Gemini_Generated_Image_jep0fijep0fijep0_swing_up")),
            KF(0.25f, BS(bat, vir, "contact",  "Gemini_Generated_Image_jep0fijep0fijep0_loft")),
            KF(0.36f, BS(bat, vir, "followthru","Gemini_Generated_Image_jep0fijep0fijep0_swing_side")),
            KF(0.48f, BS(bat, vir, "idle",     "Gemini_Generated_Image_jep0fijep0fijep0_idle")),
        };
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(), kf);

        var px = new AnimationCurve();
        px.AddKey(0f,    0f);
        px.AddKey(0.25f,-0.25f);
        px.AddKey(0.48f,-0.1f);
        clip.SetCurve("", typeof(Transform), "localPosition.x", px);

        var py = new AnimationCurve();
        py.AddKey(0f,    0f);
        py.AddKey(0.16f, 0.18f);
        py.AddKey(0.36f, 0f);
        py.AddKey(0.48f, 0f);
        clip.SetCurve("", typeof(Transform), "localPosition.y", py);

        var rz = new AnimationCurve();
        rz.AddKey(0f,    0f);
        rz.AddKey(0.07f, 25f);
        rz.AddKey(0.25f,-30f);
        rz.AddKey(0.36f,-20f);
        rz.AddKey(0.48f,  0f);
        clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rz);

        var sy = new AnimationCurve();
        sy.AddKey(0f, 1f); sy.AddKey(0.25f, 0.82f); sy.AddKey(0.36f, 1.18f); sy.AddKey(0.48f, 1f);
        clip.SetCurve("", typeof(Transform), "localScale.y", sy);
        
        var sx = new AnimationCurve();
        sx.AddKey(0f, 1f); sx.AddKey(0.25f, 1.2f);  sx.AddKey(0.36f, 0.88f); sx.AddKey(0.48f, 1f);
        clip.SetCurve("", typeof(Transform), "localScale.x", sx);

        SetLoop(clip, false, 0.5f);
        EditorUtility.SetDirty(clip);
        Debug.Log("  ✓ bat_six");
    }

    static void RebuildBatMiss(Dictionary<string, Sprite> bat, Dictionary<string, Sprite> vir)
    {
        var clip = LoadClip(BatsmanAnimDir + "bat_miss.anim");
        if (clip == null) return;
        clip.ClearCurves();

        var kf = new ObjectReferenceKeyframe[]
        {
            KF(0f,    BS(bat, vir, "ready",    "Gemini_Generated_Image_jep0fijep0fijep0_ready")),
            KF(0.08f, BS(bat, vir, "backlift", "Gemini_Generated_Image_jep0fijep0fijep0_swing_side")),
            KF(0.18f, BS(bat, vir, "sixswing", "Gemini_Generated_Image_jep0fijep0fijep0_swing_up")),
            KF(0.28f, BS(bat, vir, "followthru","Gemini_Generated_Image_jep0fijep0fijep0_sweep")),
            KF(0.42f, BS(bat, vir, "idle",     "Gemini_Generated_Image_jep0fijep0fijep0_idle")),
        };
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(), kf);

        var px = new AnimationCurve();
        px.AddKey(0f,    0f);
        px.AddKey(0.18f,-0.08f);
        px.AddKey(0.28f, 0.1f);
        px.AddKey(0.42f, 0f);
        clip.SetCurve("", typeof(Transform), "localPosition.x", px);

        var rz = new AnimationCurve();
        rz.AddKey(0f,    0f);
        rz.AddKey(0.08f, 20f);
        rz.AddKey(0.18f,-35f);
        rz.AddKey(0.3f,  -8f);
        rz.AddKey(0.42f,  0f);
        clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rz);

        var sy = new AnimationCurve();
        sy.AddKey(0f, 1f); sy.AddKey(0.18f, 1.15f); sy.AddKey(0.28f, 0.9f); sy.AddKey(0.42f, 1f);
        clip.SetCurve("", typeof(Transform), "localScale.y", sy);

        SetLoop(clip, false, 0.45f);
        EditorUtility.SetDirty(clip);
        Debug.Log("  ✓ bat_miss");
    }

    static void RebuildBatCeleb(Dictionary<string, Sprite> bat, Dictionary<string, Sprite> vir)
    {
        var clip = LoadClip(BatsmanAnimDir + "bat_celebrate.anim");
        if (clip == null) return;
        clip.ClearCurves();

        var celebSprite = BS(bat, vir, "celebrate", "Gemini_Generated_Image_jep0fijep0fijep0_celebrate");
        var idleSprite  = BS(bat, vir, "idle",      "Gemini_Generated_Image_jep0fijep0fijep0_idle");
        var kf = new ObjectReferenceKeyframe[]
        {
            KF(0f,   idleSprite),
            KF(0.1f, celebSprite),
            KF(0.3f, idleSprite),
            KF(0.4f, celebSprite),
            KF(0.6f, idleSprite),
            KF(0.7f, celebSprite),
            KF(0.9f, idleSprite),
        };
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(), kf);

        var py = new AnimationCurve();
        py.AddKey(0f, 0f);
        py.AddKey(0.1f, 0.3f); py.AddKey(0.2f, 0f);
        py.AddKey(0.4f, 0.35f); py.AddKey(0.5f, 0f);
        py.AddKey(0.7f, 0.28f); py.AddKey(0.8f, 0f);
        py.AddKey(0.9f, 0f);
        clip.SetCurve("", typeof(Transform), "localPosition.y", py);

        var sy = new AnimationCurve();
        sy.AddKey(0f, 1f);
        sy.AddKey(0.1f, 1.15f); sy.AddKey(0.2f, 0.88f);
        sy.AddKey(0.4f, 1.15f); sy.AddKey(0.5f, 0.88f);
        sy.AddKey(0.7f, 1.12f); sy.AddKey(0.8f, 0.9f);
        sy.AddKey(0.9f, 1f);
        clip.SetCurve("", typeof(Transform), "localScale.y", sy);
        
        var sx = new AnimationCurve();
        sx.AddKey(0f, 1f);
        sx.AddKey(0.1f, 0.88f); sx.AddKey(0.2f, 1.1f);
        sx.AddKey(0.4f, 0.88f); sx.AddKey(0.5f, 1.1f);
        sx.AddKey(0.7f, 0.9f);  sx.AddKey(0.8f, 1.08f);
        sx.AddKey(0.9f, 1f);
        clip.SetCurve("", typeof(Transform), "localScale.x", sx);

        var rz = new AnimationCurve();
        rz.AddKey(0f, 0f); rz.AddKey(0.1f, 10f); rz.AddKey(0.2f, 0f);
        rz.AddKey(0.4f, -10f); rz.AddKey(0.5f, 0f);
        rz.AddKey(0.7f, 10f); rz.AddKey(0.9f, 0f);
        clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rz);

        SetLoop(clip, false, 1f);
        EditorUtility.SetDirty(clip);
        Debug.Log("  ✓ bat_celebrate");
    }
}
#endif
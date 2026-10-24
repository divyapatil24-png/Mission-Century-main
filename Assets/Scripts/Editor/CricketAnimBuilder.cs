#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public static class CricketAnimBuilder
{
    const string BAT_SHEET = "Assets/Pictures/Gemini_Generated_Image_jep0fijep0fijep0.png";
    const string BOW_SHEET = "Assets/Pictures/Gemini_Generated_Image_jep0fijep0fijep0 (2).png";

    [MenuItem("Tools/Mission Century/1 - Build Animations")]
    public static void BuildAnimations()
    {
        // Ensure folders exist
        foreach (var f in new[]{"Assets/Animations","Assets/Animations/Batsman","Assets/Animations/Bowler"})
        {
            var p = f.Split('/'); string cur = p[0];
            for (int i = 1; i < p.Length; i++)
            {
                string nx = cur + "/" + p[i];
                if (!AssetDatabase.IsValidFolder(nx)) AssetDatabase.CreateFolder(cur, p[i]);
                cur = nx;
            }
        }

        var batSprs = LoadSprites(BAT_SHEET);
        var bowSprs = LoadSprites(BOW_SHEET);

        // ── BATSMAN ANIMATOR ──────────────────────────────────────────────
        var batPath = "Assets/Animations/Batsman/BatsmanCtrl.controller";
        var batCtrl = AnimatorController.CreateAnimatorControllerAtPath(batPath);
        batCtrl.AddParameter("Swing",    AnimatorControllerParameterType.Trigger);
        batCtrl.AddParameter("isSix",    AnimatorControllerParameterType.Bool);
        batCtrl.AddParameter("Celebrate",AnimatorControllerParameterType.Trigger);
        batCtrl.AddParameter("Miss",     AnimatorControllerParameterType.Trigger);

        var bRoot = batCtrl.layers[0].stateMachine;

        // Sync filenames with AnimationRebuilder!
        // IDLE: stance alternating with guard pose
        var bIdle  = Clip("Bat_Idle",  Picks(batSprs,"idle","guard"),              3f, true,  "Assets/Animations/Batsman/bat_idle.anim");
        // FOUR (cover drive): stance → backlift → contact → follow-through
        var bFour  = Clip("Bat_Four",  Picks(batSprs,"ready","side","guard","idle"), 10f, false, "Assets/Animations/Batsman/bat_drive.anim");
        // SIX (loft): stance → swing up → loft → celebrate
        var bSix   = Clip("Bat_Six",   Picks(batSprs,"ready","up","loft","celebrate"), 10f, false, "Assets/Animations/Batsman/bat_six.anim");
        // CELEBRATE: celebrate pose loops twice then back to idle
        var bCeleb = Clip("Bat_Celeb", Picks(batSprs,"celebrate","idle","celebrate","idle"), 5f, false, "Assets/Animations/Batsman/bat_celebrate.anim");
        // MISS: duck/guard then back to idle
        var bMiss  = Clip("Bat_Miss",  Picks(batSprs,"ready","guard","idle"),      8f, false, "Assets/Animations/Batsman/bat_miss.anim");

        var sIdle  = bRoot.AddState("Idle");  sIdle.motion  = bIdle;
        var sFour  = bRoot.AddState("Four");  sFour.motion  = bFour;
        var sSix   = bRoot.AddState("Six");   sSix.motion   = bSix;
        var sCeleb = bRoot.AddState("Celeb"); sCeleb.motion = bCeleb;
        var sMiss  = bRoot.AddState("Miss");  sMiss.motion  = bMiss;
        bRoot.defaultState = sIdle;

        // Idle → Four (Swing trigger + NOT isSix)
        var t = sIdle.AddTransition(sFour);
        t.AddCondition(AnimatorConditionMode.If, 0, "Swing");
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "isSix");
        t.hasExitTime = false; t.duration = 0;

        // Idle → Six (Swing trigger + isSix)
        t = sIdle.AddTransition(sSix);
        t.AddCondition(AnimatorConditionMode.If, 0, "Swing");
        t.AddCondition(AnimatorConditionMode.If, 0, "isSix");
        t.hasExitTime = false; t.duration = 0;

        // Four → Idle
        t = sFour.AddTransition(sIdle);
        t.hasExitTime = true; t.exitTime = 1f; t.duration = 0;

        // Six → Idle
        t = sSix.AddTransition(sIdle);
        t.hasExitTime = true; t.exitTime = 1f; t.duration = 0;

        // AnyState → Celeb
        var atCeleb = bRoot.AddAnyStateTransition(sCeleb);
        atCeleb.AddCondition(AnimatorConditionMode.If, 0, "Celebrate");
        atCeleb.hasExitTime = false; atCeleb.duration = 0;

        // Celeb → Idle
        t = sCeleb.AddTransition(sIdle);
        t.hasExitTime = true; t.exitTime = 1f; t.duration = 0;

        // Idle → Miss
        t = sIdle.AddTransition(sMiss);
        t.AddCondition(AnimatorConditionMode.If, 0, "Miss");
        t.hasExitTime = false; t.duration = 0;

        // Miss → Idle
        t = sMiss.AddTransition(sIdle);
        t.hasExitTime = true; t.exitTime = 1f; t.duration = 0;

        // ── BOWLER ANIMATOR ───────────────────────────────────────────────
        var bowPath = "Assets/Animations/Bowler/BowlerCtrl.controller";
        var bowCtrl = AnimatorController.CreateAnimatorControllerAtPath(bowPath);
        bowCtrl.AddParameter("Bowl",   AnimatorControllerParameterType.Trigger);
        bowCtrl.AddParameter("Appeal", AnimatorControllerParameterType.Trigger);

        var bwRoot = bowCtrl.layers[0].stateMachine;

        var bwRunup   = Clip("Bowl_Runup",   Picks(bowSprs,"runup1","runup2","runup1"),               8f,  true,  "Assets/Animations/Bowler/bowl_runup.anim");
        var bwDeliver = Clip("Bowl_Deliver", Picks(bowSprs,"loadup","leap","release","followthru","landing","finish"), 10f, false, "Assets/Animations/Bowler/bowl_deliver.anim");
        var bwAppeal  = Clip("Bowl_Appeal",  Picks(bowSprs,"finish","followthru","finish"),            5f,  false, "Assets/Animations/Bowler/bowl_appeal.anim");

        var bwRunupState   = bwRoot.AddState("Runup");   bwRunupState.motion   = bwRunup;
        var bwDeliverState = bwRoot.AddState("Deliver"); bwDeliverState.motion = bwDeliver;
        var bwAppealState  = bwRoot.AddState("Appeal");  bwAppealState.motion  = bwAppeal;
        bwRoot.defaultState = bwRunupState;

        // Runup → Deliver on Bowl trigger
        t = bwRunupState.AddTransition(bwDeliverState);
        t.AddCondition(AnimatorConditionMode.If, 0, "Bowl");
        t.hasExitTime = false; t.duration = 0;

        // Deliver → Runup
        t = bwDeliverState.AddTransition(bwRunupState);
        t.hasExitTime = true; t.exitTime = 1f; t.duration = 0;

        // Any → Appeal
        var at = bwRoot.AddAnyStateTransition(bwAppealState);
        at.AddCondition(AnimatorConditionMode.If, 0, "Appeal");
        at.hasExitTime = false; at.duration = 0;

        // Appeal → Runup
        t = bwAppealState.AddTransition(bwRunupState);
        t.hasExitTime = true; t.exitTime = 1f; t.duration = 0;

        // ── ATTACH TO SCENE OBJECTS ────────────────────────────────────────
        var player = GameObject.Find("Player");
        if (player != null)
        {
            var anim = player.GetComponent<Animator>() ?? player.AddComponent<Animator>();
            anim.runtimeAnimatorController = batCtrl;
            var sr = player.GetComponent<SpriteRenderer>();
            if (sr != null && batSprs.ContainsKey("idle")) sr.sprite = batSprs["idle"];
            Debug.Log("[CricketAnimBuilder] Batsman animator attached!");
        }

        var bowler = GameObject.Find("Bowler");
        if (bowler != null)
        {
            var anim = bowler.GetComponent<Animator>() ?? bowler.AddComponent<Animator>();
            anim.runtimeAnimatorController = bowCtrl;
            var sr = bowler.GetComponent<SpriteRenderer>();
            if (sr != null && bowSprs.ContainsKey("runup1")) sr.sprite = bowSprs["runup1"];
            Debug.Log("[CricketAnimBuilder] Bowler animator attached!");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CricketAnimBuilder] Done building and matching animation controllers!");
    }

    static AnimationClip Clip(string name, Sprite[] frames, float fps, bool loop, string path)
    {
        var clip = new AnimationClip { name = name, frameRate = fps };
        if (loop)
        {
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
        }
        var binding = new EditorCurveBinding {
            type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite"
        };
        int total = frames.Length + (loop ? 1 : 0);
        var keys  = new ObjectReferenceKeyframe[total];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        if (loop)
            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static Sprite[] Picks(Dictionary<string, Sprite> map, params string[] keys)
    {
        var list = new List<Sprite>();
        foreach (var k in keys)
            if (map.ContainsKey(k) && map[k] != null) list.Add(map[k]);
        return list.ToArray();
    }

    static Dictionary<string, Sprite> LoadSprites(string path)
    {
        var d = new Dictionary<string, Sprite>();
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            if (a is Sprite s)
            {
                string key = s.name;
                int idx = key.LastIndexOf('_');
                if (idx >= 0) key = key.Substring(idx + 1);
                d[key] = s;
            }
        return d;
    }
}
#endif
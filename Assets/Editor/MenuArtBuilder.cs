using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

// MenuArtBuilder — one-shot Editor utility that turns the separate source
// images (flat grey backgrounds) into transparent, tightly-cropped PNGs used to
// compose the main menu at runtime:
//   • batsman + bowler  (split + cut out from the two-character art)
//   • btn_start / btn_high / btn_about / btn_quit  (cut out + cropped)
//   • field background  (copied as-is)
// The grey background is removed by a flood-fill from the image borders, so
// interior white button text is never punched through.
//
// Run via menu  Tools/Mission/Build Menu Art, or it auto-runs once after a
// recompile if the outputs are missing.
public static class MenuArtBuilder
{
    const string OUT = "Assets/Resources/Menu/";

    const string CHARS = "Assets/Pictures/Gemini_Generated_Image_xskxfvxskxfvxskx (1).png";
    const string BTNS  = "Assets/Pictures/Gemini_Generated_Image_xskxfvxskxfvxskx (2).png";
    const string QUIT  = "Assets/Pictures/Gemini_Generated_Image_txv2natxv2natxv2.png";
    const string FIELD = "Assets/Pictures/Gemini_Generated_Image_w9rlxjw9rlxjw9rl (2).png";

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        // Run once when the cut-out layers don't exist yet (e.g. after pulling
        // the script). Skip if already generated so it doesn't churn on reload.
        if (File.Exists(OUT + "btn_start.png") && File.Exists(OUT + "batsman.png")) return;
        EditorApplication.delayCall += () => { try { Build(); } catch (System.Exception e) { Debug.LogError("[MenuArtBuilder] " + e); } };
    }

    [MenuItem("Tools/Mission/Build Menu Art")]
    public static void Build()
    {
        Directory.CreateDirectory(OUT);

        // ── characters: cut out grey, split L/R, crop each ──
        var chars = Load(CHARS);
        CutoutGrey(chars);
        int mid = chars.width / 2;
        SaveCropped(chars, 0, 0, mid, chars.height, OUT + "batsman.png");
        SaveCropped(chars, mid, 0, chars.width - mid, chars.height, OUT + "bowler.png");

        // ── buttons: cut out grey, split into 3 vertical thirds, crop each ──
        var btns = Load(BTNS);
        CutoutGrey(btns);
        int h3 = btns.height / 3;
        // image rows are bottom-up; START is the top third (highest y)
        SaveCropped(btns, 0, btns.height - h3,     btns.width, h3, OUT + "btn_start.png");
        SaveCropped(btns, 0, btns.height - 2 * h3, btns.width, h3, OUT + "btn_high.png");
        SaveCropped(btns, 0, 0,                    btns.width, h3, OUT + "btn_about.png");

        // ── quit button: cut out grey, crop to the button (left portion) ──
        var quit = Load(QUIT);
        CutoutGrey(quit);
        SaveCropped(quit, 0, 0, (int)(quit.width * 0.62f), quit.height, OUT + "btn_quit.png");

        // ── field background: straight copy (already full-bleed art) ──
        File.Copy(FIELD, OUT + "field.png", true);

        AssetDatabase.Refresh();
        ConfigureSprites();
        Debug.Log("[MenuArtBuilder] Menu art generated in " + OUT);
    }

    // True for the flat bright-grey background (low saturation, fairly bright).
    static bool IsBg(Color c)
    {
        float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        float mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        float sat = mx - mn;
        float bright = (c.r + c.g + c.b) / 3f;
        return sat < 0.10f && bright > 0.55f;
    }

    // Flood-fill from all border pixels, clearing connected background to alpha 0.
    // Interior pixels (e.g. white text inside a blue button) are walled off by the
    // coloured content, so they survive.
    static void CutoutGrey(Texture2D tex)
    {
        int W = tex.width, H = tex.height;
        var px = tex.GetPixels();
        var seen = new bool[W * H];
        var q = new Queue<int>();

        System.Action<int,int> seed = (x, y) => {
            int i = y * W + x;
            if (!seen[i] && IsBg(px[i])) { seen[i] = true; q.Enqueue(i); }
        };
        for (int x = 0; x < W; x++) { seed(x, 0); seed(x, H - 1); }
        for (int y = 0; y < H; y++) { seed(0, y); seed(W - 1, y); }

        while (q.Count > 0)
        {
            int i = q.Dequeue();
            int x = i % W, y = i / W;
            px[i] = new Color(px[i].r, px[i].g, px[i].b, 0f);
            if (x > 0)     { int j = i - 1; if (!seen[j] && IsBg(px[j])) { seen[j] = true; q.Enqueue(j); } }
            if (x < W - 1) { int j = i + 1; if (!seen[j] && IsBg(px[j])) { seen[j] = true; q.Enqueue(j); } }
            if (y > 0)     { int j = i - W; if (!seen[j] && IsBg(px[j])) { seen[j] = true; q.Enqueue(j); } }
            if (y < H - 1) { int j = i + W; if (!seen[j] && IsBg(px[j])) { seen[j] = true; q.Enqueue(j); } }
        }

        // Feather: soften 1px halo where a kept pixel borders cleared background
        // and is itself greyish, to reduce the grey fringe.
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (px[i].a == 0f) continue;
                bool edge = (x>0 && px[i-1].a==0f) || (x<W-1 && px[i+1].a==0f)
                         || (y>0 && px[i-W].a==0f) || (y<H-1 && px[i+W].a==0f);
                if (edge)
                {
                    float mx = Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b));
                    float mn = Mathf.Min(px[i].r, Mathf.Min(px[i].g, px[i].b));
                    if (mx - mn < 0.16f && (px[i].r+px[i].g+px[i].b)/3f > 0.5f)
                        px[i] = new Color(px[i].r, px[i].g, px[i].b, 0.35f);
                }
            }

        tex.SetPixels(px);
        tex.Apply();
    }

    // Save a sub-rect, auto-cropped to the non-transparent bounding box.
    static void SaveCropped(Texture2D tex, int rx, int ry, int rw, int rh, string path)
    {
        int W = tex.width;
        var src = tex.GetPixels(rx, ry, rw, rh);
        int minX = rw, minY = rh, maxX = -1, maxY = -1;
        for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
                if (src[y * rw + x].a > 0.05f)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        if (maxX < 0) { minX = 0; minY = 0; maxX = rw - 1; maxY = rh - 1; } // all transparent fallback
        int pad = 4;
        minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(rw - 1, maxX + pad); maxY = Mathf.Min(rh - 1, maxY + pad);
        int cw = maxX - minX + 1, ch = maxY - minY + 1;

        var outPx = new Color[cw * ch];
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                outPx[y * cw + x] = src[(minY + y) * rw + (minX + x)];

        var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        outTex.SetPixels(outPx);
        outTex.Apply();
        File.WriteAllBytes(path, outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);
    }

    static Texture2D Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(bytes);
        return t;
    }

    // Make every generated PNG import as a UI Sprite.
    static void ConfigureSprites()
    {
        foreach (var f in new[] { "batsman", "bowler", "btn_start", "btn_high", "btn_about", "btn_quit", "field" })
        {
            string p = OUT + f + ".png";
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
    }
}

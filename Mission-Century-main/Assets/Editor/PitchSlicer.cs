using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class PitchSlicer
{
    const string Src = "Assets/Pictures/ChatGPT Image Jun 7, 2026, 10_55_42 PM.png";

    static bool IsBg(Color32 c) => c.a < 16;

    [MenuItem("Tools/Mission/Analyze Pitch Reference")]
    public static void Analyze()
    {
        var tex = Load();
        int W = tex.width, H = tex.height;
        var px = tex.GetPixels32();
        // per-row content count (y=0 is bottom in Unity texture space)
        int[] rowCount = new int[H];
        for (int y = 0; y < H; y++)
        {
            int cnt = 0;
            for (int x = 0; x < W; x++) if (!IsBg(px[y * W + x])) cnt++;
            rowCount[y] = cnt;
        }
        // find content bands (rows with >0.5% width content)
        int thresh = Mathf.Max(3, W / 200);
        var sb = new StringBuilder();
        sb.Append($"W={W} H={H} thresh={thresh} || ");
        bool inBand = false; int bandStart = 0;
        for (int y = 0; y < H; y++)
        {
            bool on = rowCount[y] > thresh;
            if (on && !inBand) { inBand = true; bandStart = y; }
            else if (!on && inBand) { inBand = false; sb.Append($"BAND y[{bandStart}..{y - 1}] h={y - bandStart} ; "); }
        }
        if (inBand) sb.Append($"BAND y[{bandStart}..{H - 1}] h={H - bandStart}");
        Debug.Log("[PitchSlicer] " + sb.ToString());
    }

    static Texture2D Load()
    {
        byte[] bytes = File.ReadAllBytes(Src);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        ImageConversion.LoadImage(tex, bytes);
        return tex;
    }

    [MenuItem("Tools/Mission/Profile Rows")]
    public static void Profile()
    {
        var tex = Load();
        int W = tex.width, H = tex.height;
        var px = tex.GetPixels32();
        var sb = new StringBuilder();
        for (int y = 0; y < H; y += 24)
        {
            int minX = -1, maxX = -1, cnt = 0;
            for (int x = 0; x < W; x++)
            {
                if (IsBg(px[y * W + x])) continue;
                if (minX < 0) minX = x; maxX = x; cnt++;
            }
            sb.Append($"y{y}:[{minX}-{maxX}]c{cnt} ; ");
        }
        Debug.Log("[PitchSlicer.Profile] " + sb.ToString());
    }

    // Crop a y-band [yLo..yHi] (texture space), tighten X to content, knock out white bg to alpha 0, save PNG.
    static Vector2Int CropBand(Texture2D tex, int yLo, int yHi, string outPath)
    {
        int W = tex.width;
        var px = tex.GetPixels32();
        int minX = W, maxX = 0;
        for (int y = yLo; y <= yHi; y++)
            for (int x = 0; x < W; x++)
                if (!IsBg(px[y * W + x])) { if (x < minX) minX = x; if (x > maxX) maxX = x; }
        int cw = maxX - minX + 1, ch = yHi - yLo + 1;
        var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        var outPx = new Color32[cw * ch];
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
            {
                var c = px[(yLo + y) * W + (minX + x)];
                if (IsBg(c)) c = new Color32(0, 0, 0, 0);
                outPx[y * cw + x] = c;
            }
        outTex.SetPixels32(outPx);
        outTex.Apply();
        File.WriteAllBytes(outPath, outTex.EncodeToPNG());
        return new Vector2Int(cw, ch);
    }

    [MenuItem("Tools/Mission/Slice Pitch + Stumps")]
    public static void Slice()
    {
        // Bands measured from Profile Rows (texture space, y=0 bottom):
        //   Stumps column: y[40..372]   Pitch trapezoid: y[420..958]
        SliceWith(420, 958, 40, 372);
    }

    public static void SliceWith(int pitchLo, int pitchHi, int stumpLo, int stumpHi)
    {
        var tex = Load();
        var pSize = CropBand(tex, pitchLo, pitchHi, "Assets/Sprites/PitchField.png");
        var sSize = CropBand(tex, stumpLo, stumpHi, "Assets/Sprites/StumpsSet.png");
        AssetDatabase.ImportAsset("Assets/Sprites/PitchField.png", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Sprites/StumpsSet.png", ImportAssetOptions.ForceUpdate);
        ConfigSprite("Assets/Sprites/PitchField.png", new Vector2(0.5f, 0f));
        ConfigSprite("Assets/Sprites/StumpsSet.png", new Vector2(0.5f, 0f));
        AssetDatabase.Refresh();
        Debug.Log($"[PitchSlicer] PitchField={pSize.x}x{pSize.y}  StumpsSet={sSize.x}x{sSize.y}");
    }

    static void ConfigSprite(string path, Vector2 pivot)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.spritePixelsPerUnit = 100f;
        var ts = new TextureImporterSettings();
        ti.ReadTextureSettings(ts);
        ts.spriteAlignment = (int)SpriteAlignment.Custom;
        ts.spritePivot = pivot;
        ti.SetTextureSettings(ts);
        var so = new SerializedObject(ti);
        so.FindProperty("m_Alignment").intValue = (int)SpriteAlignment.Custom;
        var piv = so.FindProperty("m_SpritePivot");
        piv.vector2Value = pivot;
        so.ApplyModifiedProperties();
        ti.SaveAndReimport();
    }
}

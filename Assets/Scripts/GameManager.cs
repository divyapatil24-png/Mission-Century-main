using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [HideInInspector] public bool isPlaying = false;
    [HideInInspector] public int  runs      = 0;
    [HideInInspector] public int  lives     = 1;
    [HideInInspector] public int  score     = 0;

    // One wicket — get out once and the innings (game) is over, like real cricket.
    const int TARGET = 100, MAX_LIVES = 1;

    GameObject panMain, panGame, panOver, panWin, panAbout, panHigh;
    TextMeshProUGUI hudRuns, hudLives, floatTxt;
    TextMeshProUGUI overTxt, winTxt, menuBest;
    RectTransform   progFill;
    SpriteRenderer  stumpSR;
    StumpsReactor   stumpReactor;

    void Awake() { if(Instance){Destroy(gameObject);return;} Instance=this; }

    void Start()
    {
        BuildUI();
        CreateStumps();
        stumpSR = GameObject.Find("Stumps")?.GetComponent<SpriteRenderer>();
        stumpReactor = GameObject.Find("Stumps")?.GetComponent<StumpsReactor>();
        ShowPanel(panMain);
    }

    // ── GAMEPLAY ───────────────────────────────────────────────────────────
    public void AddRuns(int n)
    {
        runs+=n;
        string msg; Color col;
        if(n>=6)      { msg="SIX!";  col=new Color(1f,0.84f,0.1f); }
        else if(n>=4) { msg="FOUR!"; col=new Color(0.2f,0.8f,1f); }
        else          { msg=n+(n==1?" RUN":" RUNS"); col=new Color(0.85f,0.95f,0.8f); }
        Float(msg, col);
        RefHUD();
        if(runs>=TARGET) StartCoroutine(Win());
    }

    public void OnBallMissed()
    {
        if(!isPlaying) return;
        lives--;
        Float("OUT!",Color.red);
        if(stumpReactor) stumpReactor.Topple(); else StartCoroutine(FlashStumps());
        FindFirstObjectByType<BowlerController>()?.TriggerAppeal();
        FindFirstObjectByType<PlayerController>()?.PlayMiss();
        RefHUD();
        if(lives<=0) StartCoroutine(Over());
    }

    IEnumerator FlashStumps()
    {
        if(stumpSR==null) yield break;
        stumpSR.color = Color.red;
        yield return new WaitForSeconds(0.35f);
        if(stumpSR) stumpSR.color = Color.white;
    }

    public void OnPlayerHit()
    {
        if(!isPlaying) return;
        lives--;
        Float("OUT!",Color.red);
        FindFirstObjectByType<BowlerController>()?.TriggerAppeal();
        RefHUD();
        if(lives<=0) StartCoroutine(Over());
    }

    public void StartGame()
    {
        isPlaying=true; runs=0; score=0; lives=MAX_LIVES;
        FindFirstObjectByType<PlayerController>()?.ResetPlayer(new Vector3(-6.3f,-1.8f,0f));
        FindFirstObjectByType<BowlerController>()?.ResetBowler();
        foreach(var b in FindObjectsByType<CricketBallProjectile>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        foreach(var o in GameObject.FindGameObjectsWithTag("Obstacle")) Destroy(o);
        if(stumpSR) stumpSR.color = Color.white;
        if(stumpReactor) stumpReactor.ResetStumps();
        RefHUD(); ShowPanel(panGame);
    }

    public void ShowMainMenu()
    {
        isPlaying=false;
        if(menuBest) menuBest.text="BEST: "+GetHS(1);
        ShowPanel(panMain);
    }
    public void ShowAbout() { ShowPanel(panAbout); }
    public void ShowHigh()  { RefreshHigh(); ShowPanel(panHigh); }
    public void ShowInfo()  { ShowHigh(); } // backwards-compat alias

    IEnumerator Win()
    {
        isPlaying=false;
        FindFirstObjectByType<PlayerController>()?.PlayCelebrate();
        SaveHS(); yield return new WaitForSeconds(1.5f);
        if(winTxt) winTxt.text="RUNS: "+runs+"     BEST: "+GetHS(1);
        ShowPanel(panWin);
    }

    IEnumerator Over()
    {
        isPlaying=false; SaveHS();
        yield return new WaitForSeconds(0.8f);
        if(overTxt) overTxt.text="RUNS: "+runs+" / "+TARGET+"     BEST: "+GetHS(1);
        ShowPanel(panOver);
    }

    // ── TOP-3 HIGH SCORES ──────────────────────────────────────────────────
    int GetHS(int rank) => PlayerPrefs.GetInt("HS"+rank, 0);

    void SaveHS()
    {
        int[] arr = { GetHS(1), GetHS(2), GetHS(3), runs };
        System.Array.Sort(arr); System.Array.Reverse(arr);
        PlayerPrefs.SetInt("HS1",arr[0]);
        PlayerPrefs.SetInt("HS2",arr[1]);
        PlayerPrefs.SetInt("HS3",arr[2]);
        PlayerPrefs.SetInt("HS", arr[0]); // legacy key
        PlayerPrefs.Save();
    }

    void ShowPanel(GameObject t)
    {
        foreach(var p in new[]{panMain,panGame,panOver,panWin,panAbout,panHigh})
            if(p!=null) p.SetActive(p==t);
    }

    void RefHUD()
    {
        if(hudRuns)  hudRuns.text  = runs+" / "+TARGET;
        if(hudLives) hudLives.text = "LIVES: "+lives;
        if(progFill){ var a=progFill.anchorMax; a.x=Mathf.Clamp01((float)runs/TARGET); progFill.anchorMax=a; }
    }

    void Float(string msg, Color col)
    {
        if(floatTxt==null) return;
        floatTxt.text=msg; floatTxt.color=col;
        floatTxt.gameObject.SetActive(true);
        StopCoroutine("HF"); StartCoroutine("HF");
    }
    IEnumerator HF() { yield return new WaitForSeconds(1.2f); if(floatTxt) floatTxt.gameObject.SetActive(false); }

    // ── UI BUILD ───────────────────────────────────────────────────────────
    void BuildUI()
    {
        var old=GameObject.Find("Canvas"); if(old) Destroy(old);
        var cvGO=new GameObject("Canvas");
        var cv=cvGO.AddComponent<Canvas>(); cv.renderMode=RenderMode.ScreenSpaceOverlay; cv.sortingOrder=50;
        var cs=cvGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution=new Vector2(1920,1080); cs.matchWidthOrHeight=0.5f;
        cvGO.AddComponent<GraphicRaycaster>();

        BuildMainMenu(cvGO);
        BuildGameHUD(cvGO);
        BuildGameOver(cvGO);
        BuildWin(cvGO);
        BuildAbout(cvGO);
        BuildHigh(cvGO);
    }

    // ── MAIN MENU (composed from separate cut-out art layers) ───────────────
    void BuildMainMenu(GameObject cvGO)
    {
        panMain=FP(cvGO,"MainMenu",new Color(0.06f,0.10f,0.08f,1f));

        // 1) Green field background (full screen).
        var field = ImgLayer(panMain,"Field","Menu/field",V(0,0),V(1,1),Vector2.zero,Vector2.zero);
        if (field == null) FP(panMain,"FieldFallback",new Color(0.18f,0.42f,0.20f,1f));
        // dark vignette for title legibility
        PP(panMain,"TopShade",V(0,0.74f),V(1,1),V(0,0),V(0,0),new Color(0f,0.05f,0.02f,0.55f));

        // 2) Characters either side (cut-out, transparent bg), kept to native aspect.
        ImgFit(panMain,"Batsman","Menu/batsman",V(0.015f,0.30f),V(0.34f,0.74f));
        ImgFit(panMain,"Bowler", "Menu/bowler", V(0.66f,0.30f),V(0.985f,0.74f));

        // 3) Title.
        CT(panMain,"TitleSh","MISSION CENTURY",118,new Color(0,0,0,0.6f),V(0.503f,0.882f),V(1700,210));
        CT(panMain,"Title","MISSION CENTURY",118,new Color(1f,0.86f,0.16f),V(0.5f,0.886f),V(1700,210));
        CT(panMain,"Sub","CRICKET",54,new Color(0.9f,0.97f,1f),V(0.5f,0.80f),V(1200,80));

        // 4) Buttons — the cut-out button artwork IS the button (clickable image).
        ImgBtn(panMain,"Start","Menu/btn_start", ()=>StartGame(), V(0.34f,0.45f),V(0.66f,0.555f));
        ImgBtn(panMain,"High", "Menu/btn_high",  ()=>ShowHigh(),  V(0.34f,0.335f),V(0.66f,0.44f));
        ImgBtn(panMain,"About","Menu/btn_about", ()=>ShowAbout(), V(0.34f,0.22f),V(0.66f,0.325f));
        ImgBtn(panMain,"Quit", "Menu/btn_quit",  ()=>QuitGame(),  V(0.02f,0.04f),V(0.17f,0.12f));

        menuBest = CT(panMain,"Best","BEST:  "+GetHS(1),34,new Color(1f,0.86f,0.16f),V(0.84f,0.07f),V(420,52));
    }

    void QuitGame()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    // ── GAME HUD ────────────────────────────────────────────────────────────
    void BuildGameHUD(GameObject cvGO)
    {
        panGame=new GameObject("GamePanel"); panGame.transform.SetParent(cvGO.transform,false);
        var grt=panGame.AddComponent<RectTransform>(); grt.anchorMin=Vector2.zero; grt.anchorMax=Vector2.one; grt.offsetMin=grt.offsetMax=Vector2.zero;
        var bar=PP(panGame,"Bar",V(0,1),V(1,1),V(0,-88),V(0,0),new Color(0f,0.10f,0.02f,0.92f));
        hudRuns  = MkT(bar,"R","0/100",44,TextAlignmentOptions.Left,  new Color(1f,0.88f,0.1f),V(0.01f,0.05f),V(0.26f,0.95f));
        hudLives = MkT(bar,"L","",38,TextAlignmentOptions.Center,Color.white,             V(0.40f,0.05f),V(0.60f,0.95f));
        hudLives.gameObject.SetActive(false); // single wicket — no lives counter shown
        MkT(bar,"B","BEST: "+GetHS(1),30,TextAlignmentOptions.Right,new Color(0.5f,0.88f,1f),V(0.80f,0.05f),V(0.99f,0.95f));
        var pbg=PP(bar,"PBG",V(0.26f,0.12f),V(0.78f,0.88f),V(0,0),V(0,0),new Color(1,1,1,0.14f));
        var fill=new GameObject("Fill"); fill.transform.SetParent(pbg.transform,false);
        fill.AddComponent<Image>().color=new Color(1f,0.82f,0.06f);
        progFill=fill.GetComponent<RectTransform>(); progFill.anchorMin=V(0,0); progFill.anchorMax=V(0.01f,1);
        progFill.offsetMin=progFill.offsetMax=Vector2.zero;

        var ftGO=new GameObject("Ft"); ftGO.transform.SetParent(panGame.transform,false);
        floatTxt=ftGO.AddComponent<TextMeshProUGUI>(); floatTxt.fontSize=140; floatTxt.fontStyle=FontStyles.Bold;
        floatTxt.alignment=TextAlignmentOptions.Center; floatTxt.color=Color.yellow;
        var frt=ftGO.GetComponent<RectTransform>(); frt.anchorMin=frt.anchorMax=V(0.5f,0.62f); frt.pivot=V(0.5f,0.5f); frt.sizeDelta=V(800,200);
        ftGO.SetActive(false);
        panGame.SetActive(false);
    }

    void BuildGameOver(GameObject cvGO)
    {
        panOver=FP(cvGO,"GameOver",new Color(0.10f,0.02f,0.02f,0.96f));
        CT(panOver,"T","GAME OVER",110,new Color(1f,0.15f,0.05f),V(0.5f,0.78f),V(1100,230));
        overTxt=CT(panOver,"S","",44,new Color(1f,0.88f,0.12f),V(0.5f,0.60f),V(1000,72));
        SBtn(panOver,"R","PLAY AGAIN",()=>StartGame(),   V(0.28f,0),V(0.72f,0),V(0,200),V(0,340));
        SBtn(panOver,"M","MAIN MENU", ()=>ShowMainMenu(),V(0.28f,0),V(0.72f,0),V(0,50), V(0,188));
        panOver.SetActive(false);
    }

    void BuildWin(GameObject cvGO)
    {
        panWin=FP(cvGO,"WinPanel",new Color(0.03f,0.17f,0.04f,0.96f));
        CT(panWin,"T","CENTURY!",110,new Color(1f,0.82f,0.06f),V(0.5f,0.80f),V(1100,230));
        winTxt=CT(panWin,"S","",44,new Color(0.82f,0.96f,1f),V(0.5f,0.62f),V(1000,72));
        SBtn(panWin,"R2","PLAY AGAIN",()=>StartGame(),   V(0.28f,0),V(0.72f,0),V(0,200),V(0,340));
        SBtn(panWin,"M2","MAIN MENU", ()=>ShowMainMenu(),V(0.28f,0),V(0.72f,0),V(0,50), V(0,188));
        panWin.SetActive(false);
    }

    // ── ABOUT: how to run + controls ─────────────────────────────────────────
    void BuildAbout(GameObject cvGO)
    {
        panAbout=FP(cvGO,"AboutPanel",new Color(0.04f,0.10f,0.16f,0.98f));
        PP(panAbout,"Glow",V(0f,0f),V(1f,0.5f),V(0,0),V(0,0),new Color(0.12f,0.26f,0.18f,0.7f));
        CT(panAbout,"T","ABOUT",80,new Color(1f,0.84f,0.1f),V(0.5f,0.88f),V(900,140));

        CT(panAbout,"GoalH","THE GAME",40,new Color(0.55f,0.9f,1f),V(0.5f,0.78f),V(1400,56));
        CT(panAbout,"GoalB",
           "You are Virat Kohli chasing a CENTURY. Face the bowler,\ntime your shot and pile on the runs to reach 100 and win.\nIf a ball beats your bat and hits the stumps you are OUT.\nYou have ONE wicket - get out and it is GAME OVER.",
           32,Color.white,V(0.5f,0.655f),V(1500,260));

        CT(panAbout,"CtrlH","HOW TO PLAY / CONTROLS",40,new Color(0.55f,0.9f,1f),V(0.5f,0.52f),V(1400,56));
        CT(panAbout,"CtrlB",
           "HIT  -  F key  or  Mouse Click / Tap\n\n"+
           "The bowler runs in and pitches the ball at you.\n"+
           "Time your hit as it reaches the bat to score\n"+
           "1, 2, 3, 4 or 6 runs.  Miss and the stumps go down!\n"+
           "The bowling gets faster as you near your century.",
           32,new Color(0.9f,0.96f,0.9f),V(0.5f,0.32f),V(1500,360));

        SBtn(panAbout,"AB","BACK",()=>ShowMainMenu(),V(0.38f,0),V(0.62f,0),V(0,60),V(0,170));
        panAbout.SetActive(false);
    }

    // ── HIGHSCORE: ranks 1=crown, 2=trophy, 3=medal ─────────────────────────
    TextMeshProUGUI hsScore1, hsScore2, hsScore3;
    void BuildHigh(GameObject cvGO)
    {
        panHigh=FP(cvGO,"HighPanel",new Color(0.05f,0.08f,0.14f,0.98f));
        PP(panHigh,"Glow",V(0f,0f),V(1f,0.5f),V(0,0),V(0,0),new Color(0.18f,0.16f,0.05f,0.6f));
        CT(panHigh,"T","HIGH SCORES",80,new Color(1f,0.84f,0.1f),V(0.5f,0.88f),V(1100,140));

        hsScore1 = RankRow(panHigh,"R1","Icons/icon_crown", "1ST",new Color(1f,0.84f,0.0f), 0.66f);   // crown / gold
        hsScore2 = RankRow(panHigh,"R2","Icons/icon_trophy","2ND",new Color(0.80f,0.84f,0.88f),0.50f); // trophy / silver
        hsScore3 = RankRow(panHigh,"R3","Icons/icon_medal", "3RD",new Color(0.80f,0.52f,0.25f),0.34f); // medal / bronze

        SBtn(panHigh,"HB","BACK",()=>ShowMainMenu(),V(0.38f,0),V(0.62f,0),V(0,60),V(0,170));
        panHigh.SetActive(false);
    }

    // builds a rank row, returns the score text (filled by RefreshHigh)
    TextMeshProUGUI RankRow(GameObject p,string id,string iconRes,string rankLbl,Color col,float yAnchor)
    {
        var row = PP(p,id,V(0.16f,yAnchor-0.075f),V(0.84f,yAnchor+0.075f),V(0,0),V(0,0),new Color(0f,0f,0f,0.30f));
        // icon (crown / trophy / medal)
        var icoGO=new GameObject("Icon"); icoGO.transform.SetParent(row.transform,false);
        var ico=icoGO.AddComponent<Image>();
        ico.sprite=Resources.Load<Sprite>(iconRes); ico.preserveAspect=true;
        if(ico.sprite==null) ico.color=col; // fallback tint if icon missing
        var iRT=icoGO.GetComponent<RectTransform>(); iRT.anchorMin=V(0.03f,0.12f); iRT.anchorMax=V(0.15f,0.92f); iRT.offsetMin=iRT.offsetMax=Vector2.zero;
        // rank label
        CT(row,"Rk",rankLbl,40,col,V(0.27f,0.5f),V(260,80));
        // score
        var sc = CT(row,"Score","0 RUNS",58,col,V(0.66f,0.5f),V(760,90));
        return sc;
    }

    void RefreshHigh()
    {
        if(hsScore1) hsScore1.text = GetHS(1)+" RUNS";
        if(hsScore2) hsScore2.text = GetHS(2)+" RUNS";
        if(hsScore3) hsScore3.text = GetHS(3)+" RUNS";
    }

    // ── UI HELPERS ───────────────────────────────────────────────────────────
    void SBtn(GameObject p,string n,string lbl,System.Action act,Vector2 a0,Vector2 a1,Vector2 o0,Vector2 o1)
    {
        var o=new GameObject(n+"O"); o.transform.SetParent(p.transform,false);
        o.AddComponent<Image>().color=new Color(0.18f,0.22f,0.20f);
        var oRT=o.GetComponent<RectTransform>(); oRT.anchorMin=a0;oRT.anchorMax=a1;oRT.offsetMin=o0;oRT.offsetMax=o1;
        var i2=new GameObject(n); i2.transform.SetParent(o.transform,false);
        i2.AddComponent<Image>().color=new Color(0.55f,0.63f,0.62f);
        var iRT=i2.GetComponent<RectTransform>(); iRT.anchorMin=V(0.02f,0.10f);iRT.anchorMax=V(0.98f,0.88f);iRT.offsetMin=iRT.offsetMax=Vector2.zero;
        var btn=i2.AddComponent<Button>(); btn.onClick.AddListener(()=>act?.Invoke());
        var lGO=new GameObject("L"); lGO.transform.SetParent(i2.transform,false);
        var tmp=lGO.AddComponent<TextMeshProUGUI>(); tmp.text=lbl;tmp.fontSize=54;
        tmp.alignment=TextAlignmentOptions.Center;tmp.color=new Color(0.08f,0.12f,0.10f);tmp.fontStyle=FontStyles.Bold;
        var lRT=lGO.GetComponent<RectTransform>(); lRT.anchorMin=Vector2.zero;lRT.anchorMax=Vector2.one;lRT.offsetMin=lRT.offsetMax=Vector2.zero;
    }

    // A stretched Image layer from a Resources sprite; returns null if missing.
    Image ImgLayer(GameObject p,string n,string res,Vector2 a0,Vector2 a1,Vector2 o0,Vector2 o1)
    {
        var sp=Resources.Load<Sprite>(res); if(sp==null) return null;
        var go=new GameObject(n); go.transform.SetParent(p.transform,false);
        var img=go.AddComponent<Image>(); img.sprite=sp; img.raycastTarget=false;
        var rt=go.GetComponent<RectTransform>(); rt.anchorMin=a0;rt.anchorMax=a1;rt.offsetMin=o0;rt.offsetMax=o1;
        return img;
    }

    // An Image placed in a 0‑1 anchor box but kept to its native aspect ratio.
    void ImgFit(GameObject p,string n,string res,Vector2 a0,Vector2 a1)
    {
        var sp=Resources.Load<Sprite>(res); if(sp==null) return;
        var go=new GameObject(n); go.transform.SetParent(p.transform,false);
        var img=go.AddComponent<Image>(); img.sprite=sp; img.preserveAspect=true; img.raycastTarget=false;
        var rt=go.GetComponent<RectTransform>(); rt.anchorMin=a0;rt.anchorMax=a1;rt.offsetMin=rt.offsetMax=Vector2.zero;
    }

    // A clickable button whose visual IS the cut-out artwork sprite.
    void ImgBtn(GameObject p,string n,string res,System.Action act,Vector2 a0,Vector2 a1)
    {
        var go=new GameObject(n+"Btn"); go.transform.SetParent(p.transform,false);
        var img=go.AddComponent<Image>();
        var sp=Resources.Load<Sprite>(res);
        if(sp!=null){ img.sprite=sp; img.color=Color.white; img.preserveAspect=true; }
        else        { img.color=new Color(0.16f,0.34f,0.55f,0.95f); } // fallback plate
        img.raycastTarget=true;
        var rt=go.GetComponent<RectTransform>(); rt.anchorMin=a0;rt.anchorMax=a1;rt.offsetMin=rt.offsetMax=Vector2.zero;
        var btn=go.AddComponent<Button>(); btn.targetGraphic=img;
        var cb=btn.colors; cb.highlightedColor=new Color(1.1f,1.1f,1.1f,1f); cb.pressedColor=new Color(0.8f,0.8f,0.8f,1f); btn.colors=cb;
        btn.onClick.AddListener(()=>act?.Invoke());
        // Fallback label only when the sprite is missing.
        if(sp==null){
            var lGO=new GameObject("L"); lGO.transform.SetParent(go.transform,false);
            var tmp=lGO.AddComponent<TextMeshProUGUI>(); tmp.text=n.ToUpper();tmp.fontSize=48;
            tmp.alignment=TextAlignmentOptions.Center;tmp.color=Color.white;tmp.fontStyle=FontStyles.Bold;
            var lRT=lGO.GetComponent<RectTransform>(); lRT.anchorMin=Vector2.zero;lRT.anchorMax=Vector2.one;lRT.offsetMin=lRT.offsetMax=Vector2.zero;
        }
    }

    static Vector2 V(float x,float y)=>new Vector2(x,y);
    static GameObject FP(GameObject p,string n,Color c){var go=new GameObject(n);go.transform.SetParent(p.transform,false);go.AddComponent<Image>().color=c;var rt=go.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;return go;}
    static GameObject PP(GameObject p,string n,Vector2 a0,Vector2 a1,Vector2 o0,Vector2 o1,Color c){var go=new GameObject(n);go.transform.SetParent(p.transform,false);go.AddComponent<Image>().color=c;var rt=go.GetComponent<RectTransform>();rt.anchorMin=a0;rt.anchorMax=a1;rt.offsetMin=o0;rt.offsetMax=o1;return go;}
    static TextMeshProUGUI CT(GameObject p,string n,string txt,int sz,Color c,Vector2 anc,Vector2 sd){var go=new GameObject(n);go.transform.SetParent(p.transform,false);var t=go.AddComponent<TextMeshProUGUI>();t.text=txt;t.fontSize=sz;t.alignment=TextAlignmentOptions.Center;t.color=c;t.fontStyle=FontStyles.Bold;t.richText=true;var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anc;rt.pivot=V(0.5f,0.5f);rt.sizeDelta=sd;rt.anchoredPosition=Vector2.zero;return t;}
    static TextMeshProUGUI MkT(GameObject p,string n,string txt,int sz,TextAlignmentOptions al,Color c,Vector2 a0,Vector2 a1){var go=new GameObject(n);go.transform.SetParent(p.transform,false);var t=go.AddComponent<TextMeshProUGUI>();t.text=txt;t.fontSize=sz;t.alignment=al;t.color=c;t.fontStyle=FontStyles.Bold;var rt=go.GetComponent<RectTransform>();rt.anchorMin=a0;rt.anchorMax=a1;rt.offsetMin=rt.offsetMax=Vector2.zero;return t;}

    void CreateStumps()
    {
        var stumps = GameObject.Find("Stumps");
        if (stumps != null) return;

        stumps = new GameObject("Stumps");
        stumps.transform.position = new Vector3(-7.4f, -1.26f, 0f);

        var sr = stumps.AddComponent<SpriteRenderer>();
        #if UNITY_EDITOR
        sr.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Stumps.png");
        #endif
        sr.sortingOrder = 5;

        var col = stumps.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.2f, 1.2f);
    }
}

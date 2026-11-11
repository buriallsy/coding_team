using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun
{
    public sealed class SkywellHUD : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        private GUIStyle body,title,prompt,small,button;
        private SkywellFish fish;
        private Texture2D[] weatherIcons;
        private Material observationImageMaterial;
        public bool DesignerGuidance {get;private set;}
        private static float HudScale=>Mathf.Clamp(Screen.height/900f,.65f,1.6f);
        private static Rect WeatherSelectorRect(float width,float height)=>new Rect(width-140,height-146,124,130);
        public static bool IsWeatherSelectorHit(Vector2 screenPoint)
        {float scale=HudScale;return WeatherSelectorRect(Screen.width/scale,Screen.height/scale).Contains(new Vector2(screenPoint.x,Screen.height-screenPoint.y)/scale);}
        private static Rect WeatherCardRect(float width,float height,int index)=>new Rect(width-350+index*110,height-315,106,117);
        private void Update()
        {
            if(court&&(court.MarketInputBlocked||court.ObservationInputBlocked||court.WeatherInputBlocked&&!court.WeatherSelectionHeld))return;
            if(Keyboard.current!=null&&Keyboard.current.f2Key.wasPressedThisFrame)DesignerGuidance=!DesignerGuidance;
            if(!court||Mouse.current==null)return;
            float scale=HudScale,w=Screen.width/scale,h=Screen.height/scale;
            Vector2 raw=Mouse.current.position.ReadValue(),point=new Vector2(raw.x,Screen.height-raw.y)/scale;
            if(Mouse.current.leftButton.wasPressedThisFrame&&WeatherSelectorRect(w,h).Contains(point))court.BeginPointerWeatherPicker();
            if(court.WeatherPickerOpen&&Mouse.current.delta.ReadValue().sqrMagnitude>.01f)
                for(int i=0;i<3;i++)if(WeatherCardRect(w,h,i).Contains(point))court.ChooseWeatherCandidate((SkywellToken)(i+1));
        }
        private void Start(){fish=GetComponent<SkywellFish>();}
        public void Initialize(SkywellController owner)=>court=owner;
        private void OnGUI()
        {
            if(!court||court.Consoles==null||court.Consoles.Length<2)return;
            if(court.MarketModule&&court.MarketModule.IsShopOpen)return;
            if(body==null)
            {
                var font=Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");if(!font){enabled=false;return;}
                body=new GUIStyle{font=font,fontSize=15,wordWrap=true};body.normal.textColor=new Color(.88f,.96f,.94f);
                title=new GUIStyle(body){fontSize=22,fontStyle=FontStyle.Bold};
                prompt=new GUIStyle(body){alignment=TextAnchor.MiddleCenter,fontSize=17};prompt.normal.textColor=new Color(1,.84f,.43f);
                small=new GUIStyle(body){fontSize=13,alignment=TextAnchor.MiddleCenter};
                button=new GUIStyle(GUI.skin.button){font=font,fontSize=15,wordWrap=true};
            }
            var matrix=GUI.matrix;var color=GUI.color;
            try
            {
                float scale=HudScale,w=Screen.width/scale,h=Screen.height/scale;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
                if(court.Story&&court.Story.DialogueOpen){DrawStory(w,h);return;}
                if(court.ObservationOpen){DrawObservation(w,h);return;}
                if(!court.MarketModule&&court.FlowerMarket&&court.FlowerMarket.IsShopOpen)
                {DrawFlowerMarket(court.FlowerMarket,w,h);return;}
                if(DesignerGuidance){Panel(new Rect(16,16,355,106));GUI.Label(new Rect(30,26,327,30),"策划调试 · F2 关闭",title);GUI.Label(new Rect(30,63,327,58),court.Objective,body);}
                if(court.Journey&&court.Journey.SunnyTokenObtained&&!court.Journey.HasReturnedToTower)
                {Panel(new Rect(w-235,16,219,38));GUI.Label(new Rect(w-223,23,195,26),"晴牌已取得 · 划船爬梯返塔",body);}
                if(DesignerGuidance&&court.Journey&&!court.Journey.BelowGroundActive&&court.Height>5.8f)
                {
                    Panel(new Rect(16,134,300,109));
                    GUI.Label(new Rect(30,142,272,30),court.BorrowedSunAtVillage?"借来的阳光抵达山阴":"山阴尚未收到阳光",title);
                    GUI.Label(new Rect(30,209,272,25),(court.WaterTransferring?"自动送水 · ":"水槽 ")+court.WaterLevel.ToString("0.0")+" → 配重 "+court.BallastAmount.ToString("0.0"),body);
                    GUI.Label(new Rect(30,180,272,30),court.CargoPausedForLight?"货路等待采光接通":court.Optics.Collector&&court.Optics.Collector.Active?"向阳山正在接收世界阳光":"采光需要晴天与真实日照",body);
                }
                if(court.Journey&&court.Journey.BelowGroundActive)
                {
                    if(fish&&(fish.Phase==SkywellFishPhase.Warning||fish.Phase==SkywellFishPhase.Chase))
                    {Panel(new Rect(w/2-150,24,300,66));GUI.Label(new Rect(w/2-138,30,276,46),fish.Phase==SkywellFishPhase.Chase?"鱼正在追来！":"鱼发现了你",prompt);}
                }
                if(!court.WeatherTargeting&&!court.WeatherSelectionHeld)
                {
                string hint=court.Prompt();bool hasHint=!string.IsNullOrWhiteSpace(hint);
                float bw=Mathf.Min(900,w-180);Panel(new Rect(16,h-(hasHint?107:57),bw,hasHint?91:41));
                if(hasHint)GUI.Label(new Rect(16+12,h-100,bw-24,43),hint,prompt);
                string viewHint=court.CameraRig.IsShoulderView?"Z 斜俯视 · 鼠标观察":"Z 过肩视角";
                string controls=court.Grove&&court.Grove.Ride&&court.Grove.Ride.IsMounted
                    ? "WASD 骑行 · Space 跳跃 · E 下鹿 · "+viewHint+" · R 重置"
                    : (court.Player.IsSwimming?"WASD 水面游动":"WASD 行走")+" · 短按 E 交互 / 长按施令 · "+viewHint+" · R 重置";
                GUI.Label(new Rect(16+12,h-57,bw-24,34),controls,small);
                }
                DrawWeatherSelector(w,h);
                var reward=court.Optics?court.Optics.SettlementReceiver:null;
                if(reward&&reward.State==SettlementSunState.Spreading)
                {Panel(new Rect(w/2-166,24,332,45));GUI.Label(new Rect(w/2-154,31,308,30),"晴光正在传入村庄",prompt);}
                if(reward&&reward.RewardToastVisible)
                {
                    float y=h-(court.WeatherPickerOpen?420:246);
                    Panel(new Rect(w-276,y,260,84));
                    GUI.Label(new Rect(w-260,y+10,228,30),"获得水果",title);
                    GUI.Label(new Rect(w-260,y+46,228,26),"已装入货车",body);
                }
                if(court.WeatherTargeting)DrawTargeting(w,h);
            }
            finally{GUI.matrix=matrix;GUI.color=color;}
        }

// OnGUI already applies HudScale to GUI.matrix, and passes logical width/height.

private GUIStyle storyBody,storySpeaker,storyHint,storyPanel;
private Texture2D storyPanelTexture,storyTriangleTexture;
private Vector2 storyScroll;
private string storyDisplayedText;
private readonly GUIContent storyContent=new GUIContent();

private void EnsureStoryVisuals()
{
    // Invoked only from OnGUI, after body/font initialization. Never allocate textures per repaint.
    if(storyPanel!=null)return;
    storyPanelTexture=CreateStoryPanelTexture();
    storyTriangleTexture=CreateStoryTriangleTexture();
    storyPanel=new GUIStyle{border=new RectOffset(16,16,16,16)};
    storyPanel.normal.background=storyPanelTexture;
    storyBody=new GUIStyle(body){font=body.font,fontSize=25,alignment=TextAnchor.UpperLeft,wordWrap=true,richText=false};
    storyBody.normal.textColor=Color.white;
    storySpeaker=new GUIStyle(storyBody){fontSize=26,fontStyle=FontStyle.Normal,wordWrap=false,clipping=TextClipping.Clip};
    storyHint=new GUIStyle(storyBody){fontSize=14,wordWrap=false,alignment=TextAnchor.MiddleRight};
    storyHint.normal.textColor=new Color(1,1,1,.65f);
}

private void DrawStory(float width,float height)
{
    var story=court.Story;
    if(!story||!story.DialogueOpen)return;
    EnsureStoryVisuals();
    var oldColor=GUI.color;
    try
    {
        GUI.color=Color.white;
        float widthFactor=Mathf.Min(1,width/1000f);
        // Keep the spoken text at least 16 physical pixels even at the low end of HudScale.
        int minimumSize=Mathf.CeilToInt(16/HudScale);
        storyBody.fontSize=Mathf.Max(minimumSize,Mathf.RoundToInt(25*widthFactor));
        storySpeaker.fontSize=Mathf.Max(minimumSize,Mathf.RoundToInt(26*widthFactor));
        storyHint.fontSize=Mathf.Max(12,Mathf.RoundToInt(14*widthFactor));
        float panelWidth=Mathf.Min(800,width*.70f);
        float padding=Mathf.Clamp(panelWidth*.05f,16,40);
        float textWidth=Mathf.Max(1,panelWidth-padding*2);
        storyContent.text=story.Text??string.Empty;
        if(storyDisplayedText!=storyContent.text){storyDisplayedText=storyContent.text;storyScroll=Vector2.zero;}
        float textHeight=storyBody.CalcHeight(storyContent,textWidth);
        float panelHeight=Mathf.Clamp(textHeight+64,156,200);
        float bottomMargin=Mathf.Clamp(height*.045f,24,48);
        var panel=new Rect((width-panelWidth)/2,height-bottomMargin-panelHeight,panelWidth,panelHeight);
        GUI.Box(panel,GUIContent.none,storyPanel);

        // Speaker is above the left edge, with the body separated by the panel's upper padding.
        float speakerHeight=storySpeaker.lineHeight+8;
        GUI.Label(new Rect(panel.x+padding,panel.y-speakerHeight-8,textWidth,speakerHeight),story.Speaker??string.Empty,storySpeaker);
        var textRect=new Rect(panel.x+padding,panel.y+26,textWidth,panel.height-64);
        if(textHeight<=textRect.height+.5f)GUI.Label(textRect,storyContent,storyBody);
        else
        {
            // Pages normally occupy 2–3 lines. A narrow window must not silently crop the remainder.
            float scrollWidth=Mathf.Max(1,textRect.width-18);
            float fullHeight=storyBody.CalcHeight(storyContent,scrollWidth);
            storyScroll=GUI.BeginScrollView(textRect,storyScroll,new Rect(0,0,scrollWidth,fullHeight),false,true);
            GUI.Label(new Rect(0,0,scrollWidth,fullHeight),storyContent,storyBody);
            GUI.EndScrollView();
        }

        // A wide, unobtrusive footer hit area uses exactly the same progression as the E key.
        var continueRect=new Rect(panel.x+padding,panel.yMax-34,textWidth,26);
        if(GUI.Button(continueRect,GUIContent.none,GUIStyle.none))story.Advance();
        GUI.DrawTexture(new Rect(panel.center.x-7,panel.yMax-23,14,8),storyTriangleTexture,ScaleMode.StretchToFill);
        GUI.Label(new Rect(panel.x+padding,panel.yMax+6,textWidth,23),"E 继续  /  Esc 离开",storyHint);
    }
    finally {GUI.color=oldColor;}
}

private static Texture2D CreateStoryPanelTexture()
{
    const int size=64;const float radius=16;
    var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
    {
        name="Story dialogue rounded panel",hideFlags=HideFlags.HideAndDontSave,
        filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp
    };
    var pixels=new Color32[size*size];
    for(int y=0;y<size;y++)for(int x=0;x<size;x++)
    {
        float qx=Mathf.Abs(x+.5f-size*.5f)-(size*.5f-radius);
        float qy=Mathf.Abs(y+.5f-size*.5f)-(size*.5f-radius);
        float outsideX=Mathf.Max(qx,0),outsideY=Mathf.Max(qy,0);
        float distance=Mathf.Sqrt(outsideX*outsideX+outsideY*outsideY)+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
        float coverage=Mathf.Clamp01(.5f-distance);
        pixels[y*size+x]=new Color(.025f,.03f,.04f,.78f*coverage);
    }
    texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
}

private static Texture2D CreateStoryTriangleTexture()
{
    const int width=28,height=16;
    var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
    {
        name="Story dialogue continue triangle",hideFlags=HideFlags.HideAndDontSave,
        filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp
    };
    var pixels=new Color32[width*height];
    for(int y=0;y<height;y++)for(int x=0;x<width;x++)
    {
        // Texture y=0 is the bottom: the three vertices form a downward-pointing white triangle.
        float halfWidth=(y+.5f)/height*width*.5f;
        float coverage=Mathf.Clamp01(halfWidth-Mathf.Abs(x+.5f-width*.5f)+.5f);
        pixels[y*width+x]=new Color(1,1,1,coverage);
    }
    texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
}


        private void DrawObservation(float width,float height)
        {
            var view=court.Observation;
            GUI.color=new Color(.025f,.04f,.055f,1);
            GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture);
            GUI.color=Color.white;
            GUI.Label(new Rect(28,24,width-240,38),view.Title,title);
            if(GUI.Button(new Rect(width-184,20,156,40),"返回 · E / Esc",button))court.CloseObservation();
            if(Event.current.type==EventType.Repaint)
            {
                if(!observationImageMaterial)
                    observationImageMaterial=new Material(Resources.Load<Shader>("ObservationImage")){hideFlags=HideFlags.HideAndDontSave};
                float fit=Mathf.Min((width-48)/view.Image.width,(height-200)/view.Image.height);
                float imageWidth=view.Image.width*fit,imageHeight=view.Image.height*fit;
                var rect=new Rect((width-imageWidth)/2,80+(height-200-imageHeight)/2,imageWidth,imageHeight);
                Graphics.DrawTexture(rect,view.Image,observationImageMaterial);
            }
            float textWidth=Mathf.Min(920,width-64);
            GUI.Label(new Rect((width-textWidth)/2,height-96,textWidth,70),view.Caption,prompt);
        }
        private void DrawWeatherSelector(float w,float h)
        {
            var r=WeatherSelectorRect(w,h);
            Panel(r);WeatherIcon(new Rect(r.x+36,r.y+9,52,48),court.SelectedWeather,new Color(1,.85f,.45f));
            GUI.Label(new Rect(r.x+4,r.y+58,116,25),WeatherName(court.SelectedWeather),prompt);
            GUI.Label(new Rect(r.x+4,r.y+85,116,20),court.TokenLocation(court.SelectedWeather),small);
            GUI.Label(new Rect(r.x+4,r.y+106,116,20),"长按 Tab / 图标",small);
            if(!court.WeatherPickerOpen)return;
            float x=w-350,y=h-315;
            Panel(new Rect(x-8,y-8,342,160));
            for(int i=0;i<3;i++)
            {
                var token=(SkywellToken)(i+1);var card=WeatherCardRect(w,h,i);
                bool selected=court.WeatherCandidate==token;
                bool available=court.CanUseToken(token);
                GUI.color=selected?new Color(.15f,.36f,.36f):new Color(.055f,.14f,.15f);GUI.DrawTexture(card,Texture2D.whiteTexture);GUI.color=Color.white;
                WeatherIcon(new Rect(card.x+31,card.y+8,44,42),token,!available?new Color(.3f,.4f,.42f):selected?new Color(1,.85f,.45f):new Color(.8f,.87f,.86f));
                GUI.Label(new Rect(card.x+3,card.y+54,100,26),(i+1)+" "+WeatherName(token),small);
                GUI.Label(new Rect(card.x+3,card.y+82,100,27),court.TokenLocation(token),small);

            }
            GUI.Label(new Rect(x,y+124,322,25),"指向 / ← → / 1 2 3 选择 · 松开确认",small);
        }
        private void DrawTargeting(float w,float h)
        {
            float width=Mathf.Min(700,w-180);
            Panel(new Rect(16,h-105,width,89));
            GUI.Label(new Rect(28,h-98,width-24,43),court.TargetingPrompt,prompt);
            GUI.Label(new Rect(28,h-54,width-24,28),"按住 E · 左键施令 / 顺向风 · 右键逆向风 · Esc 取消",small);
        }
        private static string WeatherName(SkywellToken token)=>token==SkywellToken.Rain?"落雨":token==SkywellToken.Wind?"风":"晴天";
        private void WeatherIcon(Rect r,SkywellToken token,Color color)
        {
            if(weatherIcons==null)
            {
                weatherIcons=new Texture2D[3];
                for(int i=0;i<3;i++)
                {
                    var tex=new Texture2D(128,128,TextureFormat.RGBA32,false);tex.name="Weather HUD icon "+i;tex.hideFlags=HideFlags.HideAndDontSave;
                    tex.SetPixels32(new Color32[128*128]);DrawWeatherSymbol(tex,(SkywellToken)(i+1));tex.Apply(false,true);weatherIcons[i]=tex;
                }
            }
            var old=GUI.color;GUI.color=color;GUI.DrawTexture(r,weatherIcons[(int)token-1],ScaleMode.ScaleToFit);GUI.color=old;
        }
        private void OnDestroy(){if(storyPanelTexture)Destroy(storyPanelTexture);if(storyTriangleTexture)Destroy(storyTriangleTexture);if(observationImageMaterial)Destroy(observationImageMaterial);if(weatherIcons!=null)foreach(var icon in weatherIcons)if(icon)Destroy(icon);}
        private static void DrawWeatherSymbol(Texture2D r,SkywellToken token)
        {
            if(token==SkywellToken.Sun)
            {
                Arc(r,new Vector2(.5f,.5f),.22f,0,360);
                for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));IconLine(r,Vector2.one*.5f+d*.32f,Vector2.one*.5f+d*.45f);}
            }
            else if(token==SkywellToken.Rain)
            {
                Arc(r,new Vector2(.25f,.4f),.16f,90,270);Arc(r,new Vector2(.48f,.3f),.22f,180,350);Arc(r,new Vector2(.72f,.41f),.15f,240,450);IconLine(r,new Vector2(.22f,.56f),new Vector2(.73f,.56f));
                for(int i=0;i<3;i++)IconLine(r,new Vector2(.3f+i*.2f,.7f),new Vector2(.24f+i*.2f,.88f));
            }
            else
            {
                IconLine(r,new Vector2(.08f,.3f),new Vector2(.63f,.3f));Arc(r,new Vector2(.63f,.2f),.1f,90,-180);
                IconLine(r,new Vector2(.08f,.51f),new Vector2(.82f,.51f));Arc(r,new Vector2(.82f,.4f),.11f,90,-180);
                IconLine(r,new Vector2(.2f,.72f),new Vector2(.6f,.72f));Arc(r,new Vector2(.6f,.83f),.11f,-90,180);
            }
        }
        private static void Arc(Texture2D r,Vector2 center,float radius,float from,float to)
        {
            Vector2 previous=center+new Vector2(Mathf.Cos(from*Mathf.Deg2Rad),Mathf.Sin(from*Mathf.Deg2Rad))*radius;
            for(int i=1;i<=24;i++){float a=Mathf.Lerp(from,to,i/24f)*Mathf.Deg2Rad;Vector2 p=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;IconLine(r,previous,p);previous=p;}
        }
        private static void IconLine(Texture2D r,Vector2 a,Vector2 b)
        {
            a=new Vector2(a.x,1-a.y)*127;b=new Vector2(b.x,1-b.y)*127;
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*2));
            for(int n=0;n<=steps;n++)
            {
                var p=Vector2.Lerp(a,b,n/(float)steps);
                for(int dx=-2;dx<=2;dx++)for(int dy=-2;dy<=2;dy++)
                {int x=Mathf.RoundToInt(p.x)+dx,y=Mathf.RoundToInt(p.y)+dy;if(x>=0&&x<128&&y>=0&&y<128&&Vector2.Distance(new Vector2(x,y),p)<1.8f)r.SetPixel(x,y,Color.white);}
            }
        }
        private void DrawFlowerMarket(SkywellFlowerMarket market,float w,float h)
        {
            var run=market.Run;
            float width=Mathf.Min(470,w-36),x=(w-width)/2,y=(h-327)/2;
            Panel(new Rect(x,y,width,327));
            x+=18;width-=36;
            GUI.Label(new Rect(x,y+16,width,32),$"花庭小铺 · 第 {run.Day} 天",title);
            GUI.Label(new Rect(x,y+53,width,30),
                $"菌菇 {run.GetStock(FlowerIngredient.Mushroom)}   雨露果 {run.GetStock(FlowerIngredient.WeatherFruit)}   钱箱 {run.Cash}",body);
            DrawMarketRecipe(market,FlowerRecipe.MushroomBento,"1 · 菌菇便当",new Rect(x,y+86,width,36));
            DrawMarketRecipe(market,FlowerRecipe.FruitBento,"2 · 雨果菌菇盒",new Rect(x,y+128,width,36));
            string stage=run.OrderStage switch
            {
                FlowerOrderStage.Cooking=>$"烹饪中 · {run.CookTimeRemaining:0.0} 秒",
                FlowerOrderStage.Ready=>"已出餐 · E 交餐收款",
                FlowerOrderStage.Served=>"已收款 · 可继续做菜或进入次日",
                _=>"从上面的菜单选一道菜"
            };
            GUI.Label(new Rect(x,y+172,width,30),stage,body);
            GUI.Label(new Rect(x,y+204,width,33),market.Feedback??"先关店面板，再出门采集。",body);

            float gap=6,buttonWidth=(width-2*gap)/3;
            if(GUI.Button(new Rect(x,y+248,buttonWidth,34),"E 收款",button))market.Serve();
            if(GUI.Button(new Rect(x+buttonWidth+gap,y+248,buttonWidth,34),"N 次日",button))market.NextDay();
            if(GUI.Button(new Rect(x+2*(buttonWidth+gap),y+248,buttonWidth,34),"返回",button))market.CloseShop();
            GUI.Label(new Rect(x,y+291,width,27),"Q / Esc 回庭院 · R 重开关卡",small);
        }

        private void DrawMarketRecipe(SkywellFlowerMarket market,FlowerRecipe recipe,string label,Rect rect)
        {
            if(!FlowerMarketRun.TryGetRecipe(recipe,out var info))return;
            string fruit=info.WeatherFruit>0?$" + 雨露果 {info.WeatherFruit}":"";
            if(GUI.Button(rect,$"{label}   菌菇 {info.Mushrooms}{fruit}   {info.CookSeconds:0.#} 秒 · {info.Price} 元",button))
                market.Cook(recipe);
        }

        private static void Panel(Rect r){GUI.color=new Color(.025f,.08f,.09f,.96f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
    }
}

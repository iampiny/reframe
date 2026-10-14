using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

// Full Home remains a web app; this entry owns only native partial-window routing.
class FrameOverlayApp : NUIApplication
{
    const string Own = "org.framehome.overlayprobe", Home = "Fr4meHome1.Launcher";
    const string DefaultOperation = "http://tizen.org/appcontrol/operation/default";
    static readonly string[] DefaultIds = { "org.tizen.netflix-app", "9Ur5IzDKqV.TizenYouTube", "kIciSQlYEM.plex", "w0wZu3xkQq.GeForceNOW", "org.tizen.art-app", "com.samsung.tv.store", "org.tizen.menu", "com.samsung.tv.csfs" };
    readonly string run = Guid.NewGuid().ToString("N");
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly List<object> events = new List<object>();
    class Tile
    {
        public string Id, Name, Icon, StoreId;
        public View View;
        public ImageView Surface, Image;
        public TextLabel Label;
    }
    List<Tile> apps = new List<Tile>();
    List<Tile> installedApps = new List<Tile>();
    List<Tile> tiles = new List<Tile>();
    View panel, navigationViewport, navigationList, brightnessPanel, brightnessSheet, brightnessFill, brightnessThumb;
    ImageView brightnessIcon, controlsScrim;
    View brightnessAutoButton;
    ImageView brightnessAutoIcon, brightnessSurface, brightnessFocusSurface, brightnessAutoFocusSurface;
    Animation brightnessFocusMotion;
    float brightnessSurfaceScale=1, brightnessAutoScale=1;
    int automaticBrightness=-1;
    bool brightnessAutoFocused;
    Animation brightnessSlide;
    int brightnessValue=-1;
    bool brightnessFocused, brightnessBusy, pendingAutomaticToggle;
    Task catalogTask;
    int readRequests, pendingBrightnessSteps;
    bool channelPump;
    TaskCompletionSource<bool> readsDrained;
    FrameFeeds feeds;
    Animation slide, listSlide, cardMotion;
    JsonElement uiLayout;
    int Layout(string key) => uiLayout.GetProperty(key).GetInt32();
    JsonElement Brightness(string key) => uiLayout.GetProperty("brightness").GetProperty(key);
    ImageView BrightnessImage(string key,string asset)
    {
        var layer=Brightness(key);
        return new ImageView {ResourceUrl=Resource(asset),Color=new Color(1,1,1,layer.GetProperty("opacity").GetSingle()),Position2D=new Position2D(layer.GetProperty("x").GetInt32(),layer.GetProperty("y").GetInt32()),
            Size2D=new Size2D(layer.GetProperty("width").GetInt32(),layer.GetProperty("height").GetInt32())};
    }
    bool enterDown;
    string Resource(string name) => System.IO.Path.Combine(Application.Current.DirectoryInfo.SharedResource,name);
    string OrderPath => System.IO.Path.Combine(Application.Current.DirectoryInfo.Data,"pinned-apps.json");
    TextLabel status, time;
    Tizen.NUI.Timer release, idle, clockTick;
    Action pending;
    bool visible, launching, grabbed;
    int selected;
    string boot;

    [DllImport("libc.so.6")] static extern uint getuid();
    [DllImport("libc.so.6")] static extern uint geteuid();
    [DllImport("libc.so.6")] static extern void free(IntPtr p);
    [DllImport("libvconf.so.0")] static extern IntPtr vconf_get_str(string key);
    [DllImport("libdeviced.so.1")] static extern int device_power_get_ambient_mode();
    [DllImport("libdeviced.so.1")] static extern int device_power_get_state();
    [DllImport("libcapi-appfw-app-context-manager.so")] static extern int app_context_manager_request_task(string target, string action);

    // Read-only Samsung app metadata supplies store IDs and original launcher icons.
    [DllImport("libsqlite3.so.0")] static extern int sqlite3_open_v2(string path,out IntPtr db,int flags,IntPtr vfs);
    [DllImport("libsqlite3.so.0")] static extern int sqlite3_prepare_v2(IntPtr db,string sql,int bytes,out IntPtr statement,IntPtr tail);
    [DllImport("libsqlite3.so.0")] static extern int sqlite3_step(IntPtr statement);
    [DllImport("libsqlite3.so.0")] static extern IntPtr sqlite3_column_text(IntPtr statement,int column);
    [DllImport("libsqlite3.so.0")] static extern int sqlite3_finalize(IntPtr statement);
    [DllImport("libsqlite3.so.0")] static extern int sqlite3_close(IntPtr db);
    Dictionary<string,Tile> ReadStoreApps()
    {
        var result=new Dictionary<string,Tile>();IntPtr db=IntPtr.Zero,statement=IntPtr.Zero;
        try {
            if(sqlite3_open_v2("/opt/usr/apps/webappservice/wasdb_backup/.webappservice.db",out db,1,IntPtr.Zero)!=0 ||
               sqlite3_prepare_v2(db,"SELECT tizen_id,id,icon_path FROM widgetinfo WHERE tizen_id <> ''",-1,out statement,IntPtr.Zero)!=0)
                throw new IOException("Samsung app metadata unavailable");
            int state;
            while((state=sqlite3_step(statement))==100) {
                string id=Marshal.PtrToStringAnsi(sqlite3_column_text(statement,0)),store=Marshal.PtrToStringAnsi(sqlite3_column_text(statement,1)),directory=Marshal.PtrToStringAnsi(sqlite3_column_text(statement,2));
                if(string.IsNullOrEmpty(id))continue;
                string icon=string.IsNullOrEmpty(directory)?null:System.IO.Path.Combine(directory,"304x304.png");
                result[id]=new Tile {Id=id,StoreId=store,Icon=icon!=null && File.Exists(icon)?icon:null};
            }
            if(state!=101)throw new IOException("Samsung app metadata busy");
            Note("store-metadata",new {count=result.Count});
        } catch(Exception error) {Note("store-metadata-error",error.GetType().Name);}
        finally {if(statement!=IntPtr.Zero)sqlite3_finalize(statement);if(db!=IntPtr.Zero)sqlite3_close(db);}
        return result;
    }

    public FrameOverlayApp() : base("Frame Dock", WindowMode.Transparent, new Size2D(1920,1080), new Position2D(0,0)) { }
    static void Main(string[] args) { new FrameOverlayApp().Run(args); }
    static string Read(string key) { var p=vconf_get_str(key); if(p==IntPtr.Zero)return null; try{return Marshal.PtrToStringAnsi(p);}finally{free(p);} }
    object State() => new {full=Read("memory/appfw/current_fullscreen_appid"),partial=Read("memory/appfw/current_partial_appid"),ambient=device_power_get_ambient_mode()};
    void Note(string kind, object value=null)
    {
        if(events.Count==100)events.RemoveAt(0);
        var entry=new {kind,value,elapsedMs=clock.ElapsedMilliseconds};
        events.Add(entry);
        try {
            string path=System.IO.Path.Combine(Application.Current.DirectoryInfo.Data,"shell-state.json");
            File.AppendAllText(System.IO.Path.Combine(Application.Current.DirectoryInfo.Data,"shell-events.jsonl"),JsonSerializer.Serialize(new {run,boot,entry})+"\n");
            File.WriteAllText(path+".tmp",JsonSerializer.Serialize(new {run,boot,uptime=File.ReadAllText("/proc/uptime").Split(' ')[0],utc=DateTime.UtcNow.ToString("o"),events}));
            File.Move(path+".tmp",path,true);
        } catch { /* A diagnostic write must never block the remote or routing. */ }
    }

    protected override void OnCreate()
    {
        base.OnCreate();boot=File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();
        string label=File.ReadAllText("/proc/self/attr/current").Trim();
        if(getuid()!=5001 || geteuid()!=5001 || label!="User::Pkg::"+Own) { Exit();return; }
        try {
            Note("font-directory",new {loaded=FontClient.Instance.AddCustomFontDirectory(Resource("fonts"))});
            using(var document=JsonDocument.Parse(File.ReadAllText(Resource("ui-layout.json"))))uiLayout=document.RootElement.Clone();
            Window.Instance.BackgroundColor=Color.Transparent;Window.Instance.SetAcceptFocus(false);
            panel=new View {Position2D=new Position2D(0,0),Size2D=new Size2D(Layout("sidebarWidth"),Layout("viewportHeight")),BackgroundColor=Color.Transparent,Focusable=true};
            Window.Instance.Add(panel);
            panel.Add(new ImageView {ResourceUrl=Resource("sidebar-gradient.png"),Size2D=new Size2D(Layout("sidebarWidth"),Layout("viewportHeight"))});
            navigationViewport=new View {Position2D=new Position2D(Layout("viewportLeft"),0),Size2D=new Size2D(Layout("viewportWidth"),Layout("viewportHeight")),ClippingMode=ClippingModeType.ClipToBoundingBox};panel.Add(navigationViewport);
            navigationList=new View {Size2D=new Size2D(Layout("viewportWidth"),Layout("viewportHeight"))};navigationViewport.Add(navigationList);
            for(int i=0;i<4;i++)apps.Add(new Tile {Id=DefaultIds[i],Name=new[]{"Netflix","YouTube","Plex","GeForce NOW"}[i]});
            installedApps=apps.ToList();SortApps(ReadOrder());
            controlsScrim=new ImageView {ResourceUrl=Resource("controls-scrim.svg"),Size2D=new Size2D(1920,1080)};Window.Instance.Add(controlsScrim);controlsScrim.Hide();
            var clockLayout=uiLayout.GetProperty("clock");
            time=new TextLabel {Position2D=new Position2D(clockLayout.GetProperty("x").GetInt32(),clockLayout.GetProperty("y").GetInt32()),Size2D=new Size2D(clockLayout.GetProperty("width").GetInt32(),clockLayout.GetProperty("height").GetInt32()),FontFamily="Inter",PixelSize=clockLayout.GetProperty("fontSize").GetInt32(),TextColor=Color.White,HorizontalAlignment=HorizontalAlignment.End};Window.Instance.Add(time);
            status=new TextLabel {Position2D=new Position2D(64,980),Size2D=new Size2D(350,60),PointSize=17,TextColor=Color.White};panel.Add(status);
            JsonElement control=Brightness("control"),auto=Brightness("auto"),autoIcon=Brightness("autoIcon");
            int controlWidth=control.GetProperty("width").GetInt32(),controlHeight=control.GetProperty("height").GetInt32();
            brightnessPanel=new View {Position2D=new Position2D(control.GetProperty("x").GetInt32(),control.GetProperty("y").GetInt32()),Size2D=new Size2D(controlWidth,controlHeight),Focusable=true};Window.Instance.Add(brightnessPanel);
            brightnessSheet=new View {Position2D=new Position2D(Brightness("sheetOffset").GetInt32(),0),Size2D=new Size2D(controlWidth,controlHeight)};brightnessPanel.Add(brightnessSheet);
            brightnessSurface=BrightnessImage("surface","brightness-surface.png");brightnessSurface.PivotPoint=PivotPoint.Center;brightnessSurface.PositionUsesPivotPoint=true;brightnessSurface.Position2D=new Position2D(Brightness("surface").GetProperty("x").GetInt32()+Brightness("surface").GetProperty("width").GetInt32()/2,controlHeight/2);brightnessSheet.Add(brightnessSurface);
            brightnessFocusSurface=BrightnessImage("focusSurface","brightness-focus.svg");brightnessFocusSurface.Color=Color.White;brightnessFocusSurface.PivotPoint=PivotPoint.Center;brightnessFocusSurface.PositionUsesPivotPoint=true;brightnessFocusSurface.Position2D=brightnessSurface.Position2D;brightnessSheet.Add(brightnessFocusSurface);
            brightnessSheet.Add(BrightnessImage("track","brightness-track.png"));
            brightnessFill=new ImageView {ResourceUrl=Resource("brightness-fill.png")};brightnessSheet.Add(brightnessFill);
            brightnessThumb=BrightnessImage("thumb","brightness-thumb.png");brightnessSheet.Add(brightnessThumb);
            brightnessSheet.Add(BrightnessImage("dimIcon","brightness-dim-icon.svg"));
            brightnessIcon=BrightnessImage("icon","brightness-icon.svg");brightnessPanel.Add(brightnessIcon);
            int autoWidth=auto.GetProperty("width").GetInt32(),autoHeight=auto.GetProperty("height").GetInt32();
            brightnessAutoButton=new View {PivotPoint=PivotPoint.Center,PositionUsesPivotPoint=true,Position2D=new Position2D(auto.GetProperty("x").GetInt32()+autoWidth/2,auto.GetProperty("y").GetInt32()+autoHeight/2),Size2D=new Size2D(autoWidth,autoHeight),CornerRadius=autoHeight/2};brightnessSheet.Add(brightnessAutoButton);
            brightnessAutoFocusSurface=BrightnessImage("autoFocusSurface","brightness-auto-focus.svg");brightnessAutoFocusSurface.Color=Color.White;brightnessAutoButton.Add(brightnessAutoFocusSurface);
            brightnessAutoIcon=BrightnessImage("autoIcon","brightness-auto.svg");brightnessAutoIcon.Position2D=new Position2D((autoWidth-autoIcon.GetProperty("width").GetInt32())/2,(autoHeight-autoIcon.GetProperty("height").GetInt32())/2);brightnessAutoButton.Add(brightnessAutoIcon);
            brightnessPanel.KeyEvent+=HandleKey;brightnessPanel.Hide();
            panel.KeyEvent+=HandleKey;panel.Hide();time.Hide();SetSelection(selected);
            clockTick=new Tizen.NUI.Timer(60000);clockTick.Tick+=(a,b)=>{time.Text=DateTime.Now.ToString("H:mm");return visible;};
            release=new Tizen.NUI.Timer(150);release.Tick+=(s,e)=>{var action=pending;pending=null;action?.Invoke();return false;};
            idle=new Tizen.NUI.Timer(90000);idle.Tick+=(s,e)=>{PauseDock();return false;};
            Note("created",new {uid=getuid(),euid=geteuid(),label,state=State()});
            _=RefreshApps();
        } catch(Exception error) {Note("startup-error",new {message=error.Message,stack=error.StackTrace});Exit();}
    }
    string[] ReadOrder()
    {
        try {return JsonSerializer.Deserialize<string[]>(File.ReadAllText(OrderPath))??Array.Empty<string>();}
        catch {return DefaultIds.Take(4).ToArray();}
    }
    void SaveOrder(string[] order)
    {
        File.WriteAllText(OrderPath+".tmp",JsonSerializer.Serialize(order));
        File.Move(OrderPath+".tmp",OrderPath,true);
    }
    void SortApps(string[] order)
    {
        string id=tiles.Count>selected?tiles[selected].Id:null;
        var next=order.Distinct().Select(id=>installedApps.Find(x=>x.Id==id)).Where(x=>x!=null).ToList();
        if(tiles.Count>0 && next.SequenceEqual(apps))return;
        apps=next;
        BuildCards();SetSelection(Math.Max(0,tiles.FindIndex(x=>x.Id==id)));
    }
    Task RefreshApps()
    {
        if(catalogTask==null || catalogTask.IsCompleted)catalogTask=LoadApps();
        return catalogTask;
    }
    async Task LoadApps()
    {
        try {
            var installed=await ApplicationManager.GetInstalledApplicationsAsync();
            if(enterDown){foreach(var app in installed)app.Dispose();return;}
            var result=new List<Tile>();var storeApps=ReadStoreApps();
            foreach(var app in installed)using(app) {
                string id=app.ApplicationId;
                int known=Array.IndexOf(DefaultIds,id);storeApps.TryGetValue(id,out var storeApp);
                // Match Home: store-widget IDs plus the explicitly supported native apps.
                bool widget=id.Length>11 && id[10]=='.' && id.Take(10).All(char.IsLetterOrDigit);
                if(id==Home || id==Own || id==Own+".startup" || known>=4 || (known<0 && (app.IsNoDisplay || (!widget && storeApp==null))))continue;
                result.Add(new Tile {Id=id,Name=app.Label,StoreId=storeApp?.StoreId,Icon=storeApp?.Icon ?? app.IconPath});
            }
            result.Sort((a,b)=>string.CompareOrdinal(a.Id,b.Id));
            if(installedApps.Count!=result.Count || !installedApps.Zip(result,(a,b)=>a.Id==b.Id && a.Name==b.Name && a.Icon==b.Icon && a.StoreId==b.StoreId).All(equal=>equal))
                installedApps=result;
            SortApps(ReadOrder());
        } catch(Exception error) {Note("catalog-error",error.GetType().Name);}
    }
    void BuildCards()
    {
        cardMotion?.Stop();cardMotion?.Dispose();cardMotion=null;
        foreach(var tile in tiles)if(tile.View!=null){navigationList.Remove(tile.View);tile.View.Dispose();}
        tiles=apps.Concat(new[]{
            new Tile {Id="org.tizen.art-app",Name="Artwork",Icon=Resource("control-artwork.svg")},
            new Tile {Id=DefaultIds[5],Name="Apps",Icon=Resource("control-store.svg")},
            new Tile {Id=DefaultIds[6],Name="Settings",Icon=Resource("control-settings.svg")},
            new Tile {Id=DefaultIds[7],Name="Samsung UI",Icon=Resource("control-samsung.svg")}
        }).ToList();
        foreach(var tile in tiles) {
            tile.View=new View {BackgroundColor=Color.Transparent,PivotPoint=PivotPoint.TopLeft,PositionUsesPivotPoint=true};navigationList.Add(tile.View);
            tile.Surface=new ImageView {ResourceUrl=Resource(apps.Contains(tile)?"tile-surface.png":"action-surface.png")};tile.View.Add(tile.Surface);
            if(!string.IsNullOrEmpty(tile.Icon)) {
                tile.Image=new ImageView {ResourceUrl=tile.Icon};
                if(apps.Contains(tile)) {tile.Image.FittingMode=FittingModeType.ScaleToFill;tile.Image.AlphaMaskURL=Resource("tile-mask.png");tile.Image.CropToMask=true;
                    // Scale the source to the shared4:3 mask before DALi crops it.
                    // Masking a304px square first would cut off the rounded corners.
                    var maskSize=ImageLoading.GetOriginalImageSize(Resource("tile-mask.png"));
                    var sourceSize=ImageLoading.GetOriginalImageSize(tile.Icon);
                    if(sourceSize.Width>0 && sourceSize.Height>0)using(var map=tile.Image.Image) {
                        map.Insert(ImageVisualProperty.MaskContentScale,new PropertyValue(Math.Max(maskSize.Width/(float)sourceSize.Width,maskSize.Height/(float)sourceSize.Height)));
                        tile.Image.Image=map;
                    }}
                tile.View.Add(tile.Image);
            }
            bool app=apps.Contains(tile);
            int w=app?Layout("tileWidth"):Layout("controlWidth"),h=app?Layout("tileHeight"):Layout("controlHeight");
            tile.View.Size2D=new Size2D(w,h);tile.Surface.Size2D=new Size2D(w,h);
            int padding=Layout("controlPadding"),size=Layout("iconSize"),labelX=padding+size+Layout("labelGap");
            if(tile.Image!=null) {tile.Image.Size2D=app?new Size2D(w,h):new Size2D(size,size);tile.Image.Position2D=app?new Position2D(0,0):new Position2D(padding,(h-size)/2);}
            tile.Label=new TextLabel {Text=tile.Name,TextColor=Color.White,FontFamily="Inter",PixelSize=app?24:Layout("labelSize"),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=app?HorizontalAlignment.Center:HorizontalAlignment.Begin,Ellipsis=false,
                Position2D=new Position2D(app?12:labelX,(h-42)/2),Size2D=new Size2D(app?w-24:w-labelX-padding,42)};tile.View.Add(tile.Label);
            if(app && tile.Image!=null)tile.Label.Hide();
        }
    }
    void SetSelection(int index)
    {
        selected=Math.Max(0,Math.Min(tiles.Count-1,index));
        // Shared layout supplies dimensions; selected slots reserve their enlarged height.
        int inset=Layout("verticalInset"), left=Layout("horizontalInset"), viewportHeight=Layout("viewportHeight");
        int tileWidth=Layout("tileWidth"),tileHeight=Layout("tileHeight"),tileGap=Layout("tileGap");
        bool selectedApp=selected<apps.Count && !brightnessFocused;
        float appScale=uiLayout.GetProperty("appFocusScale").GetSingle(),actionScale=uiLayout.GetProperty("actionFocusScale").GetSingle();
        int focusedHeight=(int)Math.Round(tileHeight*appScale);
        int primaryHeight=apps.Count*tileHeight+Math.Max(0,apps.Count-1)*tileGap+(selectedApp?focusedHeight-tileHeight:0);
        int controlsTop=inset+primaryHeight+(apps.Count>0?Layout("controlsGap"):0);
        int controlHeight=Layout("controlHeight"),controlGap=Layout("controlGap");
        int contentHeight=controlsTop+4*controlHeight+3*controlGap+inset;
        int centre=selected<apps.Count?inset+selected*(tileHeight+tileGap)+(selectedApp?focusedHeight:tileHeight)/2:
            controlsTop+(selected-apps.Count)*(controlHeight+controlGap)+controlHeight/2;
        int offset=Math.Max(0,Math.Min(Math.Max(0,contentHeight-viewportHeight),centre-viewportHeight/2));
        navigationList.Size2D=new Size2D(Layout("viewportWidth"),contentHeight);
        float from=navigationList.PositionY;listSlide?.Stop();listSlide?.Dispose();listSlide=null;
        if(visible && Math.Abs(from+offset)>.5f) {
            listSlide=new Animation(260);
            using(var start=new Vector2(.2f,.85f))using(var end=new Vector2(.2f,1))using(var ease=new AlphaFunction(start,end))
                listSlide.AnimateTo(navigationList,nameof(View.PositionY),-(float)offset,ease);
            listSlide.Play();
        } else navigationList.PositionY=-offset;
        cardMotion?.Stop();cardMotion?.Dispose();cardMotion=null;
        if(visible)cardMotion=new Animation(250);
        using(var start=new Vector2(.2f,1.35f))using(var end=new Vector2(.4f,1))using(var ease=new AlphaFunction(start,end)) {
            // Keep decoded images, masks and text at rest size; the compositor
            // scales the whole tile. Resizing children every frame re-rasterized them.
            Action<View,int,int,float> place=(view,x,y,scale)=>{
                if(cardMotion!=null) {
                    cardMotion.AnimateTo(view,nameof(View.PositionX),(float)x,ease);
                    cardMotion.AnimateTo(view,nameof(View.PositionY),(float)y,ease);
                    cardMotion.AnimateTo(view,nameof(View.ScaleX),scale,ease);
                    cardMotion.AnimateTo(view,nameof(View.ScaleY),scale,ease);
                } else {view.Position2D=new Position2D(x,y);view.ScaleX=scale;view.ScaleY=scale;}
            };
            int top=inset;
            for(int i=0;i<tiles.Count;i++) {
                var tile=tiles[i];bool app=i<apps.Count,active=i==selected && !brightnessFocused;
                int w=app?(active?(int)Math.Round(tileWidth*appScale):tileWidth):(active?tileWidth:Layout("controlWidth"));
                int h=app?(active?focusedHeight:tileHeight):(active?(int)Math.Round(controlHeight*actionScale):controlHeight);
                int x=left+(app?0:Layout("controlInset")-(w-Layout("controlWidth"))/2);
                int y=app?top:controlsTop+(i-apps.Count)*(controlHeight+controlGap)-(h-controlHeight)/2;
                place(tile.View,x,y,active?(app?appScale:actionScale):1f);
                if(app) {
                    tile.Surface.Color=tile.Id==DefaultIds[1]?Color.White:tile.Id==DefaultIds[2]?new Color(.125f,.14f,.17f,1):tile.Id==DefaultIds[3]?new Color(.46f,.73f,0,1):new Color(.063f,.067f,.078f,1);
                    top+=h+tileGap;
                } else {
                    if(active)tile.Surface.Show();else tile.Surface.Hide();
                    if(tile.Image!=null)tile.Image.Color=new Color(1,1,1,active?1:.6f);
                }
            }
            cardMotion?.Play();
        }
    }

    void RenderBrightness()
    {
        float level=Math.Max(0,brightnessValue)/50f;
        JsonElement track=Brightness("track"),thumb=Brightness("thumb");
        int x=track.GetProperty("x").GetInt32(),y=track.GetProperty("y").GetInt32(),height=track.GetProperty("height").GetInt32();
        int top=(int)(y+height*(1-level));
        brightnessFill.Position2D=new Position2D(x,top);brightnessFill.Size2D=new Size2D(track.GetProperty("width").GetInt32(),Math.Max(1,(int)(height*level)));
        if(brightnessValue<=0)brightnessFill.Hide();else brightnessFill.Show();
        brightnessThumb.Position2D=new Position2D(thumb.GetProperty("x").GetInt32(),top-thumb.GetProperty("height").GetInt32()/2);
        brightnessIcon.Color=new Color(1,1,1,brightnessValue<0?.5f:1);
        string autoGlyph=Resource(automaticBrightness==1?"brightness-auto-on.svg":"brightness-auto.svg");
        if(brightnessAutoIcon.ResourceUrl!=autoGlyph)brightnessAutoIcon.ResourceUrl=autoGlyph;
        brightnessAutoIcon.Color=new Color(1,1,1,automaticBrightness<0?.4f:brightnessAutoFocused || automaticBrightness==1?1:.7f);
        brightnessSurface.Color=new Color(1,1,1,Brightness("inactiveOpacity").GetSingle());
        if(brightnessFocused && !brightnessAutoFocused)brightnessFocusSurface.Show();else brightnessFocusSurface.Hide();
        if(brightnessFocused && brightnessAutoFocused)brightnessAutoFocusSurface.Show();else brightnessAutoFocusSurface.Hide();
        float idleScale=Brightness("idleScale").GetSingle();
        float surfaceScale=brightnessFocused && !brightnessAutoFocused?1:idleScale,autoScale=brightnessFocused && brightnessAutoFocused?1:idleScale;
        if(surfaceScale!=brightnessSurfaceScale || autoScale!=brightnessAutoScale) {
            brightnessSurfaceScale=surfaceScale;brightnessAutoScale=autoScale;
            brightnessFocusMotion?.Stop();brightnessFocusMotion?.Dispose();brightnessFocusMotion=new Animation(200);
            using(var start=new Vector2(.2f,.8f))using(var end=new Vector2(.2f,1))using(var ease=new AlphaFunction(start,end)) {
                foreach(var view in new[]{brightnessSurface,brightnessFocusSurface}) {
                    brightnessFocusMotion.AnimateTo(view,nameof(View.ScaleX),surfaceScale,ease);
                    brightnessFocusMotion.AnimateTo(view,nameof(View.ScaleY),surfaceScale,ease);
                }
                brightnessFocusMotion.AnimateTo(brightnessAutoButton,nameof(View.ScaleX),autoScale,ease);
                brightnessFocusMotion.AnimateTo(brightnessAutoButton,nameof(View.ScaleY),autoScale,ease);
            }
            brightnessFocusMotion.Play();
        }
        var background=Brightness("auto").GetProperty("color");
        brightnessAutoButton.BackgroundColor=new Color(background[0].GetSingle(),background[1].GetSingle(),background[2].GetSingle(),background[3].GetSingle()*Brightness("inactiveOpacity").GetSingle());
    }
    void StopBrightnessSlide() {brightnessSlide?.Stop();brightnessSlide?.Dispose();brightnessSlide=null;}
    void FocusBrightness(bool focused)
    {
        bool changed=brightnessFocused!=focused;
        brightnessFocused=focused;brightnessAutoFocused=false;
        if(focused)brightnessAutoButton.Show();else brightnessAutoButton.Hide();
        RenderBrightness();SetSelection(selected);
        if(changed) {
            StopBrightnessSlide();brightnessSlide=new Animation(250);
            using(var start=new Vector2(.2f,1.12f))using(var end=new Vector2(.35f,1))using(var ease=new AlphaFunction(start,end)) {
                brightnessSlide.AnimateTo(brightnessSheet,nameof(View.PositionX),focused?0f:Brightness("sheetOffset").GetSingle(),ease);
                brightnessSlide.AnimateTo(brightnessIcon,nameof(View.PositionY),focused?Brightness("iconOpenY").GetSingle():Brightness("icon").GetProperty("y").GetSingle(),ease);
            }
            brightnessSlide.Play();
        }
        if(focused)brightnessPanel.KeyInputFocus=true;else panel.KeyInputFocus=true;
    }
    async Task<int> UpdateBrightness(int? target=null,int? expected=null,int? automaticTarget=null)
    {
        if(brightnessBusy)throw new InvalidOperationException("Brightness busy");
        brightnessBusy=true;
        // Paint the requested position before the worker writes and verifies the backlight.
        if(target.HasValue && target>=0 && target<=50) {brightnessValue=target.Value;if(automaticBrightness>=0)automaticBrightness=0;}
        if(automaticTarget.HasValue)automaticBrightness=automaticTarget.Value;
        RenderBrightness();
        try {
            var result=await Task.Run(()=>FrameBrightness.Update(target,expected,automaticTarget));
            brightnessValue=result.value;automaticBrightness=result.automatic;RenderBrightness();return brightnessValue;
        } catch {brightnessValue=-1;automaticBrightness=-1;RenderBrightness();throw;}
        finally {
            brightnessBusy=false;
            if(pendingAutomaticToggle){pendingAutomaticToggle=false;ToggleAutomaticBrightness();}
        }
    }
    async void RefreshBrightness()
    {
        try {await UpdateBrightness();}catch {if(visible)status.Text=device_power_get_ambient_mode()!=0?"Art Mode controls its own brightness.":"Brightness unavailable.";}
    }
    async void AdjustBrightness(int direction)
    {
        if(brightnessBusy || brightnessValue<0)return;
        // Match the web slider’s exact 10% stops across the hardware’s 0–50 range.
        int stop=(int)Math.Round(brightnessValue/5.0,MidpointRounding.AwayFromZero)+direction;
        int target=Math.Max(0,Math.Min(50,stop*5));
        try {await UpdateBrightness(target,brightnessValue);}
        catch {if(visible)status.Text="Brightness unavailable; reopen the bar to retry.";}
    }
    async void ToggleAutomaticBrightness()
    {
        if(brightnessBusy){pendingAutomaticToggle=!pendingAutomaticToggle;return;}
        if(automaticBrightness<0){if(visible)status.Text=device_power_get_ambient_mode()!=0?"Art Mode controls its own brightness.":"Automatic brightness unavailable.";return;}
        try {await UpdateBrightness(automaticTarget:automaticBrightness==1?0:1);}
        catch {if(visible)status.Text="Automatic brightness unavailable.";}
    }
    // Manager-routed channel keys never show or focus a window over playback.
    async void BackgroundBrightnessKey(string direction)
    {
        if(direction!="up" && direction!="down")return;
        if(visible){AdjustBrightness(direction=="up"?1:-1);return;}
        pendingBrightnessSteps=Math.Max(-10,Math.Min(10,pendingBrightnessSteps+(direction=="up"?1:-1)));
        if(channelPump)return;
        channelPump=true;
        try {
            while(pendingBrightnessSteps!=0) {
                string full=Read("memory/appfw/current_fullscreen_appid"),partial=Read("memory/appfw/current_partial_appid");
                if(visible || device_power_get_ambient_mode()!=0 || !DefaultIds.Take(4).Contains(full) || (partial!=null && partial!="" && partial!=Own))break;
                while(brightnessBusy)await Task.Delay(25);
                int directionStep=Math.Sign(pendingBrightnessSteps);pendingBrightnessSteps-=directionStep;
                int current=await UpdateBrightness();
                int stop=(int)Math.Round(current/5.0,MidpointRounding.AwayFromZero)+directionStep;
                await UpdateBrightness(Math.Max(0,Math.Min(50,stop*5)),current);
                Note("background-brightness",new {direction=directionStep>0?"up":"down",value=brightnessValue,full});
            }
        } catch(Exception error) {Note("background-brightness-error",error.GetType().Name);}
        finally {pendingBrightnessSteps=0;channelPump=false;PauseBackground();}
    }
    void PauseBackground()
    {
        if(!visible && readRequests==0 && !channelPump && Read("memory/appfw/current_partial_appid")==Own)
            app_context_manager_request_task("package:"+Own,"pause");
    }
    async Task ReadRequest(ReceivedAppControl control,string action)
    {
        if(readRequests++==0)readsDrained=new TaskCompletionSource<bool>();
        try {
            if(action=="get-feed" || action=="get-feed-image")await FeedRequest(control,action);
            else if(action!="get-order")await BrightnessRequest(control,action);
            else {
                try {
                    await RefreshApps();
                    if(control.IsReplyRequest) {var reply=new AppControl();reply.ExtraData.Add("order",JsonSerializer.Serialize(ReadOrder()));reply.ExtraData.Add("catalog",JsonSerializer.Serialize(installedApps.Select(app=>new {id=app.Id,name=app.Name,iconPath=app.Icon,show=true})));control.ReplyToLaunchRequest(reply,AppControlReplyResult.Succeeded);}
                } catch(Exception error) {Note("order-error",error.GetType().Name);if(control.IsReplyRequest)control.ReplyToLaunchRequest(new AppControl(),AppControlReplyResult.Failed);}
            }
        } finally {
            if(--readRequests==0){PauseBackground();readsDrained.TrySetResult(true);}
        }
    }
    async Task BrightnessRequest(ReceivedAppControl control,string action)
    {
        if(device_power_get_ambient_mode()!=0) {
            if(control.IsReplyRequest) {
                var reply=new AppControl();reply.ExtraData.Add("error","Art Mode controls its own brightness.");
                control.ReplyToLaunchRequest(reply,AppControlReplyResult.Succeeded);
            }
            return;
        }
        try {
            int? target=null,expected=null;
            if(action=="set-brightness") {
                int value,previous;
                if(!int.TryParse(Extra(control,"value"),out value) || !int.TryParse(Extra(control,"expected"),out previous))throw new ArgumentException("Invalid brightness");
                target=value;expected=previous;
            }
            int? automaticTarget=null;
            if(action=="set-auto-brightness") {
                int automatic;
                if(!int.TryParse(Extra(control,"automatic"),out automatic) || (automatic!=0 && automatic!=1))throw new ArgumentException("Invalid automatic brightness");
                automaticTarget=automatic;
            }
            int result=await UpdateBrightness(target,expected,automaticTarget);
            if(control.IsReplyRequest) {var reply=new AppControl();reply.ExtraData.Add("brightness",result.ToString(System.Globalization.CultureInfo.InvariantCulture));reply.ExtraData.Add("automatic",automaticBrightness.ToString());control.ReplyToLaunchRequest(reply,AppControlReplyResult.Succeeded);}
        } catch {if(control.IsReplyRequest)control.ReplyToLaunchRequest(new AppControl(),AppControlReplyResult.Failed);}
    }

    async Task FeedRequest(ReceivedAppControl control,string action)
    {
        if(!control.IsReplyRequest)return;
        try {
            if(feeds==null)feeds=new FrameFeeds();
            var reply=new AppControl();
            if(action=="get-feed") {
                var model=await feeds.Get(Extra(control,"app"));
                reply.ExtraData.Add("feed",JsonSerializer.Serialize(model));
                Note("feed-read",new {provider=model.appId,status=model.status,rows=model.sections.Count,cards=model.sections.Sum(row=>row.cards.Count)});
            } else if(action=="get-feed-image") {
                string image=await feeds.Image(Extra(control,"item"));
                reply.ExtraData.Add("image",image);
                Note("feed-image",new {bytes=image.Length});
            }
            control.ReplyToLaunchRequest(reply,AppControlReplyResult.Succeeded);
        } catch(Exception error) {
            Note("feed-error",new {action,type=error.GetType().Name});
            control.ReplyToLaunchRequest(new AppControl(),AppControlReplyResult.Failed);
        }
        // ReadRequest owns the shared read count and pauses only after the last reply.
    }

    async Task OpenFeedItem(ReceivedAppControl control)
    {
        if(launching) {if(control.IsReplyRequest)control.ReplyToLaunchRequest(new AppControl(),AppControlReplyResult.Failed);return;}
        launching=true;
        try {
            if(feeds==null)throw new InvalidOperationException("Preview unavailable; select the app again.");
            var request=await feeds.PrepareLaunch(Extra(control,"item"),Own);
            if(readRequests>0)await readsDrained.Task;
            PauseDock();
            var result=await AppControl.SendLaunchRequestAsync(request,null);
            Note("feed-launch",new {provider=request.ApplicationId,result=result.ToString()});
            if(result!=AppControlResult.None)throw new InvalidOperationException("The TV refused this title launch.");
            if(control.IsReplyRequest)control.ReplyToLaunchRequest(new AppControl(),AppControlReplyResult.Succeeded);
        } catch(Exception error) {
            Note("feed-launch-refused",new {type=error.GetType().Name});
            if(control.IsReplyRequest) {
                var reply=new AppControl();reply.ExtraData.Add("error",error is InvalidOperationException?error.Message:"Title launch unavailable; open the app from its tile.");
                control.ReplyToLaunchRequest(reply,AppControlReplyResult.Succeeded);
            }
            PauseBackground();
        } finally {launching=false;}
    }

    string Extra(AppControl control,string key) { string value; if(control.ExtraData.TryGet(key,out value))return value; IEnumerable<string> values; if(control.ExtraData.TryGet(key,out values))foreach(var item in values)return item;return null; }

    // Bare fullscreen toggles existing Art, so require normal power and Home beneath us.
    async void EnterIdleArt(ReceivedAppControl control)
    {
        try {
            string partial=Read("memory/appfw/current_partial_appid");
            if(control.CallerApplicationId!=Home || device_power_get_state()!=0 || device_power_get_ambient_mode()!=0 ||
                Read("memory/appfw/current_fullscreen_appid")!=Home || (!string.IsNullOrEmpty(partial) && partial!=Own))
                throw new InvalidOperationException("Idle Art requires full Home");
            PauseDock();
            var request=new AppControl {ApplicationId="org.tizen.art-app",Operation=DefaultOperation};
            request.ExtraData.Add("mode","fullscreen");
            var result=await AppControl.SendLaunchRequestAsync(request,null);
            Note("idle-art-launch",new {result=result.ToString(),state=State()});
            if(control.IsReplyRequest)control.ReplyToLaunchRequest(new AppControl(),
                result==AppControlResult.None?AppControlReplyResult.Succeeded:AppControlReplyResult.Failed);
        } catch(Exception error) {
            Note("idle-art-refused",error.GetType().Name);
            if(control.IsReplyRequest)control.ReplyToLaunchRequest(new AppControl(),AppControlReplyResult.Failed);
            PauseBackground();
        }
    }

    // First-screen requests and remote Home reach one entry; no polling or Mac relay.
    protected override async void OnAppControlReceived(AppControlReceivedEventArgs args)
    {
        base.OnAppControlReceived(args);var control=args.ReceivedAppControl;
        string action=Extra(control,"action"),entry=Extra(control,"entry_point"),called=Extra(control,"calledApp");
        bool hasRelocate=Extra(control,"__K_RELOCATE_BELOW")!=null;
        Note("app-control",new {action,entry,called,caller=control.CallerApplicationId,appIds=control.ExtraData.GetKeys().Where(key=>key.Contains("appId")).Select(key=>Extra(control,key)).ToArray(),hasRelocate,state=State()});
        // Hardware IPC must use an OS-reported owned sender, including channel routes.
        // Reject unknown Samsung senders until verified; never trust entry_point alone.
        if((action!=null || entry=="brightness_key") && control.CallerApplicationId!=Home && control.CallerApplicationId!=Own) {
            Note("caller-refused");
            if(control.IsReplyRequest)control.ReplyToLaunchRequest(new AppControl(),AppControlReplyResult.Failed);
            return;
        }
        if(entry=="brightness_key" && action==null) {BackgroundBrightnessKey(Extra(control,"brightness_direction"));return;}
        if(action=="enter-art") {EnterIdleArt(control);return;}
        if(action=="open-feed-item") {await OpenFeedItem(control);return;}
        if(action=="get-feed" || action=="get-feed-image" || action=="get-order" || action=="get-brightness" || action=="set-brightness" || action=="set-auto-brightness") {await ReadRequest(control,action);return;}
        if(action=="report-feed-render") {
            int cards,images;
            if(int.TryParse(Extra(control,"cards"),out cards) && int.TryParse(Extra(control,"images"),out images) && cards>=0 && cards<=40 && images>=0 && images<=cards)
                Note("feed-render",new {cards,images,provider=Extra(control,"app")});
            if(control.IsReplyRequest)control.ReplyToLaunchRequest(new AppControl(),AppControlReplyResult.Succeeded);
            PauseBackground();
            return;
        }
        // Samsung Apps sends a first-screen request with an appId when Add to Home
        // is selected. Our Home route receives it, so save the pin before showing UI.
        string pinId=control.ExtraData.GetKeys().Where(key=>key.Contains("appId")).Select(key=>Extra(control,key)).FirstOrDefault();
        if(action==null && Read("memory/appfw/current_fullscreen_appid")==DefaultIds[5] && !string.IsNullOrEmpty(pinId)) {PinFromStore(pinId);return;}
        if(action=="samsung") {
            bool opened=await Open(DefaultIds[7]);
            if(control.IsReplyRequest)control.ReplyToLaunchRequest(new AppControl(),opened?AppControlReplyResult.Succeeded:AppControlReplyResult.Failed);
            return;
        }
        if(action=="home") { await Open(Home);return; }
        if(action=="hide") { PauseDock();return; }
        string full=Read("memory/appfw/current_fullscreen_appid");
        int ambient=device_power_get_ambient_mode();
        if(ambient!=0 && entry=="booting") {Note("native-art-wake-preserved");PauseDock();return;}
        // Native Art and motion/brightness wake remain native. Only normal first-screen
        // startup/returns with no useful app underneath need the full web background.
        if(ambient==0 && (entry=="booting" || full=="org.tizen.tv-viewer" || full=="org.tizen.art-app" || full==null || full=="")) { await Open(Home);return; }
        if(visible)AfterRelease(()=>{_ = Open(Home);});else ShowDock();
    }
    async void PinFromStore(string requested)
    {
        try {
            if(requested.Length>128)throw new ArgumentException("Invalid app ID");
            await RefreshApps();
            var tile=installedApps.Find(app=>app.Id==requested || app.StoreId==requested || (requested.Length==10 && app.Id.Length>10 && app.Id[10]=='.' && app.Id.Substring(0,10)==requested));
            if(tile==null)throw new ArgumentException("App is not installed");
            var order=ReadOrder().Concat(new[]{tile.Id}).Distinct().ToArray();SaveOrder(order);SortApps(order);
            Note("app-pinned",new {id=tile.Id,count=order.Length});await Open(Home);
        } catch(Exception error) {Note("pin-error",error.GetType().Name);ShowDock();status.Text="Could not pin this app.";}
    }
    void ShowDock()
    {
        // Full Home already contains navigation; never layer the dock over it.
        if(Read("memory/appfw/current_fullscreen_appid")==Home) {Note("home-already-visible");PauseDock();return;}
        // A new opening follows the app underneath, not the last browsed tile.
        string active=Read("memory/appfw/current_fullscreen_appid");
        if(active=="com.samsung.tv.cobalt-yt")active=DefaultIds[1];
        int activeIndex=apps.FindIndex(tile=>tile.Id==active);
        pending=null;release.Stop();launching=false;brightnessFocused=false;
        StopSlide();
        if(activeIndex>=0)SetSelection(activeIndex);
        _=RefreshApps();
        if(!grabbed) { grabbed=Window.Instance.GrabKey(183,Window.KeyGrabMode.Topmost);Note("home-grab",new {grabbed}); }
        panel.PositionX=-Layout("sidebarWidth");
        slide=new Animation(300);
        // Match the web entrance: small overshoot, then settle; no input delay.
        using(var start=new Vector2(.2f,1.12f))using(var end=new Vector2(.35f,1))using(var ease=new AlphaFunction(start,end))
            slide.AnimateTo(panel,nameof(View.PositionX),0f,ease);
        visible=true;status.Text="";panel.Show();controlsScrim.Show();slide.Play();time.Text=DateTime.Now.ToString("H:mm");time.Show();clockTick.Start();
        Window.Instance.SetAcceptFocus(true);Window.Instance.Show();Window.Instance.Raise();panel.KeyInputFocus=true;
        StopBrightnessSlide();brightnessSheet.PositionX=Brightness("sheetOffset").GetSingle();brightnessIcon.PositionY=Brightness("icon").GetProperty("y").GetSingle();
        brightnessPanel.Show();FocusBrightness(false);RefreshBrightness();
        idle.Stop();idle.Start();Note("shown",new {state=State(),selection=tiles[selected].Id});
    }
    bool HandleKey(object sender,View.KeyEventArgs args)
    {
        string key=args.Key.KeyPressedName;
        bool back=key=="XF86Back" || key=="Back" || key=="BackSpace" || key=="Escape";
        bool home=key=="XF86Home" || key=="Home";
        bool enter=key=="Return" || key=="Enter" || key=="KP_Enter";
        bool arrow=key=="Left" || key=="Right" || key=="Up" || key=="Down";
        bool channelUp=key=="XF86RaiseChannel" || key=="XF86ChannelUp" || key=="ChannelUp",channelDown=key=="XF86LowerChannel" || key=="XF86ChannelDown" || key=="ChannelDown";
        bool channelMenu=key=="XF86ChannelGuide" || key=="Guide" || key=="XF86ChannelList" || key=="ChannelList" || key=="XF86PreviousChannel" || key=="PreviousChannel";
        if(!back && !home && !enter && !arrow && !channelUp && !channelDown && !channelMenu)return false;
        if(!visible || launching || pending!=null)return true;
        idle.Stop();idle.Start();
        if(channelMenu)return true;
        if(args.Key.State==Key.StateType.Down) {
            if(channelUp || channelDown) {AdjustBrightness(channelUp?1:-1);return true;}
            if(key=="Right") {if(brightnessFocused){brightnessAutoFocused=false;RenderBrightness();}else FocusBrightness(true);return true;}
            if(brightnessFocused && arrow) {
                if(key=="Left") {if(brightnessAutoFocused)FocusBrightness(false);else {brightnessAutoFocused=true;RenderBrightness();}}
                else if(!brightnessAutoFocused && (key=="Up" || key=="Down"))AdjustBrightness(key=="Up"?1:-1);
                return true;
            }
            if(enter && brightnessFocused)return true;
            if(enter)enterDown=true;
            if(key=="Up" || key=="Down")SetSelection(selected+(key=="Up"?-1:1));
        } else if(args.Key.State==Key.StateType.Up) {
            if(home)AfterRelease(()=>{_ = Open(Home);});
            else if(back)AfterRelease(PauseDock);
            else if(enter) {
                if(brightnessFocused){if(brightnessAutoFocused)ToggleAutomaticBrightness();return true;}
                enterDown=false;string target=tiles[selected].Id;AfterRelease(()=>{_ = Open(target);});
            }
        }
        return true;
    }
    void AfterRelease(Action action) {if(pending!=null)return;pending=action;release.Start();}
    async Task<bool> Open(string target)
    {
        if(string.IsNullOrEmpty(target)){status.Text="App is unavailable.";return false;}
        if(launching)return false;launching=true;
        try {
            if(readRequests>0)await readsDrained.Task;
            // Pause our partial surface first; pausing after a stock bar launch can
            // accidentally affect Samsung's newly opened OSD surface.
            string focus=target==Home && visible?tiles[selected].Id:null;
            PauseDock();
            // A Samsung visit opens its normal bar over the current screen.
            var request=new AppControl {ApplicationId=target,Operation=DefaultOperation};
            if(focus!=null)request.ExtraData.Add("frameFocus",focus);
            if(target=="org.tizen.menu")request.ExtraData.Add("uri","/opt/apps/org.tizen.menu/res/json/ATSC/menu.json");
            if(target=="com.samsung.tv.store")request.ExtraData.Add("Sub_Menu","main");
            if(target=="com.samsung.tv.csfs")request.ExtraData.Add("entry_point","smarthub_key");
            var result=await AppControl.SendLaunchRequestAsync(request,null);Note("launch-result",new {target,result=result.ToString(),state=State()});
            if(result!=AppControlResult.None) {ShowDock();status.Text="Could not open the app. Please try again.";return false;}
            if(target==DefaultIds[7]) {
                for(int i=0;i<40 && Read("memory/appfw/current_partial_appid")!=target;i++)await Task.Delay(50);
                bool active=Read("memory/appfw/current_partial_appid")==target;
                Note("samsung-visible",new {active,state=State()});return active;
            }
            return true;
        } catch(Exception error) {Note("launch-error",new {target,error=error.GetType().Name});ShowDock();status.Text="Could not open the app. Please try again.";return false;}
        finally {launching=false;}
    }
    // Stop before hiding so an interrupted entrance cannot move a later window.
    void StopSlide() {brightnessFocusMotion?.Stop();brightnessFocusMotion?.Dispose();brightnessFocusMotion=null;cardMotion?.Stop();cardMotion?.Dispose();cardMotion=null;slide?.Stop();slide?.Dispose();slide=null;listSlide?.Stop();listSlide?.Dispose();listSlide=null;}
    void ReleaseHome()
    {
        if(!grabbed)return;bool released=Window.Instance.UngrabKey(183);grabbed=false;Note("home-release",new {released});
        if(!released) {Note("release-failed-own-exit");Environment.Exit(125);}
    }
    void PauseDock()
    {
        StopSlide();StopBrightnessSlide();enterDown=false;
        ReleaseHome();release?.Stop();idle?.Stop();pending=null;pendingAutomaticToggle=false;
        int result=app_context_manager_request_task("package:"+Own,"pause");Note("pause",new {result,state=State()});
        if(result!=0) {status.Text="Press Back to retry closing.";return;}
        visible=false;panel.Hide();controlsScrim.Hide();brightnessPanel.Hide();time.Hide();clockTick.Stop();Window.Instance.SetAcceptFocus(false);status.Text="";
    }
    protected override void OnPause() {base.OnPause();StopSlide();StopBrightnessSlide();enterDown=false;pendingAutomaticToggle=false;ReleaseHome();visible=false;pending=null;release?.Stop();idle?.Stop();panel?.Hide();controlsScrim?.Hide();brightnessPanel?.Hide();time?.Hide();clockTick?.Stop();Window.Instance.SetAcceptFocus(false);}
    protected override void OnTerminate() {StopSlide();StopBrightnessSlide();ReleaseHome();release?.Dispose();idle?.Dispose();clockTick?.Dispose();base.OnTerminate();}
}

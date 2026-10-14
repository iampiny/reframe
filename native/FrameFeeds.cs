using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;

// Samsung owns feed production. Home receives presentation data and opaque references,
// never provider launch payloads, profile identifiers, or credential-bearing image URLs.
sealed class FrameFeeds
{
    public sealed class Card
    {
        public string id {get;set;}
        public string title {get;set;}
        public string subtitle {get;set;}
        public bool hasArtwork {get;set;}
        [JsonIgnore] public string Image {get;set;}
        [JsonIgnore] public string Action {get;set;}
        [JsonIgnore] public string ActionType {get;set;}
        [JsonIgnore] public long From {get;set;}
        [JsonIgnore] public long Until {get;set;}
    }
    public sealed class Section
    {
        public string id {get;set;}
        public string title {get;set;}
        public List<Card> cards {get;set;} = new List<Card>();
    }
    public sealed class Feed
    {
        public string appId {get;set;}
        public string name {get;set;}
        public string status {get;set;}
        public string fetchedAt {get;set;}
        public List<Section> sections {get;set;} = new List<Section>();
    }
    readonly Dictionary<string,Feed> feeds = new Dictionary<string,Feed>();
    readonly SemaphoreSlim reader = new SemaphoreSlim(1,1);
    readonly SemaphoreSlim downloads = new SemaphoreSlim(2,2);
    readonly object state = new object();
    readonly string directory = Path.Combine(Application.Current.DirectoryInfo.Data,"home-feeds");
    static readonly string[] Apps = {"org.tizen.netflix-app","9Ur5IzDKqV.TizenYouTube","kIciSQlYEM.plex"};
    static readonly string[] Packages = {"org.tizen.netflix-app","9Ur5IzDKqV","kIciSQlYEM"};
    static readonly string[] Names = {"Netflix","YouTube","Plex"};
    const int MaximumFeedBytes=2*1024*1024, MaximumImageBytes=512*1024;
    [DllImport("libcontent-db-api.so.0",CallingConvention=CallingConvention.Cdecl)]
    static extern IntPtr get_preview_metadata([MarshalAs(UnmanagedType.LPUTF8Str)] string package);
    [DllImport("libcontent-db-api.so.0",CallingConvention=CallingConvention.Cdecl)]
    static extern void finalize_json_bytes(IntPtr pointer);
    [DllImport("libc.so.6")] static extern int chmod(string path,uint mode);

    static string Text(JsonElement node,string key,int maximum=512)
    {
        JsonElement value;
        return node.TryGetProperty(key,out value) && value.ValueKind==JsonValueKind.String?
            (value.GetString()??"").Substring(0,Math.Min(maximum,(value.GetString()??"").Length)):"";
    }
    static long Number(JsonElement node,string key)
    {
        JsonElement value;long number;
        return node.TryGetProperty(key,out value) && value.TryGetInt64(out number)?number:0;
    }
    static bool Current(Card card)
    {
        long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return card.From<=now && (card.Until==0 || card.Until>now);
    }
    static bool OwnLibrary(string action)
    {
        try {using(var json=JsonDocument.Parse(action))return Text(json.RootElement,"metadataSourceUri",8192).StartsWith("server://",StringComparison.Ordinal);}
        catch {return false;}
    }
    readonly Uri plexOrigin = ReadPlexOrigin();
    static Uri ReadPlexOrigin()
    {
        using(var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(Application.Current.DirectoryInfo.SharedResource,"runtime-config.json")))) {
            string value=Text(json.RootElement,"plexOrigin",8192);Uri origin;
            if(value=="")return null;
            if(!Uri.TryCreate(value,UriKind.Absolute,out origin) || (origin.Scheme!="http" && origin.Scheme!="https") || origin.UserInfo!="" || origin.Query!="" || origin.Fragment!="" || origin.AbsolutePath!="/")
                throw new InvalidDataException("Invalid Plex origin");
            return origin;
        }
    }
    bool Allowed(string app,string address,out Uri uri)
    {
        uri=null;
        if(address.Length>8192 || !Uri.TryCreate(address,UriKind.Absolute,out uri) || uri.UserInfo!="" || uri.Fragment!="")return false;
        string host=uri.DnsSafeHost;
        if(app==Apps[0])return uri.Scheme=="https" && uri.Port==443 && (host=="nflxso.net" || host.EndsWith(".nflxso.net",StringComparison.OrdinalIgnoreCase));
        if(app==Apps[1])return uri.Scheme=="https" && uri.Port==443 && host=="i.ytimg.com";
        // Plex artwork is restricted to the explicitly configured personal server.
        return app==Apps[2] && (uri.Scheme=="http" || uri.Scheme=="https") && plexOrigin!=null && uri.Scheme==plexOrigin.Scheme && host==plexOrigin.DnsSafeHost && uri.Port==plexOrigin.Port;
    }
    Feed Read(string app)
    {
        int provider=Array.IndexOf(Apps,app);
        if(provider<0)throw new ArgumentException("Unsupported feed");
        var result=new Feed {appId=app,name=Names[provider],status="empty",fetchedAt=DateTime.UtcNow.ToString("o")};
        Card[] previous;
        lock(state)previous=feeds.ContainsKey(app)?feeds[app].sections.SelectMany(row=>row.cards).ToArray():new Card[0];
        IntPtr pointer=IntPtr.Zero;
        try {
            pointer=get_preview_metadata(Packages[provider]);
            if(pointer==IntPtr.Zero)return result;
            int length=0;
            while(length<=MaximumFeedBytes && Marshal.ReadByte(pointer,length)!=0)length++;
            if(length==0)return result;
            if(length>MaximumFeedBytes)throw new InvalidDataException("Feed too large");
            var bytes=new byte[length];Marshal.Copy(pointer,bytes,0,length);
            using(var document=JsonDocument.Parse(new UTF8Encoding(false,true).GetString(bytes),new JsonDocumentOptions {MaxDepth=32})) {
                var root=document.RootElement;
                if(Text(root,"app_pkg_name",128)!=Packages[provider])throw new InvalidDataException("Provider mismatch");
                long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int count=0;
                foreach(var section in root.GetProperty("sections").EnumerateArray().Take(8)) {
                    var row=new Section {id=Guid.NewGuid().ToString("N"),title=Text(section,"title",160)};
                    foreach(var tile in section.GetProperty("tiles").EnumerateArray()) {
                        long from=Number(tile,"display_from"),until=Number(tile,"display_until");
                        if(from>now || (until>0 && until<=now))continue;
                        string action=Text(tile,"action_play_url",32769);
                        if(action.Length>32768)continue; // Reject, never truncate an activation payload.
                        if(provider==2 && !OwnLibrary(action))continue;
                        string image=Text(tile,"image_url",8192);
                        var card=new Card {id=Guid.NewGuid().ToString("N"),title=Text(tile,"title"),subtitle=Text(tile,"subtitle"),
                            Image=image,Action=action,ActionType=Text(tile,"action_type",64),From=from,Until=until};
                        var retained=previous.FirstOrDefault(old=>old.Image==image && old.Action==action);
                        if(retained!=null)card.id=retained.id;
                        Uri ignored;card.hasArtwork=Allowed(app,image,out ignored);
                        row.cards.Add(card);
                        if(++count>=40)break;
                    }
                    if(row.cards.Count>0)result.sections.Add(row);
                    if(count>=40)break;
                }
                // Preserve provider array order; tied positions are not meaningful.
                result.status=result.sections.Count>0?"ready":"empty";
            }
        } finally {if(pointer!=IntPtr.Zero)finalize_json_bytes(pointer);}
        return result;
    }
    public async Task<Feed> Get(string app)
    {
        lock(state) {
            Feed saved;
            if(feeds.TryGetValue(app,out saved) && DateTime.UtcNow-DateTime.Parse(saved.fetchedAt).ToUniversalTime()<TimeSpan.FromSeconds(60) &&
                saved.sections.SelectMany(row=>row.cards).All(Current))return saved;
        }
        // The firmware getter has no caller cancellation. A timed-out UI must not
        // pile up more getters behind a stuck D-Bus call.
        if(!await reader.WaitAsync(0))throw new InvalidOperationException("Feed reader busy");
        try {
            var feed=await Task.Run(()=>Read(app));
            lock(state)feeds[app]=feed;
            Directory.CreateDirectory(directory);chmod(directory,448);
            string path=Path.Combine(directory,"models.json");
            lock(state)File.WriteAllText(path,JsonSerializer.Serialize(feeds));
            chmod(path,384);
            // Images belong only to current references; old generations do not accumulate.
            string[] current;
            lock(state)current=feeds.Values.SelectMany(f=>f.sections).SelectMany(r=>r.cards).Select(c=>c.id+".jpg").ToArray();
            foreach(string file in Directory.EnumerateFiles(directory,"*.jpg"))if(!current.Contains(Path.GetFileName(file)))File.Delete(file);
            return feed;
        } finally {reader.Release();}
    }
    Card Find(string id,out string app)
    {
        if(id==null || id.Length!=32)throw new ArgumentException("Invalid item");
        lock(state)foreach(var feed in feeds.Values)foreach(var row in feed.sections)foreach(var card in row.cards)
            if(card.id==id && Current(card)) {app=feed.appId;return card;}
        app=null;throw new InvalidOperationException("Preview expired");
    }
    // Firmware2301.1 ABI: 24 inline128-byte strings followed by35 integers.
    // This is a live read, not the stale WAS backup; unknown/locked state is refused.
    [DllImport("libwas_db_api.so",CallingConvention=CallingConvention.Cdecl)]
    static extern int was_get_app_info([MarshalAs(UnmanagedType.LPUTF8Str)] string store,IntPtr info);
    [DllImport("libvconf.so.0",CallingConvention=CallingConvention.Cdecl)]
    static extern IntPtr vconf_get_str([MarshalAs(UnmanagedType.LPUTF8Str)] string key);
    [DllImport("libc.so.6")] static extern void free(IntPtr pointer);
    static string InfoText(IntPtr info,int offset)
    {
        var bytes=new byte[128];Marshal.Copy(IntPtr.Add(info,offset),bytes,0,bytes.Length);
        int end=Array.IndexOf(bytes,(byte)0);
        if(end<0)throw new InvalidOperationException("App policy unavailable.");
        return new UTF8Encoding(false,true).GetString(bytes,0,end);
    }
    static void RequireUnlocked(string app)
    {
        string store=app==Apps[0]?"3201907018807":app==Apps[1]?"111299001912":null;
        if(store==null)throw new InvalidOperationException("Open this app from its tile; title launch is not available yet.");
        IntPtr info=Marshal.AllocHGlobal(3212);
        try {
            Marshal.Copy(new byte[3212],0,info,3212);
            if(was_get_app_info(store,info)!=0 || InfoText(info,0)!=store || InfoText(info,0x380)!=app ||
                InfoText(info,0x400)!=Packages[Array.IndexOf(Apps,app)] || Marshal.ReadInt32(info,0xc14)!=3)
                throw new InvalidOperationException("App policy unavailable; open the app from its tile.");
            if(Marshal.ReadInt32(info,0xc34)!=0 || Marshal.ReadInt32(info,0xc60)!=0 || Marshal.ReadInt32(info,0xc64)!=0 ||
                Marshal.ReadInt32(info,0xc44)!=0 || Marshal.ReadInt32(info,0xc6c)!=0)
                throw new InvalidOperationException("This app requires Samsung's lock or update screen; open it from Samsung UI.");
        } finally {Marshal.FreeHGlobal(info);}
    }
    [DllImport("libsqlite3.so.0")] static extern int sqlite3_open_v2(string path,out IntPtr db,int flags,IntPtr vfs);
    [DllImport("libsqlite3.so.0")] static extern int sqlite3_prepare_v2(IntPtr db,string sql,int bytes,out IntPtr statement,IntPtr tail);
    [DllImport("libsqlite3.so.0")] static extern int sqlite3_step(IntPtr statement);
    [DllImport("libsqlite3.so.0")] static extern IntPtr sqlite3_column_text(IntPtr statement,int column);
    [DllImport("libsqlite3.so.0")] static extern int sqlite3_finalize(IntPtr statement);
    [DllImport("libsqlite3.so.0")] static extern int sqlite3_close(IntPtr db);
    static Dictionary<string,string> YoutubeMetadata()
    {
        IntPtr db=IntPtr.Zero,statement=IntPtr.Zero;
        try {
            if(sqlite3_open_v2("/opt/dbspace/.pkgmgr_parser.db",out db,1,IntPtr.Zero)!=0 ||
                sqlite3_prepare_v2(db,"SELECT md_key,md_value FROM package_app_app_metadata WHERE app_id='9Ur5IzDKqV.TizenYouTube' AND md_key IN ('http://samsung.com/tv/metadata/app.link','http://samsung.com/tv/metadata/launch_social_v.link','http://samsung.com/tv/metadata/native.userdata','http://samsung.com/tv/metadata/nativeID')",-1,out statement,IntPtr.Zero)!=0)
                throw new InvalidOperationException("YouTube launch metadata unavailable.");
            var values=new Dictionary<string,string>();int result;
            while((result=sqlite3_step(statement))==100) {
                string key=Marshal.PtrToStringUTF8(sqlite3_column_text(statement,0)),value=Marshal.PtrToStringUTF8(sqlite3_column_text(statement,1));
                if(key==null || value==null || value.Length>8192 || values.Count>=4)throw new InvalidOperationException("Invalid YouTube launch metadata.");
                values.Add(key.Substring("http://samsung.com/tv/metadata/".Length),value);
            }
            if(result!=101)throw new InvalidOperationException("YouTube launch metadata busy.");
            return values;
        } finally {if(statement!=IntPtr.Zero)sqlite3_finalize(statement);if(db!=IntPtr.Zero)sqlite3_close(db);}
    }
    static string YoutubePayload(string action,Dictionary<string,string> metadata)
    {
        // Use the installed wrapper's vendor configuration for the native recipient.
        string link,userdata,video,nativeId;
        metadata.TryGetValue("app.link",out link);metadata.TryGetValue("native.userdata",out userdata);
        metadata.TryGetValue("launch_social_v.link",out video);metadata.TryGetValue("nativeID",out nativeId);
        string baseUrl=userdata?.Split(' ').FirstOrDefault(part=>part.StartsWith("--base_url=",StringComparison.Ordinal))?.Substring(11);
        Uri url;
        if(nativeId!="com.samsung.tv.cobalt-yt" || string.IsNullOrEmpty(link) || !link.EndsWith("#",StringComparison.Ordinal) ||
            link.Substring(0,link.Length-1)!=baseUrl || video!="#?v=" ||
            !Uri.TryCreate(baseUrl,UriKind.Absolute,out url) || url.Scheme!="https" || url.Host!="www.youtube.com" || url.Port!=443 ||
            url.AbsolutePath!="/tv" || url.Query!="" || url.Fragment!="" || url.UserInfo!="")
            throw new InvalidOperationException("YouTube launch configuration unavailable.");
        IntPtr pointer=vconf_get_str("rtc/memory/FSWebApp/9Ur5IzDKqV.TizenYouTube_URL");
        string prefix;
        try {prefix=pointer==IntPtr.Zero?null:Marshal.PtrToStringUTF8(pointer);}
        finally {if(pointer!=IntPtr.Zero)free(pointer);}
        // Stock Vconf uses an empty missing prefix; native USERDATA supplies the base URL.
        if(string.IsNullOrEmpty(prefix))prefix="";
        if(prefix.Length>8192)throw new InvalidOperationException("YouTube's TV launch address is unavailable; open YouTube from its tile.");
        var parts=action.Split(new[]{"v="},StringSplitOptions.RemoveEmptyEntries);
        return prefix+video+(parts.Length>=2?parts[1]:action);
    }
    public async Task<AppControl> PrepareLaunch(string id,string caller)
    {
        string app;var card=Find(id,out app);
        if(string.IsNullOrEmpty(card.Action))throw new InvalidOperationException("This preview has no title action.");
        // Native WASDB/config reads can block. They never run on the input thread.
        Dictionary<string,string> youtubeMetadata=null;
        string payload=await Task.Run(()=>{
            RequireUnlocked(app);
            if(app!=Apps[1])return card.Action;
            youtubeMetadata=YoutubeMetadata();
            return YoutubePayload(card.Action,youtubeMetadata);
        });
        string currentApp;var current=Find(id,out currentApp);
        if(!ReferenceEquals(card,current) || currentApp!=app)throw new InvalidOperationException("Preview changed; select it again.");
        var request=new AppControl {ApplicationId=app,Operation="http://tizen.org/appcontrol/operation/default"};
        if(app==Apps[0]) {
            long expiry=0;int fields=0;
            foreach(string field in card.Action.Split('&'))if(field.StartsWith("expireAt=",StringComparison.Ordinal)) {
                fields++;if(!long.TryParse(field.Substring(9),out expiry))expiry=0;
            }
            if(card.ActionType!="NETFLIX_LAUNCH" || fields!=1 || expiry<=DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                throw new InvalidOperationException("Netflix preview expired; reopen Netflix to refresh its feed.");
            request.ExtraData.Add("--root","app");request.ExtraData.Add("--app-js","netflix");
            request.ExtraData.Add("callerid",caller);request.ExtraData.Add("action_play_url",payload);
            request.ExtraData.Add("__K_SPLASH_SCREEN","LA_UI_DEFAULT");request.ExtraData.Add("__LEGACY_LAUNCH__","true");
        } else {
            if(card.ActionType!="WAS_LAUNCH" && card.ActionType!="APP_LAUNCH")throw new InvalidOperationException("Unsupported YouTube title action.");
            // Match WAS's native-recipient output, rather than its broker input contract.
            request.ApplicationId=youtubeMetadata["nativeID"];
            request.Operation="http://samsung.com/appcontrol/operation/eden_resume";
            request.ExtraData.Add("USERDATA",youtubeMetadata["native.userdata"]);
            request.ExtraData.Add("callerid",caller);request.ExtraData.Add("PAYLOAD",payload);
        }
        return request;
    }
    public async Task<string> Image(string id)
    {
        string app;var card=Find(id,out app);Uri url;
        if(!Allowed(app,card.Image,out url))throw new InvalidOperationException("Artwork unavailable");
        await downloads.WaitAsync();
        try {
            string path=Path.Combine(directory,id+".jpg");
            byte[] bytes;
            if(File.Exists(path))bytes=await File.ReadAllBytesAsync(path);
            else {
                using(var handler=new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false,UseDefaultCredentials=false,UseProxy=false})
                using(var client=new HttpClient(handler) {Timeout=TimeSpan.FromSeconds(5)})
                using(var timeout=new CancellationTokenSource(5000)) {
                    using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,timeout.Token)) {
                        if(response.StatusCode!=HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType!="image/jpeg" ||
                            response.Content.Headers.ContentLength>MaximumImageBytes)throw new IOException("Artwork unavailable");
                        using(var input=await response.Content.ReadAsStreamAsync())
                        using(var output=new MemoryStream()) {
                            var buffer=new byte[8192];int read;
                            while((read=await input.ReadAsync(buffer,0,buffer.Length,timeout.Token))>0) {
                                if(output.Length+read>MaximumImageBytes)throw new IOException("Artwork too large");
                                output.Write(buffer,0,read);
                            }
                            bytes=output.ToArray();
                        }
                    }
                }
                if(bytes.Length<4 || bytes[0]!=255 || bytes[1]!=216)throw new InvalidDataException("Invalid artwork");
                Directory.CreateDirectory(directory);chmod(directory,448);await File.WriteAllBytesAsync(path,bytes);chmod(path,384);
            }
            return "data:image/jpeg;base64,"+Convert.ToBase64String(bytes);
        } finally {downloads.Release();}
    }
}

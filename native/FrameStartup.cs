using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;

// One bounded cold-boot action. No persistent daemon, remote relay or power policy.
sealed class FrameStartup : ServiceApplication
{
    const string Home="Fr4meHome1.Launcher", Package="org.framehome.overlayprobe";
    readonly string run=Guid.NewGuid().ToString("N");
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly List<object> events=new List<object>();
    string boot, path;
    [DllImport("libc.so.6")] static extern uint getuid();
    [DllImport("libc.so.6")] static extern uint geteuid();
    [DllImport("libc.so.6")] static extern void free(IntPtr p);
    [DllImport("libdeviced.so.1")] static extern int device_power_get_state();
    [DllImport("libdeviced.so.1")] static extern int device_power_get_ambient_mode();
    [DllImport("libvconf.so.0")] static extern IntPtr vconf_get_str(string key);
    static string Read(string key){var p=vconf_get_str(key);if(p==IntPtr.Zero)return null;try{return Marshal.PtrToStringAnsi(p);}finally{free(p);}}
    static double Uptime()=>double.Parse(File.ReadAllText("/proc/uptime").Split(' ')[0],CultureInfo.InvariantCulture);
    static void Main(string[] args)
    {
        new Thread(()=>{Thread.Sleep(60000);Environment.Exit(124);}){IsBackground=true}.Start();
        new FrameStartup().Run(args);
    }
    void Note(string kind,object value=null)
    {
        if(events.Count==100)events.RemoveAt(0);events.Add(new{kind,value,elapsedMs=clock.ElapsedMilliseconds});
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(new{run,boot,uptime=Uptime(),events}));File.Move(path+".tmp",path,true);
    }
    protected override async void OnCreate()
    {
        base.OnCreate();
        try {
            path=Path.Combine(DirectoryInfo.Data,"startup-state.json");
            boot=File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();
            string label=File.ReadAllText("/proc/self/attr/current").Trim();
            Note("started",new{uid=getuid(),euid=geteuid(),label});
            if(getuid()!=5001 || geteuid()!=5001 || label!="User::Pkg::"+Package || !Guid.TryParse(boot,out var parsed)) {Note("identity-refused");return;}
            if(Uptime()>120) {Note("outside-cold-window");return;}
            string attempted=Path.Combine(DirectoryInfo.Data,"startup-attempted-boot");
            if(File.Exists(attempted)&&File.ReadAllText(attempted)==boot) {Note("already-attempted");return;}
            int settled=0;string previous=null;
            while(clock.ElapsedMilliseconds<50000 && Uptime()<=120) {
                int power=device_power_get_state(),ambient=device_power_get_ambient_mode();
                string full=Read("memory/appfw/current_fullscreen_appid"),partial=Read("memory/appfw/current_partial_appid"),airplay=Read("memory/menu/network/airplay_status");
                string state=power+"|"+ambient+"|"+full+"|"+partial+"|"+airplay;
                if(state!=previous){previous=state;Note("state",new{power,ambient,full,partial,airplay});}
                if(ambient>0){Note("native-art-preserved");return;}
                if(full==Home){Note("already-home");return;}
                // Four stable samples after the normal tuner background is ready.
                // Never steal focus from playback, setup, Settings, Apps or AirPlay.
                bool ready=power==0 && ambient==0 && full=="org.tizen.tv-viewer" &&
                    (partial=="" || partial=="com.samsung.tv.csfs") && airplay!=null && airplay!="running";
                settled=ready?settled+1:0;
                if(settled>=4) {
                    File.WriteAllText(attempted,boot); // A crash cannot cause another attempt this boot.
                    var result=await AppControl.SendLaunchRequestAsync(new AppControl{ApplicationId=Home,Operation="http://tizen.org/appcontrol/operation/default"},null);
                    Note("launch-result",new{result=result.ToString()});
                    for(int i=0;i<40 && Read("memory/appfw/current_fullscreen_appid")!=Home;i++)await Task.Delay(100);
                    Note("settled",new{full=Read("memory/appfw/current_fullscreen_appid"),partial=Read("memory/appfw/current_partial_appid")});return;
                }
                await Task.Delay(500);
            }
            Note("readiness-timeout");
        } catch(Exception error) {try{Note("error",new{type=error.GetType().Name});}catch{}}
        finally {try{Note("finished");}catch{}Exit();}
    }
}

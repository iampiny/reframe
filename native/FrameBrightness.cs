using System;
using System.Runtime.InteropServices;

// Hardware profile access is separate from NUI rendering and app routing.
// Both Home IPC and native remote controls use the same guarded transaction.
static class FrameBrightness
{
    [DllImport("libc.so.6")] static extern void free(IntPtr pointer);
    [DllImport("libdeviced.so.1")] static extern int device_power_get_ambient_mode();
    [DllImport("libavoc.so")] static extern int avoc_get_backlight(int window,out int value,int saveType);
    [DllImport("libsettings-api.so")][return: MarshalAs(UnmanagedType.I1)]
    static extern bool CSharpGetItemValue(string id,out IntPtr name,out IntPtr value);
    [DllImport("libsettings-api.so")][return: MarshalAs(UnmanagedType.I1)]
    static extern bool CSharpSetItemValue(string id,string value);
    [DllImport("libsettings-api.so")][return: MarshalAs(UnmanagedType.I1)]
    static extern bool CSharpGetItemEnable(string id,[MarshalAs(UnmanagedType.I1)]out bool enabled);
    // Current-profile backlight only: never write Art brightness or all profiles.
    public static int Read()
    {
        if(device_power_get_ambient_mode()!=0)throw new InvalidOperationException("Art brightness is separate");
        IntPtr name=IntPtr.Zero,value=IntPtr.Zero;
        try {
            int result;
            if(!CSharpGetItemValue("brightness",out name,out value) || !int.TryParse(Marshal.PtrToStringAnsi(value),out result) || result<0 || result>50)
                throw new InvalidOperationException("Brightness unavailable");
            return result;
        } finally {if(name!=IntPtr.Zero)free(name);if(value!=IntPtr.Zero && value!=name)free(value);}
    }
    // Samsung's ambient-light sensor is separate from backlight and Art sensors.
    public static int ReadAutomatic()
    {
        bool enabled;if(!CSharpGetItemEnable("brightness-optimization",out enabled) || !enabled)return -1;
        IntPtr name=IntPtr.Zero,value=IntPtr.Zero;
        try {
            int result;
            return CSharpGetItemValue("brightness-optimization",out name,out value) && int.TryParse(Marshal.PtrToStringAnsi(value),out result) && (result==0 || result==1)?result:-1;
        } finally {if(name!=IntPtr.Zero)free(name);if(value!=IntPtr.Zero && value!=name)free(value);}
    }
    static void SetAutomatic(int value)
    {
        if(device_power_get_ambient_mode()!=0 || ReadAutomatic()<0)throw new InvalidOperationException("Automatic brightness unavailable");
        if(!CSharpSetItemValue("brightness-optimization",value.ToString()) || ReadAutomatic()!=value)throw new InvalidOperationException("Automatic brightness write failed");
    }
    // AVOC save type 1 is the active setting; 2 is that profile's default.
    // Cross-check the menu scale before restoring: never guess 50% or reset a profile.
    static int ReadDefault(int current)
    {
        int configured,value;
        if(avoc_get_backlight(0,out configured,1)!=0 || configured!=current ||
           avoc_get_backlight(0,out value,2)!=0 || value<0 || value>50 || Read()!=current)
            throw new InvalidOperationException("Default brightness unavailable");
        return value;
    }
    static int SetBrightness(int target)
    {
        if(device_power_get_ambient_mode()!=0)throw new InvalidOperationException("Art brightness is separate");
        if(Read()!=target && !CSharpSetItemValue("brightness",target.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            throw new InvalidOperationException("Brightness write failed");
        System.Threading.Thread.Sleep(150);
        int current=Read();
        if(current!=target)throw new InvalidOperationException("Brightness readback mismatch");
        return current;
    }
    public static (int value,int automatic) Update(int? target,int? expected,int? automaticTarget)
    {
        int current=Read(),automatic=ReadAutomatic();
        if(target.HasValue && (target<0 || target>50 || !expected.HasValue || current!=expected.Value))
            throw new InvalidOperationException("Brightness changed");
        if(automaticTarget==1) {
            if(automatic<0)throw new InvalidOperationException("Automatic brightness unavailable");
            int baseline=ReadDefault(current);
            try {
                SetAutomatic(0);
                SetBrightness(baseline);
                SetAutomatic(1);
            } catch {
                // Restore only this attempted transaction while normal-picture mode remains active.
                if(device_power_get_ambient_mode()==0) {SetBrightness(current);SetAutomatic(automatic);}
                throw;
            }
        } else if(automaticTarget.HasValue)SetAutomatic(automaticTarget.Value);
        if(target.HasValue) {
            // A manual choice must hold steady instead of being reduced by Eco.
            if(ReadAutomatic()==1)SetAutomatic(0);
            current=SetBrightness(target.Value);
        }
        return (value:automaticTarget.HasValue?Read():current,automatic:ReadAutomatic());
    }
}

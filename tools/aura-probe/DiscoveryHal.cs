// Experimental COM contracts read from the installed ASUS type library.
// This component has no hardware or network access and is registered only in its
// host process. It is not an ASUS component and does not claim ASUS certification.
using System;
using System.Runtime.InteropServices;

[ComVisible(true), Guid("F2C8D5B4-3854-4325-8A4F-FD7C5072E3B9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IProbeHal {
    [PreserveSig] int Enumerate(IntPtr devices, IntPtr count);
    [PreserveSig] int Enumerate2(IntPtr devices, IntPtr count);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class DiscoveryHal : IProbeHal, ICustomQueryInterface {
    public static int EnumerationCalls;
    public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr value) {
        Console.WriteLine("Probe: HAL queried IID " + iid);
        value = IntPtr.Zero;
        return CustomQueryInterfaceResult.NotHandled;
    }
    public int Enumerate(IntPtr devices, IntPtr count) {
        Console.WriteLine("Probe: HAL Enumerate callback.");
        EnumerationCalls++;
        if (count != IntPtr.Zero) Marshal.WriteInt32(count, 0);
        return 0;
    }
    public int Enumerate2(IntPtr devices, IntPtr count) {
        Console.WriteLine("Probe: HAL Enumerate2 callback.");
        EnumerationCalls++;
        if (devices != IntPtr.Zero) Marshal.GetNativeVariantForObject(null, devices);
        if (count != IntPtr.Zero) Marshal.WriteInt32(count, 0);
        return 0;
    }
}

[ComVisible(true), Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IProbeFactory {
    [PreserveSig] int CreateInstance(IntPtr outer, ref Guid iid, out IntPtr instance);
    [PreserveSig] int LockServer([MarshalAs(UnmanagedType.Bool)] bool locked);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class DiscoveryFactory : IProbeFactory {
    public static int ActivationCalls;
    public int CreateInstance(IntPtr outer, ref Guid iid, out IntPtr instance) {
        instance = IntPtr.Zero;
        if (outer != IntPtr.Zero) return unchecked((int)0x80040110);
        ActivationCalls++;
        Console.WriteLine("Probe: factory requested IID " + iid);
        IntPtr unknown = Marshal.GetIUnknownForObject(new DiscoveryHal());
        try { return Marshal.QueryInterface(unknown, ref iid, out instance); }
        finally { Marshal.Release(unknown); }
    }
    public int LockServer(bool locked) { return 0; }
}

public static class DiscoveryRegistration {
    private static DiscoveryFactory factory;
    [DllImport("ole32.dll", PreserveSig=false)]
    private static extern void CoRegisterClassObject(ref Guid clsid,
        [MarshalAs(UnmanagedType.IUnknown)] object instance, uint context, uint flags, out uint cookie);
    [DllImport("ole32.dll", PreserveSig=false)]
    public static extern void CoRevokeClassObject(uint cookie);
    public static uint Register(Guid clsid) {
        factory = new DiscoveryFactory();
        uint cookie;
        CoRegisterClassObject(ref clsid, factory, 1, 1, out cookie);
        return cookie;
    }
}

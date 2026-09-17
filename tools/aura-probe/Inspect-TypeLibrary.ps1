# Reads COM type metadata only. Does not instantiate classes or register libraries.
param(
    [Parameter(Mandatory=$true)][string]$Path,
    [string]$TypePattern = 'Aura|Aac|Led|Hal'
)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using TYPEDESC = System.Runtime.InteropServices.ComTypes.TYPEDESC;
using TYPEATTR = System.Runtime.InteropServices.ComTypes.TYPEATTR;
using FUNCDESC = System.Runtime.InteropServices.ComTypes.FUNCDESC;
using ELEMDESC = System.Runtime.InteropServices.ComTypes.ELEMDESC;

public static class AuraTypeMetadata {
    [DllImport("oleaut32.dll", CharSet=CharSet.Unicode, PreserveSig=false)]
    private static extern void LoadTypeLibEx(string path, int regkind, out ITypeLib library);

    private static string Describe(ITypeInfo info, TYPEDESC desc) {
        var kind = (VarEnum)desc.vt;
        if (kind == VarEnum.VT_PTR || kind == VarEnum.VT_SAFEARRAY)
            return kind + "<" + Describe(info, (TYPEDESC)Marshal.PtrToStructure(desc.lpValue, typeof(TYPEDESC))) + ">";
        if (kind == VarEnum.VT_USERDEFINED) {
            ITypeInfo referenced;
            info.GetRefTypeInfo(unchecked((int)desc.lpValue.ToInt64()), out referenced);
            try {
                string name, doc, help; int context;
                referenced.GetDocumentation(-1, out name, out doc, out context, out help);
                return name;
            } finally { Marshal.ReleaseComObject(referenced); }
        }
        return kind.ToString();
    }

    public static object[] Read(string path, string pattern) {
        ITypeLib library;
        LoadTypeLibEx(path, 2, out library); // REGKIND_NONE: never register.
        var result = new List<object>();
        try {
            for (int i = 0; i < library.GetTypeInfoCount(); i++) {
                string name, doc, help; int context;
                library.GetDocumentation(i, out name, out doc, out context, out help);
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, pattern)) continue;
                ITypeInfo info; library.GetTypeInfo(i, out info);
                IntPtr pointer = IntPtr.Zero;
                try {
                    info.GetTypeAttr(out pointer);
                    var attr = (TYPEATTR)Marshal.PtrToStructure(pointer, typeof(TYPEATTR));
                    var functions = new List<object>();
                    for (int f = 0; f < attr.cFuncs; f++) {
                        IntPtr function; info.GetFuncDesc(f, out function);
                        try {
                            var desc = (FUNCDESC)Marshal.PtrToStructure(function, typeof(FUNCDESC));
                            var names = new string[desc.cParams + 1]; int count;
                            info.GetNames(desc.memid, names, names.Length, out count);
                            var parameters = new List<object>();
                            for (int p = 0; p < desc.cParams; p++) {
                                var element = (ELEMDESC)Marshal.PtrToStructure(IntPtr.Add(desc.lprgelemdescParam, p * Marshal.SizeOf(typeof(ELEMDESC))), typeof(ELEMDESC));
                                parameters.Add(new { Name = p + 1 < count ? names[p + 1] : null, Type = Describe(info, element.tdesc), Flags = element.desc.paramdesc.wParamFlags.ToString() });
                            }
                            functions.Add(new { Name = names[0], Id = desc.memid, Invoke = desc.invkind.ToString(), VtableOffset = desc.oVft, ReturnType = Describe(info, desc.elemdescFunc.tdesc), Parameters = parameters });
                        } finally { info.ReleaseFuncDesc(function); }
                    }
                    result.Add(new { Name = name, Guid = attr.guid.ToString(), Kind = attr.typekind.ToString(), Functions = functions });
                } finally {
                    if (pointer != IntPtr.Zero) info.ReleaseTypeAttr(pointer);
                    Marshal.ReleaseComObject(info);
                }
            }
        } finally { Marshal.ReleaseComObject(library); }
        return result.ToArray();
    }
}
'@
[AuraTypeMetadata]::Read((Resolve-Path -LiteralPath $Path).Path, $TypePattern) | ConvertTo-Json -Depth 8

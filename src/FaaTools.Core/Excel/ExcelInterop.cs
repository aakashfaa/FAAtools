using System.Reflection;
using System.Runtime.InteropServices;

namespace FaaTools.Core.Excel;

/// <summary>
/// Late-bound COM helpers for driving Excel.Application via reflection, ported directly from
/// the com_get/com_set/com_call/com_item/release_com helpers used throughout the existing
/// pyRevit scripts. Late binding (rather than the Interop PIA) avoids PIA version/bitness
/// mismatches across client machines and requires no compile-time Excel dependency.
/// </summary>
internal static class ExcelInterop
{
    public static object? Get(object target, string propertyName)
        => target.GetType().InvokeMember(propertyName, BindingFlags.GetProperty, null, target, null);

    public static void Set(object target, string propertyName, object? value)
        => target.GetType().InvokeMember(propertyName, BindingFlags.SetProperty, null, target, [value]);

    public static object? Call(object target, string methodName, params object?[] args)
        => target.GetType().InvokeMember(methodName, BindingFlags.InvokeMethod, null, target, args);

    /// <summary>
    /// Mirrors com_item: tries the "Item" property first, then "get_Item" as a method,
    /// then "Item" as a method, since different COM collections expose indexers differently.
    /// </summary>
    public static object? Item(object target, params object?[] args)
    {
        var type = target.GetType();
        try
        {
            return type.InvokeMember("Item", BindingFlags.GetProperty, null, target, args);
        }
        catch
        {
            // fall through
        }

        try
        {
            return type.InvokeMember("get_Item", BindingFlags.InvokeMethod, null, target, args);
        }
        catch
        {
            // fall through
        }

        return type.InvokeMember("Item", BindingFlags.InvokeMethod, null, target, args);
    }

    public static void Release(object? comObject)
    {
        if (comObject is null)
        {
            return;
        }

        try
        {
            if (Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
            }
        }
        catch
        {
            // best-effort cleanup, matches the existing scripts' swallow-and-continue behavior
        }
    }
}

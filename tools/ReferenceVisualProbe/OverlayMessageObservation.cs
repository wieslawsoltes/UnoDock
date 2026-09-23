// Public Win32 message protocol observation, NOT end-to-end pointer acceptance.
// Only application-owned layouts, rendered pixels and public arranged rectangles.
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Controls;

internal static class OverlayMessageObservation
{
    public static void TryObserve(LayoutFloatingWindowControl floating, Point point, string output, string name)
    {
        var hwnd = new WindowInteropHelper(floating).Handle;
        if (!GetCursorPos(out var previous)) throw new InvalidOperationException("GetCursorPos failed: " + Marshal.GetLastWin32Error());
        var data = IntPtr.Zero;
        var entered = false;
        try
        {
            if (!SetCursorPos((int)point.X, (int)point.Y)) throw new InvalidOperationException("SetCursorPos failed: " + Marshal.GetLastWin32Error());
            if (!GetCursorPos(out var cursor)) throw new InvalidOperationException("GetCursorPos failed: " + Marshal.GetLastWin32Error());
            Console.WriteLine($"Actual cursor={cursor.X},{cursor.Y}; requested={point}");
            var rect = new NativeRect();
            if (!GetWindowRect(hwnd, ref rect)) throw new InvalidOperationException("GetWindowRect failed: " + Marshal.GetLastWin32Error());
            data = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeRect)));
            Marshal.StructureToPtr(rect, data, false);
            entered = true;
            SendMessage(hwnd, 0x0231, IntPtr.Zero, IntPtr.Zero); // WM_ENTERSIZEMOVE
            SendMessage(hwnd, 0x0216, IntPtr.Zero, data);       // WM_MOVING: valid native RECT
            SendMessage(hwnd, 0x0003, IntPtr.Zero, IntPtr.Zero); // WM_MOVE
            foreach (var overlay in Application.Current.Windows.OfType<OverlayWindow>())
            {
                Console.WriteLine("Message-observed overlay visible=" + overlay.IsVisible);
                if (!overlay.IsVisible) continue;
                overlay.UpdateLayout();
                var root = VisualTreeHelper.GetChild(overlay, 0) as FrameworkElement;
                if (root == null || root.ActualWidth <= 0 || root.ActualHeight <= 0) continue;
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(Path.Combine(output, name + "-message.png"))) encoder.Save(file);
                var xml = new XElement("Overlay", new XAttribute("name", name), new XAttribute("method", "public-native-message-sequence-not-pointer-acceptance"),
                    new XAttribute("width", root.ActualWidth), new XAttribute("height", root.ActualHeight));
                Walk(root, root, xml); new XDocument(xml).Save(Path.Combine(output, name + "-message.xml"));
            }
        }
        finally
        {
            try { if (entered) SendMessage(hwnd, 0x0232, IntPtr.Zero, IntPtr.Zero); }
            finally
            {
                if (data != IntPtr.Zero) Marshal.FreeHGlobal(data);
                SetCursorPos(previous.X, previous.Y);
            }
        }
    }
    private static void Walk(DependencyObject node, FrameworkElement root, XElement xml)
    {
        if (node is FrameworkElement e && e.IsVisible && e.ActualWidth > 0 && e.ActualHeight > 0)
        {
            var bounds = e.TransformToAncestor(root).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight));
            xml.Add(new XElement("Element", new XAttribute("type", e.GetType().Name), new XAttribute("name", e.Name ?? ""),
                new XAttribute("x", bounds.X), new XAttribute("y", bounds.Y), new XAttribute("width", bounds.Width), new XAttribute("height", bounds.Height)));
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i), root, xml);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr hwnd, ref NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}

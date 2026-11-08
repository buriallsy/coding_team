using Godot;

namespace BorrowingSun2D;

internal static class TowerUiFont
{
    internal static Font Load()
    {
        if (System.OperatingSystem.IsMacOS())
        {
            var localFont = new FontFile();
            if (localFont.LoadDynamicFont("/System/Library/Fonts/STHeiti Medium.ttc") == Error.Ok)
                return localFont;
        }

        return new SystemFont
        {
            FontNames = new[] { "Noto Sans CJK SC", "Microsoft YaHei", "PingFang SC", "Arial" },
        };
    }
}

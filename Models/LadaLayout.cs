using System;
using System.Collections.Generic;

namespace Lada.Models;

public sealed class LadaLayout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Lada";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 240;
    public bool IsFolded { get; set; }
    public string IconId { get; set; } = "table";
    public string IconColor { get; set; } = "#5B8DEF";
    public bool AutoSortEnabled { get; set; }
    public List<LadaItem> Items { get; set; } = new();
    public List<LadaTab> Tabs { get; set; } = new();
    public int ActiveTabIndex { get; set; }

    // Layouts saved before tabs existed only have the flat Items/AutoSortEnabled
    // fields above. Once a layout has been saved with Tabs populated, those
    // flat fields are ignored in favor of this list.
    public List<LadaTab> ResolveTabs()
    {
        if (Tabs.Count > 0)
            return Tabs;

        return new List<LadaTab>
        {
            new() { Items = Items, AutoSortEnabled = AutoSortEnabled }
        };
    }
}

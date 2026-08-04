using System;
using System.Collections.Generic;
using Lada.Services;

namespace Lada.Models;

public sealed class LadaTab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Tab";
    public bool AutoSortEnabled { get; set; }
    public List<FileCategory> AutoOrganizeCategories { get; set; } = new();
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
    public List<LadaItem> Items { get; set; } = new();
    public TabContentMode ContentMode { get; set; } = TabContentMode.Icons;
    public List<ToDoTaskEntry> ToDoTasks { get; set; } = new();
    public string MemoText { get; set; } = "";
    public ItemViewMode ViewMode { get; set; } = ItemViewMode.Grid;
    public bool ShowTypeColumn { get; set; }
    public bool ShowSizeColumn { get; set; }
    public bool ShowModifiedDateColumn { get; set; }
}

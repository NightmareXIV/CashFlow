﻿using CashFlow.Data;
using CashFlow.Gui.Components;
using CsvHelper;
using ECommons.Throttlers;
using System.Globalization;
using System.IO;

namespace CashFlow.Gui;
public abstract unsafe class BaseTab<T> where T : IDescriptorBase
{
    public volatile bool NeedsUpdate = false;
    public uint LastFrame = 0;
    public volatile List<T> Data = [];
    public string SearchName = "";
    public string SearchItem = "";
    public DateTime DateMin = new(2025, 1, 1);
    public DateTime DateMax = new(DateTimeOffset.Now.Year + 1, 1, 1);
    public string DateMinStr = "";
    public string DateMaxStr = "";
    public volatile int Results = 0;
    public int Page = 1;
    private bool ShouldLoadAll = false;
    public ImGuiSortDirection RequestedSortDirection = ImGuiSortDirection.None;
    private ImGuiSortDirection PrevRequestedSortDirection = ImGuiSortDirection.None;
    public int SortColumn = 0;
    private int PrevSortColumn = 0;

    public virtual string SearchNameHint { get; } = "Search Player's Name...";
    public virtual string SearchItemHint { get; } = "Search Item...";
    public virtual bool ShouldDrawPaginator { get; } = true;

    public int IndexBegin
    {
        get
        {
            if((Page - 1) * C.PerPage > Data.Count)
            {
                Page = 1;
            }
            return C.PerPage * (Page - 1);
        }
    }

    public int IndexEnd
    {
        get
        {
            return Math.Min(Data.Count, Page * C.PerPage);
        }
    }

    public void Draw()
    {
        FrameThrottler.Throttle("UpdateBlocked", 2, true);
        if(S.WorkerThread.IsBusy)
        {
            LastFrame = CSFramework.Instance()->FrameCounter;
            ImGuiEx.Text("Loading, please wait...");
            return;
        }
        DrawSearchBar(out var updateBlocked);
        if(CSFramework.Instance()->FrameCounter - LastFrame > 2) NeedsUpdate = true;
        if(NeedsUpdate && !updateBlocked)
        {
            Load(false);
            NeedsUpdate = false;
        }
        LastFrame = CSFramework.Instance()->FrameCounter;
        if(updateBlocked && NeedsUpdate)
        {
            ImGuiEx.Text(EColor.YellowBright, "Press enter load the data");
        }
        if(ShouldDrawPaginator) DrawPaginator();
        DrawTable();
        if(RequestedSortDirection != PrevRequestedSortDirection || SortColumn != PrevSortColumn)
        {
            PrevSortColumn = SortColumn;
            PrevRequestedSortDirection = RequestedSortDirection;
            new TickScheduler(() => Load(false));
        }
    }

    public void DrawPaginator()
    {
        if(Data.Count <= C.PerPage) return;
        if(Page < 1) Page = 1;
        ImGuiEx.SetNextItemFullWidth();
        ImGui.SliderInt("##page", ref Page, 1, (int)Math.Ceiling((double)Data.Count / (double)C.PerPage));
    }

    public virtual void DrawSearchBar(out bool updateBlocked)
    {
        var blocked = false;
        ImGuiEx.InputWithRightButtonsArea(() =>
        {
            if(ImGui.InputTextWithHint("##SearchPlayer", SearchNameHint, ref SearchName, 50))
            {
                Data.Clear();
                NeedsUpdate = true;
            }
            if(ImGui.IsItemActive())
            {
                blocked = true;
            }
        }, () =>
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X / 3);
            if(ImGui.InputTextWithHint("##SearchItem", SearchItemHint, ref SearchItem, 50))
            {
                Data.Clear();
                NeedsUpdate = true;
            }
            if(ImGui.IsItemActive())
            {
                blocked = true;
            }
            ImGui.SameLine();
            DrawDateFilter(out var isOpen);
            if(isOpen)
            {
                Data.Clear();
                NeedsUpdate = true;
                blocked = true;
            }
            DrawExtraFilters();
        });
        updateBlocked = blocked;
    }

    public void DrawDateFilter(out bool isOpen)
    {
        if(DateWidget.DatePickerWithInput("##min", 1, ref DateMinStr, ref DateMin, out var open1))
        {
            NeedsUpdate = true;
        }
        ImGui.SameLine(0, 1);
        ImGuiEx.Text("-");
        ImGui.SameLine(0, 1);
        if(DateWidget.DatePickerWithInput("##max", 2, ref DateMaxStr, ref DateMax, out var open2))
        {
            NeedsUpdate = true;
        }
        isOpen = open1 || open2;
        ImGui.SameLine();
        if(ImGuiEx.IconButton(FontAwesomeIcon.FileCsv))
        {
            S.CashflowFileDialogManager.Open(ExportToCSV);
        }
        ImGuiEx.Tooltip("Export as CSV");
    }

    public void Load(bool ignoreSearchFilters, bool clear = true)
    {
        OnPreLoadData();
        ((Action<Action>)(clear ? S.WorkerThread.ClearAndEnqueue : S.WorkerThread.Enqueue))(() =>
        {
            var newCidMap = P.DataProvider.GetRegisteredPlayers();
            var data = LoadData();
            var results = data.Count;
            var newData = new List<T>();
            foreach(var x in data.OrderBy(x => x.UnixTime))
            {
                if(!ignoreSearchFilters)
                {
                    if(!ShouldAddData(x)) continue;
                    if(SearchName != "" && !ProcessSearchByName(x)) continue;
                    if(SearchItem != "" && !ProcessSearchByItem(x)) continue;
                    if(x.UnixTime > DateMax.ToUnixTimeMilliseconds()) continue;
                    if(x.UnixTime < DateMin.ToUnixTimeMilliseconds()) continue;
                }
                else
                {
                    ShouldAddData(x);
                }
                AddData(x, newData);
            }
            newData.Reverse();
            if(RequestedSortDirection != ImGuiSortDirection.None)
            {
                newData = SortData(newData);
            }
            OnPostLoadDataAsync(newData);
            Svc.Framework.RunOnFrameworkThread(() =>
            {
                S.MainWindow.CIDMap = newCidMap;
                Results = data.Count;
                Data = newData;

            }).Wait();
        });
    }

    public virtual void OnPreLoadData() { }

    public virtual void OnPostLoadDataAsync(List<T> newData) { }

    /// <summary>
    /// Called asynchronously
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public virtual bool ShouldAddData(T data)
    {
        return true;
    }

    public virtual void DrawExtraFilters()
    {
    }

    public virtual void AddData(T data, List<T> list)
    {
        list.Add(data);
    }
    public abstract void DrawTable();
    public abstract List<T> LoadData();
    public abstract bool ProcessSearchByName(T data);
    public abstract bool ProcessSearchByItem(T data);
    public virtual List<T> SortData(List<T> data) { return data; }

    public void ImGuiCheckSorting()
    {
        if(S.WorkerThread.IsBusy) return;
        if(ImGui.TableGetSortSpecs().SpecsDirty)
        {
            if(ImGui.TableGetSortSpecs().Specs.NativePtr == null || ImGui.TableGetSortSpecs().Specs.SortDirection == ImGuiSortDirection.None)
            {
                SortColumn = 0;
                RequestedSortDirection = ImGuiSortDirection.None;
                return;
            }
            SortColumn = ImGui.TableGetSortSpecs().Specs.ColumnIndex;
            RequestedSortDirection = ImGui.TableGetSortSpecs().Specs.SortDirection;
        }
    }

    public List<T> Order<O>(List<T> list, Func<T, O> func)
    {
        return RequestedSortDirection switch
        {
            ImGuiSortDirection.Ascending => [.. list.OrderBy(func)],
            ImGuiSortDirection.Descending => [.. list.OrderByDescending(func)],
            _ => list,
        };
    }

    public void ExportToCSV(string pathToFile)
    {
        if(Data.Count == 0) return;
        List<T> data = [.. Data];
        S.ThreadPool.Run(() =>
        {
            if(File.Exists(pathToFile)) DeleteFileToRecycleBin(pathToFile);
            using var fs = new FileStream(pathToFile, FileMode.Create);
            using var writer = new StreamWriter(fs);
            using var csv = new CsvWriter(writer, CultureInfo.CurrentCulture);
            csv.WriteFields(data[0].GetCsvHeaders());
            foreach(var t in data)
            {
                foreach(var s in t.GetCsvExport())
                {
                    csv.WriteFields(s);
                }
            }
            fs.Flush();
        });
    }
}

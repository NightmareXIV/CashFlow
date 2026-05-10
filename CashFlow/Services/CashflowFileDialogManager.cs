using Dalamud.Interface.ImGuiFileDialog;
using ECommons.SimpleGui;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CashFlow.Services;
public unsafe sealed class CashflowFileDialogManager : IDisposable
{
    FileDialogManager Manager = new();
    private CashflowFileDialogManager()
    {
        Svc.PluginInterface.UiBuilder.Draw += Manager.Draw;
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= Manager.Draw;
    }

    public void Open(Action<string> callback)
    {
        Manager.SaveFileDialog("Export as CSV...", "", "exported", "csv", (result, path) =>
        {
            if(result)
            {
                callback(path.EndsWith(".csv")?path:$"{path}.csv");
            }
        }, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    }

    public void OpenXlsx(Action<string> callback)
    {
        Manager.SaveFileDialog("Export as Excel...", "", "exported", "xlsx", (result, path) =>
        {
            if(result)
            {
                callback(path.EndsWith(".xlsx")?path:$"{path}.xlsx");
            }
        }, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    }
}
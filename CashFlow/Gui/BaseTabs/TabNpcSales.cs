using CashFlow.Data.SqlDescriptors;
using ECommons.ExcelServices;
using NightmareUI.Censoring;

namespace CashFlow.Gui.BaseTabs;
public unsafe class TabNpcSales : BaseTab<NpcSaleSqlDescriptor>
{
    public Dictionary<uint, long> ItemValues = [];

    public override List<NpcSaleSqlDescriptor> SortData(List<NpcSaleSqlDescriptor> data)
    {
        return SortColumn switch
        {
            0 => Order(data, x => S.MainWindow.CIDMap.SafeSelect(x.CidUlong).ToString()),
            1 => Order(data, x => x.Price),
            2 => Order(data, x => ExcelItemHelper.GetName((uint)(x.Item % 1000000))),
            3 => Order(data, x => x.Quantity),
            4 => Order(data, x => x.UnixTime),
            _ => data
        };
    }

    public override void DrawTable()
    {
        if(ImGuiEx.BeginDefaultTable(["Your Character", "Paid", "~Item Name", "Qty", "Date", "##actions"], extraFlags: ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate))
        {
            ImGuiCheckSorting();
            for(var i = IndexBegin; i < IndexEnd; i++)
            {
                var t = Data[i];
                if(!IsRowVisible(i)) continue;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                {
                    ImGuiEx.Text(S.MainWindow.CIDMap.TryGetValue(t.CidUlong, out var s) ? Censor.Character(s.ToString()) : Censor.Hide($"{t.CidUlong:X16}"));
                }

                ImGui.TableNextColumn();
                Utils.DrawGilIncrease();
                ImGuiEx.Text($" {t.Price:N0}");

                ImGui.TableNextColumn();
                ImGuiEx.Text(ExcelItemHelper.GetName((uint)(t.Item % 1000000)));

                ImGui.TableNextColumn();
                ImGuiEx.Text($"x{t.Quantity}" + (t.Item > 1000000 ? "" : ""));

                ImGui.TableNextColumn();
                ImGuiEx.Text(DateTimeOffset.FromUnixTimeMilliseconds(t.UnixTime).ToPreferredTimeString());

                ImGui.TableNextColumn();
                DrawRowActions(i);
                DrawRowHighlight();
            }
            ImGui.EndTable();
        }
    }

    public override void AddData(NpcSaleSqlDescriptor data, List<NpcSaleSqlDescriptor> list)
    {
        if(C.Blacklist.Contains(data.CidUlong)) return;
        if(C.DisplayExclusionsNpcSales.Contains(data.CidUlong)) return;
        var id = (uint)data.Item % 1_000_000u;
        if(!ItemValues.ContainsKey(id)) ItemValues[id] = 0;
        ItemValues[id] += data.Price;
        base.AddData(data, list);
    }

    public override List<NpcSaleSqlDescriptor> LoadData()
    {
        ItemValues.Clear();
        return P.DataProvider.GetNpcSales();
    }

    public override bool ProcessSearchByItem(NpcSaleSqlDescriptor x)
    {
        return ExcelItemHelper.GetName((uint)(x.Item % 1000000), true).Contains(SearchItem, StringComparison.OrdinalIgnoreCase);
    }

    public override bool ProcessSearchByName(NpcSaleSqlDescriptor x)
    {
        var name = S.MainWindow.CIDMap.SafeSelect(x.CidUlong);
        return name.ToString().Contains(SearchName, StringComparison.OrdinalIgnoreCase);
    }
}

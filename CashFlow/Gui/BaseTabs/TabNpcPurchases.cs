using CashFlow.Data.SqlDescriptors;
using ECommons.ExcelServices;
using NightmareUI.Censoring;

namespace CashFlow.Gui.BaseTabs;
public unsafe class TabNpcPurchases : BaseTab<NpcPurchaseSqlDescriptor>
{
    public Dictionary<uint, long> ItemValues = [];

    public override List<NpcPurchaseSqlDescriptor> SortData(List<NpcPurchaseSqlDescriptor> data)
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
                Utils.DrawGilDecrease();
                ImGuiEx.Text($" {t.Price:N0}");
                if(t.IsBuybackBool)
                {
                    ImGui.SameLine(0, 2);
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGuiEx.Text(FontAwesomeIcon.FastBackward.ToIconString());
                    ImGui.PopFont();
                    ImGuiEx.Tooltip("This is a buyback");
                }

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

    public override List<NpcPurchaseSqlDescriptor> LoadData()
    {

        ItemValues.Clear();
        return P.DataProvider.GetNpcPurchases();
    }

    public override void AddData(NpcPurchaseSqlDescriptor data, List<NpcPurchaseSqlDescriptor> list)
    {
        if(C.Blacklist.Contains(data.CidUlong)) return;
        if(C.DisplayExclusionsNpcPurchases.Contains(data.CidUlong)) return;
        var id = (uint)data.Item % 1_000_000u;
        if(!ItemValues.ContainsKey(id)) ItemValues[id] = 0;
        ItemValues[id] += data.Price;
        base.AddData(data, list);
    }

    public override bool ProcessSearchByItem(NpcPurchaseSqlDescriptor x)
    {
        return ExcelItemHelper.GetName((uint)(x.Item % 1000000), true).Contains(SearchItem, StringComparison.OrdinalIgnoreCase);
    }

    public override bool ProcessSearchByName(NpcPurchaseSqlDescriptor x)
    {
        var name = S.MainWindow.CIDMap.SafeSelect(x.CidUlong);
        return name.ToString().Contains(SearchName, StringComparison.OrdinalIgnoreCase);
    }
}

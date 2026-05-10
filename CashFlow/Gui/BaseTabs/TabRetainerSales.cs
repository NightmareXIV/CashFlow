using CashFlow.Data.LegacyDescriptors;
using ECommons.ExcelServices;
using NightmareUI.Censoring;

namespace CashFlow.Gui.BaseTabs;
public unsafe class TabRetainerSales : BaseTab<RetainerSaleDescriptor>
{
    public Dictionary<uint, long> ItemValues = [];
    public override string SearchNameHint { get; } = "Search Player's/Retainer's Name...";

    public override List<RetainerSaleDescriptor> SortData(List<RetainerSaleDescriptor> data)
    {
        return SortColumn switch
        {
            0 => Order(data, x => S.MainWindow.CIDMap.SafeSelect(x.CidUlong).ToString()),
            1 => Order(data, x => x.RetainerName),
            2 => Order(data, x => x.BuyerName),
            3 => Order(data, x => x.Price),
            4 => Order(data, x => ExcelItemHelper.GetName((uint)(x.ItemID % 1000000))),
            5 => Order(data, x => x.Quantity),
            6 => Order(data, x => x.UnixTime),
            _ => data
        };
    }

    public override void DrawTable()
    {
        if(ImGuiEx.BeginDefaultTable(["Your Character", "Your Retainer", "Buyer", "Paid", "~Item", "Qty", "Date", "##actions"], extraFlags: ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate))
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
                {
                    ImGuiEx.Text(t.RetainerName);
                }

                ImGui.TableNextColumn();
                ImGuiEx.Text(Censor.Character(t.BuyerName));

                ImGui.TableNextColumn();
                Utils.DrawGilIncrease();
                ImGuiEx.Text($" {t.Price:N0}");
                if(t.IsMannequinn)
                {
                    ImGui.SameLine(0, 2);
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGuiEx.Text("\uf183");
                    ImGui.PopFont();
                    ImGuiEx.Tooltip("This sale was done via Mannequinn");
                }

                ImGui.TableNextColumn();
                ImGuiEx.Text(ExcelItemHelper.GetName((uint)(t.ItemID % 1000000)));

                ImGui.TableNextColumn();
                ImGuiEx.Text($"x{t.Quantity}" + (t.ItemID > 1000000 ? "" : ""));

                ImGui.TableNextColumn();
                ImGuiEx.Text(DateTimeOffset.FromUnixTimeMilliseconds(t.UnixTime).ToPreferredTimeString());

                ImGui.TableNextColumn();
                DrawRowActions(i);
                DrawRowHighlight();
            }
            ImGui.EndTable();
        }
    }

    public override void AddData(RetainerSaleDescriptor data, List<RetainerSaleDescriptor> list)
    {
        if(C.Blacklist.Contains(data.CidUlong)) return;
        if(C.DisplayExclusionsRetainerSalesNormal.Contains(data.CidUlong) && !data.IsMannequinn) return;
        if(C.DisplayExclusionsRetainerSalesMannequinn.Contains(data.CidUlong) && data.IsMannequinn) return;
        var id = (uint)data.ItemID % 1_000_000u;
        if(!ItemValues.ContainsKey(id)) ItemValues[id] = 0;
        ItemValues[id] += data.Price;
        base.AddData(data, list);
    }

    public override List<RetainerSaleDescriptor> LoadData()
    {
        ItemValues.Clear();
        return P.DataProvider.GetRetainerHistory(DateMin.ToUnixTimeMilliseconds(), DateMax.ToUnixTimeMilliseconds());
    }

    public override bool ProcessSearchByItem(RetainerSaleDescriptor x)
    {
        return ExcelItemHelper.GetName((uint)(x.ItemID % 1000000), true).Contains(SearchItem, StringComparison.OrdinalIgnoreCase);
    }

    public override bool ProcessSearchByName(RetainerSaleDescriptor x)
    {
        var name = S.MainWindow.CIDMap.SafeSelect(x.CidUlong);
        return name.ToString().Contains(SearchName, StringComparison.OrdinalIgnoreCase)
            || x.RetainerName.ToString().Contains(SearchName, StringComparison.OrdinalIgnoreCase)
            || x.BuyerName.ToString().Contains(SearchName, StringComparison.OrdinalIgnoreCase);
    }
}

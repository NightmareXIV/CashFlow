using CashFlow.Data.SqlDescriptors;
using ECommons.ExcelServices;
using NightmareUI.Censoring;

namespace CashFlow.Gui.BaseTabs;
public unsafe class TabShopPurchases : BaseTab<ShopPurchaseSqlDescriptor>
{
    public Dictionary<uint, long> ItemValues = [];
    public override string SearchNameHint { get; } = "Search Player's/Retainer's Name...";
    public override void DrawTable()
    {
        if(ImGuiEx.BeginDefaultTable(["Your Character", "Retainer", "Paid", "~Item", "##qty", "Date", "##actions"], extraFlags: ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate))
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
                ImGuiEx.Text(Censor.Character(t.RetainerName));

                ImGui.TableNextColumn();
                Utils.DrawGilDecrease();
                ImGuiEx.Text($" {(int)(t.Price * t.Quantity * (t.IsMannequinnBool?1f:1.05f)):N0}");
                if(t.IsMannequinnBool)
                {
                    ImGui.SameLine(0, 2);
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGuiEx.Text("\uf183");
                    ImGui.PopFont();
                    ImGuiEx.Tooltip("This purchase was done via Mannequinn");
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

    public override void AddData(ShopPurchaseSqlDescriptor data, List<ShopPurchaseSqlDescriptor> list)
    {
        if(C.Blacklist.Contains(data.CidUlong)) return;
        if(C.DisplayExclusionsShopPurchasesNormal.Contains(data.CidUlong) && !data.IsMannequinnBool) return;
        if(C.DisplayExclusionsShopPurchasesMannequinn.Contains(data.CidUlong) && data.IsMannequinnBool) return;
        var id = (uint)data.Item % 1_000_000u;
        if(!ItemValues.ContainsKey(id)) ItemValues[id] = 0;
        ItemValues[id] += data.Price * data.Quantity;
        base.AddData(data, list);
    }

    public override List<ShopPurchaseSqlDescriptor> LoadData()
    {
        ItemValues.Clear();
        return P.DataProvider.GetShopPurchases();
    }

    public override bool ProcessSearchByItem(ShopPurchaseSqlDescriptor x)
    {
        return ExcelItemHelper.GetName((uint)(x.Item % 1000000), true).Contains(SearchItem, StringComparison.OrdinalIgnoreCase);
    }

    public override bool ProcessSearchByName(ShopPurchaseSqlDescriptor x)
    {
        var name = S.MainWindow.CIDMap.SafeSelect(x.CidUlong);
        return name.ToString().Contains(SearchName, StringComparison.OrdinalIgnoreCase)
            || x.RetainerName.ToString().Contains(SearchName, StringComparison.OrdinalIgnoreCase)
            || x.RetainerName.ToString().Contains(SearchName, StringComparison.OrdinalIgnoreCase);
    }
}

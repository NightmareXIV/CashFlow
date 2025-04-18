﻿using CashFlow.Data.SqlDescriptors;
using ECommons.ExcelServices;
using NightmareUI.Censoring;

namespace CashFlow.Gui.BaseTabs;
public unsafe class TabShopPurchases : BaseTab<ShopPurchaseSqlDescriptor>
{
    public override string SearchNameHint { get; } = "Search Player's/Retainer's Name...";
    public override void DrawTable()
    {
        if(ImGuiEx.BeginDefaultTable(["Your Character", "Retainer", "Paid", "~Item", "##qty", "Date"]))
        {
            for(int i = IndexBegin; i < IndexEnd; i++)
            {
                var t = Data[i];
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                {
                    ImGuiEx.Text(S.MainWindow.CIDMap.TryGetValue(t.CidUlong, out var s) ? Censor.Character(s.ToString()) : Censor.Hide($"{t.CidUlong:X16}"));
                }
                ImGui.TableNextColumn();
                ImGuiEx.Text(Censor.Character(t.RetainerName));

                ImGui.TableNextColumn();
                Utils.DrawGilDecrease();
                ImGuiEx.Text($" {(int)(t.Price * t.Quantity * 1.05f):N0}");
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
                ImGuiEx.Text(DateTimeOffset.FromUnixTimeMilliseconds(t.UnixTime).ToLocalTime().ToString());

            }
            ImGui.EndTable();
        }
    }

    public override List<ShopPurchaseSqlDescriptor> LoadData()
    {
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

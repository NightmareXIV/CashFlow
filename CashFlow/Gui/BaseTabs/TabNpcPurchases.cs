﻿using CashFlow.Data.SqlDescriptors;
using ECommons.ExcelServices;
using NightmareUI.Censoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CashFlow.Gui.BaseTabs;
public unsafe class TabNpcPurchases : BaseTab<NpcPurchaseSqlDescriptor>
{
    public override void DrawTable()
    {
        if(ImGuiEx.BeginDefaultTable(["Your Character", "Paid", "~Item Name", "Qty", "Date"]))
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
                ImGuiEx.Text(DateTimeOffset.FromUnixTimeMilliseconds(t.UnixTime).ToLocalTime().ToString());
            }
            ImGui.EndTable();
        }
    }

    public override List<NpcPurchaseSqlDescriptor> LoadData()
    {
        return P.DataProvider.GetNpcPurchases();
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

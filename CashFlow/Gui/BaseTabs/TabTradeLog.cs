using CashFlow.Data.LegacyDescriptors;
using ECommons.ExcelServices;
using NightmareUI.Censoring;

namespace CashFlow.Gui;
public unsafe class TabTradeLog : BaseTab<TradeDescriptor>
{

    public Dictionary<ulong, Dictionary<ulong, long>> CharasGivenGilTo = [];
    public Dictionary<ulong, Dictionary<ulong, long>> CharasRecvGilFrom = [];
    public Dictionary<ulong, long> CharasGivenGilToTotal = [];
    public Dictionary<ulong, long> CharasRecvGilFromTotal = [];
    public bool OnlyGil = false;
    private string[] OnlyGilHeaders = ["Your Character", "Counterparty", "Date", "~Gil", "##actions"];
    private string[] NormalHeaders = ["Your Character", "Counterparty", "Date", "Gil", "~Items", "##actions"];
    public override void DrawExtraFilters()
    {
        ImGui.SameLine();
        if(ImGui.Checkbox("Only Gil", ref OnlyGil))
        {
            NeedsUpdate = true;
        }
    }

    public override List<TradeDescriptor> SortData(List<TradeDescriptor> data)
    {
        return SortColumn switch
        {
            0 => Order(data, x => S.MainWindow.CIDMap.SafeSelect(x.CidUlong).ToString()),
            1 => Order(data, x => S.MainWindow.CIDMap.SafeSelect(x.TradePartnerCID).ToString()),
            2 => Order(data, x => x.UnixTime),
            3 => Order(data, x => x.ReceivedGil),
            _ => data
        };
    }

    public override void DrawTable()
    {
        if(ImGuiEx.BeginDefaultTable(OnlyGil ? OnlyGilHeaders : NormalHeaders, extraFlags: ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate))
        {
            ImGuiCheckSorting();
            for(var i = IndexBegin; i < IndexEnd; i++)
            {
                var t = Data[i];
                if(!IsRowVisible(i)) continue;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                {
                    ImGuiEx.Text(S.MainWindow.CIDMap.TryGetValue(t.CidUlong, out var s) ? Censor.Character(s.ToString()) : Censor.Hide($"{t.TradePartnerCID:X16}"));
                }
                ImGui.TableNextColumn();
                {
                    ImGuiEx.Text(S.MainWindow.CIDMap.TryGetValue(t.TradePartnerCID, out var s) ? Censor.Character(s.ToString()) : Censor.Hide($"{t.TradePartnerCID:X16}"));
                }
                ImGui.TableNextColumn();
                ImGuiEx.Text(DateTimeOffset.FromUnixTimeMilliseconds(t.UnixTime).ToPreferredTimeString());
                ImGui.TableNextColumn();
                if(t.ReceivedGil > 0)
                {
                    Utils.DrawGilIncrease();
                    ImGuiEx.Text(EColor.Green, $" {Math.Abs(t.ReceivedGil):N0}");
                    ImGuiEx.Tooltip("Received gil");
                }
                if(t.ReceivedGil < 0)
                {
                    Utils.DrawGilDecrease();
                    ImGuiEx.Text(EColor.Red, $" {Math.Abs(t.ReceivedGil):N0}");
                    ImGuiEx.Tooltip("Gave gil");
                }
                if(!OnlyGil)
                {
                    ImGui.TableNextColumn();
                    if(t.ReceivedItems != null)
                    {
                        var itemsSent = t.ReceivedItems.Where(x => x.Quantity < 0);
                        var itemsReceived = t.ReceivedItems.Where(x => x.Quantity > 0);
                        if(itemsSent.Any())
                        {
                            if(ImGuiEx.BeginDefaultTable($"TradeGiven{t.ID}", ["##type", "~Item", "Qty"], false, ImGuiEx.DefaultTableFlags & ~ImGuiTableFlags.Borders | ImGuiTableFlags.BordersInnerV, true))
                            {
                                foreach(var x in itemsSent)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();
                                    Utils.DrawGilDecrease(false);
                                    ImGuiEx.Tooltip("Gave item");
                                    ImGui.TableNextColumn();
                                    ImGuiEx.Text(ExcelItemHelper.GetName(x.ItemID % 1000000));
                                    ImGui.TableNextColumn();
                                    ImGuiEx.Text($"x{Math.Abs(x.Quantity)}" + (x.ItemID > 1000000 ? "" : ""));
                                }
                                ImGui.EndTable();
                            }
                        }
                        if(itemsReceived.Any())
                        {
                            if(ImGuiEx.BeginDefaultTable($"TradeReceived{t.ID}", ["##type", "~Item", "Qty"], false, ImGuiEx.DefaultTableFlags & ~ImGuiTableFlags.Borders | ImGuiTableFlags.BordersInnerV, true))
                            {
                                foreach(var x in itemsReceived)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();
                                    Utils.DrawGilIncrease(false);
                                    ImGuiEx.Tooltip("Received item");
                                    ImGui.TableNextColumn();
                                    ImGuiEx.Text(ExcelItemHelper.GetName(x.ItemID % 1000000));
                                    ImGui.TableNextColumn();
                                    ImGuiEx.Text($"x{Math.Abs(x.Quantity)}" + (x.ItemID > 1000000 ? "" : ""));
                                }
                                ImGui.EndTable();
                            }
                        }
                    }
                }
                ImGui.TableNextColumn();
                DrawRowActions(i);
                DrawRowHighlight();
            }
            ImGui.EndTable();
        }
    }

    public override List<TradeDescriptor> LoadData()
    {
        if(SearchItem == "" && SearchName == "")
        {
            CharasGivenGilToTotal.Clear();
            CharasRecvGilFromTotal.Clear();
            CharasGivenGilTo.Clear();
            CharasRecvGilFrom.Clear();

        }
        return P.DataProvider.GetTrades(DateMin.ToUnixTimeMilliseconds(), DateMax.ToUnixTimeMilliseconds());
    }

    public override bool ProcessSearchByItem(TradeDescriptor x)
    {
        if(x.ReceivedItems == null) return false;
        return x.ReceivedItems != null && x.ReceivedItems.Select(s => ExcelItemHelper.GetName(s.ItemID % 1000000, true)).Append(x.ReceivedGil != 0 ? ExcelItemHelper.GetName(1) : "").Any(s => s.Contains(SearchItem, StringComparison.OrdinalIgnoreCase));
    }

    public override bool ProcessSearchByName(TradeDescriptor x)
    {
        var name = S.MainWindow.CIDMap.SafeSelect(x.TradePartnerCID);
        return name.ToString().Contains(SearchName, StringComparison.OrdinalIgnoreCase);
    }

    public override void AddData(TradeDescriptor x, List<TradeDescriptor> list)
    {
        if(C.Blacklist.Contains(x.CidUlong)) return;
        if(C.DisplayExclusionsTradeLog.Contains(x.CidUlong)) return;
        if(SearchItem == "" && SearchName == "")
        {
            if(x.ReceivedGil > 0)
            {
                var d = CharasRecvGilFrom.GetOrCreate(x.CidUlong);
                if(!d.ContainsKey(x.TradePartnerCID))
                {
                    d[x.TradePartnerCID] = x.ReceivedGil;
                }
                else
                {
                    d[x.TradePartnerCID] += x.ReceivedGil;
                }
                if(!CharasRecvGilFromTotal.ContainsKey(x.TradePartnerCID))
                {
                    CharasRecvGilFromTotal[x.TradePartnerCID] = x.ReceivedGil;
                }
                else
                {
                    CharasRecvGilFromTotal[x.TradePartnerCID] += x.ReceivedGil;
                }
            }
            if(x.ReceivedGil < 0)
            {
                var d = CharasGivenGilTo.GetOrCreate(x.CidUlong);
                if(!d.ContainsKey(x.TradePartnerCID))
                {
                    d[x.TradePartnerCID] = -x.ReceivedGil;
                }
                else
                {
                    d[x.TradePartnerCID] += -x.ReceivedGil;
                }
                if(!CharasGivenGilToTotal.ContainsKey(x.TradePartnerCID))
                {
                    CharasGivenGilToTotal[x.TradePartnerCID] = -x.ReceivedGil;
                }
                else
                {
                    CharasGivenGilToTotal[x.TradePartnerCID] += -x.ReceivedGil;
                }
            }
        }
        if(OnlyGil)
        {
            x.ReceivedItems = null;
            if(x.ReceivedGil == 0) return;
        }
        if(list.Count > 0 && C.MergeTrades)
        {
            var last = list[^1];
            var timeDiff = x.UnixTime - last.UnixTime;
            if(timeDiff < C.MergeTradeTreshold * 60 * 1000 && last.CidUlong == x.CidUlong && last.TradePartnerCID == x.TradePartnerCID && x.ReceivedItems.IsNullOr(s => s.Length == 0) && last.ReceivedItems.IsNullOr(s => s.Length == 0))
            {
                last.ReceivedGil += x.ReceivedGil;
                last.UnixTime = x.UnixTime;
                return;
            }
        }
        list.Add(x);
    }
}

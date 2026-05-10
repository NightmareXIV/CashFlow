using CashFlow.Data;
using CashFlow.Data.LegacyDescriptors;
using CashFlow.Data.SqlDescriptors;
using CashFlow.Gui.Components;
using ClosedXML.Excel;
using CsvHelper;
using ECommons.ExcelServices;
using ECommons.Throttlers;
using NightmareUI;
using NightmareUI.Censoring;
using System.Globalization;
using System.IO;

namespace CashFlow.Gui.BaseTabs;
public unsafe class TabDashboard
{
    public volatile bool NeedsUpdate = false;
    private uint LastFrame = 0;

    private DateTime DateMin = new(2025, 1, 1);
    private DateTime DateMax = new(DateTimeOffset.Now.Year + 1, 1, 1);
    private string DateMinStr = "";
    private string DateMaxStr = "";

    private long TotalFlipCost;
    private long TotalFlipSales;
    private long FlipProfit;
    private double GlobalRoi;
    private long TotalTaxPaid;
    private int DistinctItems;
    private int FullySoldItems;
    private long EstimatedRemainingValue;

    private List<FlipData> Flips = [];
    private List<RetainerStats> RetainerPerf = [];
    private List<ServerItemData> ServerInventory = [];

    private string FilterItem = "";
    private string FilterServerInv = "";
    private string FilterItemInv = "";

    private volatile bool IsLoading = false;

    public void Draw()
    {
        FrameThrottler.Throttle("UpdateBlocked", 2, true);
        if(IsLoading)
        {
            LastFrame = CSFramework.Instance()->FrameCounter;
            ImGuiEx.Text("Loading, please wait...");
            return;
        }
        DrawDateBar();
        if(CSFramework.Instance()->FrameCounter - LastFrame > 2) NeedsUpdate = true;
        LastFrame = CSFramework.Instance()->FrameCounter;
        if(NeedsUpdate)
        {
            LoadData();
            NeedsUpdate = false;
        }
        NuiTools.ButtonTabs("DashTabs", [[
            new("Résumé", DrawResume),
            new("Items", DrawItems),
            new("Par Serveur", DrawServerInventory),
            new("Retainers", DrawRetainers)
        ]], child: false);
    }

    private void DrawDateBar()
    {
        if(DateWidget.DatePickerWithInput("##dmin", 3, ref DateMinStr, ref DateMin, out var open1))
            NeedsUpdate = true;
        ImGui.SameLine(0, 1);
        ImGuiEx.Text("-");
        ImGui.SameLine(0, 1);
        if(DateWidget.DatePickerWithInput("##dmax", 4, ref DateMaxStr, ref DateMax, out var open2))
            NeedsUpdate = true;
        if(open1 || open2) NeedsUpdate = true;
        ImGui.SameLine();
        if(ImGui.SmallButton("7d")) { SetDateRange(7); }
        ImGui.SameLine(0, 2);
        if(ImGui.SmallButton("30d")) { SetDateRange(30); }
        ImGui.SameLine(0, 2);
        if(ImGui.SmallButton("90d")) { SetDateRange(90); }
        ImGui.SameLine(0, 2);
        if(ImGui.SmallButton("1y")) { SetDateRange(365); }
        ImGui.SameLine(0, 2);
        if(ImGui.SmallButton("All")) { SetDateRange(0); }
    }

    private void SetDateRange(int days)
    {
        var format = DateWidget.DateFormat;
        if(days == 0)
        {
            DateMin = new DateTime(2020, 1, 1);
            DateMax = DateTime.Now.AddDays(1);
        }
        else
        {
            DateMin = DateTime.Now.AddDays(-days);
            DateMax = DateTime.Now.AddDays(1);
        }
        DateMinStr = DateMin.ToString(format);
        DateMaxStr = DateMax.ToString(format);
        NeedsUpdate = true;
    }

    private void LoadData()
    {
        IsLoading = true;
        var dMin = DateMin.ToUnixTimeMilliseconds();
        var dMax = DateMax.ToUnixTimeMilliseconds();

        HashSet<long> hiddenPurchaseTimes = [];
        HashSet<long> hiddenSaleTimes = [];
        HashSet<long> hiddenTradeTimes = [];
        if(C.ExcludeHiddenFromFlipAnalytics)
        {
            var sp = S.MainWindow.TabShopPurchases;
            for(int i = 0; i < sp.Data.Count; i++)
                if(sp.HiddenIndices.Contains(i)) hiddenPurchaseTimes.Add(sp.Data[i].UnixTime);
            var rs = S.MainWindow.TabRetainerSales;
            for(int i = 0; i < rs.Data.Count; i++)
                if(rs.HiddenIndices.Contains(i)) hiddenSaleTimes.Add(rs.Data[i].UnixTime);
            var tl = S.MainWindow.TabTradeLog;
            for(int i = 0; i < tl.Data.Count; i++)
                if(tl.HiddenIndices.Contains(i)) hiddenTradeTimes.Add(tl.Data[i].UnixTime);
        }

        S.WorkerThread.ClearAndEnqueue(() =>
        {
            var trades = P.DataProvider.GetTrades(dMin, dMax);
            var retainerSales = P.DataProvider.GetRetainerHistory(dMin, dMax);
            var shopPurchases = P.DataProvider.GetShopPurchases(dMin, dMax);
            var cidMap = S.MainWindow.CIDMap;

            if(C.ExcludeHiddenFromFlipAnalytics)
            {
                shopPurchases = shopPurchases.Where(p => !hiddenPurchaseTimes.Contains(p.UnixTime)).ToList();
                retainerSales = retainerSales.Where(s => !hiddenSaleTimes.Contains(s.UnixTime)).ToList();
                trades = trades.Where(t => !hiddenTradeTimes.Contains(t.UnixTime)).ToList();
            }

            // Purchased item IDs — only these count for buy/resell analytics
            HashSet<uint> purchasedItemIds = [];
            Dictionary<uint, (long totalCost, long totalQty)> costByItem = [];
            long totalCost = 0;
            long taxTotal = 0;
            foreach(var p in shopPurchases)
            {
                if(C.Blacklist.Contains(p.CidUlong)) continue;
                var id = (uint)(p.Item % 1000000);
                purchasedItemIds.Add(id);
                var existing = costByItem.GetValueOrDefault(id);
                existing.totalCost += (long)p.Price * p.Quantity;
                existing.totalQty += p.Quantity;
                costByItem[id] = existing;
                totalCost += (long)(p.Price * p.Quantity * (p.IsMannequinnBool ? 1f : 1.05f));
                if(!p.IsMannequinnBool)
                    taxTotal += (long)(p.Price * p.Quantity * 0.05f);
            }

            // Filter retainer sales to only items we purchased
            var flipSales = retainerSales.Where(s => !C.Blacklist.Contains(s.CidUlong) && purchasedItemIds.Contains((uint)(s.ItemID % 1000000))).ToList();

            long totalSales = 0;
            foreach(var s in flipSales)
                totalSales += s.Price;

            // Items analytics
            Dictionary<uint, (long totalBuyPrice, long qtyBought, long totalSellPrice, long qtySold)> itemAgg = [];
            foreach(var p in shopPurchases)
            {
                if(C.Blacklist.Contains(p.CidUlong)) continue;
                var id = (uint)(p.Item % 1000000);
                var existing = itemAgg.GetValueOrDefault(id);
                existing.totalBuyPrice += (long)p.Price * p.Quantity;
                existing.qtyBought += p.Quantity;
                itemAgg[id] = existing;
            }
            foreach(var s in flipSales)
            {
                var id = (uint)(s.ItemID % 1000000);
                var existing = itemAgg.GetValueOrDefault(id);
                existing.totalSellPrice += s.Price;
                existing.qtySold += s.Quantity;
                itemAgg[id] = existing;
            }

            var flips = new List<FlipData>();
            int fullySold = 0;
            long estRemaining = 0;
            foreach(var kv in itemAgg)
            {
                if(kv.Value.qtyBought <= 0) continue;
                var avgBuy = (double)kv.Value.totalBuyPrice / kv.Value.qtyBought;
                var avgSell = kv.Value.qtySold > 0 ? (double)kv.Value.totalSellPrice / kv.Value.qtySold : 0;
                var margin = kv.Value.qtySold > 0 ? avgSell - avgBuy * 1.05 : 0;
                var roi = avgBuy > 0 && kv.Value.qtySold > 0 ? margin / (avgBuy * 1.05) * 100.0 : 0;
                var matchedQty = Math.Min(kv.Value.qtyBought, kv.Value.qtySold);
                var remaining = kv.Value.qtyBought - kv.Value.qtySold;
                if(remaining <= 0) fullySold++;
                if(remaining > 0 && avgSell > 0) estRemaining += (long)(remaining * avgSell);
                flips.Add(new()
                {
                    ItemId = kv.Key,
                    QtyBought = kv.Value.qtyBought,
                    QtySold = kv.Value.qtySold,
                    AvgBuyPrice = avgBuy,
                    AvgSellPrice = avgSell,
                    Margin = margin,
                    RoiPercent = roi,
                    TotalProfit = (long)(margin * matchedQty),
                    Remaining = remaining
                });
            }
            flips.Sort((a, b) => b.TotalProfit.CompareTo(a.TotalProfit));
            var profit = totalSales - totalCost;
            var globalRoi = totalCost > 0 ? (double)profit / totalCost * 100.0 : 0;

            // Retainer performance (filtered to flip items only)
            Dictionary<string, (long revenue, int count, Dictionary<uint, long> items)> retAgg = [];
            foreach(var s in flipSales)
            {
                if(!retAgg.TryGetValue(s.RetainerName, out var val))
                    val = (0, 0, []);
                val.revenue += s.Price;
                val.count++;
                var id = (uint)(s.ItemID % 1000000);
                val.items[id] = val.items.GetValueOrDefault(id) + s.Price;
                retAgg[s.RetainerName] = val;
            }
            var retPerf = new List<RetainerStats>();
            foreach(var kv in retAgg)
            {
                var topItem = kv.Value.items.Count > 0
                    ? kv.Value.items.OrderByDescending(x => x.Value).First().Key
                    : 0u;
                retPerf.Add(new()
                {
                    Name = kv.Key,
                    TotalRevenue = kv.Value.revenue,
                    SalesCount = kv.Value.count,
                    AvgSalePrice = kv.Value.count > 0 ? kv.Value.revenue / kv.Value.count : 0,
                    TopItemId = topItem
                });
            }
            retPerf.Sort((a, b) => b.TotalRevenue.CompareTo(a.TotalRevenue));

            // Server Inventory (uses trades to determine server, filtered to purchased items)
            Dictionary<(string server, uint itemId), long> sentByServerItem = [];
            foreach(var t in trades)
            {
                if(C.Blacklist.Contains(t.CidUlong)) continue;
                if(t.ReceivedItems == null) continue;
                foreach(var item in t.ReceivedItems)
                {
                    var id = item.ItemID % 1000000;
                    if(!purchasedItemIds.Contains(id)) continue;
                    if(item.Quantity < 0)
                    {
                        var server = cidMap.TryGetValue(t.TradePartnerCID, out var partner)
                            ? ExcelWorldHelper.GetName(partner.HomeWorld) : "Unknown";
                        var key = (server, id);
                        sentByServerItem[key] = sentByServerItem.GetValueOrDefault(key) + Math.Abs(item.Quantity);
                    }
                    else if(item.Quantity > 0)
                    {
                        var server = cidMap.TryGetValue(t.CidUlong, out var self)
                            ? ExcelWorldHelper.GetName(self.HomeWorld) : "Unknown";
                        var key = (server, id);
                        sentByServerItem[key] = sentByServerItem.GetValueOrDefault(key) + item.Quantity;
                    }
                }
            }

            Dictionary<(string server, uint itemId), (long totalRevenue, long qtySold)> salesByServerItem = [];
            foreach(var s in flipSales)
            {
                var server = cidMap.TryGetValue(s.CidUlong, out var owner)
                    ? ExcelWorldHelper.GetName(owner.HomeWorld) : "Unknown";
                var id = (uint)(s.ItemID % 1000000);
                var key = (server, id);
                var existing = salesByServerItem.GetValueOrDefault(key);
                existing.totalRevenue += s.Price;
                existing.qtySold += s.Quantity;
                salesByServerItem[key] = existing;
            }

            var allServerItemKeys = sentByServerItem.Keys.Union(salesByServerItem.Keys).Distinct();
            var serverInv = new List<ServerItemData>();
            foreach(var key in allServerItemKeys)
            {
                var qtySent = sentByServerItem.GetValueOrDefault(key);
                var (srvRevenue, srvQtySold) = salesByServerItem.GetValueOrDefault(key);
                var avgBuyPriceSrv = costByItem.TryGetValue(key.itemId, out var cost) && cost.totalQty > 0
                    ? (double)cost.totalCost / cost.totalQty : 0;
                var avgSellPriceSrv = srvQtySold > 0 ? (double)srvRevenue / srvQtySold : 0;
                var srvTotalCost = (long)(avgBuyPriceSrv * qtySent);
                var srvRemaining = qtySent - srvQtySold;
                serverInv.Add(new()
                {
                    Server = key.server,
                    ItemId = key.itemId,
                    QtyBought = cost.totalQty,
                    AvgBuyPrice = avgBuyPriceSrv,
                    QtySent = qtySent,
                    QtySold = srvQtySold,
                    AvgSellPrice = avgSellPriceSrv,
                    TotalRevenue = srvRevenue,
                    TotalCost = srvTotalCost,
                    GainLoss = srvRevenue - srvTotalCost,
                    Remaining = srvRemaining,
                    SellProgress = qtySent > 0 ? (double)srvQtySold / qtySent * 100.0 : 0
                });
            }
            serverInv.Sort((a, b) =>
            {
                var c = string.Compare(a.Server, b.Server, StringComparison.Ordinal);
                return c != 0 ? c : b.GainLoss.CompareTo(a.GainLoss);
            });

            Svc.Framework.RunOnFrameworkThread(() =>
            {
                TotalFlipCost = totalCost;
                TotalFlipSales = totalSales;
                FlipProfit = profit;
                GlobalRoi = globalRoi;
                TotalTaxPaid = taxTotal;
                DistinctItems = purchasedItemIds.Count;
                FullySoldItems = fullySold;
                EstimatedRemainingValue = estRemaining;
                Flips = flips;
                RetainerPerf = retPerf;
                ServerInventory = serverInv;
                IsLoading = false;
            }).Wait();
        });
    }

    // ── Résumé ──

    private void DrawResume()
    {
        if(ImGui.BeginTable("Resume", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableSetupColumn("Metric");
            ImGui.TableSetupColumn("Value");
            ImGui.TableHeadersRow();

            ResumeRow("Total Achats (MB)", -TotalFlipCost);
            ResumeRow("Total Ventes (items achetés)", TotalFlipSales);
            ResumeRow("Taxes MB Payées", -TotalTaxPaid);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Separator();
            ImGui.TableNextColumn(); ImGui.Separator();

            ResumeRow("Profit Net", FlipProfit);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiEx.Text("ROI Global");
            ImGui.TableNextColumn();
            var roiCol = GlobalRoi > 0 ? EColor.Green : GlobalRoi < 0 ? EColor.Red : (Vector4?)null;
            ImGuiEx.Text(roiCol, $"{GlobalRoi:F1}%");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Separator();
            ImGui.TableNextColumn(); ImGui.Separator();

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGuiEx.Text("Items Distincts Achetés");
            ImGui.TableNextColumn(); ImGuiEx.Text($"{DistinctItems}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGuiEx.Text("Items Entièrement Vendus");
            ImGui.TableNextColumn(); ImGuiEx.Text($"{FullySoldItems}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGuiEx.Text("Stock Restant (valeur estimée)");
            ImGui.TableNextColumn(); ImGuiEx.Text($"{EstimatedRemainingValue:N0}");

            ImGui.EndTable();
        }
    }

    private void ResumeRow(string label, long value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGuiEx.Text(label);
        ImGui.TableNextColumn();
        Utils.DrawColoredGilText(value);
    }

    // ── Items ──

    private void DrawItems()
    {
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##filterFlipItem", "Filter Item...", ref FilterItem, 50);
        ImGui.SameLine();
        if(ImGuiEx.IconButton(FontAwesomeIcon.FileCsv))
            S.CashflowFileDialogManager.Open(ExportItemsCsv);
        ImGuiEx.Tooltip("Export Items as CSV");
        ImGui.SameLine(0, 2);
        if(ImGuiEx.IconButton(FontAwesomeIcon.FileExcel))
            S.CashflowFileDialogManager.OpenXlsx(ExportItemsXlsx);
        ImGuiEx.Tooltip("Export Items as Excel (.xlsx)");

        if(Flips.Count == 0)
        {
            ImGuiEx.Text("No purchased items found for this period.");
            return;
        }
        var filtered = FilterItem == "" ? Flips : Flips.Where(f => ExcelItemHelper.GetName(f.ItemId).Contains(FilterItem, StringComparison.OrdinalIgnoreCase)).ToList();
        if(filtered.Count == 0) { ImGuiEx.Text("No results matching filter."); return; }

        if(ImGui.BeginTable("FlipItems", 9,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate | ImGuiTableFlags.ScrollX | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.None, 180);
            ImGui.TableSetupColumn("Bought", ImGuiTableColumnFlags.None, 55);
            ImGui.TableSetupColumn("Sold", ImGuiTableColumnFlags.None, 50);
            ImGui.TableSetupColumn("Avg Buy", ImGuiTableColumnFlags.None, 70);
            ImGui.TableSetupColumn("Avg Sell", ImGuiTableColumnFlags.None, 70);
            ImGui.TableSetupColumn("Margin", ImGuiTableColumnFlags.None, 75);
            ImGui.TableSetupColumn("ROI%", ImGuiTableColumnFlags.None, 55);
            ImGui.TableSetupColumn("Profit", ImGuiTableColumnFlags.None, 80);
            ImGui.TableSetupColumn("Left", ImGuiTableColumnFlags.None, 45);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            var data = SortFlips(filtered);
            foreach(var f in data)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.Text(ExcelItemHelper.GetName(f.ItemId));
                ImGui.TableNextColumn();
                ImGuiEx.Text($"x{f.QtyBought}");
                ImGui.TableNextColumn();
                ImGuiEx.Text($"x{f.QtySold}");
                ImGui.TableNextColumn();
                ImGuiEx.Text($"{f.AvgBuyPrice:N0}");
                ImGui.TableNextColumn();
                ImGuiEx.Text(f.QtySold > 0 ? $"{f.AvgSellPrice:N0}" : "-");
                ImGui.TableNextColumn();
                if(f.QtySold > 0) Utils.DrawColoredGilText((long)f.Margin);
                else ImGuiEx.Text("-");
                ImGui.TableNextColumn();
                if(f.QtySold > 0)
                {
                    var roiCol = f.RoiPercent > 0 ? EColor.Green : f.RoiPercent < 0 ? EColor.Red : (Vector4?)null;
                    ImGuiEx.Text(roiCol, $"{f.RoiPercent:F1}%");
                }
                else ImGuiEx.Text("-");
                ImGui.TableNextColumn();
                if(f.QtySold > 0) Utils.DrawColoredGilText(f.TotalProfit);
                else ImGuiEx.Text("-");
                ImGui.TableNextColumn();
                var leftCol = f.Remaining > 0 ? EColor.YellowBright : f.Remaining <= 0 ? EColor.Green : (Vector4?)null;
                ImGuiEx.Text(leftCol, $"x{Math.Max(0, f.Remaining)}");
            }
            ImGui.EndTable();
        }
    }

    private IEnumerable<FlipData> SortFlips(List<FlipData> source)
    {
        var specs = ImGui.TableGetSortSpecs();
        if(specs.SpecsDirty && specs.Handle != null && specs.SpecsCount > 0 && specs.Specs.SortDirection != ImGuiSortDirection.None)
        {
            var col = specs.Specs.ColumnIndex;
            var asc = specs.Specs.SortDirection == ImGuiSortDirection.Ascending;
            return col switch
            {
                0 => asc ? source.OrderBy(x => ExcelItemHelper.GetName(x.ItemId)) : source.OrderByDescending(x => ExcelItemHelper.GetName(x.ItemId)),
                1 => asc ? source.OrderBy(x => x.QtyBought) : source.OrderByDescending(x => x.QtyBought),
                2 => asc ? source.OrderBy(x => x.QtySold) : source.OrderByDescending(x => x.QtySold),
                3 => asc ? source.OrderBy(x => x.AvgBuyPrice) : source.OrderByDescending(x => x.AvgBuyPrice),
                4 => asc ? source.OrderBy(x => x.AvgSellPrice) : source.OrderByDescending(x => x.AvgSellPrice),
                5 => asc ? source.OrderBy(x => x.Margin) : source.OrderByDescending(x => x.Margin),
                6 => asc ? source.OrderBy(x => x.RoiPercent) : source.OrderByDescending(x => x.RoiPercent),
                7 => asc ? source.OrderBy(x => x.TotalProfit) : source.OrderByDescending(x => x.TotalProfit),
                8 => asc ? source.OrderBy(x => x.Remaining) : source.OrderByDescending(x => x.Remaining),
                _ => source
            };
        }
        return source;
    }

    // ── Retainers ──

    private void DrawRetainers()
    {
        if(RetainerPerf.Count == 0)
        {
            ImGuiEx.Text("No retainer resales found for this period.");
            return;
        }
        if(ImGuiEx.BeginDefaultTable("Retainers", ["Retainer", "~Revenue", "Sales", "~Avg Sale", "~Top Item"], false, ImGuiTableFlags.BordersInner, true))
        {
            foreach(var r in RetainerPerf)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.Text(Censor.Character(r.Name));
                ImGui.TableNextColumn();
                Utils.DrawColoredGilText(r.TotalRevenue);
                ImGui.TableNextColumn();
                ImGuiEx.Text($"{r.SalesCount}");
                ImGui.TableNextColumn();
                ImGuiEx.Text($"{r.AvgSalePrice:N0}");
                ImGui.TableNextColumn();
                ImGuiEx.Text(r.TopItemId > 0 ? ExcelItemHelper.GetName(r.TopItemId) : "-");
            }
            ImGui.EndTable();
        }
    }

    // ── Par Serveur ──

    private void DrawServerInventory()
    {
        if(ImGuiEx.IconButton(FontAwesomeIcon.FileCsv))
            S.CashflowFileDialogManager.Open(ExportServerInventoryCsv);
        ImGuiEx.Tooltip("Export as CSV");
        ImGui.SameLine(0, 2);
        if(ImGuiEx.IconButton(FontAwesomeIcon.FileExcel))
            S.CashflowFileDialogManager.OpenXlsx(ExportServerInventoryXlsx);
        ImGuiEx.Tooltip("Export as Excel (.xlsx)");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##filterSrv", "Filter Server...", ref FilterServerInv, 50);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##filterItmSrv", "Filter Item...", ref FilterItemInv, 50);

        if(ServerInventory.Count == 0)
        {
            ImGuiEx.Text("No cross-server trade data found for this period.");
            return;
        }
        var filtered = GetFilteredServerInventory();
        if(filtered.Count == 0) { ImGuiEx.Text("No results matching filters."); return; }

        if(ImGui.BeginTable("SrvInv", 12,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate | ImGuiTableFlags.ScrollX | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Server", ImGuiTableColumnFlags.None, 90);
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.None, 180);
            ImGui.TableSetupColumn("Bought", ImGuiTableColumnFlags.None, 55);
            ImGui.TableSetupColumn("Avg Buy", ImGuiTableColumnFlags.None, 70);
            ImGui.TableSetupColumn("Sent", ImGuiTableColumnFlags.None, 50);
            ImGui.TableSetupColumn("Sold", ImGuiTableColumnFlags.None, 50);
            ImGui.TableSetupColumn("Avg Sell", ImGuiTableColumnFlags.None, 70);
            ImGui.TableSetupColumn("Revenue", ImGuiTableColumnFlags.None, 80);
            ImGui.TableSetupColumn("Cost", ImGuiTableColumnFlags.None, 80);
            ImGui.TableSetupColumn("Gain/Loss", ImGuiTableColumnFlags.None, 85);
            ImGui.TableSetupColumn("Left", ImGuiTableColumnFlags.None, 45);
            ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.None, 60);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            var data = SortServerInventory(filtered);
            foreach(var si in data)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGuiEx.Text(si.Server);
                ImGui.TableNextColumn(); ImGuiEx.Text(ExcelItemHelper.GetName(si.ItemId));
                ImGui.TableNextColumn(); ImGuiEx.Text($"x{si.QtyBought}");
                ImGui.TableNextColumn(); ImGuiEx.Text($"{si.AvgBuyPrice:N0}");
                ImGui.TableNextColumn(); ImGuiEx.Text($"x{si.QtySent}");
                ImGui.TableNextColumn(); ImGuiEx.Text($"x{si.QtySold}");
                ImGui.TableNextColumn(); ImGuiEx.Text(si.QtySold > 0 ? $"{si.AvgSellPrice:N0}" : "-");
                ImGui.TableNextColumn();
                if(si.TotalRevenue > 0) Utils.DrawColoredGilText(si.TotalRevenue); else ImGuiEx.Text("-");
                ImGui.TableNextColumn(); ImGuiEx.Text($"{si.TotalCost:N0}");
                ImGui.TableNextColumn(); Utils.DrawColoredGilText(si.GainLoss);
                ImGui.TableNextColumn(); ImGuiEx.Text($"x{si.Remaining}");
                ImGui.TableNextColumn();
                var progCol = si.SellProgress >= 100 ? EColor.Green : si.SellProgress > 50 ? EColor.YellowBright : (Vector4?)null;
                ImGuiEx.Text(progCol, $"{si.SellProgress:F0}%");
            }
            ImGui.EndTable();
        }
    }

    private List<ServerItemData> GetFilteredServerInventory()
    {
        if(FilterServerInv == "" && FilterItemInv == "") return ServerInventory;
        return ServerInventory.Where(si =>
        {
            if(FilterServerInv != "" && !si.Server.Contains(FilterServerInv, StringComparison.OrdinalIgnoreCase)) return false;
            if(FilterItemInv != "" && !ExcelItemHelper.GetName(si.ItemId).Contains(FilterItemInv, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }).ToList();
    }

    private IEnumerable<ServerItemData> SortServerInventory(List<ServerItemData> source)
    {
        var specs = ImGui.TableGetSortSpecs();
        if(specs.SpecsDirty && specs.Handle != null && specs.SpecsCount > 0 && specs.Specs.SortDirection != ImGuiSortDirection.None)
        {
            var col = specs.Specs.ColumnIndex;
            var asc = specs.Specs.SortDirection == ImGuiSortDirection.Ascending;
            return col switch
            {
                0 => asc ? source.OrderBy(x => x.Server) : source.OrderByDescending(x => x.Server),
                1 => asc ? source.OrderBy(x => ExcelItemHelper.GetName(x.ItemId)) : source.OrderByDescending(x => ExcelItemHelper.GetName(x.ItemId)),
                2 => asc ? source.OrderBy(x => x.QtyBought) : source.OrderByDescending(x => x.QtyBought),
                3 => asc ? source.OrderBy(x => x.AvgBuyPrice) : source.OrderByDescending(x => x.AvgBuyPrice),
                4 => asc ? source.OrderBy(x => x.QtySent) : source.OrderByDescending(x => x.QtySent),
                5 => asc ? source.OrderBy(x => x.QtySold) : source.OrderByDescending(x => x.QtySold),
                6 => asc ? source.OrderBy(x => x.AvgSellPrice) : source.OrderByDescending(x => x.AvgSellPrice),
                7 => asc ? source.OrderBy(x => x.TotalRevenue) : source.OrderByDescending(x => x.TotalRevenue),
                8 => asc ? source.OrderBy(x => x.TotalCost) : source.OrderByDescending(x => x.TotalCost),
                9 => asc ? source.OrderBy(x => x.GainLoss) : source.OrderByDescending(x => x.GainLoss),
                10 => asc ? source.OrderBy(x => x.Remaining) : source.OrderByDescending(x => x.Remaining),
                11 => asc ? source.OrderBy(x => x.SellProgress) : source.OrderByDescending(x => x.SellProgress),
                _ => source
            };
        }
        return source;
    }

    // ── Exports ──

    private static readonly string[] ItemHeaders = ["Item", "Qty Bought", "Qty Sold", "Avg Buy Price", "Avg Sell Price", "Margin", "ROI%", "Total Profit", "Remaining"];
    private static readonly string[] ServerInvHeaders = ["Server", "Item", "Qty Bought", "Avg Buy Price", "Qty Sent", "Qty Sold", "Avg Sell Price", "Total Revenue", "Total Cost", "Gain/Loss", "Remaining", "Sell Progress %"];

    private void ExportItemsCsv(string pathToFile)
    {
        List<FlipData> data = [.. Flips];
        if(data.Count == 0) return;
        S.ThreadPool.Run(() =>
        {
            if(File.Exists(pathToFile)) DeleteFileToRecycleBin(pathToFile);
            using var fs = new FileStream(pathToFile, FileMode.Create);
            fs.Write([0xEF, 0xBB, 0xBF]);
            using var writer = new StreamWriter(fs, new System.Text.UTF8Encoding(false));
            using var csv = new CsvWriter(writer, CultureInfo.CurrentCulture);
            csv.WriteFields(ItemHeaders);
            foreach(var f in data)
            {
                csv.WriteFields([
                    ExcelItemHelper.GetName(f.ItemId), f.QtyBought.ToString(), f.QtySold.ToString(),
                    $"{f.AvgBuyPrice:F0}", $"{f.AvgSellPrice:F0}", $"{f.Margin:F0}",
                    $"{f.RoiPercent:F1}", f.TotalProfit.ToString(), f.Remaining.ToString()
                ]);
            }
            fs.Flush();
            Svc.Framework.RunOnFrameworkThread(() => Svc.Chat.Print($"[CashFlow] Items exported to {pathToFile}"));
        });
    }

    private void ExportItemsXlsx(string pathToFile)
    {
        List<FlipData> data = [.. Flips];
        if(data.Count == 0) return;
        S.ThreadPool.Run(() =>
        {
            if(File.Exists(pathToFile)) DeleteFileToRecycleBin(pathToFile);
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Items");
            for(int c = 0; c < ItemHeaders.Length; c++) ws.Cell(1, c + 1).Value = ItemHeaders[c];
            var hr = ws.Range(1, 1, 1, ItemHeaders.Length);
            hr.Style.Font.Bold = true; hr.Style.Fill.BackgroundColor = XLColor.FromArgb(0x4472C4); hr.Style.Font.FontColor = XLColor.White;
            hr.SetAutoFilter();
            int row = 2;
            foreach(var f in data)
            {
                ws.Cell(row, 1).Value = ExcelItemHelper.GetName(f.ItemId);
                ws.Cell(row, 2).Value = f.QtyBought;
                ws.Cell(row, 3).Value = f.QtySold;
                ws.Cell(row, 4).Value = (long)f.AvgBuyPrice;
                ws.Cell(row, 5).Value = (long)f.AvgSellPrice;
                ws.Cell(row, 6).Value = (long)f.Margin;
                ws.Cell(row, 7).Value = Math.Round(f.RoiPercent, 1);
                ws.Cell(row, 8).Value = f.TotalProfit;
                ws.Cell(row, 9).Value = f.Remaining;
                row++;
            }
            ws.SheetView.FreezeRows(1); ws.Columns().AdjustToContents();
            wb.SaveAs(pathToFile);
            Svc.Framework.RunOnFrameworkThread(() => Svc.Chat.Print($"[CashFlow] Items exported to {pathToFile}"));
        });
    }

    private void ExportServerInventoryCsv(string pathToFile)
    {
        List<ServerItemData> data = [.. ServerInventory];
        if(data.Count == 0) return;
        S.ThreadPool.Run(() =>
        {
            if(File.Exists(pathToFile)) DeleteFileToRecycleBin(pathToFile);
            using var fs = new FileStream(pathToFile, FileMode.Create);
            fs.Write([0xEF, 0xBB, 0xBF]);
            using var writer = new StreamWriter(fs, new System.Text.UTF8Encoding(false));
            using var csv = new CsvWriter(writer, CultureInfo.CurrentCulture);
            csv.WriteFields(ServerInvHeaders);
            foreach(var si in data)
            {
                csv.WriteFields([
                    si.Server, ExcelItemHelper.GetName(si.ItemId), si.QtyBought.ToString(),
                    $"{si.AvgBuyPrice:F0}", si.QtySent.ToString(), si.QtySold.ToString(),
                    $"{si.AvgSellPrice:F0}", si.TotalRevenue.ToString(), si.TotalCost.ToString(),
                    si.GainLoss.ToString(), si.Remaining.ToString(), $"{si.SellProgress:F1}"
                ]);
            }
            fs.Flush();
            Svc.Framework.RunOnFrameworkThread(() => Svc.Chat.Print($"[CashFlow] Server Inventory exported to {pathToFile}"));
        });
    }

    private void ExportServerInventoryXlsx(string pathToFile)
    {
        List<ServerItemData> data = [.. ServerInventory];
        if(data.Count == 0) return;
        S.ThreadPool.Run(() =>
        {
            if(File.Exists(pathToFile)) DeleteFileToRecycleBin(pathToFile);
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Server Inventory");
            for(int c = 0; c < ServerInvHeaders.Length; c++) ws.Cell(1, c + 1).Value = ServerInvHeaders[c];
            var hr = ws.Range(1, 1, 1, ServerInvHeaders.Length);
            hr.Style.Font.Bold = true; hr.Style.Fill.BackgroundColor = XLColor.FromArgb(0x4472C4); hr.Style.Font.FontColor = XLColor.White;
            hr.SetAutoFilter();
            int row = 2;
            foreach(var si in data)
            {
                ws.Cell(row, 1).Value = si.Server;
                ws.Cell(row, 2).Value = ExcelItemHelper.GetName(si.ItemId);
                ws.Cell(row, 3).Value = si.QtyBought;
                ws.Cell(row, 4).Value = (long)si.AvgBuyPrice;
                ws.Cell(row, 5).Value = si.QtySent;
                ws.Cell(row, 6).Value = si.QtySold;
                ws.Cell(row, 7).Value = (long)si.AvgSellPrice;
                ws.Cell(row, 8).Value = si.TotalRevenue;
                ws.Cell(row, 9).Value = si.TotalCost;
                ws.Cell(row, 10).Value = si.GainLoss;
                ws.Cell(row, 11).Value = si.Remaining;
                ws.Cell(row, 12).Value = Math.Round(si.SellProgress, 1);
                row++;
            }
            ws.SheetView.FreezeRows(1); ws.Columns().AdjustToContents();
            wb.SaveAs(pathToFile);
            Svc.Framework.RunOnFrameworkThread(() => Svc.Chat.Print($"[CashFlow] Server Inventory exported to {pathToFile}"));
        });
    }

    // ── Data classes ──

    private class FlipData
    {
        public uint ItemId;
        public long QtyBought, QtySold, Remaining;
        public double AvgBuyPrice, AvgSellPrice, Margin, RoiPercent;
        public long TotalProfit;
    }

    private class RetainerStats
    {
        public string Name;
        public long TotalRevenue, AvgSalePrice;
        public int SalesCount;
        public uint TopItemId;
    }

    private class ServerItemData
    {
        public string Server;
        public uint ItemId;
        public long QtyBought, QtySent, QtySold;
        public double AvgBuyPrice, AvgSellPrice;
        public long TotalRevenue, TotalCost, GainLoss, Remaining;
        public double SellProgress;
    }
}

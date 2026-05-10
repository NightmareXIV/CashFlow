using ECommons.Configuration;
using NightmareUI.Censoring;

namespace CashFlow.Data;
public class Configuration : IEzConfig
{
    public CensorConfig CensorConfig = new();
    public int PerPage = 1000;
    public bool MergeTrades = true;
    public int MergeTradeTreshold = 10;
    public int LastGilTradesMin = 30;
    public bool ShowTradeOverlay = false;
    public bool ReverseArrows = false;
    public bool ReverseDayMonth = true;
    public bool UseUTCTime = false;
    public bool UseCustomTimeFormat = false;
    public string CustomTimeFormat = "MM.dd.yyyy HH:mm";
    public Dictionary<ulong, long> CachedRetainerGil = [];
    public HashSet<ulong> DisplayExclusionsGilHistory = [];
    public HashSet<ulong> DisplayExclusionsNpcPurchases = [];
    public HashSet<ulong> DisplayExclusionsNpcSales = [];
    public HashSet<ulong> DisplayExclusionsRetainerSalesNormal = [];
    public HashSet<ulong> DisplayExclusionsRetainerSalesMannequinn = [];
    public HashSet<ulong> DisplayExclusionsShopPurchasesMannequinn = [];
    public HashSet<ulong> DisplayExclusionsShopPurchasesNormal = [];
    public HashSet<ulong> DisplayExclusionsTradeLog = [];
    public HashSet<ulong> Blacklist = [];
    public bool UseGraphStartDate = false;
    public long GraphStartDate = DateTimeOffset.Now.ToUnixTimeSeconds();
    public bool ExcludeHiddenFromFlipAnalytics = false;
}

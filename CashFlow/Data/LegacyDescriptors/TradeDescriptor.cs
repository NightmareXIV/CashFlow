using ECommons.ExcelServices;
using NightmareUI.Censoring;

namespace CashFlow.Data.LegacyDescriptors;

public class TradeDescriptor : IDescriptorBase
{
    internal string ID = Guid.NewGuid().ToString();
    public ulong CidUlong { get; set; }
    public ulong TradePartnerCID;
    public int ReceivedGil;
    public ItemWithQuantity[] ReceivedItems = [];
    public long UnixTime { get; set; }

    public TradeDescriptor()
    {
    }

    public override string ToString()
    {
        return $"""
            TradePartnerCID: {TradePartnerCID:X16},
            Gil: {ReceivedGil};
            Items: 
            {ReceivedItems?.Select(x => $"    {x}").Print("\n")}
            """;
    }

    public string[] GetCsvHeaders()
    {
        return ["Your Character", "Counterparty", "Date", "Gil", "Item", "Is HQ", "Quantity"];
    }

    public string[][] GetCsvExport()
    {
        var ret = new List<string[]>();
        var itemCount = ReceivedItems?.Length ?? 0;
        for(int i = 0; i < Math.Max(1, itemCount); i++)
        {
            var item = ReceivedItems?.SafeSelect(i);
            ret.Add([
            S.MainWindow.CIDMap.TryGetValue(CidUlong, out var s) ? Censor.Character(s.ToString()) : Censor.Hide($"{TradePartnerCID:X16}"),
            S.MainWindow.CIDMap.TryGetValue(TradePartnerCID, out var s2) ? Censor.Character(s2.ToString()) : Censor.Hide($"{TradePartnerCID:X16}"),
            DateTimeOffset.FromUnixTimeMilliseconds(UnixTime).ToPreferredTimeString(),
            (i == 0?ReceivedGil.ToString():""),
            item != null?ExcelItemHelper.GetName((uint)(item.ItemID% 1000000)):"",
            item != null? (item.ItemID > 1000000).ToString() :"",
            item != null?item.Quantity.ToString():""
            ]);
        }
        return [.. ret];
    }

    public unsafe (string table, Dictionary<string, object> conditions) GetDeleteKey()
    {
        var cid = TradePartnerCID;
        var self = CidUlong;
        return ("Trades", new() { ["Cid"] = *(long*)&cid, ["Self"] = *(long*)&self, ["UnixTime"] = UnixTime, ["Gil"] = ReceivedGil });
    }
}

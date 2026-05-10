using ECommons.ExcelServices;
using NightmareUI.Censoring;
using SqlKata;

namespace CashFlow.Data.SqlDescriptors;
public unsafe class NpcSaleSqlDescriptor : IDescriptorBase
{
    private long Cid { get; set; }
    public int Item { get; set; }
    public int Price { get; set; }
    public int Quantity { get; set; }
    public long UnixTime { get; set; }

    [Ignore]
    public ulong CidUlong
    {
        get
        {
            var cid = Cid;
            return *(ulong*)&cid;
        }
        set
        {
            Cid = *(long*)&value;
        }
    }

    public string[] GetCsvHeaders()
    {
        return ["Your Character", "Paid", "~Item Name", "Is HQ", "Qty", "Date"];
    }

    public string[][] GetCsvExport()
    {
        return [[
                S.MainWindow.CIDMap.TryGetValue(CidUlong, out var s) ? Censor.Character(s.ToString()) : Censor.Hide($"{CidUlong:X16}"),
                Price.ToString(),
                ExcelItemHelper.GetName((uint)(Item % 1000000)),
                (Item > 1000000).ToString(),
                Quantity.ToString(),
                DateTimeOffset.FromUnixTimeMilliseconds(UnixTime).ToPreferredTimeString()
                ]];
    }

    public unsafe (string table, Dictionary<string, object> conditions) GetDeleteKey()
    {
        var cid = CidUlong;
        return ("NpcSales", new() { ["Cid"] = *(long*)&cid, ["UnixTime"] = UnixTime, ["Item"] = Item, ["Price"] = Price });
    }
}

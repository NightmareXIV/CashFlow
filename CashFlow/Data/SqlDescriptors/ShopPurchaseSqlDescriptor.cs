using ECommons.ExcelServices;
using NightmareUI.Censoring;
using SqlKata;

namespace CashFlow.Data.SqlDescriptors;
public unsafe class ShopPurchaseSqlDescriptor : IDescriptorBase
{
    private long Cid { get; set; }
    public string RetainerName { get; set; }
    public int Item { get; set; }
    public int Price { get; set; }
    public int Quantity { get; set; }
    private int IsMannequinn { get; set; }
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


    [Ignore]
    internal bool IsMannequinnBool
    {
        get
        {
            return IsMannequinn != 0;
        }
        set
        {
            IsMannequinn = value ? 1 : 0;
        }
    }

    public string[] GetCsvHeaders()
    {
        return ["Your Character", "Retainer", "Is Mannequinn", "Paid", "Item", "Is HQ", "Quantity", "Date"];
    }

    public string[][] GetCsvExport()
    {
        return [[
                S.MainWindow.CIDMap.TryGetValue(CidUlong, out var s) ? Censor.Character(s.ToString()) : Censor.Hide($"{CidUlong:X16}"),
                RetainerName,
                IsMannequinnBool.ToString(),
                ((int)(Price * Quantity * (IsMannequinnBool?1f:1.05f))).ToString(),
                ExcelItemHelper.GetName((uint)(Item % 1000000)),
                (Item > 1000000).ToString(),
                Quantity.ToString(),
                DateTimeOffset.FromUnixTimeMilliseconds(UnixTime).ToPreferredTimeString(),
                ]];
    }

    public unsafe (string table, Dictionary<string, object> conditions) GetDeleteKey()
    {
        var cid = CidUlong;
        return ("ShopPurchases", new() { ["Cid"] = *(long*)&cid, ["UnixTime"] = UnixTime, ["Item"] = Item, ["Price"] = Price, ["Quantity"] = Quantity });
    }
}

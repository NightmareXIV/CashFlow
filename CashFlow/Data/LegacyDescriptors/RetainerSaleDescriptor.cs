using ECommons.ExcelServices;
using NightmareUI.Censoring;

namespace CashFlow.Data.LegacyDescriptors;
public class RetainerSaleDescriptor : IEquatable<RetainerSaleDescriptor>, IDescriptorBase
{
    public ulong CidUlong { get; set; }
    public string RetainerName;
    public int ItemID;
    public int Quantity;
    public int Price;
    public bool IsMannequinn;
    public string BuyerName;
    public long UnixTime { get; set; }

    public override bool Equals(object obj)
    {
        return Equals(obj as RetainerSaleDescriptor);
    }

    public bool Equals(RetainerSaleDescriptor other)
    {
        return other is not null &&
               CidUlong == other.CidUlong &&
               RetainerName == other.RetainerName &&
               ItemID == other.ItemID &&
               Quantity == other.Quantity &&
               Price == other.Price &&
               IsMannequinn == other.IsMannequinn &&
               BuyerName == other.BuyerName &&
               UnixTime == other.UnixTime;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CidUlong, RetainerName, ItemID, Quantity, Price, IsMannequinn, BuyerName, UnixTime);
    }

    public string[] GetCsvHeaders()
    {
        return ["Your Character", "Your Retainer", "Buyer", "Paid", "Is Mannequinn", "Item", "Quantity", "Is HQ", "Date"];
    }

    public string[][] GetCsvExport()
    {
        return [[
            S.MainWindow.CIDMap.TryGetValue(this.CidUlong, out var s) ? Censor.Character(s.ToString()) : Censor.Hide($"{this.CidUlong:X16}"),
            this.RetainerName,
            this.BuyerName,
            this.Price.ToString(),
            this.IsMannequinn.ToString(),
            ExcelItemHelper.GetName((uint)(this.ItemID % 1000000)),
            this.Quantity.ToString(),
            (this.ItemID > 1000000).ToString(),
            DateTimeOffset.FromUnixTimeMilliseconds(this.UnixTime).ToPreferredTimeString()
        ]];
    }

    public static bool operator ==(RetainerSaleDescriptor left, RetainerSaleDescriptor right)
    {
        return EqualityComparer<RetainerSaleDescriptor>.Default.Equals(left, right);
    }

    public static bool operator !=(RetainerSaleDescriptor left, RetainerSaleDescriptor right)
    {
        return !(left == right);
    }

    public unsafe (string table, Dictionary<string, object> conditions) GetDeleteKey()
    {
        var cid = CidUlong;
        return ("RetainerSales", new() { ["Cid"] = *(long*)&cid, ["UnixTime"] = UnixTime, ["Item"] = ItemID, ["Price"] = Price, ["Quantity"] = Quantity });
    }
}

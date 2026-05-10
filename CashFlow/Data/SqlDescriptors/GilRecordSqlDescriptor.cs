using SqlKata;

namespace CashFlow.Data.SqlDescriptors;
public unsafe class GilRecordSqlDescriptor : IDescriptorBase
{
    private long Cid { get; set; }
    public long GilPlayer { get; set; }
    public long GilRetainer { get; set; }
    public long UnixTime { get; set; }

    [Ignore] public long Diff { get; set; }

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

    public string[][] GetCsvExport()
    {
        return [[S.MainWindow.CIDMap.TryGetValue(this.CidUlong, out var s) ? s.ToString() : $"{this.CidUlong:X16}",
                $"{this.GilPlayer + this.GilRetainer}",
                $"{this.Diff}",
                DateTimeOffset.FromUnixTimeMilliseconds(this.UnixTime).ToPreferredTimeString()]];
    }

    public string[] GetCsvHeaders()
    {
        return ["Your Character", "Total Gil", "Diff", "Date"];
    }

    public unsafe (string table, Dictionary<string, object> conditions) GetDeleteKey()
    {
        var cid = CidUlong;
        return ("GilRecords", new() { ["Cid"] = *(long*)&cid, ["UnixTime"] = UnixTime, ["GilPlayer"] = GilPlayer, ["GilRetainer"] = GilRetainer });
    }
}
namespace CashFlow.Data;
public interface IDescriptorBase
{
    ulong CidUlong { get; }
    long UnixTime { get; set; }
    public string[] GetCsvHeaders();
    public string[][] GetCsvExport();
    public (string table, Dictionary<string, object> conditions) GetDeleteKey() => (null, null);
}

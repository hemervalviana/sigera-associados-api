namespace SigeraAssociados.Infrastructure.Systems;

public static class SigeraSystemCatalog
{
    private static readonly IReadOnlyDictionary<int, string> Databases =
        new Dictionary<int, string>
        {
            [1] = "PROASP_SIG",
            [2] = "SONASP_SIG",
            [3] = "ABSP_SIG",
            [4] = "GSP_SIG"
        };

    public static bool TryGetDatabase(int code, out string database)
    {
        return Databases.TryGetValue(code, out database!);
    }
}

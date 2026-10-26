namespace SigeraAssociados.Infrastructure.Systems;

public static class SigeraSystemCatalog
{
    /// <summary>
    /// Código usado na concessão de acesso para liberar todos os sistemas.
    /// Não é gravado: vira uma linha de acesso para cada sistema.
    /// </summary>
    public const int Todos = 0;

    /// <summary>
    /// Códigos dos sistemas existentes, em ordem.
    /// </summary>
    public static IReadOnlyList<int> Codigos { get; } = [1, 2, 3, 4];

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

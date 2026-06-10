namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>IDs estables para modo in-memory / tests (alineados a seed catalogs).</summary>
public static class IdentityDocumentTypes
{
    public static Guid ResolveTypeId(string documentTypeCode) =>
        documentTypeCode.ToUpperInvariant() switch
        {
            "CC" => Guid.Parse("01930301-0001-7001-8001-000000000001"),
            "NIT" => Guid.Parse("01930301-0001-7001-8001-000000000002"),
            "CE" => Guid.Parse("01930301-0001-7001-8001-000000000003"),
            _ => Guid.Parse("01930301-0001-7001-8001-000000000099"),
        };
}

namespace Momentum.Domain.Sync;

/// <summary>
/// IS-EMRI-o85-B (GOREV slice-3a D2 desenin birebir tekrari): pure, IO-less projection from
/// <see cref="EntityState"/> to the materialized "projects" row shape. Project's registry has no
/// int/date/Guid scalar (only name/color/isDeleted/pos), so <see cref="MalformedFields"/> is always
/// empty for the SCALAR channel -- TaskListProjection'daki ayni gerekcenin tekrari. `Members`
/// (IS-EMRI-o86-A §C2) IS-TIR bir istisna: eleman bir userId'dir, `Guid.TryParse` basarisiz olursa
/// o eleman ATLANIR ve "members" `MalformedFields`e girer (sessizce yutma yok).
/// </summary>
public sealed record ProjectProjection(
    Guid EntityId, string? Name, string? Color, bool IsDeleted, string? Pos,
    bool HasDeleteEditConflict, IReadOnlyList<string> MalformedFields, IReadOnlyList<Guid> Members)
{
    public static ProjectProjection From(Guid entityId, EntityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var malformed = new List<string>();

        var name = ProjectionFields.ReadText(state.Fields, "name");
        var color = ProjectionFields.ReadText(state.Fields, "color");
        // CHANNEL WARNING (mutant-16'nin muadili): pos bir Fractional -- YALNIZ state.Orders'ta yasar.
        // state.Fields'ten okumak (savunma amacli TryGetValue ile bile) sonsuza dek sessizce NULL doner.
        var pos = ProjectionFields.ReadText(state.Orders, "pos");

        // IS-EMRI-o86-A §C2: TaskProjection.Tags'in birebir deseni (state.TryGetSet + PresentElements),
        // tek fark eleman Guid.TryParse'tan GECMEK ZORUNDA -- basarisizsa satir YAZILMAZ, "members" tek
        // seferlik malformed isareti alir (FinalizeMalformed coklu basarisizligi tek girdiye indirger).
        var members = new List<Guid>();
        if (state.TryGetSet("members", out var memberSet))
        {
            foreach (var element in memberSet.PresentElements().OrderBy(e => e, StringComparer.Ordinal))
            {
                if (Guid.TryParse(element, out var userId))
                {
                    members.Add(userId);
                }
                else
                {
                    malformed.Add("members");
                }
            }
        }

        return new ProjectProjection(
            entityId, name, color, state.IsDeleted, pos, state.HasDeleteEditConflict,
            ProjectionFields.FinalizeMalformed(malformed), members);
    }
}

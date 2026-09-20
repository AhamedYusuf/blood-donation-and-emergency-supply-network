namespace BloodDonationNetwork.Application.Common;

// Standard red-cell donor→recipient compatibility table (ABO + Rh).
// O- donors are universal (compatible with every recipient); AB+
// recipients can accept from every donor type.
//
// Tech Doc §1.4 rule 5 leaves the compatible-vs-exact-match choice to the
// group and assigns the authoritative rule to the Eligibility Validation
// Agent (Student 1). This helper only widens Mode 1's search candidate
// pool to donors who could plausibly match — it does not replace that
// authoritative check, the same way Mode 2 dispatch's own server-side
// approval guard doesn't trust the Coordinator's in-memory state either.
public static class BloodCompatibility
{
    private static readonly Dictionary<string, string[]> CompatibleDonorTypes = new()
    {
        ["O-"] = new[] { "O-" },
        ["O+"] = new[] { "O-", "O+" },
        ["A-"] = new[] { "O-", "A-" },
        ["A+"] = new[] { "O-", "O+", "A-", "A+" },
        ["B-"] = new[] { "O-", "B-" },
        ["B+"] = new[] { "O-", "O+", "B-", "B+" },
        ["AB-"] = new[] { "O-", "A-", "B-", "AB-" },
        ["AB+"] = new[] { "O-", "O+", "A-", "A+", "B-", "B+", "AB-", "AB+" },
    };

    /// <summary>Donor blood types that can safely donate to a recipient of
    /// <paramref name="recipientBloodType"/>. Throws for an unrecognized
    /// blood type string rather than silently returning an empty/wrong
    /// set.</summary>
    public static IReadOnlyList<string> GetCompatibleDonorTypes(string recipientBloodType)
    {
        if (!CompatibleDonorTypes.TryGetValue(recipientBloodType, out var donors))
        {
            throw new ArgumentException(
                $"Unrecognized blood type: '{recipientBloodType}'", nameof(recipientBloodType));
        }

        return donors;
    }
}

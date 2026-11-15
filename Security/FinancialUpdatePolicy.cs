namespace GestaoFinanceira.Security;

public static class FinancialUpdatePolicy
{
    // Enabled by the authorized activation release. Requires the exact Development
    // database grants described in SECURITY_TESTING.md; mismatches fail closed.
    // No request, environment variable or configuration setting can override this gate.
    public static bool DevelopmentEnabled => true;

    public static bool AllowsUpdates(string environmentName) => environmentName == "Testing" ||
        (environmentName == "Development" && DevelopmentEnabled);
}

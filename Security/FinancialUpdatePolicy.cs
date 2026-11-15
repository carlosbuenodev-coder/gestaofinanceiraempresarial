namespace GestaoFinanceira.Security;

public static class FinancialUpdatePolicy
{
    // Kept off in this preparation release. Enabling requires a separately authorized
    // code release and the exact database grants described in SECURITY_TESTING.md.
    // No request, environment variable or configuration setting can override this gate.
    public static bool DevelopmentEnabled => false;

    public static bool AllowsUpdates(string environmentName) => environmentName == "Testing" ||
        (environmentName == "Development" && DevelopmentEnabled);
}

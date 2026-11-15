namespace GestaoFinanceira.Security;

public static class FinancialPaymentPolicy
{
    // Explicit activation; no request or configuration key can broaden the allowed environments.
    public static bool DevelopmentEnabled => true;

    public static bool AllowsPayments(string environmentName) => environmentName == "Testing" ||
        (environmentName == "Development" && DevelopmentEnabled);
}

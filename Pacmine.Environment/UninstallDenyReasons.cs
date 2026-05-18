namespace Pacmine.Environment;

public abstract record UninstallDenyReason(string DeniedPackageName);

public record NotExistDenyReason
(
    string DeniedPackageName
) : UninstallDenyReason(DeniedPackageName);

public record BreakDependDenyReason
(
    string DeniedPackageName,
    string DependedBy
) : UninstallDenyReason(DeniedPackageName);

// unused
public record KeptPackageDenyReason
(
    string DeniedPackageName
) : UninstallDenyReason(DeniedPackageName);
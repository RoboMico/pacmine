namespace Pacmine.Environment;

/// <summary>
/// Specifies the reason why a package was installed.
/// </summary>
public enum InstallReasons
{
    /// <summary>
    /// The package was explicitly requested by the user.
    /// </summary>
    Explicit,

    /// <summary>
    /// The package was installed as a dependency of another package.
    /// </summary>
    AsDependency,

    /// <summary>
    /// The package is an environment package.
    /// </summary>
    Environment
}

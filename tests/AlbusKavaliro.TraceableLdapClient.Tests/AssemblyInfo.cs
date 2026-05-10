using TUnit.Core;

// Disable parallel test execution across the entire test assembly to mitigate flakiness
// If you only wanted to constrain a subset, you could use [NotInParallel("LdapContainer")]
// on specific classes instead. Global attribute chosen for deterministic ordering.
[assembly: NotInParallel]

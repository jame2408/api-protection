using Xunit;

// BDD functional tests share one PostgreSQL container and reset it wholesale via
// Respawn in [AfterScenario] (TestHooks.cs). That reset model presumes scenarios run
// one at a time; xUnit's default cross-collection parallelism lets one feature's
// reset wipe another feature's freshly seeded data mid-scenario. Latent through
// Wave 1 (only one feature class had non-@ignore scenarios); surfaced the moment
// 02_RevokeKey came online. Serial execution aligns the runtime model with the
// shared-database architecture.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

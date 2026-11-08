using Xunit;

namespace AVEXOS.Backup.Tests;

[CollectionDefinition("BackupTests", DisableParallelization = true)]
public class BackupTestCollection : ICollectionFixture<BackupTestFixture> { }

public class BackupTestFixture { }

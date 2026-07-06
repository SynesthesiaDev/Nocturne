// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;

namespace Nocturne.Database.Migrations;

public interface IMigrationStrategy
{
    static IMigrationStrategy DeleteIfMigrationRequired() => new DeleteIfRequired();
    static Migration.Builder Migrations() => new Migration.Builder();

    internal class DeleteIfRequired : IMigrationStrategy;

    record Migration(IDictionary<int, Func<IByteBuffer, IByteBuffer>> Steps) : IMigrationStrategy
    {
        public class Builder
        {
            private readonly Dictionary<int, Func<IByteBuffer, IByteBuffer>> steps = [];

            public Builder Add(int fromVersion, Func<IByteBuffer, IByteBuffer> migration)
            {
                steps[fromVersion] = migration;
                return this;
            }

            public IMigrationStrategy Build() => new Migration(steps);
        }
    }
}



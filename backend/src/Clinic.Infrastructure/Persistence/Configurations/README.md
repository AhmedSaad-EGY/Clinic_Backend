# Entity configurations

Add one `IEntityTypeConfiguration<TEntity>` per entity. Keep table names, keys,
indexes, check constraints, precision, concurrency tokens, and delete behavior here.

Do not use cascade delete for financial, clinical, approval, or audit history.

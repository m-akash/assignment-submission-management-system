using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence;

/// <summary>
/// Maps CLR names to Postgres-idiomatic snake_case table/column names
/// (e.g. TeacherAssignment → teacher_assignments, DeadlineUtc → deadline_utc).
/// Applied globally in OnModelCreating.
/// </summary>
public static class SnakeCaseNamingPolicy
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Table name
            if (entity.GetTableName() is { } tableName)
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (var index in entity.GetIndexes())
            {
                if (index.GetDatabaseName() is { } indexName)
                {
                    index.SetDatabaseName(ToSnakeCase(indexName));
                }
            }

            foreach (var key in entity.GetKeys())
            {
                if (key.GetName() is { } keyName)
                {
                    key.SetName(ToSnakeCase(keyName));
                }
            }

            foreach (var fk in entity.GetForeignKeys())
            {
                fk.SetConstraintName(fk.GetConstraintName() is { } n ? ToSnakeCase(n) : null);
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        // EF Core conventional prefixes (PK_, FK_, IX_, AK_) come pre-formatted in
        // UPPER_CASE with underscores — preserve them verbatim so they stay readable
        // (e.g. "PK_users" → "pk_users", not "p_k_users").
        if (name.Length >= 3 && (name[..3] is "PK_" or "FK_" or "IX_" or "AK_"))
        {
            return name.ToLowerInvariant();
        }

        var builder = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && name[i - 1] is not ('_' or '.'))
                {
                    builder.Append('_');
                }
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}

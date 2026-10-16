using System.Data.Common;
using System.Globalization;

namespace Ged.Infrastructure.Sql;

/// <summary>Petits utilitaires ADO.NET. Seul du SQL standard est utilisé, compris par toutes les bases.</summary>
internal static class SqlExtensions
{
    public static DbCommand Command(this DbConnection connection, string sql, DbTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    public static DbCommand With(this DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
        return command;
    }

    public static long Count(this DbCommand command) => Convert.ToInt64(command.ExecuteScalar());

    // Les dates sont stockées en texte ISO 8601 UTC : même format partout, et l'ordre alphabétique = l'ordre chronologique.
    public static string ToDb(this DateTime value) => value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    public static string? ToDb(this DateTime? value) => value?.ToDb();

    public static string ToDb(this Guid value) => value.ToString("D");

    public static string? ToDb(this Guid? value) => value?.ToDb();

    public static DateTime ReadDate(this DbDataReader reader, string column)
        => DateTime.Parse(reader.GetString(reader.GetOrdinal(column)), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    public static DateTime? ReadNullableDate(this DbDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.ReadDate(column);

    // Certains drivers (MySQL) renvoient déjà un Guid pour une colonne CHAR(36), d'autres une chaîne.
    public static Guid ReadGuid(this DbDataReader reader, string column)
        => reader.GetValue(reader.GetOrdinal(column)) switch
        {
            Guid guid => guid,
            var value => Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!),
        };

    public static Guid? ReadNullableGuid(this DbDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.ReadGuid(column);

    public static string ReadString(this DbDataReader reader, string column) => reader.GetString(reader.GetOrdinal(column));

    public static long ReadLong(this DbDataReader reader, string column)
        => Convert.ToInt64(reader.GetValue(reader.GetOrdinal(column)));
}

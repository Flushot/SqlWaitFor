using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace SqlWaitFor.Util
{
    public static class DbUtils
    {
        /// <summary>
        /// Gets the named column and casts it to the specified type.
        /// If the cast fails or the type is a DBNull, it's casted to the default type.
        /// This makes things alot less messier in code when using the SqlDataReader.
        /// </summary>
        /// <typeparam name="T">Type to cast result to.</typeparam>
        /// <param name="name">Column name.</param>
        /// <param name="reader">SqlDataReader to query.</param>
        /// <returns>Casted value.</returns>
        public static T GetCol<T>(String name, SqlDataReader reader)
        {
            var value = reader[name];

            // To ensure consistency, treat DBNull and null the same.
            if (value == null || value == DBNull.Value)
                return default(T);

            try
            {
                // Cast the value.
                return (T)value;
            }
            catch (InvalidCastException)
            {
                // Typecast failed. Use the default.
                return default(T);
            }
        }

        /// <summary>
        /// Gets Server Process ID (SPID) for given connection.
        /// </summary>
        /// <param name="conn">Connection</param>
        /// <returns>SPID for connection</returns>
        public static int GetSPID(SqlConnection conn)
        {
            var cmd = new SqlCommand("select @@spid", conn);
            return (Int16)cmd.ExecuteScalar();
        }
    }
}
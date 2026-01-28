/*
* Licensed to the Apache Software Foundation (ASF) under one or more
* contributor license agreements.  See the NOTICE file distributed with
* this work for additional information regarding copyright ownership.
* The ASF licenses this file to You under the Apache License, Version 2.0
* (the "License"); you may not use this file except in compliance with
* the License.  You may obtain a copy of the License at
*
*    http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Apache.Arrow.Adbc.Drivers.Apache.Hive2.Metadata
{
    /// <summary>
    /// Provides unified type mapping for converting database type names to XDBC (JDBC/ODBC) metadata.
    /// This class serves as a single source of truth for type mappings across HiveServer2 (Thrift)
    /// and StatementExecution API (REST) protocols.
    ///
    /// Internally uses <see cref="HiveServer2Connection.ColumnTypeId"/> enum and <see cref="SqlTypeNameParser"/>
    /// for consistent type mapping and parsing logic.
    /// </summary>
    public class ColumnTypeMapper
    {
        /// <summary>
        /// Maps base type names to XDBC (ODBC/JDBC) SQL type codes using ColumnTypeId enum values.
        /// Type codes follow the SQL/CLI specification (ISO/IEC 9075-3).
        /// </summary>
        private static readonly Dictionary<string, short> XdbcTypeCodes = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase)
        {
            // String types
            { "STRING", (short)HiveServer2Connection.ColumnTypeId.VARCHAR },
            { "VARCHAR", (short)HiveServer2Connection.ColumnTypeId.VARCHAR },
            { "CHAR", (short)HiveServer2Connection.ColumnTypeId.CHAR },

            // Integer types
            { "TINYINT", (short)HiveServer2Connection.ColumnTypeId.TINYINT },
            { "SMALLINT", (short)HiveServer2Connection.ColumnTypeId.SMALLINT },
            { "INT", (short)HiveServer2Connection.ColumnTypeId.INTEGER },
            { "INTEGER", (short)HiveServer2Connection.ColumnTypeId.INTEGER },
            { "BIGINT", (short)HiveServer2Connection.ColumnTypeId.BIGINT },

            // Floating-point types
            { "FLOAT", (short)HiveServer2Connection.ColumnTypeId.FLOAT },
            { "REAL", (short)HiveServer2Connection.ColumnTypeId.REAL },
            { "DOUBLE", (short)HiveServer2Connection.ColumnTypeId.DOUBLE },

            // Exact numeric types
            { "DECIMAL", (short)HiveServer2Connection.ColumnTypeId.DECIMAL },
            { "NUMERIC", (short)HiveServer2Connection.ColumnTypeId.NUMERIC },

            // Boolean type
            { "BOOLEAN", (short)HiveServer2Connection.ColumnTypeId.BOOLEAN },

            // Date/time types
            { "DATE", (short)HiveServer2Connection.ColumnTypeId.DATE },
            { "TIMESTAMP", (short)HiveServer2Connection.ColumnTypeId.TIMESTAMP },
            { "TIMESTAMP_NTZ", (short)HiveServer2Connection.ColumnTypeId.TIMESTAMP },

            // Binary types
            { "BINARY", (short)HiveServer2Connection.ColumnTypeId.BINARY },

            // Complex types
            { "ARRAY", (short)HiveServer2Connection.ColumnTypeId.ARRAY },
            { "STRUCT", (short)HiveServer2Connection.ColumnTypeId.STRUCT },
            { "MAP", (short)HiveServer2Connection.ColumnTypeId.JAVA_OBJECT },

            // Special types
            { "VARIANT", (short)HiveServer2Connection.ColumnTypeId.OTHER },
            { "VOID", (short)HiveServer2Connection.ColumnTypeId.NULL },
            { "INTERVAL YEAR TO MONTH", (short)HiveServer2Connection.ColumnTypeId.OTHER },
            { "INTERVAL DAY TO SECOND", (short)HiveServer2Connection.ColumnTypeId.OTHER },
            { "NULL", (short)HiveServer2Connection.ColumnTypeId.NULL }
        };

        /// <summary>
        /// Default column sizes for types when size is not specified.
        /// These values match the HiveServer2 protocol for ADBC parity.
        /// For numeric types, this represents the byte size of the type's binary representation.
        /// </summary>
        private static readonly Dictionary<string, int> DefaultColumnSizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "TINYINT", 1 },
            { "SMALLINT", 2 },
            { "INT", 4 },
            { "INTEGER", 4 },
            { "BIGINT", 8 },
            { "FLOAT", 4 },
            { "REAL", 4 },
            { "DOUBLE", 8 },
            { "DECIMAL", 38 },      // Default precision
            { "NUMERIC", 38 },
            { "BOOLEAN", 1 },
            { "DATE", 4 },
            { "TIMESTAMP", 8 },
            { "TIMESTAMP_NTZ", 8 },
            { "STRING", 2147483647 },    // Max int32
            { "VARCHAR", 2147483647 },
            { "CHAR", 2147483647 },
            { "BINARY", 0 },
            { "ARRAY", 0 },
            { "MAP", 0 },
            { "STRUCT", 0 },
            { "VARIANT", 0 },
            { "VOID", 1 }
        };

        /// <summary>
        /// Buffer lengths (in bytes) for binary representation of types.
        /// Used for the BUFFER_LENGTH metadata field.
        /// </summary>
        private static readonly Dictionary<string, int> BufferLengths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "TINYINT", 1 },
            { "SMALLINT", 2 },
            { "INT", 4 },
            { "INTEGER", 4 },
            { "BIGINT", 8 },
            { "FLOAT", 4 },
            { "REAL", 4 },
            { "DOUBLE", 8 },
            { "BOOLEAN", 1 }
        };

        /// <summary>
        /// Numeric precision radix for numeric types.
        /// All numeric types use base-10 to match HiveServer2 protocol.
        /// </summary>
        private const short DecimalRadix = 10;

        /// <summary>
        /// Extracts the base type name from a parameterized type string.
        /// Applies XDBC normalization for compatibility with HiveServer2 protocol.
        /// Examples:
        ///   "INT" -> "INTEGER" (XDBC normalization)
        ///   "DECIMAL(10,2)" -> "DECIMAL"
        ///   "VARCHAR(50)" -> "VARCHAR"
        ///   "ARRAY&lt;STRING&gt;" -> "ARRAY"
        ///   "STRUCT&lt;name: STRING, age: INT&gt;" -> "STRUCT"
        ///   "MAP&lt;STRING, INT&gt;" -> "MAP"
        ///   "TIMESTAMP_NTZ" -> "TIMESTAMP" (XDBC normalization)
        ///   "INTERVAL YEAR TO MONTH" -> "INTERVAL" (XDBC normalization)
        /// </summary>
        /// <param name="typeName">The full type name</param>
        /// <returns>The base type name without parameters, with XDBC normalization applied</returns>
        public string GetBaseTypeName(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return string.Empty;

            // Try using SqlTypeNameParser first for consistent parsing
            if (SqlTypeNameParser<SqlTypeNameParserResult>.TryParse(typeName, out SqlTypeNameParserResult? result, null) && result != null)
            {
                return result.BaseTypeName;
            }

            // Fallback: manual extraction for types not handled by parser
            // Check for angle brackets (complex types: ARRAY<T>, STRUCT<...>, MAP<K,V>)
            int angleIndex = typeName.IndexOf('<');
            if (angleIndex != -1)
            {
                return typeName.Substring(0, angleIndex).ToUpperInvariant();
            }

            // Check for parentheses (parameterized types: DECIMAL(p,s), VARCHAR(n), CHAR(n))
            int parenIndex = typeName.IndexOf('(');
            if (parenIndex != -1)
            {
                return typeName.Substring(0, parenIndex).ToUpperInvariant();
            }

            var baseType = typeName.ToUpperInvariant();

            // Apply XDBC normalization to match HiveServer2 behavior
            if (baseType == "TIMESTAMP_NTZ")
                return "TIMESTAMP";

            if (baseType.StartsWith("INTERVAL ", StringComparison.OrdinalIgnoreCase))
                return "INTERVAL";

            // Normalize INT to INTEGER to match HiveServer2
            if (baseType == "INT")
                return "INTEGER";

            return baseType;
        }

        /// <summary>
        /// Maps a type name to its XDBC (ODBC/JDBC) SQL type code.
        /// Returns null if the type is not recognized.
        /// </summary>
        /// <param name="typeName">The type name (e.g., "INT", "VARCHAR", "ARRAY")</param>
        /// <returns>The XDBC type code, or null if not found</returns>
        public short? GetXdbcDataType(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            // For INTERVAL types, check the full type name first before stripping
            var upperTypeName = typeName.ToUpperInvariant();
            if (upperTypeName.StartsWith("INTERVAL ", StringComparison.OrdinalIgnoreCase))
            {
                // Try exact match first for full INTERVAL type names
                if (XdbcTypeCodes.TryGetValue(upperTypeName, out var intervalCode))
                {
                    return intervalCode;
                }
            }

            var baseType = GetBaseTypeName(typeName);
            if (XdbcTypeCodes.TryGetValue(baseType, out var code))
            {
                return code;
            }

            return null; // Unknown type
        }

        /// <summary>
        /// Calculates the COLUMN_SIZE metadata field for a type.
        /// For numeric types (INT, BIGINT, FLOAT, DOUBLE, etc.), returns the byte size of the binary representation.
        /// For DECIMAL/NUMERIC types, returns the precision.
        /// For character types, returns the maximum character length.
        /// This matches the HiveServer2 protocol behavior for ADBC parity.
        /// </summary>
        /// <param name="typeName">The full type name (e.g., "DECIMAL(10,2)", "VARCHAR(100)")</param>
        /// <returns>The column size, or null if not applicable</returns>
        public int? GetColumnSize(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            // Special handling for INTERVAL types before normalization
            var upperTypeName = typeName.ToUpperInvariant();
            if (upperTypeName.StartsWith("INTERVAL ", StringComparison.OrdinalIgnoreCase))
            {
                if (upperTypeName.Contains("YEAR") || upperTypeName.Contains("MONTH"))
                    return 4; // INTERVAL YEAR TO MONTH
                if (upperTypeName.Contains("DAY") || upperTypeName.Contains("SECOND"))
                    return 8; // INTERVAL DAY TO SECOND
                return 4; // Default INTERVAL size
            }

            var baseType = GetBaseTypeName(typeName);

            // For DECIMAL(p,s), extract precision using SqlTypeNameParser
            if (baseType == "DECIMAL" || baseType == "NUMERIC")
            {
                var typeCode = GetXdbcDataType(typeName);
                if (SqlTypeNameParser<SqlDecimalParserResult>.TryParse(typeName, out SqlDecimalParserResult? result, typeCode) && result != null)
                {
                    return result.Precision;
                }

                if (DefaultColumnSizes.TryGetValue(baseType, out var decimalSize))
                    return decimalSize;
            }

            // For VARCHAR(n) or CHAR(n), extract length using SqlTypeNameParser
            if (baseType == "VARCHAR" || baseType == "CHAR" || baseType == "STRING")
            {
                var typeCode = GetXdbcDataType(typeName);
                if (SqlTypeNameParser<SqlCharVarcharParserResult>.TryParse(typeName, out SqlCharVarcharParserResult? result, typeCode) && result != null)
                {
                    return result.ColumnSize;
                }

                // Default length for string types
                if (DefaultColumnSizes.TryGetValue(baseType, out var stringSize))
                    return stringSize;
                return 65535;
            }

            // For other types, use default size
            if (DefaultColumnSizes.TryGetValue(baseType, out var size))
            {
                return size;
            }

            // For complex types or unknown types, return a large default
            return 2147483647; // Max int32
        }

        /// <summary>
        /// Calculates the BUFFER_LENGTH metadata field for a type.
        /// This is the maximum number of bytes required to store the binary representation.
        /// Returns null for types where buffer length is not applicable (e.g., strings, complex types).
        /// </summary>
        /// <param name="typeName">The type name</param>
        /// <returns>The buffer length in bytes, or null if not applicable</returns>
        public int? GetBufferLength(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            var baseType = GetBaseTypeName(typeName);

            if (BufferLengths.TryGetValue(baseType, out var length))
            {
                return length;
            }

            // For DECIMAL, buffer length is calculated based on precision
            if (baseType == "DECIMAL" || baseType == "NUMERIC")
            {
                var typeCode = GetXdbcDataType(typeName);
                if (SqlTypeNameParser<SqlDecimalParserResult>.TryParse(typeName, out SqlDecimalParserResult? result, typeCode) && result != null)
                {
                    // Approximate: 5 bytes per 9 digits + 1 byte overhead
                    return ((result.Precision + 8) / 9) * 5 + 1;
                }
                return 20; // Default for DECIMAL without specified precision
            }

            // Buffer length not applicable for strings, dates, and complex types
            return null;
        }

        /// <summary>
        /// Calculates the CHAR_OCTET_LENGTH metadata field for character types.
        /// This is the maximum number of bytes in the character representation.
        /// Returns null for non-character types.
        /// </summary>
        /// <param name="typeName">The full type name (e.g., "VARCHAR(100)", "CHAR(50)")</param>
        /// <returns>The character octet length, or null if not applicable</returns>
        public int? GetCharOctetLength(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            var baseType = GetBaseTypeName(typeName);

            // Only applicable for character types
            if (baseType != "STRING" && baseType != "VARCHAR" && baseType != "CHAR")
                return null;

            // Extract length from VARCHAR(n) or CHAR(n)
            var typeCode = GetXdbcDataType(typeName);
            if (SqlTypeNameParser<SqlCharVarcharParserResult>.TryParse(typeName, out SqlCharVarcharParserResult? result, typeCode) && result != null)
            {
                return result.ColumnSize;
            }

            // Default for unbounded strings
            if (DefaultColumnSizes.TryGetValue(baseType, out var defaultSize))
                return defaultSize;
            return 65535;
        }

        /// <summary>
        /// Extracts the DECIMAL_DIGITS (scale) metadata field for numeric types.
        /// For DECIMAL(p,s), this returns s (the number of digits after the decimal point).
        /// For integer types, returns 0.
        /// For floating-point types, returns the fractional precision (matches HiveServer2).
        /// For TIMESTAMP types, returns 6 (microsecond precision).
        /// For DATE type, returns 0.
        /// For other types (VARCHAR, CHAR, BINARY, ARRAY, etc.), returns null.
        /// </summary>
        /// <param name="typeName">The full type name (e.g., "DECIMAL(10,2)")</param>
        /// <returns>The decimal digits (scale), or null if not applicable</returns>
        public int? GetDecimalDigits(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            var baseType = GetBaseTypeName(typeName);

            // For DECIMAL(p,s), extract scale
            if (baseType == "DECIMAL" || baseType == "NUMERIC")
            {
                var typeCode = GetXdbcDataType(typeName);
                if (SqlTypeNameParser<SqlDecimalParserResult>.TryParse(typeName, out SqlDecimalParserResult? result, typeCode) && result != null)
                {
                    return result.Scale;
                }
                return 0; // Default scale is 0
            }

            // For integer types, scale is always 0
            if (baseType == "TINYINT" || baseType == "SMALLINT" ||
                baseType == "INT" || baseType == "INTEGER" || baseType == "BIGINT")
            {
                return 0;
            }

            // For floating-point types, return fractional precision (matches HiveServer2)
            if (baseType == "FLOAT" || baseType == "REAL")
            {
                return 7; // Single precision fractional digits
            }

            if (baseType == "DOUBLE")
            {
                return 15; // Double precision fractional digits
            }

            // For TIMESTAMP types, return fractional seconds precision (matches HiveServer2)
            if (baseType == "TIMESTAMP" || baseType == "TIMESTAMP_NTZ")
            {
                return 6; // Microsecond precision
            }

            // For DATE type, return 0 (matches HiveServer2)
            if (baseType == "DATE")
            {
                return 0;
            }

            // For all other types (STRING, CHAR, VARCHAR, BINARY, ARRAY, etc.), return null
            // This matches the HiveServer2 GetObjects behavior where types without decimal components have null scale
            return null;
        }

        /// <summary>
        /// Gets the NUM_PREC_RADIX metadata field for numeric types.
        /// Returns 10 for all numeric types to match HiveServer2 protocol.
        /// Returns null for non-numeric types.
        /// </summary>
        /// <param name="typeName">The type name</param>
        /// <returns>The numeric precision radix (10), or null if not applicable</returns>
        public short? GetNumPrecRadix(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            var baseType = GetBaseTypeName(typeName);

            // All numeric types use base-10 (matches HiveServer2)
            if (baseType == "TINYINT" || baseType == "SMALLINT" ||
                baseType == "INT" || baseType == "INTEGER" || baseType == "BIGINT" ||
                baseType == "DECIMAL" || baseType == "NUMERIC" ||
                baseType == "FLOAT" || baseType == "REAL" || baseType == "DOUBLE")
            {
                return DecimalRadix;
            }

            return null; // Not applicable for non-numeric types
        }

        /// <summary>
        /// Gets the SQL data type category for SQL_DATA_TYPE metadata field.
        /// This follows the ODBC specification for SQL_DATA_TYPE values.
        /// For most types, this is the same as the XDBC type code.
        /// Matches HiveServer2 behavior: returns the specific type code (e.g., 91 for DATE, 93 for TIMESTAMP).
        /// </summary>
        /// <param name="typeName">The type name</param>
        /// <returns>The SQL data type code, or null if not applicable</returns>
        public short? GetSqlDataType(string? typeName)
        {
            // SQL_DATA_TYPE is the same as DATA_TYPE (XDBC type code) for all types
            // This matches HiveServer2 behavior
            return GetXdbcDataType(typeName);
        }

        /// <summary>
        /// Gets the SQL datetime subtype for SQL_DATETIME_SUB metadata field.
        /// Only applicable for date/time types.
        /// Returns null to match HiveServer2 behavior (uses specific type codes in SQL_DATA_TYPE instead).
        /// </summary>
        /// <param name="typeName">The type name</param>
        /// <returns>The datetime subtype code, or null if not a datetime type</returns>
        public short? GetSqlDatetimeSub(string? typeName)
        {
            // HiveServer2 returns null for SQL_DATETIME_SUB because it uses
            // specific type codes (91 for DATE, 93 for TIMESTAMP) in SQL_DATA_TYPE
            // instead of the generic SQL_DATETIME (9) approach
            return null;
        }
    }
}

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

using System.Collections.Generic;

namespace Apache.Arrow.Adbc.Drivers.Apache.Hive2.Metadata
{
    /// <summary>
    /// Protocol-agnostic data model for column metadata.
    /// Used by both HiveServer2 (Thrift) and StatementExecution API (REST) protocols.
    /// Contains 24 fields: 23 standard fields plus BASE_TYPE_NAME (a Databricks/Spark extension).
    /// </summary>
    public class ColumnMetadataRecord
    {
        // Identity fields (4 fields)

        /// <summary>
        /// Catalog name (TABLE_CAT in statement-based metadata)
        /// </summary>
        public string? CatalogName { get; set; }

        /// <summary>
        /// Schema name (TABLE_SCHEM in statement-based metadata)
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// Table name (TABLE_NAME in statement-based metadata)
        /// </summary>
        public string? TableName { get; set; }

        /// <summary>
        /// Column name (COLUMN_NAME in statement-based metadata)
        /// </summary>
        public string? ColumnName { get; set; }

        // Type information fields (6 fields)

        /// <summary>
        /// SQL data type from java.sql.Types (DATA_TYPE in statement-based metadata)
        /// This is the XDBC type code (e.g., 4 for INTEGER, 12 for VARCHAR, 3 for DECIMAL)
        /// </summary>
        public int? XdbcDataType { get; set; }

        /// <summary>
        /// Data source-specific type name (TYPE_NAME in statement-based metadata)
        /// For example: "INT", "VARCHAR(100)", "DECIMAL(10,2)"
        /// </summary>
        public string? TypeName { get; set; }

        /// <summary>
        /// Column size (COLUMN_SIZE in statement-based metadata)
        /// For numeric types: byte size; For DECIMAL: precision; For character types: max length
        /// </summary>
        public int? XdbcColumnSize { get; set; }

        /// <summary>
        /// Transfer size of the data in bytes (BUFFER_LENGTH in statement-based metadata)
        /// Nullable for most types, only applicable for certain binary types
        /// </summary>
        public byte? BufferLength { get; set; }

        /// <summary>
        /// Number of fractional digits (DECIMAL_DIGITS in statement-based metadata)
        /// For DECIMAL: scale; For integer: 0; For float/double: fractional precision
        /// </summary>
        public int? XdbcDecimalDigits { get; set; }

        /// <summary>
        /// Radix for numeric precision (NUM_PREC_RADIX in statement-based metadata)
        /// Typically 10 for decimal, 2 for binary, or null for non-numeric types
        /// </summary>
        public int? XdbcNumPrecRadix { get; set; }

        // Nullability and constraints (3 fields)

        /// <summary>
        /// Is NULL allowed? (NULLABLE in statement-based metadata)
        /// 0 = no nulls, 1 = nullable, 2 = unknown
        /// </summary>
        public int? Nullable { get; set; }

        /// <summary>
        /// Explanatory comment on the column (REMARKS in statement-based metadata)
        /// </summary>
        public string? Remarks { get; set; }

        /// <summary>
        /// Default value for the column (COLUMN_DEF in statement-based metadata)
        /// </summary>
        public string? ColumnDefault { get; set; }

        // SQL type details (2 fields)

        /// <summary>
        /// SQL data type (SQL_DATA_TYPE in statement-based metadata)
        /// Similar to DATA_TYPE but from ODBC specification
        /// </summary>
        public int? SqlDataType { get; set; }

        /// <summary>
        /// SQL datetime subtype (SQL_DATETIME_SUB in statement-based metadata)
        /// Only for datetime types; null for most types
        /// </summary>
        public int? SqlDatetimeSub { get; set; }

        // Character and octet lengths (2 fields)

        /// <summary>
        /// Maximum length in octets for char types (CHAR_OCTET_LENGTH in statement-based metadata)
        /// Same as COLUMN_SIZE for single-byte character sets
        /// </summary>
        public int? XdbcCharOctetLength { get; set; }

        /// <summary>
        /// Index of column in table (ORDINAL_POSITION in statement-based metadata)
        /// First column is 1
        /// </summary>
        public int? OrdinalPosition { get; set; }

        // Nullability string representation (1 field)

        /// <summary>
        /// String representation of nullability (IS_NULLABLE in statement-based metadata)
        /// "YES", "NO", or "" (empty string for unknown)
        /// </summary>
        public string? IsNullable { get; set; }

        // Scope fields for REF types (3 fields - typically null)

        /// <summary>
        /// Catalog of table that is the scope of a reference attribute (SCOPE_CATALOG in statement-based metadata)
        /// Null if DATA_TYPE isn't REF
        /// </summary>
        public string? ScopeCatalog { get; set; }

        /// <summary>
        /// Schema of table that is the scope of a reference attribute (SCOPE_SCHEMA in statement-based metadata)
        /// Null if DATA_TYPE isn't REF
        /// </summary>
        public string? ScopeSchema { get; set; }

        /// <summary>
        /// Table name that is the scope of a reference attribute (SCOPE_TABLE in statement-based metadata)
        /// Null if DATA_TYPE isn't REF
        /// </summary>
        public string? ScopeTable { get; set; }

        // Auto-increment and source data type (2 fields)

        /// <summary>
        /// SQL data type from a distinct type, UDT, or REF column (SOURCE_DATA_TYPE in statement-based metadata)
        /// Null if DATA_TYPE isn't DISTINCT or structured type
        /// </summary>
        public short? SourceDataType { get; set; }

        /// <summary>
        /// Whether the column is auto-incrementing (IS_AUTO_INCREMENT in statement-based metadata)
        /// "YES", "NO", or "" (empty string if not determinable)
        /// </summary>
        public string? IsAutoIncrement { get; set; }

        // Extension field (1 field) - Databricks/Spark specific

        /// <summary>
        /// Base type name without parameters (BASE_TYPE_NAME in statement-based metadata)
        /// This is the 24th field, a Databricks/Spark extension.
        /// Examples: "DECIMAL" from "DECIMAL(10,2)", "VARCHAR" from "VARCHAR(100)", "INTEGER" from "INT"
        /// </summary>
        public string? BaseTypeName { get; set; }

        // Custom properties for vendor-specific extensions

        /// <summary>
        /// Custom properties dictionary for vendor-specific metadata extensions.
        /// For example, Delta Lake-specific fields like generation expressions.
        /// </summary>
        public Dictionary<string, object?>? CustomProperties { get; set; }

        /// <summary>
        /// Creates a new column metadata record with basic identity information.
        /// </summary>
        public ColumnMetadataRecord(
            string? catalogName,
            string? schemaName,
            string? tableName,
            string? columnName)
        {
            CatalogName = catalogName;
            SchemaName = schemaName;
            TableName = tableName;
            ColumnName = columnName;
        }
    }
}

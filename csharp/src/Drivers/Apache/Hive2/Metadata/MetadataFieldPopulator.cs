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

namespace Apache.Arrow.Adbc.Drivers.Apache.Hive2.Metadata
{
    /// <summary>
    /// Abstract base class for populating metadata records from protocol-specific data.
    /// Orchestrates field population for all metadata types using <see cref="ColumnTypeMapper"/>.
    /// Provides virtual extension points for vendor-specific customization.
    /// </summary>
    public class MetadataFieldPopulator
    {
        private readonly ColumnTypeMapper _columnTypeMapper;

        /// <summary>
        /// Creates a new metadata field populator with the default column type mapper.
        /// </summary>
        public MetadataFieldPopulator()
            : this(new ColumnTypeMapper())
        {
        }

        /// <summary>
        /// Creates a new metadata field populator with a custom column type mapper.
        /// </summary>
        /// <param name="columnTypeMapper">The column type mapper to use for type synthesis</param>
        public MetadataFieldPopulator(ColumnTypeMapper columnTypeMapper)
        {
            _columnTypeMapper = columnTypeMapper;
        }

        /// <summary>
        /// Populates a catalog metadata record.
        /// </summary>
        /// <param name="catalogName">The catalog name</param>
        /// <returns>A populated catalog metadata record</returns>
        public virtual CatalogMetadataRecord PopulateCatalogMetadata(string? catalogName)
        {
            return new CatalogMetadataRecord(catalogName);
        }

        /// <summary>
        /// Populates a schema metadata record.
        /// </summary>
        /// <param name="catalogName">The catalog name</param>
        /// <param name="schemaName">The schema name</param>
        /// <returns>A populated schema metadata record</returns>
        public virtual SchemaMetadataRecord PopulateSchemaMetadata(
            string? catalogName,
            string? schemaName)
        {
            return new SchemaMetadataRecord(catalogName, schemaName);
        }

        /// <summary>
        /// Populates a table metadata record.
        /// </summary>
        /// <param name="catalogName">The catalog name</param>
        /// <param name="schemaName">The schema name</param>
        /// <param name="tableName">The table name</param>
        /// <param name="tableType">The table type (e.g., "TABLE", "VIEW")</param>
        /// <param name="remarks">Optional explanatory comment</param>
        /// <param name="typeCatalog">Optional type catalog</param>
        /// <param name="typeSchema">Optional type schema</param>
        /// <param name="typeName">Optional type name</param>
        /// <param name="selfReferencingColName">Optional self-referencing column name</param>
        /// <param name="refGeneration">Optional reference generation method</param>
        /// <returns>A populated table metadata record</returns>
        public virtual TableMetadataRecord PopulateTableMetadata(
            string? catalogName,
            string? schemaName,
            string? tableName,
            string? tableType,
            string? remarks = null,
            string? typeCatalog = null,
            string? typeSchema = null,
            string? typeName = null,
            string? selfReferencingColName = null,
            string? refGeneration = null)
        {
            return new TableMetadataRecord(
                catalogName,
                schemaName,
                tableName,
                tableType,
                remarks,
                typeCatalog,
                typeSchema,
                typeName,
                selfReferencingColName,
                refGeneration);
        }

        /// <summary>
        /// Populates a column metadata record with all 24 fields.
        /// Uses <see cref="ColumnTypeMapper"/> to synthesize xdbc_* fields from the type name.
        /// </summary>
        /// <param name="catalogName">The catalog name</param>
        /// <param name="schemaName">The schema name</param>
        /// <param name="tableName">The table name</param>
        /// <param name="columnName">The column name</param>
        /// <param name="typeName">The full type name (e.g., "DECIMAL(10,2)", "VARCHAR(100)")</param>
        /// <param name="ordinalPosition">The column position (1-indexed)</param>
        /// <param name="isNullable">Whether the column is nullable</param>
        /// <param name="remarks">Optional explanatory comment</param>
        /// <param name="columnDefault">Optional default value</param>
        /// <param name="customData">Optional custom data for vendor-specific extensions</param>
        /// <returns>A fully populated column metadata record</returns>
        public virtual ColumnMetadataRecord PopulateColumnMetadata(
            string? catalogName,
            string? schemaName,
            string? tableName,
            string? columnName,
            string? typeName,
            int? ordinalPosition,
            bool? isNullable,
            string? remarks = null,
            string? columnDefault = null,
            object? customData = null)
        {
            var record = new ColumnMetadataRecord(catalogName, schemaName, tableName, columnName);

            // Basic identity and type information
            record.TypeName = typeName;
            record.OrdinalPosition = ordinalPosition;
            record.Remarks = remarks;
            record.ColumnDefault = columnDefault;

            // Synthesize XDBC fields using ColumnTypeMapper
            record.XdbcDataType = (int?)_columnTypeMapper.GetXdbcDataType(typeName);
            record.BaseTypeName = _columnTypeMapper.GetBaseTypeName(typeName);
            record.XdbcColumnSize = GetColumnSize(typeName);
            record.BufferLength = (byte?)_columnTypeMapper.GetBufferLength(typeName);
            record.XdbcDecimalDigits = GetDecimalDigits(typeName);
            record.XdbcNumPrecRadix = (int?)_columnTypeMapper.GetNumPrecRadix(typeName);
            record.XdbcCharOctetLength = GetCharOctetLength(typeName);

            // Nullability fields
            record.Nullable = isNullable.HasValue ? (isNullable.Value ? 1 : 0) : 2; // 0=no nulls, 1=nullable, 2=unknown
            record.IsNullable = isNullable.HasValue ? (isNullable.Value ? "YES" : "NO") : "";

            // SQL type fields (same as XDBC for most types)
            record.SqlDataType = (int?)_columnTypeMapper.GetSqlDataType(typeName);
            record.SqlDatetimeSub = (int?)_columnTypeMapper.GetSqlDatetimeSub(typeName);

            // Scope fields for REF types (typically null)
            record.ScopeCatalog = null;
            record.ScopeSchema = null;
            record.ScopeTable = null;

            // Source data type (null for non-distinct types)
            record.SourceDataType = null;

            // Auto-increment (typically empty string, meaning not determinable)
            record.IsAutoIncrement = "";

            // Allow derived classes to populate custom fields
            PopulateCustomFields(record, customData);

            return record;
        }

        /// <summary>
        /// Populates a primary key metadata record.
        /// </summary>
        /// <param name="catalogName">The catalog name</param>
        /// <param name="schemaName">The schema name</param>
        /// <param name="tableName">The table name</param>
        /// <param name="columnName">The column name</param>
        /// <param name="keySequence">The sequence number within the primary key (1-indexed)</param>
        /// <param name="primaryKeyName">Optional primary key name</param>
        /// <returns>A populated primary key metadata record</returns>
        public virtual PrimaryKeyMetadataRecord PopulatePrimaryKeyMetadata(
            string? catalogName,
            string? schemaName,
            string? tableName,
            string? columnName,
            int? keySequence,
            string? primaryKeyName = null)
        {
            return new PrimaryKeyMetadataRecord(
                catalogName,
                schemaName,
                tableName,
                columnName,
                keySequence,
                primaryKeyName);
        }

        /// <summary>
        /// Populates a foreign key metadata record.
        /// </summary>
        /// <param name="pkCatalogName">Primary key catalog name</param>
        /// <param name="pkSchemaName">Primary key schema name</param>
        /// <param name="pkTableName">Primary key table name</param>
        /// <param name="pkColumnName">Primary key column name</param>
        /// <param name="fkCatalogName">Foreign key catalog name</param>
        /// <param name="fkSchemaName">Foreign key schema name</param>
        /// <param name="fkTableName">Foreign key table name</param>
        /// <param name="fkColumnName">Foreign key column name</param>
        /// <param name="keySequence">Sequence number within the foreign key (1-indexed)</param>
        /// <param name="updateRule">Update rule (0=CASCADE, 1=RESTRICT, 2=SET NULL, 3=NO ACTION, 4=SET DEFAULT)</param>
        /// <param name="deleteRule">Delete rule (0=CASCADE, 1=RESTRICT, 2=SET NULL, 3=NO ACTION, 4=SET DEFAULT)</param>
        /// <param name="fkName">Optional foreign key name</param>
        /// <param name="pkName">Optional primary key name</param>
        /// <param name="deferrability">Deferrability (5=INITIALLY_DEFERRED, 6=INITIALLY_IMMEDIATE, 7=NOT_DEFERRABLE)</param>
        /// <returns>A populated foreign key metadata record</returns>
        public virtual ForeignKeyMetadataRecord PopulateForeignKeyMetadata(
            string? pkCatalogName,
            string? pkSchemaName,
            string? pkTableName,
            string? pkColumnName,
            string? fkCatalogName,
            string? fkSchemaName,
            string? fkTableName,
            string? fkColumnName,
            int? keySequence,
            int? updateRule = null,
            int? deleteRule = null,
            string? fkName = null,
            string? pkName = null,
            int? deferrability = null)
        {
            return new ForeignKeyMetadataRecord(
                pkCatalogName,
                pkSchemaName,
                pkTableName,
                pkColumnName,
                fkCatalogName,
                fkSchemaName,
                fkTableName,
                fkColumnName,
                keySequence,
                updateRule,
                deleteRule,
                fkName,
                pkName,
                deferrability);
        }

        /// <summary>
        /// Virtual extension point for populating vendor-specific custom fields on column metadata.
        /// Override in derived classes to add Delta Lake, Iceberg, or other vendor-specific fields.
        /// </summary>
        /// <param name="record">The column metadata record to populate</param>
        /// <param name="customData">Custom data from the vendor-specific protocol</param>
        protected virtual void PopulateCustomFields(ColumnMetadataRecord record, object? customData)
        {
            // Base implementation does nothing
            // Derived classes can override to populate CustomProperties dictionary
        }

        /// <summary>
        /// Virtual extension point for calculating column size.
        /// Override in derived classes for custom type handling.
        /// </summary>
        /// <param name="typeName">The type name</param>
        /// <returns>The column size</returns>
        protected virtual int? GetColumnSize(string? typeName)
        {
            return _columnTypeMapper.GetColumnSize(typeName);
        }

        /// <summary>
        /// Virtual extension point for calculating decimal digits (scale).
        /// Override in derived classes for custom type handling.
        /// </summary>
        /// <param name="typeName">The type name</param>
        /// <returns>The decimal digits (scale)</returns>
        protected virtual int? GetDecimalDigits(string? typeName)
        {
            return _columnTypeMapper.GetDecimalDigits(typeName);
        }

        /// <summary>
        /// Virtual extension point for calculating character octet length.
        /// Override in derived classes for custom type handling.
        /// </summary>
        /// <param name="typeName">The type name</param>
        /// <returns>The character octet length</returns>
        protected virtual int? GetCharOctetLength(string? typeName)
        {
            return _columnTypeMapper.GetCharOctetLength(typeName);
        }
    }
}

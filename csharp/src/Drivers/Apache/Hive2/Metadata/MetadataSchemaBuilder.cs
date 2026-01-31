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
using Apache.Arrow.Adbc.Extensions;
using Apache.Arrow.Types;

namespace Apache.Arrow.Adbc.Drivers.Apache.Hive2.Metadata
{
    /// <summary>
    /// Builds Arrow arrays and record batches from metadata record collections.
    /// Provides methods for both statement-based flat metadata (GetCatalogs, GetSchemas, etc.)
    /// and hierarchical GetObjects metadata (BuildColumnsStructArray).
    /// </summary>
    public static class MetadataSchemaBuilder
    {
        #region Flat Statement-Based Metadata Builders

        /// <summary>
        /// Builds a flat RecordBatch for GetCatalogs metadata (1 column: TABLE_CAT).
        /// </summary>
        /// <param name="records">Collection of catalog metadata records</param>
        /// <returns>A RecordBatch with catalog data</returns>
        public static RecordBatch BuildFlatCatalogsSchema(IEnumerable<CatalogMetadataRecord> records)
        {
            var tableCatBuilder = new StringArray.Builder();

            foreach (var record in records)
            {
                if (record.CatalogName != null)
                    tableCatBuilder.Append(record.CatalogName);
                else
                    tableCatBuilder.AppendNull();
            }

            var schema = new Schema(new[]
            {
                new Field("TABLE_CAT", StringType.Default, true)
            }, null);

            var arrays = new IArrowArray[]
            {
                tableCatBuilder.Build()
            };

            return new RecordBatch(schema, arrays, arrays[0].Length);
        }

        /// <summary>
        /// Builds a flat RecordBatch for GetSchemas metadata (2 columns: TABLE_SCHEM, TABLE_CATALOG).
        /// </summary>
        /// <param name="records">Collection of schema metadata records</param>
        /// <returns>A RecordBatch with schema data</returns>
        public static RecordBatch BuildFlatSchemasSchema(IEnumerable<SchemaMetadataRecord> records)
        {
            var tableSchemaBuilder = new StringArray.Builder();
            var tableCatalogBuilder = new StringArray.Builder();

            foreach (var record in records)
            {
                if (record.SchemaName != null)
                    tableSchemaBuilder.Append(record.SchemaName);
                else
                    tableSchemaBuilder.AppendNull();

                if (record.CatalogName != null)
                    tableCatalogBuilder.Append(record.CatalogName);
                else
                    tableCatalogBuilder.AppendNull();
            }

            var schema = new Schema(new[]
            {
                new Field("TABLE_SCHEM", StringType.Default, true),
                new Field("TABLE_CATALOG", StringType.Default, true)
            }, null);

            var arrays = new IArrowArray[]
            {
                tableSchemaBuilder.Build(),
                tableCatalogBuilder.Build()
            };

            return new RecordBatch(schema, arrays, arrays[0].Length);
        }

        /// <summary>
        /// Builds a flat RecordBatch for GetTables metadata (10 columns).
        /// </summary>
        /// <param name="records">Collection of table metadata records</param>
        /// <returns>A RecordBatch with table data</returns>
        public static RecordBatch BuildFlatTablesSchema(IEnumerable<TableMetadataRecord> records)
        {
            var tableCatBuilder = new StringArray.Builder();
            var tableSchemaBuilder = new StringArray.Builder();
            var tableNameBuilder = new StringArray.Builder();
            var tableTypeBuilder = new StringArray.Builder();
            var remarksBuilder = new StringArray.Builder();
            var typeCatBuilder = new StringArray.Builder();
            var typeSchemaBuilder = new StringArray.Builder();
            var typeNameBuilder = new StringArray.Builder();
            var selfReferencingColNameBuilder = new StringArray.Builder();
            var refGenerationBuilder = new StringArray.Builder();

            foreach (var record in records)
            {
                AppendOrNull(tableCatBuilder, record.CatalogName);
                AppendOrNull(tableSchemaBuilder, record.SchemaName);
                AppendOrNull(tableNameBuilder, record.TableName);
                AppendOrNull(tableTypeBuilder, record.TableType);
                AppendOrNull(remarksBuilder, record.Remarks);
                AppendOrNull(typeCatBuilder, record.TypeCatalog);
                AppendOrNull(typeSchemaBuilder, record.TypeSchema);
                AppendOrNull(typeNameBuilder, record.TypeName);
                AppendOrNull(selfReferencingColNameBuilder, record.SelfReferencingColName);
                AppendOrNull(refGenerationBuilder, record.RefGeneration);
            }

            var schema = new Schema(new[]
            {
                new Field("TABLE_CAT", StringType.Default, true),
                new Field("TABLE_SCHEM", StringType.Default, true),
                new Field("TABLE_NAME", StringType.Default, true),
                new Field("TABLE_TYPE", StringType.Default, true),
                new Field("REMARKS", StringType.Default, true),
                new Field("TYPE_CAT", StringType.Default, true),
                new Field("TYPE_SCHEM", StringType.Default, true),
                new Field("TYPE_NAME", StringType.Default, true),
                new Field("SELF_REFERENCING_COL_NAME", StringType.Default, true),
                new Field("REF_GENERATION", StringType.Default, true)
            }, null);

            var arrays = new IArrowArray[]
            {
                tableCatBuilder.Build(),
                tableSchemaBuilder.Build(),
                tableNameBuilder.Build(),
                tableTypeBuilder.Build(),
                remarksBuilder.Build(),
                typeCatBuilder.Build(),
                typeSchemaBuilder.Build(),
                typeNameBuilder.Build(),
                selfReferencingColNameBuilder.Build(),
                refGenerationBuilder.Build()
            };

            return new RecordBatch(schema, arrays, arrays[0].Length);
        }

        /// <summary>
        /// Builds a flat RecordBatch for GetColumns metadata (24 columns: 23 standard + BASE_TYPE_NAME).
        /// </summary>
        /// <param name="records">Collection of column metadata records</param>
        /// <returns>A RecordBatch with column data</returns>
        public static RecordBatch BuildFlatColumnsSchema(IEnumerable<ColumnMetadataRecord> records)
        {
            var tableCatBuilder = new StringArray.Builder();
            var tableSchemaBuilder = new StringArray.Builder();
            var tableNameBuilder = new StringArray.Builder();
            var columnNameBuilder = new StringArray.Builder();
            var dataTypeBuilder = new Int32Array.Builder();
            var typeNameBuilder = new StringArray.Builder();
            var columnSizeBuilder = new Int32Array.Builder();
            var bufferLengthBuilder = new Int8Array.Builder();
            var decimalDigitsBuilder = new Int32Array.Builder();
            var numPrecRadixBuilder = new Int32Array.Builder();
            var nullableBuilder = new Int32Array.Builder();
            var remarksBuilder = new StringArray.Builder();
            var columnDefBuilder = new StringArray.Builder();
            var sqlDataTypeBuilder = new Int32Array.Builder();
            var sqlDatetimeSubBuilder = new Int32Array.Builder();
            var charOctetLengthBuilder = new Int32Array.Builder();
            var ordinalPositionBuilder = new Int32Array.Builder();
            var isNullableBuilder = new StringArray.Builder();
            var scopeCatalogBuilder = new StringArray.Builder();
            var scopeSchemaBuilder = new StringArray.Builder();
            var scopeTableBuilder = new StringArray.Builder();
            var sourceDataTypeBuilder = new Int16Array.Builder();
            var isAutoIncrementBuilder = new StringArray.Builder();
            var baseTypeNameBuilder = new StringArray.Builder();

            foreach (var record in records)
            {
                AppendOrNull(tableCatBuilder, record.CatalogName);
                AppendOrNull(tableSchemaBuilder, record.SchemaName);
                AppendOrNull(tableNameBuilder, record.TableName);
                AppendOrNull(columnNameBuilder, record.ColumnName);
                AppendOrNull(dataTypeBuilder, record.XdbcDataType);
                AppendOrNull(typeNameBuilder, record.TypeName);
                AppendOrNull(columnSizeBuilder, record.XdbcColumnSize);
                AppendOrNull(bufferLengthBuilder, record.BufferLength);
                AppendOrNull(decimalDigitsBuilder, record.XdbcDecimalDigits);
                AppendOrNull(numPrecRadixBuilder, record.XdbcNumPrecRadix);
                AppendOrNull(nullableBuilder, record.Nullable);
                AppendOrNull(remarksBuilder, record.Remarks);
                AppendOrNull(columnDefBuilder, record.ColumnDefault);
                AppendOrNull(sqlDataTypeBuilder, record.SqlDataType);
                AppendOrNull(sqlDatetimeSubBuilder, record.SqlDatetimeSub);
                AppendOrNull(charOctetLengthBuilder, record.XdbcCharOctetLength);
                AppendOrNull(ordinalPositionBuilder, record.OrdinalPosition);
                AppendOrNull(isNullableBuilder, record.IsNullable);
                AppendOrNull(scopeCatalogBuilder, record.ScopeCatalog);
                AppendOrNull(scopeSchemaBuilder, record.ScopeSchema);
                AppendOrNull(scopeTableBuilder, record.ScopeTable);
                AppendOrNull(sourceDataTypeBuilder, record.SourceDataType);
                AppendOrNull(isAutoIncrementBuilder, record.IsAutoIncrement);
                AppendOrNull(baseTypeNameBuilder, record.BaseTypeName);
            }

            var schema = new Schema(new[]
            {
                new Field("TABLE_CAT", StringType.Default, true),
                new Field("TABLE_SCHEM", StringType.Default, true),
                new Field("TABLE_NAME", StringType.Default, true),
                new Field("COLUMN_NAME", StringType.Default, true),
                new Field("DATA_TYPE", Int32Type.Default, true),
                new Field("TYPE_NAME", StringType.Default, true),
                new Field("COLUMN_SIZE", Int32Type.Default, true),
                new Field("BUFFER_LENGTH", Int8Type.Default, true),
                new Field("DECIMAL_DIGITS", Int32Type.Default, true),
                new Field("NUM_PREC_RADIX", Int32Type.Default, true),
                new Field("NULLABLE", Int32Type.Default, true),
                new Field("REMARKS", StringType.Default, true),
                new Field("COLUMN_DEF", StringType.Default, true),
                new Field("SQL_DATA_TYPE", Int32Type.Default, true),
                new Field("SQL_DATETIME_SUB", Int32Type.Default, true),
                new Field("CHAR_OCTET_LENGTH", Int32Type.Default, true),
                new Field("ORDINAL_POSITION", Int32Type.Default, true),
                new Field("IS_NULLABLE", StringType.Default, true),
                new Field("SCOPE_CATALOG", StringType.Default, true),
                new Field("SCOPE_SCHEMA", StringType.Default, true),
                new Field("SCOPE_TABLE", StringType.Default, true),
                new Field("SOURCE_DATA_TYPE", Int16Type.Default, true),
                new Field("IS_AUTO_INCREMENT", StringType.Default, true),
                new Field("BASE_TYPE_NAME", StringType.Default, true)
            }, null);

            var arrays = new IArrowArray[]
            {
                tableCatBuilder.Build(),
                tableSchemaBuilder.Build(),
                tableNameBuilder.Build(),
                columnNameBuilder.Build(),
                dataTypeBuilder.Build(),
                typeNameBuilder.Build(),
                columnSizeBuilder.Build(),
                bufferLengthBuilder.Build(),
                decimalDigitsBuilder.Build(),
                numPrecRadixBuilder.Build(),
                nullableBuilder.Build(),
                remarksBuilder.Build(),
                columnDefBuilder.Build(),
                sqlDataTypeBuilder.Build(),
                sqlDatetimeSubBuilder.Build(),
                charOctetLengthBuilder.Build(),
                ordinalPositionBuilder.Build(),
                isNullableBuilder.Build(),
                scopeCatalogBuilder.Build(),
                scopeSchemaBuilder.Build(),
                scopeTableBuilder.Build(),
                sourceDataTypeBuilder.Build(),
                isAutoIncrementBuilder.Build(),
                baseTypeNameBuilder.Build()
            };

            return new RecordBatch(schema, arrays, arrays[0].Length);
        }

        /// <summary>
        /// Builds a flat RecordBatch for GetPrimaryKeys metadata (6 columns).
        /// </summary>
        /// <param name="records">Collection of primary key metadata records</param>
        /// <returns>A RecordBatch with primary key data</returns>
        public static RecordBatch BuildFlatPrimaryKeysSchema(IEnumerable<PrimaryKeyMetadataRecord> records)
        {
            var tableCatBuilder = new StringArray.Builder();
            var tableSchemaBuilder = new StringArray.Builder();
            var tableNameBuilder = new StringArray.Builder();
            var columnNameBuilder = new StringArray.Builder();
            var keySeqBuilder = new Int32Array.Builder();
            var pkNameBuilder = new StringArray.Builder();

            foreach (var record in records)
            {
                AppendOrNull(tableCatBuilder, record.CatalogName);
                AppendOrNull(tableSchemaBuilder, record.SchemaName);
                AppendOrNull(tableNameBuilder, record.TableName);
                AppendOrNull(columnNameBuilder, record.ColumnName);
                AppendOrNull(keySeqBuilder, record.KeySequence);
                AppendOrNull(pkNameBuilder, record.PrimaryKeyName);
            }

            var schema = new Schema(new[]
            {
                new Field("TABLE_CAT", StringType.Default, true),
                new Field("TABLE_SCHEM", StringType.Default, true),
                new Field("TABLE_NAME", StringType.Default, true),
                new Field("COLUMN_NAME", StringType.Default, true),
                new Field("KEQ_SEQ", Int32Type.Default, true),
                new Field("PK_NAME", StringType.Default, true)
            }, null);

            var arrays = new IArrowArray[]
            {
                tableCatBuilder.Build(),
                tableSchemaBuilder.Build(),
                tableNameBuilder.Build(),
                columnNameBuilder.Build(),
                keySeqBuilder.Build(),
                pkNameBuilder.Build()
            };

            return new RecordBatch(schema, arrays, arrays[0].Length);
        }

        /// <summary>
        /// Builds a flat RecordBatch for GetCrossReference (foreign keys) metadata (14 columns).
        /// </summary>
        /// <param name="records">Collection of foreign key metadata records</param>
        /// <returns>A RecordBatch with foreign key data</returns>
        public static RecordBatch BuildFlatForeignKeysSchema(IEnumerable<ForeignKeyMetadataRecord> records)
        {
            var pkTableCatBuilder = new StringArray.Builder();
            var pkTableSchemaBuilder = new StringArray.Builder();
            var pkTableNameBuilder = new StringArray.Builder();
            var pkColumnNameBuilder = new StringArray.Builder();
            var fkTableCatBuilder = new StringArray.Builder();
            var fkTableSchemaBuilder = new StringArray.Builder();
            var fkTableNameBuilder = new StringArray.Builder();
            var fkColumnNameBuilder = new StringArray.Builder();
            var keySeqBuilder = new Int32Array.Builder();
            var updateRuleBuilder = new Int32Array.Builder();
            var deleteRuleBuilder = new Int32Array.Builder();
            var fkNameBuilder = new StringArray.Builder();
            var pkNameBuilder = new StringArray.Builder();
            var deferrabilityBuilder = new Int32Array.Builder();

            foreach (var record in records)
            {
                AppendOrNull(pkTableCatBuilder, record.PkCatalogName);
                AppendOrNull(pkTableSchemaBuilder, record.PkSchemaName);
                AppendOrNull(pkTableNameBuilder, record.PkTableName);
                AppendOrNull(pkColumnNameBuilder, record.PkColumnName);
                AppendOrNull(fkTableCatBuilder, record.FkCatalogName);
                AppendOrNull(fkTableSchemaBuilder, record.FkSchemaName);
                AppendOrNull(fkTableNameBuilder, record.FkTableName);
                AppendOrNull(fkColumnNameBuilder, record.FkColumnName);
                AppendOrNull(keySeqBuilder, record.KeySequence);
                AppendOrNull(updateRuleBuilder, record.UpdateRule);
                AppendOrNull(deleteRuleBuilder, record.DeleteRule);
                AppendOrNull(fkNameBuilder, record.FkName);
                AppendOrNull(pkNameBuilder, record.PkName);
                AppendOrNull(deferrabilityBuilder, record.Deferrability);
            }

            var schema = new Schema(new[]
            {
                new Field("PKTABLE_CAT", StringType.Default, true),
                new Field("PKTABLE_SCHEM", StringType.Default, true),
                new Field("PKTABLE_NAME", StringType.Default, true),
                new Field("PKCOLUMN_NAME", StringType.Default, true),
                new Field("FKTABLE_CAT", StringType.Default, true),
                new Field("FKTABLE_SCHEM", StringType.Default, true),
                new Field("FKTABLE_NAME", StringType.Default, true),
                new Field("FKCOLUMN_NAME", StringType.Default, true),
                new Field("KEQ_SEQ", Int32Type.Default, true),
                new Field("UPDATE_RULE", Int32Type.Default, true),
                new Field("DELETE_RULE", Int32Type.Default, true),
                new Field("FK_NAME", StringType.Default, true),
                new Field("PK_NAME", StringType.Default, true),
                new Field("DEFERRABILITY", Int32Type.Default, true)
            }, null);

            var arrays = new IArrowArray[]
            {
                pkTableCatBuilder.Build(),
                pkTableSchemaBuilder.Build(),
                pkTableNameBuilder.Build(),
                pkColumnNameBuilder.Build(),
                fkTableCatBuilder.Build(),
                fkTableSchemaBuilder.Build(),
                fkTableNameBuilder.Build(),
                fkColumnNameBuilder.Build(),
                keySeqBuilder.Build(),
                updateRuleBuilder.Build(),
                deleteRuleBuilder.Build(),
                fkNameBuilder.Build(),
                pkNameBuilder.Build(),
                deferrabilityBuilder.Build()
            };

            return new RecordBatch(schema, arrays, arrays[0].Length);
        }

        #endregion

        #region GetObjects Hierarchical Metadata Builder

        /// <summary>
        /// Builds the table_columns StructArray for GetObjects hierarchical metadata (19 fields per ADBC spec).
        /// This builds ONLY the leaf-level column metadata portion, NOT the entire catalog→schema→table→column hierarchy.
        /// The full hierarchy assembly happens in GetObjects itself via nested dictionaries and ListArrays.
        /// </summary>
        /// <param name="records">Collection of column metadata records</param>
        /// <returns>A StructArray containing column metadata for a single table</returns>
        public static StructArray BuildColumnsStructArray(IEnumerable<ColumnMetadataRecord> records)
        {
            var columnNameBuilder = new StringArray.Builder();
            var ordinalPositionBuilder = new Int32Array.Builder();
            var remarksBuilder = new StringArray.Builder();
            var xdbcDataTypeBuilder = new Int16Array.Builder();
            var xdbcTypeNameBuilder = new StringArray.Builder();
            var xdbcColumnSizeBuilder = new Int32Array.Builder();
            var xdbcDecimalDigitsBuilder = new Int16Array.Builder();
            var xdbcNumPrecRadixBuilder = new Int16Array.Builder();
            var xdbcNullableBuilder = new Int16Array.Builder();
            var xdbcColumnDefBuilder = new StringArray.Builder();
            var xdbcSqlDataTypeBuilder = new Int16Array.Builder();
            var xdbcDatetimeSubBuilder = new Int16Array.Builder();
            var xdbcCharOctetLengthBuilder = new Int32Array.Builder();
            var xdbcIsNullableBuilder = new StringArray.Builder();
            var xdbcScopeCatalogBuilder = new StringArray.Builder();
            var xdbcScopeSchemaBuilder = new StringArray.Builder();
            var xdbcScopeTableBuilder = new StringArray.Builder();
            var xdbcIsAutoincrementBuilder = new BooleanArray.Builder();
            var xdbcIsGeneratedcolumnBuilder = new BooleanArray.Builder();

            var nullBitmapBuffer = new ArrowBuffer.BitmapBuilder();
            int length = 0;

            foreach (var record in records)
            {
                AppendOrNull(columnNameBuilder, record.ColumnName);
                AppendOrNull(ordinalPositionBuilder, record.OrdinalPosition);
                // For GetObjects, remarks field stores the original TypeName
                AppendOrNull(remarksBuilder, record.TypeName);
                AppendOrNull(xdbcDataTypeBuilder, (short?)record.XdbcDataType);
                // xdbcTypeName stores the base type name for GetObjects
                AppendOrNull(xdbcTypeNameBuilder, record.BaseTypeName);
                AppendOrNull(xdbcColumnSizeBuilder, record.XdbcColumnSize);
                AppendOrNull(xdbcDecimalDigitsBuilder, (short?)record.XdbcDecimalDigits);
                xdbcNumPrecRadixBuilder.AppendNull(); // Typically null in GetObjects
                AppendOrNull(xdbcNullableBuilder, (short?)record.Nullable);
                AppendOrNull(xdbcColumnDefBuilder, record.ColumnDefault);
                AppendOrNull(xdbcSqlDataTypeBuilder, (short?)record.SqlDataType);
                xdbcDatetimeSubBuilder.AppendNull(); // Typically null in GetObjects
                xdbcCharOctetLengthBuilder.AppendNull(); // Typically null in GetObjects
                AppendOrNull(xdbcIsNullableBuilder, record.IsNullable);
                xdbcScopeCatalogBuilder.AppendNull(); // Always null for REF types
                xdbcScopeSchemaBuilder.AppendNull(); // Always null for REF types
                xdbcScopeTableBuilder.AppendNull(); // Always null for REF types
                // IsAutoIncrement: Convert string "YES"/"NO"/"" to boolean
                var isAutoInc = record.IsAutoIncrement == "YES";
                xdbcIsAutoincrementBuilder.Append(isAutoInc);
                // IsGeneratedColumn: Typically true for all columns in GetObjects
                xdbcIsGeneratedcolumnBuilder.Append(true);

                nullBitmapBuffer.Append(true);
                length++;
            }

            // Define the 19-field schema for GetObjects column metadata
            var schema = StandardSchemas.ColumnSchema;

            var dataArrays = schema.Validate(new List<IArrowArray>
            {
                columnNameBuilder.Build(),
                ordinalPositionBuilder.Build(),
                remarksBuilder.Build(),
                xdbcDataTypeBuilder.Build(),
                xdbcTypeNameBuilder.Build(),
                xdbcColumnSizeBuilder.Build(),
                xdbcDecimalDigitsBuilder.Build(),
                xdbcNumPrecRadixBuilder.Build(),
                xdbcNullableBuilder.Build(),
                xdbcColumnDefBuilder.Build(),
                xdbcSqlDataTypeBuilder.Build(),
                xdbcDatetimeSubBuilder.Build(),
                xdbcCharOctetLengthBuilder.Build(),
                xdbcIsNullableBuilder.Build(),
                xdbcScopeCatalogBuilder.Build(),
                xdbcScopeSchemaBuilder.Build(),
                xdbcScopeTableBuilder.Build(),
                xdbcIsAutoincrementBuilder.Build(),
                xdbcIsGeneratedcolumnBuilder.Build()
            });

            return new StructArray(
                new StructType(schema),
                length,
                dataArrays,
                nullBitmapBuffer.Build());
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Appends a value or null to a StringArray.Builder.
        /// </summary>
        private static void AppendOrNull(StringArray.Builder builder, string? value)
        {
            if (value != null)
                builder.Append(value);
            else
                builder.AppendNull();
        }

        /// <summary>
        /// Appends a value or null to an Int32Array.Builder.
        /// </summary>
        private static void AppendOrNull(Int32Array.Builder builder, int? value)
        {
            if (value.HasValue)
                builder.Append(value.Value);
            else
                builder.AppendNull();
        }

        /// <summary>
        /// Appends a value or null to an Int16Array.Builder.
        /// </summary>
        private static void AppendOrNull(Int16Array.Builder builder, short? value)
        {
            if (value.HasValue)
                builder.Append(value.Value);
            else
                builder.AppendNull();
        }

        /// <summary>
        /// Appends a value or null to an Int8Array.Builder.
        /// </summary>
        private static void AppendOrNull(Int8Array.Builder builder, byte? value)
        {
            if (value.HasValue)
                builder.Append((sbyte)value.Value);
            else
                builder.AppendNull();
        }

        #endregion
    }
}

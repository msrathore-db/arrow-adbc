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
    /// Protocol-agnostic data model for table metadata.
    /// Supports multiple protocol implementations including REST and RPC-based protocols.
    /// Contains 10 fields as per ADBC specification.
    /// </summary>
    public class TableMetadataRecord
    {
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
        /// Table type (TABLE_TYPE in statement-based metadata)
        /// Typical values: "TABLE", "VIEW", "SYSTEM TABLE", "GLOBAL TEMPORARY", "LOCAL TEMPORARY", "ALIAS", "SYNONYM"
        /// </summary>
        public string? TableType { get; set; }

        /// <summary>
        /// Explanatory comment on the table (REMARKS in statement-based metadata)
        /// </summary>
        public string? Remarks { get; set; }

        /// <summary>
        /// The types catalog (TYPE_CAT in statement-based metadata)
        /// May be null. Typically null for most databases.
        /// </summary>
        public string? TypeCatalog { get; set; }

        /// <summary>
        /// The types schema (TYPE_SCHEM in statement-based metadata)
        /// May be null. Typically null for most databases.
        /// </summary>
        public string? TypeSchema { get; set; }

        /// <summary>
        /// Type name (TYPE_NAME in statement-based metadata)
        /// May be null. Typically null for most databases.
        /// </summary>
        public string? TypeName { get; set; }

        /// <summary>
        /// Name of the designated "identifier" column of a typed table (SELF_REFERENCING_COL_NAME in statement-based metadata)
        /// May be null. Typically null for most databases.
        /// </summary>
        public string? SelfReferencingColName { get; set; }

        /// <summary>
        /// Specifies how values in SELF_REFERENCING_COL_NAME are created (REF_GENERATION in statement-based metadata)
        /// Values are "SYSTEM", "USER", "DERIVED". May be null.
        /// </summary>
        public string? RefGeneration { get; set; }

        /// <summary>
        /// Creates a new table metadata record.
        /// </summary>
        public TableMetadataRecord(
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
            CatalogName = catalogName;
            SchemaName = schemaName;
            TableName = tableName;
            TableType = tableType;
            Remarks = remarks;
            TypeCatalog = typeCatalog;
            TypeSchema = typeSchema;
            TypeName = typeName;
            SelfReferencingColName = selfReferencingColName;
            RefGeneration = refGeneration;
        }
    }
}

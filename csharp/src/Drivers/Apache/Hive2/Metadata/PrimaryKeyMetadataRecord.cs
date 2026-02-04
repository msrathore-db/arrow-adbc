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
    /// Protocol-agnostic data model for primary key metadata.
    /// Supports multiple protocol implementations including REST and RPC-based protocols.
    /// Contains 6 fields as per ADBC specification.
    /// </summary>
    public class PrimaryKeyMetadataRecord
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
        /// Column name (COLUMN_NAME in statement-based metadata)
        /// </summary>
        public string? ColumnName { get; set; }

        /// <summary>
        /// Sequence number within primary key (KEQ_SEQ in statement-based metadata)
        /// Starting at 1 for the first column in the primary key
        /// </summary>
        public int? KeySequence { get; set; }

        /// <summary>
        /// Primary key name (PK_NAME in statement-based metadata)
        /// May be null if the primary key is unnamed
        /// </summary>
        public string? PrimaryKeyName { get; set; }

        /// <summary>
        /// Creates a new primary key metadata record.
        /// </summary>
        public PrimaryKeyMetadataRecord(
            string? catalogName,
            string? schemaName,
            string? tableName,
            string? columnName,
            int? keySequence = null,
            string? primaryKeyName = null)
        {
            CatalogName = catalogName;
            SchemaName = schemaName;
            TableName = tableName;
            ColumnName = columnName;
            KeySequence = keySequence;
            PrimaryKeyName = primaryKeyName;
        }
    }
}

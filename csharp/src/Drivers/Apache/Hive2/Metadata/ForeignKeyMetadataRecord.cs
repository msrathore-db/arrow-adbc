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
    /// Protocol-agnostic data model for foreign key metadata (cross-reference).
    /// Supports multiple protocol implementations including REST and RPC-based protocols.
    /// Contains 14 fields as per ADBC specification.
    /// </summary>
    public class ForeignKeyMetadataRecord
    {
        // Primary key table information (4 fields)

        /// <summary>
        /// Primary key table catalog (PKTABLE_CAT in statement-based metadata)
        /// </summary>
        public string? PkCatalogName { get; set; }

        /// <summary>
        /// Primary key table schema (PKTABLE_SCHEM in statement-based metadata)
        /// </summary>
        public string? PkSchemaName { get; set; }

        /// <summary>
        /// Primary key table name (PKTABLE_NAME in statement-based metadata)
        /// </summary>
        public string? PkTableName { get; set; }

        /// <summary>
        /// Primary key column name (PKCOLUMN_NAME in statement-based metadata)
        /// </summary>
        public string? PkColumnName { get; set; }

        // Foreign key table information (4 fields)

        /// <summary>
        /// Foreign key table catalog (FKTABLE_CAT in statement-based metadata)
        /// </summary>
        public string? FkCatalogName { get; set; }

        /// <summary>
        /// Foreign key table schema (FKTABLE_SCHEM in statement-based metadata)
        /// </summary>
        public string? FkSchemaName { get; set; }

        /// <summary>
        /// Foreign key table name (FKTABLE_NAME in statement-based metadata)
        /// </summary>
        public string? FkTableName { get; set; }

        /// <summary>
        /// Foreign key column name (FKCOLUMN_NAME in statement-based metadata)
        /// </summary>
        public string? FkColumnName { get; set; }

        // Relationship metadata (6 fields)

        /// <summary>
        /// Sequence number within foreign key (KEQ_SEQ in statement-based metadata)
        /// Starting at 1
        /// </summary>
        public int? KeySequence { get; set; }

        /// <summary>
        /// What happens to FK when primary key is updated (UPDATE_RULE in statement-based metadata)
        /// Values: 0 = CASCADE, 1 = RESTRICT, 2 = SET NULL, 3 = NO ACTION, 4 = SET DEFAULT
        /// </summary>
        public int? UpdateRule { get; set; }

        /// <summary>
        /// What happens to FK when primary key is deleted (DELETE_RULE in statement-based metadata)
        /// Values: 0 = CASCADE, 1 = RESTRICT, 2 = SET NULL, 3 = NO ACTION, 4 = SET DEFAULT
        /// </summary>
        public int? DeleteRule { get; set; }

        /// <summary>
        /// Foreign key name (FK_NAME in statement-based metadata)
        /// May be null if the foreign key is unnamed
        /// </summary>
        public string? FkName { get; set; }

        /// <summary>
        /// Primary key name (PK_NAME in statement-based metadata)
        /// May be null if the primary key is unnamed
        /// </summary>
        public string? PkName { get; set; }

        /// <summary>
        /// Can the evaluation of foreign key constraints be deferred (DEFERRABILITY in statement-based metadata)
        /// Values: 5 = INITIALLY_DEFERRED, 6 = INITIALLY_IMMEDIATE, 7 = NOT_DEFERRABLE
        /// </summary>
        public int? Deferrability { get; set; }

        /// <summary>
        /// Creates a new foreign key metadata record.
        /// </summary>
        public ForeignKeyMetadataRecord(
            string? pkCatalogName,
            string? pkSchemaName,
            string? pkTableName,
            string? pkColumnName,
            string? fkCatalogName,
            string? fkSchemaName,
            string? fkTableName,
            string? fkColumnName,
            int? keySequence = null,
            int? updateRule = null,
            int? deleteRule = null,
            string? fkName = null,
            string? pkName = null,
            int? deferrability = null)
        {
            PkCatalogName = pkCatalogName;
            PkSchemaName = pkSchemaName;
            PkTableName = pkTableName;
            PkColumnName = pkColumnName;
            FkCatalogName = fkCatalogName;
            FkSchemaName = fkSchemaName;
            FkTableName = fkTableName;
            FkColumnName = fkColumnName;
            KeySequence = keySequence;
            UpdateRule = updateRule;
            DeleteRule = deleteRule;
            FkName = fkName;
            PkName = pkName;
            Deferrability = deferrability;
        }
    }
}

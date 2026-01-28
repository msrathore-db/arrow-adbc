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
    /// Protocol-agnostic data model for schema metadata.
    /// Used by both HiveServer2 (Thrift) and StatementExecution API (REST) protocols.
    /// </summary>
    public class SchemaMetadataRecord
    {
        /// <summary>
        /// Catalog name (TABLE_CATALOG in statement-based metadata)
        /// </summary>
        public string? CatalogName { get; set; }

        /// <summary>
        /// Schema name (TABLE_SCHEM in statement-based metadata)
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// Creates a new schema metadata record.
        /// </summary>
        /// <param name="catalogName">The catalog name</param>
        /// <param name="schemaName">The schema name</param>
        public SchemaMetadataRecord(string? catalogName, string? schemaName)
        {
            CatalogName = catalogName;
            SchemaName = schemaName;
        }
    }
}

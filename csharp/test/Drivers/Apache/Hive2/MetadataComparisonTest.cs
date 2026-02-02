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

#pragma warning disable CS8602 // Dereference of a possibly null reference
#pragma warning disable CS8604 // Possible null reference argument

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Apache.Arrow;
using Apache.Arrow.Adbc.Drivers.Apache;
using Apache.Arrow.Adbc.Drivers.Apache.Hive2;
using Apache.Arrow.Adbc.Drivers.Apache.Spark;
using Apache.Arrow.Arrays;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using Xunit;
using Xunit.Abstractions;

namespace Apache.Arrow.Adbc.Tests.Drivers.Apache.Hive2
{
    /// <summary>
    /// Tests to verify Thrift metadata output matches baseline after refactoring.
    /// This test captures GetObjects and statement-based metadata to verify no breaking changes.
    /// </summary>
    public class MetadataComparisonTest
    {
        private readonly ITestOutputHelper _output;
        private StringBuilder _capturedOutput = new StringBuilder();

        public MetadataComparisonTest(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Captures all Thrift metadata command outputs to verify against baseline.
        /// Set SPARK_TEST_CONFIG_FILE environment variable to run this test.
        /// </summary>
        [Fact(Skip = "Manual test - requires live Spark server and baseline file")]
        public async Task CaptureThriftMetadataOutput()
        {
            // This test is designed to be run manually when verifying the metadata refactoring
            var configFile = Environment.GetEnvironmentVariable("SPARK_TEST_CONFIG_FILE");
            if (string.IsNullOrEmpty(configFile) || !File.Exists(configFile))
            {
                _output.WriteLine("Skipping: SPARK_TEST_CONFIG_FILE not set or file doesn't exist");
                return;
            }

            var config = LoadConfig(configFile);
            var outputPath = Path.Combine(Path.GetDirectoryName(configFile) ?? ".", "thrift_metadata_output.txt");

            WriteLine(new string('=', 120));
            WriteLine("THRIFT METADATA CAPTURE - Verifying Refactored Implementation");
            WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteLine(new string('=', 120));
            WriteLine();

            using (var driver = NewDriver())
            using (var database = driver.Open(config))
            using (var connection = database.Connect(new Dictionary<string, string>()))
            {
                // Part 1: GetObjects (Hierarchical)
                WriteLine(new string('█', 120));
                WriteLine("PART 1: GETOBJECTS METADATA (Hierarchical ADBC Structure)");
                WriteLine(new string('█', 120));
                WriteLine();
                await CaptureGetObjects(connection);

                // Part 2: GetCatalogs (Statement-Based)
                WriteLine();
                WriteLine(new string('█', 120));
                WriteLine("PART 2: GETCATALOGS METADATA (Statement-Based)");
                WriteLine(new string('█', 120));
                WriteLine();
                await CaptureStatementMetadata(connection, "GetCatalogs", new Dictionary<string, string>());

                // Part 3: GetSchemas (Statement-Based)
                WriteLine();
                WriteLine(new string('█', 120));
                WriteLine("PART 3: GETSCHEMAS METADATA (Statement-Based)");
                WriteLine(new string('█', 120));
                WriteLine();
                await CaptureStatementMetadata(connection, "GetSchemas", new Dictionary<string, string>
                {
                    { ApacheParameters.CatalogName, "main" }
                });

                // Part 4: GetTables (Statement-Based)
                WriteLine();
                WriteLine(new string('█', 120));
                WriteLine("PART 4: GETTABLES METADATA (Statement-Based)");
                WriteLine(new string('█', 120));
                WriteLine();
                await CaptureStatementMetadata(connection, "GetTables", new Dictionary<string, string>
                {
                    { ApacheParameters.CatalogName, "main" },
                    { ApacheParameters.SchemaName, "default" }
                });

                // Part 5: GetColumns (Statement-Based)
                WriteLine();
                WriteLine(new string('█', 120));
                WriteLine("PART 5: GETCOLUMNS METADATA (Statement-Based)");
                WriteLine(new string('█', 120));
                WriteLine();
                await CaptureStatementMetadata(connection, "GetColumns", new Dictionary<string, string>
                {
                    { ApacheParameters.CatalogName, "main" },
                    { ApacheParameters.SchemaName, "default" },
                    { ApacheParameters.TableName, "%" },
                    { ApacheParameters.ColumnName, "%" }
                });
            }

            // Save to file
            File.WriteAllText(outputPath, _capturedOutput.ToString());
            WriteLine();
            WriteLine(new string('=', 120));
            WriteLine($"✅ Output saved to: {outputPath}");
            WriteLine($"📋 Compare this output with the baseline file to verify no breaking changes");
            WriteLine(new string('=', 120));

            _output.WriteLine(_capturedOutput.ToString());
        }

        private async Task CaptureGetObjects(AdbcConnection connection)
        {
            WriteLine("Executing GetObjects with depth=All, catalogPattern='main', dbSchemaPattern='default'...");
            WriteLine();

            using var stream = connection.GetObjects(
                AdbcConnection.GetObjectsDepth.All,
                catalogPattern: "main",
                dbSchemaPattern: "default",
                tableNamePattern: null,
                tableTypes: null,
                columnNamePattern: null);

            int batchCount = 0;
            while (true)
            {
                using var batch = await stream.ReadNextRecordBatchAsync();
                if (batch == null) break;

                batchCount++;
                WriteLine($"Batch {batchCount}:");
                WriteLine(new string('-', 120));

                // Display schema
                if (batchCount == 1)
                {
                    WriteLine("Schema:");
                    foreach (var field in batch.Schema.FieldsList)
                    {
                        WriteLine($"  - {field.Name}: {field.DataType}");
                    }
                    WriteLine();
                }

                // Extract and display catalogs
                var catalogArray = batch.Column("catalog_name") as StringArray;
                var dbSchemasArray = batch.Column("catalog_db_schemas") as ListArray;

                for (int catIdx = 0; catIdx < batch.Length; catIdx++)
                {
                    if (catalogArray?.IsNull(catIdx) != false)
                        continue;

                    var catalogName = catalogArray.GetString(catIdx);
                    WriteLine($"Catalog: {catalogName}");

                    if (dbSchemasArray?.IsNull(catIdx) != false)
                        continue;

                    var schemaStruct = dbSchemasArray.GetSlicedValues(catIdx) as StructArray;
                    if (schemaStruct == null) continue;

                    var schemaNameArray = schemaStruct.Fields[0] as StringArray;
                    var tablesListArray = schemaStruct.Fields[1] as ListArray;

                    for (int schemaIdx = 0; schemaIdx < schemaStruct.Length; schemaIdx++)
                    {
                        var schemaName = schemaNameArray?.GetString(schemaIdx);
                        WriteLine($"  Schema: {schemaName}");

                        if (tablesListArray?.IsNull(schemaIdx) != false) continue;

                        var tableStruct = tablesListArray.GetSlicedValues(schemaIdx) as StructArray;
                        if (tableStruct == null) continue;

                        var tableNameArray = tableStruct.Fields[0] as StringArray;
                        var tableTypeArray = tableStruct.Fields[1] as StringArray;
                        var columnsListArray = tableStruct.Fields[2] as ListArray;

                        for (int tableIdx = 0; tableIdx < tableStruct.Length; tableIdx++)
                        {
                            var tableName = tableNameArray?.GetString(tableIdx);
                            var tableType = tableTypeArray?.GetString(tableIdx);
                            WriteLine($"    Table: {tableName} (Type: {tableType})");

                            if (columnsListArray == null || columnsListArray.IsNull(tableIdx)) continue;

                            var columnStruct = columnsListArray.GetSlicedValues(tableIdx) as StructArray;
                            if (columnStruct == null) continue;

                            var columnSchema = ((IArrowRecord)columnStruct).Schema;
                            WriteLine($"      Columns ({columnStruct.Length} total):");

                            // Display first few columns with all their metadata fields
                            int displayLimit = Math.Min(3, columnStruct.Length);
                            for (int colIdx = 0; colIdx < displayLimit; colIdx++)
                            {
                                WriteLine($"        Column {colIdx}:");
                                for (int fieldIdx = 0; fieldIdx < columnStruct.Fields.Count; fieldIdx++)
                                {
                                    var fieldName = columnSchema.GetFieldByIndex(fieldIdx).Name;
                                    var fieldArray = columnStruct.Fields[fieldIdx];
                                    var value = GetValue(fieldArray, colIdx) ?? "null";
                                    WriteLine($"          {fieldName}: {value}");
                                }
                            }

                            if (columnStruct.Length > displayLimit)
                            {
                                WriteLine($"        ... and {columnStruct.Length - displayLimit} more columns");
                            }
                        }
                    }
                }

                WriteLine();
            }

            WriteLine($"Total batches processed: {batchCount}");
            WriteLine();
        }

        private async Task CaptureStatementMetadata(AdbcConnection connection, string command, Dictionary<string, string> options)
        {
            WriteLine($"Executing {command} with options:");
            foreach (var opt in options)
            {
                WriteLine($"  {opt.Key} = {opt.Value}");
            }
            WriteLine();

            using var statement = connection.CreateStatement();
            statement.SetOption(ApacheParameters.IsMetadataCommand, "true");

            foreach (var opt in options)
            {
                statement.SetOption(opt.Key, opt.Value);
            }

            statement.SqlQuery = command;
            var queryResult = statement.ExecuteQuery();
            using var reader = queryResult.Stream;

            // Display schema
            WriteLine("Schema:");
            WriteLine(new string('-', 120));
            WriteLine($"{"Column Name",-40} {"Data Type",-30}");
            WriteLine(new string('-', 120));
            foreach (var field in reader.Schema!.FieldsList!)
            {
                WriteLine($"{field.Name,-40} {field.DataType,-30}");
            }
            WriteLine();

            // Read and display all rows
            WriteLine("Data:");
            WriteLine(new string('-', 120));

            int rowCount = 0;
            while (true)
            {
                using var batch = await reader.ReadNextRecordBatchAsync();
                if (batch == null) break;

                for (int i = 0; i < batch.Length; i++)
                {
                    WriteLine($"Row {rowCount}:");
                    for (int colIdx = 0; colIdx < batch.ColumnCount; colIdx++)
                    {
                        var columnName = reader.Schema?.FieldsList[colIdx].Name ?? $"Column{colIdx}";
                        var array = batch.Column(colIdx);
                        var value = GetValue(array, i) ?? "null";
                        WriteLine($"  {columnName}: {value}");
                    }
                    WriteLine();
                    rowCount++;

                    // Limit display to first 10 rows
                    if (rowCount >= 10)
                    {
                        WriteLine("  ... (displaying first 10 rows only)");
                        goto EndDisplay;
                    }
                }
            }

        EndDisplay:
            WriteLine(new string('-', 120));
            WriteLine($"Total rows: {rowCount}");
            WriteLine();
        }

        private static object? GetValue(IArrowArray array, int index)
        {
            if (array.IsNull(index))
                return null;

            return array switch
            {
                Int8Array a => a.GetValue(index),
                Int16Array a => a.GetValue(index),
                Int32Array a => a.GetValue(index),
                Int64Array a => a.GetValue(index),
                FloatArray a => a.GetValue(index),
                DoubleArray a => a.GetValue(index),
                StringArray a => a.GetString(index),
                BooleanArray a => a.GetValue(index),
                BinaryArray a => BitConverter.ToString(a.GetBytes(index).ToArray()),
                _ => $"[{array.Data.DataType}]"
            };
        }

        private void WriteLine(string line = "")
        {
            _capturedOutput.AppendLine(line);
        }

        private Dictionary<string, string> LoadConfig(string configFile)
        {
            var json = File.ReadAllText(configFile);
            var parsed = System.Text.Json.JsonDocument.Parse(json);
            var config = new Dictionary<string, string>();
            foreach (var prop in parsed.RootElement.EnumerateObject())
            {
                config[prop.Name] = prop.Value.GetString() ?? "";
            }
            return config;
        }

        private AdbcDriver NewDriver()
        {
            // Use Spark driver for HiveServer2 protocol testing
            return new SparkDriver();
        }
    }
}

# Metadata Comparison Test - Execution Guide

## Test Status

The test encountered a **503 Service Unavailable** error when attempting to connect to the Databricks SQL Warehouse. This typically means:

1. **Warehouse is stopped** - SQL Warehouses auto-stop after inactivity
2. **Thrift protocol not enabled** - Some Databricks configurations may not expose Thrift endpoints
3. **Authentication issue** - Token might be expired or insufficient permissions

## Error Details

```
Apache.Arrow.Adbc.Drivers.Apache.Hive2.HiveServer2Exception:
An unexpected error occurred while running metadata query.
Response status code does not indicate success: 503 (Service Unavailable).
```

## How to Run the Test Successfully

### Prerequisites

1. **Start the SQL Warehouse** in Databricks UI
2. **Verify Thrift endpoint** is accessible (Databricks SQL Warehouses should support Thrift)
3. **Update token** if expired

### Configuration File

Create a config file (e.g., `/tmp/spark-test-config.json`):

```json
{
  "uri": "https://adb-XXXXXXXXX.azuredatabricks.net/sql/1.0/warehouses/XXXXXXXX",
  "token": "dapi...",
  "adbc.spark.type": "http",
  "adbc.spark.token": "dapi...",
  "adbc.connection.catalog": "main"
}
```

### Run the Test

```bash
# Set config file location
export SPARK_TEST_CONFIG_FILE=/tmp/spark-test-config.json

# Navigate to arrow-adbc directory
cd /Users/madhavendra.rathore/Desktop/arrow-adbc/csharp

# Run the test (temporarily remove Skip attribute first)
dotnet test test/Drivers/Apache/Apache.Arrow.Adbc.Tests.Drivers.Apache.csproj \
  --filter "FullyQualifiedName~MetadataComparisonTest.CaptureThriftMetadataOutput"
```

## Expected Output Structure

When the test runs successfully, it produces a file `thrift_metadata_output.txt` with the following structure:

### Part 1: GetObjects (Hierarchical)

```
========================================================
THRIFT METADATA CAPTURE - Verifying Refactored Implementation
Timestamp: 2026-02-03 XX:XX:XX
========================================================

████████████████████████████████████████████████████████
PART 1: GETOBJECTS METADATA (Hierarchical ADBC Structure)
████████████████████████████████████████████████████████

Executing GetObjects with depth=All, catalogPattern='main', dbSchemaPattern='default'...

Batch 1:
--------------------------------------------------------
Schema:
  - catalog_name: String
  - catalog_db_schemas: List<Struct<...>>

Catalog: main
  Schema: default
    Table: test_table (Type: TABLE)
      Columns (10 total):
        Column 0:
          column_name: id
          ordinal_position: 1
          xdbc_data_type: 4        # INTEGER type code
          xdbc_type_name: INT
          xdbc_column_size: null   # KEY: Was null in original, should be null after fix
          xdbc_decimal_digits: null # KEY: Was null in original, should be null after fix
          xdbc_num_prec_radix: null # KEY: MUST be null (was always null in original)
          xdbc_sql_data_type: 4     # KEY: Must match xdbc_data_type from Thrift
          xdbc_char_octet_length: null # KEY: MUST be null
          xdbc_datetime_sub: null    # KEY: MUST be null
          ...
```

### Part 2-5: Statement-Based Metadata

```
████████████████████████████████████████████████████████
PART 2: GETCATALOGS METADATA (Statement-Based)
████████████████████████████████████████████████████████

Schema:
--------------------------------------------------------
Column Name                              Data Type
--------------------------------------------------------
TABLE_CAT                                String

Data:
--------------------------------------------------------
Row 0:
  TABLE_CAT: main

Total rows: 1


████████████████████████████████████████████████████████
PART 3: GETSCHEMAS METADATA (Statement-Based)
████████████████████████████████████████████████████████
...


████████████████████████████████████████████████████████
PART 4: GETTABLES METADATA (Statement-Based)
████████████████████████████████████████████████████████
...


████████████████████████████████████████████████████████
PART 5: GETCOLUMNS METADATA (Statement-Based)
████████████████████████████████████████████████████████

Schema:
--------------------------------------------------------
Column Name                              Data Type
--------------------------------------------------------
TABLE_CAT                                String
TABLE_SCHEM                              String
TABLE_NAME                               String
COLUMN_NAME                              String
DATA_TYPE                                Int16
TYPE_NAME                                String
COLUMN_SIZE                              Int32
BUFFER_LENGTH                            Int8
DECIMAL_DIGITS                           Int32
NUM_PREC_RADIX                           Int16
...

Data:
--------------------------------------------------------
Row 0:
  TABLE_CAT: main
  TABLE_SCHEM: default
  TABLE_NAME: test_table
  COLUMN_NAME: id
  DATA_TYPE: 4                    # XDBC type code for INTEGER
  TYPE_NAME: INT
  COLUMN_SIZE: null              # KEY: Should be null for INTEGER (from Thrift)
  DECIMAL_DIGITS: null           # KEY: Should be null for INTEGER (from Thrift)
  NUM_PREC_RADIX: null           # KEY: MUST be null
  ...
```

## Key Fields to Verify

When comparing with baseline (`comparison_new_20251229_015612.txt`), pay special attention to these fields that were affected by the breaking changes:

### Critical Fields (Must Match Baseline Exactly):

1. **xdbc_column_size** / **COLUMN_SIZE**
   - For INTEGER/BIGINT/STRING: Should be `null` (not `4`, `8`, or `2147483647`)
   - For DECIMAL(10,2): Should be `10` (from Thrift precision)
   - For VARCHAR(100): Should be `100` (from Thrift)

2. **xdbc_decimal_digits** / **DECIMAL_DIGITS**
   - For INTEGER/VARCHAR/STRING: Should be `null` (not `0`)
   - For DECIMAL(10,2): Should be `2` (from Thrift scale)

3. **xdbc_num_prec_radix** / **NUM_PREC_RADIX**
   - For ALL types: Should be `null` (not `10`)

4. **xdbc_sql_data_type** / **SQL_DATA_TYPE**
   - Should match DATA_TYPE value exactly (both from Thrift server)

5. **xdbc_char_octet_length** / **CHAR_OCTET_LENGTH**
   - For ALL types: Should be `null`

6. **xdbc_datetime_sub** / **SQL_DATETIME_SUB**
   - For ALL types: Should be `null`

## Verification Checklist

- [ ] All xdbc_column_size values match baseline (null for most types)
- [ ] All xdbc_decimal_digits values match baseline (null for non-DECIMAL types)
- [ ] All xdbc_num_prec_radix values are null (not 10)
- [ ] All xdbc_sql_data_type values match xdbc_data_type
- [ ] All xdbc_char_octet_length values are null
- [ ] All xdbc_datetime_sub values are null
- [ ] No BufferLength overflow errors (range -128 to 127)

## Troubleshooting

### 503 Service Unavailable

**Solution**:
1. Open Databricks UI
2. Navigate to SQL Warehouses
3. Start the warehouse
4. Wait for "Running" status
5. Retry the test

### Token Expired

**Solution**:
1. Generate new Personal Access Token in Databricks
2. Update config file with new token
3. Retry the test

### Thrift Not Supported

**Solution**:
If Databricks doesn't support Thrift protocol for your warehouse type:
1. Try using a different warehouse type (Classic SQL Warehouse)
2. Or test against a local Spark/Hive2 server
3. Or compare with existing baseline manually

## Alternative: Manual Verification

If you can't run the test, you can manually verify the fixes by:

1. **Check the code changes** in commit `9bf7563c`:
   ```bash
   git show 9bf7563c csharp/src/Drivers/Apache/Hive2/HiveServer2Connection.cs
   ```

2. **Review the override logic** at lines 658-667:
   ```csharp
   record.XdbcColumnSize = tempTableInfo.Precision[0];
   record.XdbcDecimalDigits = tempTableInfo.Scale[0];
   record.XdbcNumPrecRadix = null;
   record.SqlDataType = colType;
   record.XdbcCharOctetLength = null;
   record.SqlDatetimeSub = null;
   ```

3. **Compare with original behavior** documented in:
   - `BREAKING_CHANGES_ANALYSIS.md`
   - `FIX_PLAN.md`
   - `/tmp/original_hiveserver2connection.cs` lines 1305-1315

## Success Criteria

✅ Test passes without errors
✅ Output file generated at specified location
✅ All critical fields match baseline values
✅ Zero breaking changes detected
✅ Byte-identical output for xdbc_* fields

## Next Steps After Verification

1. **Update databricks repo submodule** to point to this fixed commit
2. **Continue with SEA implementation** (Phase 3)
3. **Run full test suite** to ensure no regressions
4. **Document any differences** if found (should be zero)

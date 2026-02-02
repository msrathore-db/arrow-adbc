# ADBC C# Metadata Refactor - Detailed Review

## Executive Summary

This PR introduces a metadata abstraction layer to enable driver-specific overrides of metadata field generation. While the architecture is sound, there are **correctness issues** and **behavioral changes** that will affect output compared to the pre-refactor baseline.

**Key Finding**: Based on the comparison file `comparison_refactor2_20251229_195015.txt`, the **Thrift** output represents the **current/original behavior**. The refactored code will **change Thrift behavior to match SEA** for some fields, which represents a **regression for Thrift users**.

---

## Critical Issues

### Issue 1: `xdbc_column_size` Changed from `null` to Synthesized Values

**Severity**: HIGH - Breaking Change for Thrift

**Before Refactor (Thrift)**:
```
c_tinyint:  xdbc_column_size = null
c_int:      xdbc_column_size = null
c_bigint:   xdbc_column_size = null
c_decimal:  xdbc_column_size = 10  (parsed from DECIMAL(10,2))
c_string:   xdbc_column_size = 2147483647  (parsed from STRING)
```

**After Refactor (will match SEA)**:
```
c_tinyint:  xdbc_column_size = 1
c_int:      xdbc_column_size = 4
c_bigint:   xdbc_column_size = 8
c_decimal:  xdbc_column_size = 10  (unchanged)
c_string:   xdbc_column_size = 2147483647  (unchanged)
```

**Code Path**:
```
HiveServer2Connection.GetObjects() (line 656)
  └─> MetadataFieldPopulator.PopulateColumnMetadata()
        └─> GetColumnSize(typeName)
              └─> ColumnTypeMapper.GetColumnSize(typeName) (line 259-304)
                    └─> Returns DefaultColumnSizes[baseType] for non-parameterized types
```

**Original Code Path** (SparkConnection.SetPrecisionScaleAndTypeName, line 70-114):
```csharp
default:
    tableInfo?.Precision.Add(null);  // Returns NULL for non-DECIMAL/VARCHAR types
```

**New Code Path** (ColumnTypeMapper.GetColumnSize, line 298-304):
```csharp
// For other types, use default size
if (DefaultColumnSizes.TryGetValue(baseType, out var size))
{
    return size;  // Returns 1 for TINYINT, 4 for INT, etc.
}
return 2147483647;
```

**Recommendation**: To preserve original Thrift behavior, override fields after PopulateColumnMetadata:
```csharp
// In HiveServer2Connection.GetObjects(), after line 673:
// Preserve original null behavior for non-parameterized types
if (!IsParameterizedType(typeName))
{
    record.XdbcColumnSize = null;
}
```

---

### Issue 2: `xdbc_decimal_digits` Changed from `null` to Synthesized Values

**Severity**: HIGH - Breaking Change for Thrift

**Before Refactor (Thrift)**:
```
c_tinyint:   xdbc_decimal_digits = null
c_int:       xdbc_decimal_digits = null
c_float:     xdbc_decimal_digits = null
c_double:    xdbc_decimal_digits = null
c_decimal:   xdbc_decimal_digits = 2  (parsed)
c_string:    xdbc_decimal_digits = null
c_timestamp: xdbc_decimal_digits = null
```

**After Refactor (will match SEA)**:
```
c_tinyint:   xdbc_decimal_digits = 0
c_int:       xdbc_decimal_digits = 0
c_float:     xdbc_decimal_digits = 7
c_double:    xdbc_decimal_digits = 15
c_decimal:   xdbc_decimal_digits = 2  (unchanged)
c_string:    xdbc_decimal_digits = 0
c_timestamp: xdbc_decimal_digits = 6
```

**Code Path**:
```
HiveServer2Connection.GetObjects() (line 656)
  └─> MetadataFieldPopulator.PopulateColumnMetadata()
        └─> GetDecimalDigits(typeName)
              └─> ColumnTypeMapper.GetDecimalDigits(typeName) (line 389-444)
```

**ColumnTypeMapper.GetDecimalDigits** (lines 405-430):
```csharp
// For integer types, scale is always 0
if (baseType == "TINYINT" || baseType == "SMALLINT" || ...)
    return 0;  // Original returned: null

// For floating-point types
if (baseType == "FLOAT" || baseType == "REAL")
    return 7;  // Original returned: null

if (baseType == "DOUBLE")
    return 15;  // Original returned: null

// For TIMESTAMP types
if (baseType == "TIMESTAMP" || baseType == "TIMESTAMP_NTZ" || ...)
    return 6;  // Original returned: null
```

**Recommendation**: Add override in GetObjects to restore original behavior:
```csharp
// Preserve original null behavior for scale on non-decimal types
if (colType != (short)ColumnTypeId.DECIMAL && colType != (short)ColumnTypeId.NUMERIC)
{
    record.XdbcDecimalDigits = null;
}
```

---

### Issue 3: `xdbc_num_prec_radix` Now Populated (Was Always `null`)

**Severity**: MEDIUM - Behavioral Change

**Before Refactor**: Always `null` for all types in GetObjects
**After Refactor**: Returns `10` for numeric types

**Comparison Output**:
```
c_tinyint:  Thrift=null, SEA=10
c_decimal:  Thrift=null, SEA=10
c_float:    Thrift=null, SEA=10
```

**Code Path**: The BuildColumnsStructArray explicitly nulls this:
```csharp
// MetadataSchemaBuilder.cs line 456
xdbcNumPrecRadixBuilder.AppendNull(); // Typically null in GetObjects
```

**Status**: This appears to be correctly handled - the builder explicitly appends null regardless of record value.

---

### Issue 4: `xdbc_sql_data_type` May Differ from Thrift-Provided Value

**Severity**: MEDIUM - Potential Correctness Issue

**Code Path**:
```
HiveServer2Connection.GetObjects() line 656
  └─> PopulateColumnMetadata()
        └─> record.SqlDataType = ColumnTypeMapper.GetSqlDataType(typeName)  // Synthesized!

// record.SqlDataType is NOT overwritten with Thrift value!

MetadataSchemaBuilder.BuildColumnsStructArray() line 459
  └─> AppendOrNull(xdbcSqlDataTypeBuilder, (short?)record.SqlDataType)  // Uses synthesized value
```

**Original Code** (HiveServer2Connection.GetColumnSchema):
```csharp
xdbcSqlDataTypeBuilder.Append(tableInfo.ColType[i]);  // Used Thrift value
```

**New Code** uses synthesized value that may differ if type name parsing produces different result than server.

**Recommendation**: Override SqlDataType with Thrift value:
```csharp
// In HiveServer2Connection.GetObjects(), add after line 673:
record.SqlDataType = colType;  // Use Thrift-provided value
```

---

## Field-by-Field Comparison: Will Results Be The Same?

Based on `comparison_refactor2_20251229_195015.txt`, comparing **Thrift (original)** vs **SEA (uses new abstractions)**:

### GetObjects (19 fields per column)

| Field | Original Thrift | After Refactor | Same? |
|-------|-----------------|----------------|-------|
| `column_name` | from Thrift | from Thrift | ✅ YES |
| `ordinal_position` | from Thrift + offset | from Thrift + offset | ✅ YES |
| `remarks` | TypeName (e.g., "INT") | TypeName | ✅ YES |
| `xdbc_data_type` | from Thrift (e.g., 4) | from Thrift (overwritten) | ✅ YES |
| `xdbc_type_name` | BaseTypeName (parsed) | BaseTypeName (parsed) | ✅ YES |
| `xdbc_column_size` | null (for most types) | synthesized values | ❌ **DIFFERS** |
| `xdbc_decimal_digits` | null (for non-decimal) | synthesized values | ❌ **DIFFERS** |
| `xdbc_num_prec_radix` | null | null (explicit) | ✅ YES |
| `xdbc_nullable` | from Thrift | from Thrift (overwritten) | ✅ YES |
| `xdbc_column_def` | from Thrift | from Thrift | ✅ YES |
| `xdbc_sql_data_type` | from Thrift | synthesized | ⚠️ **RISK** |
| `xdbc_datetime_sub` | null | null (explicit) | ✅ YES |
| `xdbc_char_octet_length` | null | null (explicit) | ✅ YES |
| `xdbc_is_nullable` | from Thrift | from Thrift (overwritten) | ✅ YES |
| `xdbc_scope_catalog` | null | null | ✅ YES |
| `xdbc_scope_schema` | null | null | ✅ YES |
| `xdbc_scope_table` | null | null | ✅ YES |
| `xdbc_is_autoincrement` | from Thrift | from Thrift (overwritten) | ✅ YES |
| `xdbc_is_generatedcolumn` | true | true | ✅ YES |

### GetColumns (24-field Statement-Based Metadata)

| Field | Original Thrift | After Refactor | Same? |
|-------|-----------------|----------------|-------|
| `BUFFER_LENGTH` | null | synthesized | ❌ **DIFFERS** |
| `SQL_DATA_TYPE` | null | synthesized | ❌ **DIFFERS** |
| `CHAR_OCTET_LENGTH` | null | synthesized (for strings) | ❌ **DIFFERS** |
| All other fields | from Thrift | from Thrift | ✅ YES |

---

## Type Casting Analysis

### Safe Casts

| Location | Cast | Max Value | Type Range | Safe? |
|----------|------|-----------|------------|-------|
| `(short?)record.XdbcDataType` | int? → short? | 2003 (ARRAY) | -32768 to 32767 | ✅ |
| `(short?)record.XdbcDecimalDigits` | int? → short? | 38 | -32768 to 32767 | ✅ |
| `(short?)record.Nullable` | int? → short? | 2 | -32768 to 32767 | ✅ |
| `(short?)record.SqlDataType` | int? → short? | 2003 | -32768 to 32767 | ✅ |
| `(short)columnTypeList[i]` | int → short | 2003 | -32768 to 32767 | ✅ |

### Problematic Cast

**Location**: `MetadataSchemaBuilder.cs` lines 549-555

```csharp
private static void AppendOrNull(Int8Array.Builder builder, byte? value)
{
    if (value.HasValue)
        builder.Append((sbyte)value.Value);  // PROBLEM!
    else
        builder.AppendNull();
}
```

**Issue**: `byte` (0-255) cast to `sbyte` (-128 to 127)
- If `BufferLength = 200`, cast produces `-56`
- Maximum DECIMAL(38) BufferLength = ~26, so safe for current types
- **Risk**: Custom types with larger buffer lengths would produce incorrect negative values

**Recommendation**: Change `BufferLength` type from `byte?` to `int?`:
```csharp
// ColumnMetadataRecord.cs
public int? BufferLength { get; set; }  // Changed from byte?
```

---

## Performance Implications

### Memory Allocation

| Metric | Original | After Refactor | Impact |
|--------|----------|----------------|--------|
| Objects per column | 0 (uses Lists) | 1 (ColumnMetadataRecord) | Minor GC pressure |
| SqlTypeNameParser calls | 1 per column | 1 per column (cached) | Neutral |
| GetBaseTypeName calls | 1 per column | 8+ per column | Method dispatch overhead |

### Redundant Type Parsing

**MetadataFieldPopulator.PopulateColumnMetadata** calls GetBaseTypeName 8+ times:
```csharp
record.XdbcDataType = GetXdbcDataType(typeName);     // → GetBaseTypeName
record.BaseTypeName = GetBaseTypeName(typeName);      // → GetBaseTypeName
record.XdbcColumnSize = GetColumnSize(typeName);      // → GetBaseTypeName
record.BufferLength = GetBufferLength(typeName);      // → GetBaseTypeName
record.XdbcDecimalDigits = GetDecimalDigits(typeName);// → GetBaseTypeName
record.XdbcNumPrecRadix = GetNumPrecRadix(typeName);  // → GetBaseTypeName
record.XdbcCharOctetLength = GetCharOctetLength(typeName); // → GetBaseTypeName
record.SqlDataType = GetSqlDataType(typeName);        // → GetXdbcDataType → GetBaseTypeName
```

**Recommendation**: Cache base type name:
```csharp
public ColumnMetadataRecord PopulateColumnMetadata(...)
{
    string baseTypeName = _columnTypeMapper.GetBaseTypeName(typeName);
    record.BaseTypeName = baseTypeName;
    record.XdbcDataType = _columnTypeMapper.GetXdbcDataTypeFromBase(baseTypeName);
    record.XdbcColumnSize = GetColumnSizeFromBase(baseTypeName, typeName);
    // ... use baseTypeName for all lookups
}
```

---

## Recommended Fixes

### Fix 1: Preserve Original GetObjects Null Behavior

**File**: `HiveServer2Connection.cs`, after line 673

```csharp
// Override fields with Thrift-provided values (preserves exact current behavior)
record.XdbcDataType = colType;
record.Nullable = nullable;
record.IsNullable = isNullable;
record.IsAutoIncrement = isAutoIncrement ? "YES" : "NO";

// ADD THESE LINES to preserve original null behavior:
record.SqlDataType = colType;  // Fix Issue 4

// Preserve null for non-parameterized types (original behavior)
bool isParameterized = colType == (short)ColumnTypeId.DECIMAL ||
                       colType == (short)ColumnTypeId.NUMERIC ||
                       colType == (short)ColumnTypeId.CHAR ||
                       colType == (short)ColumnTypeId.VARCHAR ||
                       colType == (short)ColumnTypeId.NCHAR ||
                       colType == (short)ColumnTypeId.NVARCHAR ||
                       colType == (short)ColumnTypeId.LONGVARCHAR ||
                       colType == (short)ColumnTypeId.LONGNVARCHAR;

if (!isParameterized)
{
    record.XdbcColumnSize = null;      // Fix Issue 1
    record.XdbcDecimalDigits = null;   // Fix Issue 2
}
```

### Fix 2: Change BufferLength Type

**File**: `ColumnMetadataRecord.cs`, line 75

```csharp
// Change from:
public byte? BufferLength { get; set; }

// To:
public int? BufferLength { get; set; }
```

**File**: `MetadataSchemaBuilder.cs`, update AppendOrNull for Int8:
```csharp
private static void AppendOrNull(Int8Array.Builder builder, int? value)
{
    if (value.HasValue && value.Value >= -128 && value.Value <= 127)
        builder.Append((sbyte)value.Value);
    else
        builder.AppendNull();
}
```

### Fix 3: Add Base Type Name Caching (Performance)

**File**: `MetadataFieldPopulator.cs`

```csharp
public virtual ColumnMetadataRecord PopulateColumnMetadata(...)
{
    // Cache base type name for reuse
    string? baseTypeName = _columnTypeMapper.GetBaseTypeName(typeName);

    var record = new ColumnMetadataRecord
    {
        // ... existing assignments
        BaseTypeName = baseTypeName,
    };

    // Use cached baseTypeName for subsequent lookups
    record.XdbcDataType = (int?)_columnTypeMapper.GetXdbcDataTypeFromBase(baseTypeName);
    record.XdbcColumnSize = GetColumnSizeFromBase(baseTypeName, typeName);
    // ... etc
}
```

---

## Extensibility: Overriding Fields in DatabricksConnection

### Use Case
You have a different backend (like SEA) that doesn't return certain values that Thrift does, and you want to synthesize or override those values in `DatabricksConnection.cs`.

### Current Architecture Limitation

The `MetadataFieldPopulator` is created directly in `HiveServer2Connection.GetObjects()`:

```csharp
// HiveServer2Connection.cs line 625
var populator = new Metadata.MetadataFieldPopulator();  // Hard-coded instantiation
```

This means **DatabricksConnection cannot inject a custom populator** without overriding the entire `GetObjects()` method.

### Available Extension Points

#### 1. Virtual Methods in MetadataFieldPopulator

The following methods are `virtual` and can be overridden:

| Method | Purpose |
|--------|---------|
| `PopulateColumnMetadata()` | Override entire column metadata population |
| `GetColumnSize(typeName)` | Customize column size calculation |
| `GetDecimalDigits(typeName)` | Customize decimal digits calculation |
| `GetCharOctetLength(typeName)` | Customize character octet length |
| `PopulateCustomFields(record, customData)` | Add vendor-specific custom fields |

#### 2. Custom ColumnTypeMapper

You can pass a custom `ColumnTypeMapper` to the populator:

```csharp
// DatabricksColumnTypeMapper.cs
public class DatabricksColumnTypeMapper : ColumnTypeMapper
{
    public override int? GetColumnSize(string? typeName)
    {
        // Custom logic for Databricks-specific types
        if (typeName?.StartsWith("VARIANT") == true)
            return 0;  // Custom handling

        return base.GetColumnSize(typeName);
    }
}
```

#### 3. Post-Processing Records (Current Pattern)

The current code already post-processes records after `PopulateColumnMetadata`:

```csharp
// HiveServer2Connection.cs lines 669-673
var record = populator.PopulateColumnMetadata(...);

// Override fields with Thrift-provided values
record.XdbcDataType = colType;
record.Nullable = nullable;
record.IsNullable = isNullable;
record.IsAutoIncrement = isAutoIncrement ? "YES" : "NO";
```

### Recommended Solution: Add Virtual Factory Method

To enable DatabricksConnection to use a custom populator, add a virtual factory method:

**File**: `HiveServer2Connection.cs`

```csharp
/// <summary>
/// Creates the metadata field populator. Override in derived classes to use custom populators.
/// </summary>
protected virtual Metadata.MetadataFieldPopulator CreateMetadataFieldPopulator()
{
    return new Metadata.MetadataFieldPopulator();
}
```

**Update GetObjects() to use the factory**:
```csharp
// Line 625 - change from:
var populator = new Metadata.MetadataFieldPopulator();

// To:
var populator = CreateMetadataFieldPopulator();
```

**Then in DatabricksConnection.cs**:
```csharp
public class DatabricksConnection : SparkConnection
{
    protected override Metadata.MetadataFieldPopulator CreateMetadataFieldPopulator()
    {
        return new DatabricksMetadataFieldPopulator();
    }
}

public class DatabricksMetadataFieldPopulator : MetadataFieldPopulator
{
    protected override int? GetColumnSize(string? typeName)
    {
        // SEA doesn't return COLUMN_SIZE, so synthesize it
        return base.GetColumnSize(typeName);
    }

    protected override int? GetDecimalDigits(string? typeName)
    {
        // SEA doesn't return DECIMAL_DIGITS, so synthesize it
        return base.GetDecimalDigits(typeName);
    }
}
```

### Alternative: Add Virtual Customization Hook

If you don't want to change the factory pattern, add a virtual hook:

**File**: `HiveServer2Connection.cs`

```csharp
/// <summary>
/// Customizes a column metadata record after population. Override in derived classes
/// to apply backend-specific transformations.
/// </summary>
/// <param name="record">The record to customize</param>
/// <param name="colType">The Thrift-provided column type code</param>
/// <param name="typeName">The type name string</param>
/// <param name="columnSize">The Thrift-provided column size (may be 0 if not available)</param>
/// <param name="decimalDigits">The Thrift-provided decimal digits (may be 0 if not available)</param>
protected virtual void CustomizeColumnMetadataRecord(
    ColumnMetadataRecord record,
    short colType,
    string typeName,
    int columnSize,
    int decimalDigits)
{
    // Base implementation: use Thrift-provided values
    record.XdbcDataType = colType;
    record.SqlDataType = colType;
}
```

**In GetObjects(), replace lines 669-673**:
```csharp
var record = populator.PopulateColumnMetadata(...);

// Allow derived classes to customize
CustomizeColumnMetadataRecord(record, colType, typeName, columnSize, decimalDigits);

// Apply common overrides
record.Nullable = nullable;
record.IsNullable = isNullable;
record.IsAutoIncrement = isAutoIncrement ? "YES" : "NO";
```

**Then in DatabricksConnection.cs**:
```csharp
protected override void CustomizeColumnMetadataRecord(
    ColumnMetadataRecord record,
    short colType,
    string typeName,
    int columnSize,
    int decimalDigits)
{
    base.CustomizeColumnMetadataRecord(record, colType, typeName, columnSize, decimalDigits);

    // SEA backend doesn't return columnSize, so use synthesized value if Thrift value is 0
    if (columnSize == 0 && record.XdbcColumnSize.HasValue)
    {
        // Keep synthesized value from populator
    }
    else
    {
        // Use Thrift-provided value
        record.XdbcColumnSize = columnSize > 0 ? columnSize : null;
    }

    // Similar logic for decimalDigits
    if (decimalDigits == 0 && colType != (short)ColumnTypeId.DECIMAL)
    {
        // Keep synthesized value for non-DECIMAL types on SEA
    }
    else
    {
        record.XdbcDecimalDigits = decimalDigits;
    }
}
```

### Example: Override a Single Field

If you just want to change one field value when SEA doesn't return it:

```csharp
// In DatabricksConnection.cs (assuming virtual hook is added)

protected override void CustomizeColumnMetadataRecord(
    ColumnMetadataRecord record,
    short colType,
    string typeName,
    int columnSize,
    int decimalDigits)
{
    base.CustomizeColumnMetadataRecord(record, colType, typeName, columnSize, decimalDigits);

    // SEA returns BUFFER_LENGTH=null, but we want to synthesize it
    if (record.BufferLength == null)
    {
        record.BufferLength = (byte?)new ColumnTypeMapper().GetBufferLength(typeName);
    }
}
```

### Summary of Extensibility Options

| Option | Pros | Cons |
|--------|------|------|
| **Virtual Factory Method** | Clean, follows factory pattern | Requires adding method to base class |
| **Virtual Customization Hook** | Simple, targeted overrides | Method signature may need evolution |
| **Custom ColumnTypeMapper** | No base class changes needed | Only affects type mapping, not post-processing |
| **Override GetObjects entirely** | Full control | Code duplication, maintenance burden |

**Recommendation**: Add both the virtual factory method AND the customization hook to provide maximum flexibility for derived drivers.

---

## Conclusion

The refactoring introduces a well-architected abstraction layer but **changes output behavior** for several fields. To maintain backward compatibility with existing Thrift users:

1. **Must Fix**: Override `record.XdbcColumnSize`, `record.XdbcDecimalDigits`, and `record.SqlDataType` with original Thrift values/nulls
2. **Should Fix**: Change `BufferLength` from `byte?` to `int?` to prevent overflow
3. **Nice to Have**: Cache base type name to reduce redundant parsing (8x improvement per column)

Without these fixes, **Thrift output will differ from pre-refactor baseline** for the following fields:
- `xdbc_column_size`: Will have values instead of null for non-parameterized types
- `xdbc_decimal_digits`: Will have 0/7/15/6 instead of null for numeric/datetime types
- `xdbc_sql_data_type`: May differ if type parsing produces different result than server

---

## Quick Reference: Answers to Key Questions

### Q: Will the output be the same as before the refactor?

**NO** - For Thrift (GetObjects), the following fields will change:

| Field | Before | After |
|-------|--------|-------|
| `xdbc_column_size` | `null` for INT, FLOAT, etc. | `4`, `8`, etc. |
| `xdbc_decimal_digits` | `null` for non-DECIMAL | `0`, `7`, `15`, `6` |
| `xdbc_sql_data_type` | Thrift value | Synthesized (usually same) |

### Q: Are all the type casts correct?

**YES** - All `int? → short?` casts are safe (JDBC codes fit in short range).

**ONE ISSUE**: `byte → sbyte` cast for `BufferLength` can produce negative values if > 127. Current types don't exceed this, but it's a latent bug.

### Q: Can I override fields in DatabricksConnection when SEA doesn't return values?

**NOT DIRECTLY** with current code. The populator is hard-coded:
```csharp
var populator = new Metadata.MetadataFieldPopulator();  // No way to inject custom
```

**RECOMMENDED**: Add a virtual factory method to `HiveServer2Connection`:
```csharp
protected virtual MetadataFieldPopulator CreateMetadataFieldPopulator()
    => new MetadataFieldPopulator();
```

Then override in `DatabricksConnection`:
```csharp
protected override MetadataFieldPopulator CreateMetadataFieldPopulator()
    => new DatabricksMetadataFieldPopulator();
```

### Q: What's the performance impact?

**MINOR** - ~8x more method calls per column (cached), 1 object allocation per column. Dwarfed by network I/O.

# Breaking Changes Analysis - Metadata Refactoring

## Summary
The metadata refactoring introduced **5 breaking changes** in Thrift behavior by synthesizing values instead of using server-provided values. This document details each issue and the required fixes.

## Original Thrift Behavior (main branch)

**File**: `HiveServer2Connection.cs` lines 1274-1356 (GetColumnSchema method)

```csharp
private static StructArray GetColumnSchema(TableInfo tableInfo)
{
    for (int i = 0; i < tableInfo.ColumnName.Count; i++)
    {
        xdbcColumnSizeBuilder.Append(tableInfo.Precision[i]);      // Line 1305: FROM THRIFT
        xdbcDecimalDigitsBuilder.Append(tableInfo.Scale[i]);       // Line 1306: FROM THRIFT
        xdbcNumPrecRadixBuilder.AppendNull();                      // Line 1310: ALWAYS NULL
        xdbcSqlDataTypeBuilder.Append(tableInfo.ColType[i]);       // Line 1313: FROM THRIFT
        xdbcCharOctetLengthBuilder.AppendNull();                   // Line 1315: ALWAYS NULL
        xdbcDatetimeSubBuilder.AppendNull();                       // Line 1314: ALWAYS NULL
    }
}
```

**Key Point**: Values came from `tableInfo` which was populated by `SetPrecisionScaleAndTypeName()` using Thrift-provided data.

---

## Issue 1: `xdbc_column_size` - Changed from Thrift to Synthesized

### Original Behavior
```csharp
xdbcColumnSizeBuilder.Append(tableInfo.Precision[i]);  // Thrift-provided value
```

**Source**: `tableInfo.Precision` was populated in `SetPrecisionScaleAndTypeName`:
- For DECIMAL: Parsed from type string
- For VARCHAR/CHAR: Parsed from type string
- For others: `null`

### Current Behavior (metadata-refactor branch)
```csharp
// HiveServer2Connection.cs line 656
var record = populator.PopulateColumnMetadata(...);
// record.XdbcColumnSize = ColumnTypeMapper.GetColumnSize(typeName)  // SYNTHESIZED!

// NOT overridden with Thrift value!

// MetadataSchemaBuilder.BuildColumnsStructArray() line 335
AppendOrNull(xdbcColumnSizeBuilder, record.XdbcColumnSize);  // Uses synthesized
```

### Breaking Change
- **Old**: Used Thrift server's precision value (could be `null` for many types)
- **New**: Always synthesizes a value from type name (rarely `null`)

### Impact
- INTEGER: Was `null`, now `4`
- BIGINT: Was `null`, now `8`
- STRING: Was `null`, now `2147483647`

---

## Issue 2: `xdbc_decimal_digits` - Changed from Thrift to Synthesized

### Original Behavior
```csharp
xdbcDecimalDigitsBuilder.Append(tableInfo.Scale[i]);  // Thrift-provided value
```

### Current Behavior
```csharp
var record = populator.PopulateColumnMetadata(...);
// record.XdbcDecimalDigits = ColumnTypeMapper.GetDecimalDigits(typeName)  // SYNTHESIZED!

AppendOrNull(xdbcDecimalDigitsBuilder, (short?)record.XdbcDecimalDigits);
```

### Breaking Change
- **Old**: Used Thrift server's scale value (could be `null`)
- **New**: Synthesizes value - returns `0` for most types, `null` only for non-numeric types

### Impact
- VARCHAR: Was `null`, now `0` (after fix 90c4e685, but still wrong - should be `null`)
- INTEGER: Was `null`, now `0`

---

## Issue 3: `xdbc_num_prec_radix` - Now Populated (Was Always `null`)

### Original Behavior
```csharp
xdbcNumPrecRadixBuilder.AppendNull();  // ALWAYS NULL
```

### Current Behavior
```csharp
var record = populator.PopulateColumnMetadata(...);
// record.XdbcNumPrecRadix = ColumnTypeMapper.GetNumPrecRadix(typeName)  // SYNTHESIZED!

AppendOrNull(xdbcNumPrecRadixBuilder, (short?)record.XdbcNumPrecRadix);
```

### Breaking Change
- **Old**: Always `null` for all types
- **New**: Returns `10` for numeric types, `null` for others

### Impact
- INTEGER/DECIMAL/etc: Was `null`, now `10`

---

## Issue 4: `xdbc_sql_data_type` - May Differ from Thrift

### Original Behavior
```csharp
xdbcSqlDataTypeBuilder.Append(tableInfo.ColType[i]);  // Thrift-provided ColType
```

### Current Behavior
```csharp
var record = populator.PopulateColumnMetadata(...);
// record.SqlDataType = ColumnTypeMapper.GetSqlDataType(typeName)  // SYNTHESIZED!

// NOT overridden with Thrift value!

AppendOrNull(xdbcSqlDataTypeBuilder, (short?)record.SqlDataType);
```

### Breaking Change
- **Old**: Used exact type code from Thrift server
- **New**: Synthesizes from type name - could differ if type name parsing produces different result

### Risk Level
**MEDIUM** - If Thrift server sends non-standard type codes, the synthesized value will differ.

---

## Issue 5: `BufferLength` Cast - byte to sbyte Overflow

### Location
`MetadataSchemaBuilder.cs` lines 549-555

```csharp
private static void AppendOrNull(Int8Array.Builder builder, byte? value)
{
    if (value.HasValue)
        builder.Append((sbyte)value.Value);  // PROBLEM!
    else
        builder.AppendNull();
}
```

### Problem
- `byte` range: 0-255
- `sbyte` range: -128 to 127
- Cast truncates/wraps: `200` becomes `-56`

### Current Risk
- Maximum DECIMAL(38) BufferLength ≈ 26, so currently safe
- Custom types with larger buffer lengths would produce incorrect negative values

### Original Behavior
**BufferLength was NOT in the original GetColumnSchema** - This is a NEW field added by the refactoring!

Checking the original StandardSchemas.ColumnSchema (19 fields), BufferLength was NOT included in GetObjects output.

---

## Issue 6: Performance - Redundant GetBaseTypeName Calls

### Problem
`MetadataFieldPopulator.PopulateColumnMetadata` calls each ColumnTypeMapper method, and each method calls `GetBaseTypeName`:

```csharp
// PopulateColumnMetadata internally does:
record.XdbcDataType = GetXdbcDataType(typeName);         // calls GetBaseTypeName
record.BaseTypeName = GetBaseTypeName(typeName);          // calls GetBaseTypeName
record.XdbcColumnSize = GetColumnSize(typeName);          // calls GetBaseTypeName
record.BufferLength = GetBufferLength(typeName);          // calls GetBaseTypeName
record.XdbcDecimalDigits = GetDecimalDigits(typeName);    // calls GetBaseTypeName
record.XdbcNumPrecRadix = GetNumPrecRadix(typeName);      // calls GetBaseTypeName
record.XdbcCharOctetLength = GetCharOctetLength(typeName);// calls GetBaseTypeName
record.SqlDataType = GetSqlDataType(typeName);            // calls GetXdbcDataType → GetBaseTypeName
```

**Total**: 8+ calls to `GetBaseTypeName` per column!

### Impact
- Redundant string operations (IndexOf, Substring, ToUpperInvariant) × 8
- Method dispatch overhead
- Not critical for small result sets, but adds up for large tables

---

## Required Fixes

### Fix 1-4: Preserve Thrift Server Values

The shared abstractions should support **optional override** of synthesized values:

#### Option A: Add Override Parameters to PopulateColumnMetadata

```csharp
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
    object? customData = null,
    // NEW: Optional overrides for Thrift
    int? overrideXdbcDataType = null,
    int? overrideXdbcColumnSize = null,
    int? overrideXdbcDecimalDigits = null,
    short? overrideXdbcNumPrecRadix = null,
    int? overrideXdbcSqlDataType = null)
{
    var record = new ColumnMetadataRecord(...);

    // Use override if provided, otherwise synthesize
    record.XdbcColumnSize = overrideXdbcColumnSize ?? _columnTypeMapper.GetColumnSize(typeName);
    record.XdbcDecimalDigits = overrideXdbcDecimalDigits ?? _columnTypeMapper.GetDecimalDigits(typeName);
    record.XdbcNumPrecRadix = overrideXdbcNumPrecRadix ?? _columnTypeMapper.GetNumPrecRadix(typeName);
    record.SqlDataType = overrideXdbcSqlDataType ?? _columnTypeMapper.GetSqlDataType(typeName);

    return record;
}
```

#### Option B: Set Fields After Creation (Current Approach)

Keep PopulateColumnMetadata as-is for SEA, but for Thrift:

```csharp
// In HiveServer2Connection.GetObjects()
var record = populator.PopulateColumnMetadata(...);

// Override with Thrift-provided values to preserve exact behavior
record.XdbcColumnSize = precision;              // From SetPrecisionScaleAndTypeName
record.XdbcDecimalDigits = scale;               // From SetPrecisionScaleAndTypeName
record.XdbcNumPrecRadix = null;                 // Always null in original Thrift
record.SqlDataType = colType;                   // From Thrift server
record.XdbcCharOctetLength = null;              // Always null in original Thrift
```

### Fix 5: Change BufferLength Type

**In ColumnMetadataRecord.cs**:
```csharp
// OLD:
public byte? BufferLength { get; set; }

// NEW:
public int? BufferLength { get; set; }
```

**In MetadataSchemaBuilder.cs**:
```csharp
// OLD:
private static void AppendOrNull(Int8Array.Builder builder, byte? value)
{
    builder.Append((sbyte)value.Value);  // Cast issue
}

// NEW:
private static void AppendOrNull(Int8Array.Builder builder, int? value)
{
    builder.Append((sbyte)value.Value);  // Still needs validation!
}
```

**Better**: Validate range or use Int16Array instead of Int8Array for BUFFER_LENGTH field.

### Fix 6: Cache GetBaseTypeName Result

**In ColumnTypeMapper.cs**, add caching:

```csharp
public virtual ColumnMetadataRecord PopulateColumnMetadata(...)
{
    var record = new ColumnMetadataRecord(...);

    // Calculate base type ONCE
    string? baseType = _columnTypeMapper.GetBaseTypeName(typeName);

    // Pass baseType to each method to avoid redundant calls
    record.XdbcDataType = _columnTypeMapper.GetXdbcDataType(typeName, baseType);
    record.BaseTypeName = baseType;
    record.XdbcColumnSize = _columnTypeMapper.GetColumnSize(typeName, baseType);
    // ... etc
}
```

Or use a struct to hold all computed values:
```csharp
var typeInfo = _columnTypeMapper.GetAllFields(typeName);  // Single pass
record.XdbcDataType = typeInfo.XdbcDataType;
record.BaseTypeName = typeInfo.BaseTypeName;
// ...
```

---

## Recommended Approach

**Option B (Post-Override)** is simplest and preserves backward compatibility:

1. **For Thrift**: Use PopulateColumnMetadata(), then override specific fields with server values
2. **For SEA**: Use PopulateColumnMetadata() as-is (fully synthesized)
3. **Performance**: Add GetBaseTypeName caching (optional optimization)
4. **BufferLength**: Change from `byte?` to `int?` to prevent overflow

This approach:
- ✅ Preserves exact Thrift behavior
- ✅ Minimal changes to shared abstractions
- ✅ SEA can still benefit from full synthesis
- ✅ Clear separation of concerns

---

## Testing Requirements

After fixes, verify:

1. **Byte-level comparison**: Thrift GetObjects output on main vs. metadata-refactor
   - Compare xdbc_column_size, xdbc_decimal_digits, xdbc_num_prec_radix fields
   - Should be **exactly identical**

2. **Type coverage**: Test with all Databricks types:
   - INTEGER, BIGINT, DECIMAL(10,2), VARCHAR(100), STRING, TIMESTAMP, etc.

3. **NULL handling**: Verify fields that should be NULL are NULL

4. **Performance**: Measure GetObjects latency (should be ±5%)

# Fix Plan for Breaking Changes

## Strategy: Preserve Thrift Behavior, Enable SEA Synthesis

**Goal**: Make shared abstractions flexible enough to support BOTH patterns:
1. **Thrift**: Use server-provided values (preserve existing behavior)
2. **SEA**: Synthesize all values (no server metadata available)

---

## Implementation Steps

### Step 1: Fix HiveServer2Connection.GetObjects() - Override Synthesized Values

**File**: `csharp/src/Drivers/Apache/Hive2/HiveServer2Connection.cs`

**Current Code** (lines ~624-676):
```csharp
var record = populator.PopulateColumnMetadata(
    catalog, schemaDb, tableName, columnName, typeName,
    ordinalPos, isNullableBool, remarks: null, columnDefault, customData: null
);

// Override fields with Thrift-provided values (preserves exact current behavior)
record.XdbcDataType = colType;
record.Nullable = nullable;
record.IsNullable = isNullable;
record.IsAutoIncrement = isAutoIncrement ? "YES" : "NO";

tableMeta.Value.Columns.Add(record);
```

**Problem**: Not enough overrides!

**Fixed Code**:
```csharp
// First, get precision/scale from TableInfo (populated by SetPrecisionScaleAndTypeName)
// These come from Thrift server or parsing
int? precision = tableMeta.Value.Precision?.Count > i ? tableMeta.Value.Precision[i] : null;
short? scale = tableMeta.Value.Scale?.Count > i ? tableMeta.Value.Scale[i] : null;

var record = populator.PopulateColumnMetadata(
    catalog, schemaDb, tableName, columnName, typeName,
    ordinalPos, isNullableBool, remarks: null, columnDefault, customData: null
);

// Override ALL fields with Thrift-provided values to preserve exact behavior
record.XdbcDataType = colType;                           // From Thrift
record.XdbcColumnSize = precision;                       // From Thrift/parsing
record.XdbcDecimalDigits = scale;                        // From Thrift/parsing
record.XdbcNumPrecRadix = null;                          // Original Thrift: always null
record.SqlDataType = colType;                            // From Thrift (same as XdbcDataType)
record.XdbcCharOctetLength = null;                       // Original Thrift: always null
record.SqlDatetimeSub = null;                            // Original Thrift: always null
record.Nullable = nullable;                              // From Thrift
record.IsNullable = isNullable;                          // From Thrift
record.IsAutoIncrement = isAutoIncrement ? "YES" : "NO"; // From Thrift

tableMeta.Value.Columns.Add(record);
```

**Key Points**:
- Uses `PopulateColumnMetadata()` to get the structure
- Overrides specific fields with Thrift server values
- Preserves exact original behavior for all fields

---

### Step 2: Add TableInfo Access in GetObjects

**Problem**: Current code doesn't have access to the TableInfo.Precision and TableInfo.Scale that were populated by `SetPrecisionScaleAndTypeName`.

**Solution**: Need to track this in the `TableMetadata` struct.

**Current TableMetadata** (line ~1660):
```csharp
internal struct TableMetadata(string type)
{
    public string Type { get; } = type;
    public List<Metadata.ColumnMetadataRecord> Columns { get; } = new();
}
```

**Updated TableMetadata**:
```csharp
internal struct TableMetadata(string type)
{
    public string Type { get; } = type;
    public List<Metadata.ColumnMetadataRecord> Columns { get; } = new();

    // Store Thrift-provided precision/scale for override
    public List<int?> ThriftPrecision { get; } = new();
    public List<short?> ThriftScale { get; } = new();
}
```

**Update Population Code** (where SetPrecisionScaleAndTypeName is called):

Actually, looking at the current code more carefully, I see that `SetPrecisionScaleAndTypeName` is still being called during GetObjects population, but it's called on a local TableInfo that gets discarded. We need to capture those values.

Wait, let me re-read the current GetObjects implementation...

Actually, the current implementation doesn't call SetPrecisionScaleAndTypeName at all during GetObjects! It was removed. This is the problem.

**Better Solution**: Call SetPrecisionScaleAndTypeName to get precision/scale, then use those in the override.

---

### Step 3: Proper Fix - Preserve SetPrecisionScaleAndTypeName Flow

Looking at the original code, the flow was:
1. GetObjects fetches columns from Thrift
2. For each column, calls `SetPrecisionScaleAndTypeName(colType, typeName, tableInfo, columnSize, decimalDigits)`
3. This populates tableInfo.Precision, tableInfo.Scale, tableInfo.BaseTypeName
4. GetColumnSchema uses these values

The refactored code removed this flow entirely, which breaks the behavior.

**Proper Fix**:
```csharp
// In GetObjects column population loop (around line 624):

// Create a temporary TableInfo to get Thrift-style precision/scale
var tempTableInfo = new TableInfo(string.Empty);
Connection.SetPrecisionScaleAndTypeName(colType, typeName ?? string.Empty, tempTableInfo, columnSize, decimalDigits);

// Now use MetadataFieldPopulator to get the full record structure
var record = populator.PopulateColumnMetadata(
    catalog, schemaDb, tableName, columnName, typeName,
    ordinalPos, isNullableBool, remarks: null, columnDefault, customData: null
);

// Override with Thrift-provided values
record.XdbcDataType = colType;
record.XdbcColumnSize = tempTableInfo.Precision.Count > 0 ? tempTableInfo.Precision[0] : null;
record.XdbcDecimalDigits = tempTableInfo.Scale.Count > 0 ? tempTableInfo.Scale[0] : null;
record.XdbcNumPrecRadix = null;                          // Original: always null
record.SqlDataType = colType;
record.XdbcCharOctetLength = null;                       // Original: always null
record.SqlDatetimeSub = null;                            // Original: always null
record.Nullable = nullable;
record.IsNullable = isNullable;
record.IsAutoIncrement = isAutoIncrement ? "YES" : "NO";

tableMeta.Value.Columns.Add(record);
```

This preserves the exact original flow while still using the shared abstractions for structure.

---

### Step 4: Fix BufferLength Type

**File**: `csharp/src/Drivers/Apache/Hive2/Metadata/ColumnMetadataRecord.cs`

```csharp
// Change from:
public byte? BufferLength { get; set; }

// To:
public int? BufferLength { get; set; }
```

**File**: `csharp/src/Drivers/Apache/Hive2/Metadata/MetadataSchemaBuilder.cs`

```csharp
// Update AppendOrNull for BufferLength (find the Int8Array.Builder overload)
private static void AppendOrNull(Int8Array.Builder builder, int? value)
{
    if (value.HasValue)
    {
        // Validate range for Int8
        if (value.Value < -128 || value.Value > 127)
        {
            throw new ArgumentOutOfRangeException(nameof(value),
                $"Value {value.Value} is out of range for Int8 (-128 to 127)");
        }
        builder.Append((sbyte)value.Value);
    }
    else
        builder.AppendNull();
}
```

---

### Step 5: Performance Optimization - Cache GetBaseTypeName (Optional)

**File**: `csharp/src/Drivers/Apache/Hive2/Metadata/MetadataFieldPopulator.cs`

Add overload that accepts pre-computed base type:

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
    object? customData = null)
{
    var record = new ColumnMetadataRecord(catalogName, schemaName, tableName, columnName);

    // Calculate base type ONCE
    string? baseType = _columnTypeMapper.GetBaseTypeName(typeName);

    record.TypeName = typeName;
    record.BaseTypeName = baseType;
    record.OrdinalPosition = ordinalPosition;
    record.Remarks = remarks;
    record.ColumnDefault = columnDefault;

    // Pass baseType to avoid redundant parsing
    record.XdbcDataType = (int?)_columnTypeMapper.GetXdbcDataType(typeName);  // This still calls GetBaseTypeName internally

    // TODO: Refactor ColumnTypeMapper methods to accept optional baseType parameter

    // ... rest of population
}
```

**Better approach**: Refactor ColumnTypeMapper methods to accept optional baseType:

```csharp
// In ColumnTypeMapper.cs
public short? GetXdbcDataType(string typeName, string? baseType = null)
{
    if (string.IsNullOrEmpty(typeName))
        return null;

    baseType ??= GetBaseTypeName(typeName);  // Calculate only if not provided

    if (XdbcTypeCodes.TryGetValue(baseType, out var code))
        return code;

    return null;
}

// Similar updates for GetColumnSize, GetDecimalDigits, etc.
```

---

## Testing Plan

### Test 1: Byte-Level Output Comparison

```bash
cd /Users/madhavendra.rathore/Desktop/arrow-adbc/csharp

# Checkout main and build
git checkout main
dotnet build src/Drivers/Apache/Apache.Arrow.Adbc.Drivers.Apache.csproj
# Run test that captures GetObjects output
dotnet test --filter "GetObjectsMetadata" > /tmp/main-output.txt

# Checkout fixed branch and build
git checkout metadata-refactor-fixed
dotnet build src/Drivers/Apache/Apache.Arrow.Adbc.Drivers.Apache.csproj
# Run same test
dotnet test --filter "GetObjectsMetadata" > /tmp/fixed-output.txt

# Compare
diff /tmp/main-output.txt /tmp/fixed-output.txt
# Should be IDENTICAL for all xdbc_* fields
```

### Test 2: Field Value Verification

Create test to verify each field matches original behavior:

```csharp
[Fact]
public void GetObjects_PreservesThriftFieldValues()
{
    using var connection = CreateTestConnection();
    var stream = connection.GetObjects(GetObjectsDepth.All, null, null, null, null, null);

    while (await stream.ReadNextRecordBatchAsync() != null)
    {
        // For INTEGER type:
        Assert.Null(xdbcColumnSize);      // Was null in original
        Assert.Null(xdbcDecimalDigits);   // Was null in original
        Assert.Null(xdbcNumPrecRadix);    // Was always null

        // For DECIMAL(10,2):
        Assert.Equal(10, xdbcColumnSize);     // From Thrift
        Assert.Equal(2, xdbcDecimalDigits);   // From Thrift
        Assert.Null(xdbcNumPrecRadix);        // Was always null
    }
}
```

---

## Summary

**Required Changes**:
1. ✅ Override synthesized values with Thrift-provided values in GetObjects
2. ✅ Keep SetPrecisionScaleAndTypeName flow to get precision/scale
3. ✅ Change BufferLength from byte? to int? to prevent overflow
4. ✅ Add range validation for Int8 casts
5. 🔧 (Optional) Cache GetBaseTypeName for performance

**Result**:
- Thrift behavior: 100% preserved (byte-identical output)
- SEA can use full synthesis (no server metadata)
- Shared abstractions remain flexible for both use cases

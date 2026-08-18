-- Day 8 - Covering Indexes + Included Columns
-- Using the IndexPractice table from the previous indexing task.
--
-- The goal here was to see what happens when a query needs
-- a column that is not already in the index, and then fix it
-- by creating a covering index.

-- 1. BEFORE
-- The existing CustomerId index can find the matching rows,
-- but Status is not included in the index.
-- This can cause SQL Server to do a Key Lookup.

SELECT
    CustomerId,
    Status
FROM dbo.IndexPractice WITH (INDEX(IX_IndexPractice_CustomerId))
WHERE CustomerId = 5000
OPTION (RECOMPILE);


-- 2. CREATE THE COVERING INDEX
-- CustomerId is used to search for the rows.
-- Status is included so SQL Server can get everything it needs
-- directly from the index.

CREATE NONCLUSTERED INDEX IX_IndexPractice_CustomerId_Covering
ON dbo.IndexPractice(CustomerId)
INCLUDE (Status);


-- 3. AFTER
-- Run the same query again, but use the new covering index.
-- The Key Lookup should now be gone.

SELECT
    CustomerId,
    Status
FROM dbo.IndexPractice WITH (INDEX(IX_IndexPractice_CustomerId_Covering))
WHERE CustomerId = 5000
OPTION (RECOMPILE);


-- 4. CHECK THE NEW INDEX
-- This confirms that CustomerId is the key column
-- and Status is an included column.

SELECT
    i.name AS index_name,
    i.type_desc AS index_type,
    c.name AS column_name,
    ic.is_included_column
FROM sys.indexes AS i
JOIN sys.index_columns AS ic
    ON i.object_id = ic.object_id
    AND i.index_id = ic.index_id
JOIN sys.columns AS c
    ON ic.object_id = c.object_id
    AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('dbo.IndexPractice')
  AND i.name = 'IX_IndexPractice_CustomerId_Covering'
ORDER BY ic.key_ordinal, ic.index_column_id;


-- Results from the test:
-- Before: 30 logical reads
-- After: 2 logical reads
-- Difference: 28 fewer logical reads
--
-- The covering index removed the Key Lookup because
-- Status was included in the index.
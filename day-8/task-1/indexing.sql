-- Day 7 - Task 4: SQL Indexing
-- Table: dbo.IndexPractice
-- Dataset has around 120,000 rows


-- Create a clustered index on Id
CREATE CLUSTERED INDEX CX_IndexPractice_Id
ON dbo.IndexPractice(Id);


-- Add an index for CustomerId searches
CREATE NONCLUSTERED INDEX IX_IndexPractice_CustomerId
ON dbo.IndexPractice(CustomerId);


-- Add an index for Status searches
CREATE NONCLUSTERED INDEX IX_IndexPractice_Status
ON dbo.IndexPractice(Status);


-- Check records for a specific customer
SELECT *
FROM dbo.IndexPractice
WHERE CustomerId = 5000;


-- Check completed orders
SELECT *
FROM dbo.IndexPractice
WHERE Status = 'Completed';


-- Check a small range of Ids
SELECT *
FROM dbo.IndexPractice
WHERE Id BETWEEN 50000 AND 50100;


-- Look at recent queries involving IndexPractice
-- This helps compare things like execution count and logical reads
SELECT TOP (10)
    qs.execution_count,
    qs.total_logical_reads,
    qs.last_logical_reads,
    qs.total_elapsed_time,
    st.text AS query_text
FROM sys.dm_exec_query_stats AS qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
WHERE st.text LIKE '%IndexPractice%'
ORDER BY qs.last_execution_time DESC;


-- Check the CustomerId query in the query statistics
SELECT TOP (10)
    qs.execution_count,
    qs.total_logical_reads,
    qs.last_logical_reads,
    qs.total_elapsed_time,
    st.text AS query_text
FROM sys.dm_exec_query_stats AS qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
WHERE st.text LIKE 'SELECT%IndexPractice%'
  AND st.text LIKE '%CustomerId = 5000%'
ORDER BY qs.last_execution_time DESC;


-- Check the Id range query in the query statistics
SELECT TOP (10)
    qs.execution_count,
    qs.total_logical_reads,
    qs.last_logical_reads,
    qs.total_elapsed_time,
    st.text AS query_text
FROM sys.dm_exec_query_stats AS qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
WHERE st.text LIKE 'SELECT%IndexPractice%'
  AND st.text LIKE '%Id BETWEEN 50000 AND 50100%'
ORDER BY qs.last_execution_time DESC;


-- Look at CustomerId queries stored in Query Store
SELECT TOP (20)
    rs.count_executions,
    rs.avg_logical_io_reads,
    rs.last_logical_io_reads,
    qt.query_sql_text
FROM sys.query_store_query_text AS qt
JOIN sys.query_store_query AS q
    ON qt.query_text_id = q.query_text_id
JOIN sys.query_store_plan AS p
    ON q.query_id = p.query_id
JOIN sys.query_store_runtime_stats AS rs
    ON p.plan_id = rs.plan_id
WHERE qt.query_sql_text LIKE '%CustomerId%'
ORDER BY rs.last_execution_time DESC;


-- Look at Status queries stored in Query Store
SELECT TOP (20)
    rs.last_execution_time,
    rs.count_executions,
    rs.avg_logical_io_reads,
    rs.last_logical_io_reads,
    qt.query_sql_text
FROM sys.query_store_query_text AS qt
JOIN sys.query_store_query AS q
    ON qt.query_text_id = q.query_text_id
JOIN sys.query_store_plan AS p
    ON q.query_id = p.query_id
JOIN sys.query_store_runtime_stats AS rs
    ON p.plan_id = rs.plan_id
WHERE qt.query_sql_text LIKE '%Status%'
  AND qt.query_sql_text NOT LIKE '%INSERT INTO%'
  AND qt.query_sql_text NOT LIKE '%CREATE%'
  AND qt.query_sql_text NOT LIKE '%query_store%'
ORDER BY rs.last_execution_time DESC;


-- Verify that the indexes were created
SELECT
    i.name AS index_name,
    c.name AS column_name
FROM sys.indexes AS i
JOIN sys.index_columns AS ic
    ON i.object_id = ic.object_id
    AND i.index_id = ic.index_id
JOIN sys.columns AS c
    ON ic.object_id = c.object_id
    AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('dbo.IndexPractice')
ORDER BY i.name;


-- Results I observed:
-- CustomerId lookup: 827 logical reads
-- Status lookup: 827 logical reads
-- Id range query: 101 rows returned
--
-- The indexes reduced the amount of data SQL Server
-- needed to read for the tested queries.
Day 7 Task 4 Clustered vs Non Clustered Indexes

What I did

For this task I created a table called IndexPractice with 120000 rows.

I tested a few queries before and after adding indexes to see how indexes affect query performance.

I created

- A clustered index on Id
- A non clustered index on CustomerId
- A non clustered index on Status

Queries I tested

SELECT *
FROM dbo.IndexPractice
WHERE CustomerId = 5000;

SELECT *
FROM dbo.IndexPractice
WHERE Status = Completed;

SELECT *
FROM dbo.IndexPractice
WHERE Id BETWEEN 50000 AND 50100;

Logical Reads

I used Query Store to compare the logical reads before and after adding the indexes.

CustomerId query

Before index: 12773 logical reads
After index: 827 logical reads

Status query

Before index: 12773 logical reads
After index: 827 logical reads

The indexed queries used fewer logical reads, which shows that the indexes helped SQL Server find the required data more efficiently.

Indexes Created

CX_IndexPractice_Id
Type: Clustered
Column: Id

IX_IndexPractice_CustomerId
Type: Non clustered
Column: CustomerId

IX_IndexPractice_Status
Type: Non clustered
Column: Status

What I learned

I learned that indexes can make read queries faster because SQL Server does not need to scan as much data.

I also learned that a clustered index is used for the main table data structure, while non clustered indexes create additional structures that help SQL Server search specific columns.

What would break this

Adding too many indexes can make INSERT UPDATE and DELETE operations slower because SQL Server has to maintain the indexes when data changes.

Indexes also use additional storage.

The main trade off I observed is that indexes can improve read performance but add extra work on the write side.
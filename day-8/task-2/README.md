Day 8 Covering Indexes and Included Columns

What I did

For this task I used the dbo.IndexPractice table from the previous indexing task.

I tested a query that searches for a specific CustomerId and returns the Status.

At first the existing CustomerId index could find the matching rows, but it did not contain the Status column. Because of this SQL Server had to use a Key Lookup to get the missing data.

I then created a new covering index and added Status using INCLUDE.

Covering index

CREATE NONCLUSTERED INDEX IX_IndexPractice_CustomerId_Covering
ON dbo.IndexPractice(CustomerId)
INCLUDE (Status)

After creating this index I ran the same query again.

The Key Lookup was no longer needed because the index already contained both CustomerId and Status.

Logical reads

Before: 30 logical reads

After: 2 logical reads

Difference: 28 fewer logical reads

This was a 93.3 percent reduction in logical reads.

What I learned

I learned that INCLUDE can add extra columns to an index without making them part of the index key.

A covering index can help SQL Server avoid a Key Lookup and get all the required data directly from the index.

What would break this

Adding too many covering indexes can use more storage and can make INSERT UPDATE and DELETE operations slower because the indexes also need to be maintained.

If a query needs columns that are not covered by the index SQL Server may need to perform a Key Lookup again.
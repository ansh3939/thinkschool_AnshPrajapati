# Day 9 Task 1 - Reproduce and Resolve a Deadlock

## What I worked on

I created a simple two account setup to reproduce a classic deadlock using two SQL Server sessions.

Session 1 locks AccountId 1 first and then tries to update AccountId 2.

Session 2 locks AccountId 2 first and then tries to update AccountId 1.

When both sessions run at the same time, they wait for each other and SQL Server chooses one transaction as the deadlock victim.

## Deadlock victim message

Transaction (Process ID 52) was deadlocked on lock resources with another process and has been chosen as the deadlock victim. Rerun the transaction.
Create a deliberately bad legacy ASP.NET Core 10 minimal API project for a Day 1 refactoring exercise.
The main target is OrderController.cs.
Requirements:
1. Create an ASP.NET Core 10 Web API project.
2. Create an OrderController.cs of approximately 300 lines.
3. Put one giant POST /api/orders action in the controller.
4. The action must mix all of these responsibilities inline:
   - HTTP request handling
   - validation
   - business rules
   - EF Core DbContext access
   - database queries
   - entity creation
   - order total calculation
   - error handling
   - HTTP response construction
5. Use synchronous EF Core calls such as ToList(), FirstOrDefault(), SaveChanges(), etc. inside an async action.
6. Make the action return object rather than a typed ActionResult or typed response.
7. Include four empty catch { } blocks that swallow exceptions.
8. Include several poor practices typical of legacy code.
9. Include at least two subtle bugs:
   - one off-by-one bug
   - one possible null dereference
10. Do not add tests.
11. Keep the code intentionally messy but compilable.
12. Include realistic entities/models such as Order, OrderItem, Product, Customer and an EF Core DbContext.
13. Do not refactor anything.
14. The goal is to give another developer realistic legacy code to refactor later.
Create the project files and save the original OrderController.cs unchanged.
Also create a file called ORIGINAL_PROMPT.md containing this exact prompt.
Do not explain or improve the code. Generate the deliberately bad version only.

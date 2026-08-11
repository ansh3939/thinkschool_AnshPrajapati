# Why the rich model is better

The old `Quote` model was anemic because it only had properties. Other parts of the application were responsible for deciding whether a quote was valid. This meant different parts of the application could follow different rules.

The rich model keeps the important rules inside the `Quote` entity. `Author` must be 1–200 characters and `Text` must be 1–1000 characters. A quote is created through `Quote.Create(author, text)`, so invalid data is rejected before a valid Quote is created. The `Text` property cannot be changed after creation, and `Delete()` performs a soft delete instead of removing the quote.

This makes the model safer because the rules are in one place. Any code that creates or deletes a Quote has to follow the domain rules instead of remembering them separately.

For example, imagine a future background job imports quotes directly into the database. With the old anemic model, it could save a quote with an empty author or 1,001 characters because the entity itself did not stop it. The API might validate the data, but the background job could bypass that validation. With the rich model, `Quote.Create()` rejects the invalid data before it becomes a valid Quote.

The rich model therefore prevents invalid states instead of relying on every caller to remember the rules.

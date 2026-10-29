# ADR-0034: File-upload questions

**Status:** Accepted.

## Context
Some service questions are answered with documents or photos (a licence copy, site photos), and some files are needed
only for a given answer ("Do you have a previous licence?" — *Yes* asks for its copy). The question bank had no file
type, and requests could not keep files.

## Decision
- A question of type **`FILE`** with validation `extensions` (default: pdf, jpg, jpeg, png, doc, docx, xls, xlsx),
  `maxSizeMb` (1–25, default 10) and, when *multiple*, `maxFiles` (1–20, default 5). Other types ignore these settings.
- Files are **uploaded to the draft** one by one (`POST /requests/{id}/files?question=`), checked against the question
  (type by extension, size, count) and stored through `IFileStorage`; the `request_file` row keeps the question, file
  name, size, SHA-256, storage key and the content type of its extension (the browser's is not trusted). The upload
  puts the file's id in the answer; a single-file question replaces its previous file.
- The **answer is the list of file ids**. Saving keeps only ids of files uploaded with that request for that question,
  so a client cannot point at another request's files. Validation treats the answer like any other: required,
  required-when and visible-when (JSONLogic) decide whether a file is needed — no separate mechanism for "a file for a
  given answer".
- Applicants open and remove their files while the request is a draft; after submission files cannot change. Staff
  with *View* on requests open them from the request (`GET /admin/requests/{id}/files/{fileId}`), from the request
  screen and the Action Center task.
- The shared `dc-file-input` (choose or drop, size and type checked in the browser, open, remove) works through a
  `FileUploader`: the portal's uploads to the request; previews keep files in the browser.

## Consequences
- Files of a request live as long as the request (cascade delete of the rows; stored content follows the storage's
  retention).
- Workflow and document rules see file answers as lists of ids; rules that need a file only test that it exists.

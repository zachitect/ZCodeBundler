# ZCodeBundler

**ZCodeBundler** is a Windows utility for packaging selected code files into one readable `.zcb.txt` bundle and for decoding existing bundles into a reviewable source comparison workflow.

It is designed for chat-based LLM workflows, code review discussions, debugging sessions, and any situation where related project files need to be shared as one structured plain-text file instead of many separate uploads.

<img width="1586" height="793" alt="image" src="https://github.com/user-attachments/assets/aded3649-fb9d-4a2e-8200-89a9a072aea3" />
<img width="1586" height="793" alt="image" src="https://github.com/user-attachments/assets/a513c72c-703c-4e55-8ccd-3ef64172a1b8" />

---

## What ZCodeBundler Does

ZCodeBundler has two main workflows:

1. **Bundle selected source files** into one `.zcb.txt` file.
2. **Decode `.zcb.txt` bundles** to compare bundled content with local source files and optionally apply decoded changes back to disk.

The bundle format keeps file boundaries and metadata explicit while preserving the original file content exactly.

---

## Why Use It

Chat-based LLM tools work better when related files are provided together with clear file boundaries and source identity.

ZCodeBundler helps by preserving:

- the original source file path
- the file name
- the detected file type
- the source file modified timestamp
- the declared content length
- the original file content

This makes a selected codebase snapshot easier to upload, inspect, archive, compare, or discuss.

---

## Main Features

### Select Files from a Directory

Choose a folder and include eligible code/text files from the project structure.

### Tree-Based Selection

Optionally use a folder tree to choose specific files or folders.

### Drag and Drop

Drop files, folders, or `.zcb.txt` bundle files directly into the app.

### Review Before Bundling

Selected source files are listed before creating the output bundle.

### Create a Single `.zcb.txt` Bundle

Generate one plain-text bundle containing all selected files and metadata.

### Decode Existing Bundles

Add or drop one or more `.zcb.txt` bundles into the **ZDecoder** panel.

ZDecoder shows decoded files with compact columns for status, type, file name, bundled modified date, and bundle name. Full source and bundle paths are still available through tooltips, viewer text, and apply confirmations.

### Compare Source and Decoded Content

Double-click a decoded file to open a read-only side-by-side diff viewer.

The viewer shows:

- current source content on the left
- decoded bundle content on the right
- line numbers for both sides
- changed, added, and removed rows with visual highlighting

### Apply Decoded Changes

Decoded content can be applied back to the source file path when safe.

Supported apply cases include:

- replacing existing files when decoded content differs
- creating missing source files
- creating missing parent folders when needed

Apply actions use confirmation dialogs and refresh decoded statuses after writing.

### Duplicate Target Protection

If multiple decoded files target the same source path, ZCodeBundler marks them as duplicate targets and blocks apply until the duplicates are resolved.

### Exact Content Handling

Comparisons and writes use exact text content. ZCodeBundler does not normalize line endings, trim whitespace, reformat code, or alter decoded content before writing.

### Safe Content Framing

Each bundled file block includes `CONTENT_LENGTH`, allowing readers to recover the exact original file content even if the content itself contains text that looks like a bundle delimiter.

---

## Bundle Format

ZCodeBundler creates files using the `.zcb.txt` convention.

Example file name:

```text
MyProject-2026-07-07-22-02-57.zcb.txt
```

A bundle contains:

- a short header
- reader instructions
- one file block per bundled file
- metadata for each file
- the original source content for each file

Example structure:

```text
ZCODEBUNDLE_VERSION: 1
BUNDLE_TITLE: Selected Code Bundle
ROOT_PATH: C:\Projects\MyProject
GENERATED_AT: 2026-07-07 22:02
FILE_COUNT: 2

ZCODEBUNDLE_READER_INSTRUCTIONS:
This file is a ZCodeBundle multi-file source bundle.
Each bundled file starts with --- ZCODEBUNDLE_FILE_START ---.
File metadata appears before --- ZCODEBUNDLE_CONTENT_START ---.
Original file content appears after --- ZCODEBUNDLE_CONTENT_START ---.
If CONTENT_LENGTH is present, read exactly that many characters after the content start delimiter.
Each bundled file ends with --- ZCODEBUNDLE_FILE_END ---.
Use PATH as the file identity.
Do not treat bundle headers, metadata lines, or delimiter lines as source code.

--- ZCODEBUNDLE_FILE_START ---
FILE_NAME: Example.cs
PATH: C:\Projects\ExampleProject\Example.cs
TYPE: csharp
DATE_MODIFIED: 2026-07-07 15:42:10
CONTENT_LENGTH: 74
--- ZCODEBUNDLE_CONTENT_START ---
namespace ExampleProject;

public sealed class Example
{
}
--- ZCODEBUNDLE_FILE_END ---
```

The `PATH` value is the absolute source file path and is the source of truth for decoder compare/apply operations.

The bundle is intentionally plain text. It does not wrap source content in Markdown code fences and does not escape file content as JSON.

---

## Reading Bundle Content Correctly

Newer bundle files include this metadata line for each bundled file:

```text
CONTENT_LENGTH: 1234
```

When `CONTENT_LENGTH` is present, a reader should:

1. Find the `--- ZCODEBUNDLE_CONTENT_START ---` delimiter.
2. Move to the first character after the following line break.
3. Read exactly `CONTENT_LENGTH` characters.
4. Treat those characters as the original file content.
5. Treat `--- ZCODEBUNDLE_FILE_END ---` as the block terminator, not as the content boundary.

This matters because source files, README examples, tests, or generated files may legitimately contain text that looks like a ZCodeBundle delimiter.

Older bundles that do not contain `CONTENT_LENGTH` can still be read by using the file-end delimiter as the content boundary.

---

## Decoder Statuses

Decoded files can show these statuses:

- **Same**: source content exactly matches decoded content.
- **Different**: source content exists but differs from decoded content.
- **Missing**: the target source file does not exist.
- **InvalidPath**: the decoded path is not a valid absolute source path.
- **DuplicateTarget**: multiple decoded files target the same source path.
- **SourceReadError**: the source file exists but could not be read.

Only `Different` and `Missing` files are applied. Duplicate, invalid path, same, and read-error files are skipped or blocked according to the action.

---

## Basic Bundling Usage

1. Open ZCodeBundler.
2. Select a folder or drag files/folders into the app.
3. Optionally enable tree selection or unknown file type selection.
4. Review the selected file list.
5. Click **Create Bundle TXT**.
6. Save the generated `.zcb.txt` file.

---

## Basic Decoder Usage

1. Open the ZDecoder panel.
2. Click **Add Bundle** or drop `.zcb.txt` files into the app.
3. Review decoded file statuses.
4. Double-click a file to inspect the side-by-side diff.
5. Use apply actions only when the status and confirmation are correct.

A new decode action replaces the current decoded list and closes open decoded viewer windows.

---

## What It Is For

ZCodeBundler is useful for:

- sharing code context with chat-based LLM tools
- preparing selected files for code review discussions
- collecting related files for debugging conversations
- comparing bundled source snapshots against local files
- applying decoded bundle content back to source files after review
- keeping readable snapshots of selected project files

---

## What It Is Not

ZCodeBundler is not:

- a general code editor
- a source control system
- a build tool
- a test runner
- a merge tool
- an AI client
- a full project analyser

It is a practical source bundling and bundle decoding utility.

---

## Output

The result of bundling is a single `.zcb.txt` file.

This file can be opened, inspected, uploaded, copied, archived, or shared wherever one plain-text file is easier to handle than many separate source files.

---

## Project Status

ZCodeBundler is an early-stage utility focused on a practical workflow:

```text
select files -> create one readable bundle -> decode/compare/apply when needed
```

Future improvements may be added over time, but the main goal is to keep the tool simple, explicit, and safe.

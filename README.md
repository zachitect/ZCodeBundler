<img width="986" height="793" alt="image" src="https://github.com/user-attachments/assets/ba10ff9f-ae58-47cb-b727-bfdc165072e1" />

# ZCodeBundler

**ZCodeBundler** is a simple Windows app for creating a single text bundle from selected files in a codebase.

It is designed for situations where selected project files need to be shared as context in a chat-based LLM session. Instead of uploading files one by one, ZCodeBundler packages the selected files into one readable `.zcb.txt` bundle.

The bundle keeps each file clearly separated and includes basic information such as the file name, relative path, file type, last modified date, and original file content.

---

## What ZCodeBundler Does

ZCodeBundler helps turn a group of selected project files into one clean text file.

The basic workflow is:

1. Select or drop files and folders.
2. Review the files to include.
3. Create a `.zcb.txt` bundle.
4. Upload or share the generated bundle as code context.

The generated bundle is plain text, so it can be opened, inspected, copied, archived, or shared easily.

---

## Why Use It

Chat-based LLM tools often work better when related files are provided together with clear file boundaries.

ZCodeBundler helps with this by creating a single structured bundle that preserves:

- where each file came from
- what each file is called
- the file type
- the last modified date
- the original file content

This makes it easier to communicate a selected part of a codebase without manually copying and labelling multiple files.

---

## Main Features

### Select Files from a Directory

Choose a folder and select files from the project structure.

### Tree-Based Selection

Use a folder tree to choose specific files or groups of files.

### Drag and Drop

Drag files or folders directly into the app.

### Review Before Bundling

Selected files are listed before the bundle is created.

### Create a Single Text Bundle

Generate one `.zcb.txt` file containing all selected files.

### Clear File Boundaries

Each file in the bundle is separated with readable markers, so the structure remains easy to follow.

---

## Bundle Format

ZCodeBundler creates files using the `.zcb.txt` convention.

Example:

```text
MyProject-2026-07-07-22-02-57.zcb.txt
```

A bundle contains a short header followed by one block per selected file.

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
Each bundled file ends at --- ZCODEBUNDLE_FILE_END ---.
Use PATH as the file identity.
Do not treat bundle headers, metadata lines, or delimiter lines as source code.

--- ZCODEBUNDLE_FILE_START ---
FILE_NAME: Example.cs
PATH: src/Example.cs
TYPE: csharp
DATE_MODIFIED: 2026-07-07 15:42:10
--- ZCODEBUNDLE_CONTENT_START ---
[original file content]
--- ZCODEBUNDLE_FILE_END ---
```

The bundle is intentionally plain text. It does not wrap file content in Markdown code fences and does not escape the file content as JSON.

---

## What It Is For

ZCodeBundler is useful when selected files from a codebase need to be communicated clearly in a single file.

Typical uses include:

- sharing code context with a chat-based LLM session
- preparing files for code review discussions
- collecting related source files for debugging conversations
- packaging a focused part of a project for explanation or planning
- keeping a readable snapshot of selected files

---

## What It Is Not

ZCodeBundler is not:

- a code editor
- a source control tool
- a build tool
- a test runner
- a diff tool
- an AI client
- a project analyser

It only packages selected files into a structured plain-text bundle.

---

## Basic Usage

1. Open ZCodeBundler.
2. Select a folder or drag files/folders into the app.
3. Choose the files to include.
4. Review the selected file list.
5. Click **Create Bundle TXT**.
6. Save the generated `.zcb.txt` file.

---

## Output

The result is a single `.zcb.txt` file.

This file can be uploaded or shared wherever a single text file is easier to handle than many separate project files.

---

## Project Status

ZCodeBundler is an early-stage utility focused on one core workflow:

```text
select files → create one readable text bundle
```

Future improvements may be added over time, but the main goal is to keep the tool simple and practical.

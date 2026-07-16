<img width="1586" height="793" alt="image" src="https://github.com/user-attachments/assets/cab6053f-a8ea-40b3-92b5-a3b5d049ad9d" />
<img width="1586" height="793" alt="image" src="https://github.com/user-attachments/assets/579566fd-41bf-4705-8705-3cfe6c18f296" />

# ZCodeBundler (Coding with AI Chat)

**Want to use AI chat for coding across an entire project, not just individual code snippets?**

The app combines two connected workflows: **ZCodeBundler** sends the project context you choose—from a few related files to the full codebase—to a chat-based LLM, while **ZDecoder** brings the LLM's proposed multi-file changes back for side-by-side review and direct application.

There is no need to copy code blocks from the conversation, find each target file, and replace its contents manually. ZCodeBundler and ZDecoder handle the round trip between your project and the LLM chat while keeping every change under your review.

## Why ZCodeBundler?

- work with chat-based LLMs with common codebases, including C#, Python, Java, HTML, and many more;
- use ZCodeBundler to provide anything from focused file context to the full codebase;
- use ZDecoder to receive and review changes across multiple files from a single reply;
- apply approved changes directly to the original files or create new files;
- use a cost-effective alternative alongside AI coding apps, agents, and IDE integrations.

## Two Connected Workflows

### ZCodeBundler — send project context to the LLM

1. **Choose the context**  
   Select individual files, folders, or the full project.

2. **Create a code bundle**  
   ZCodeBundler combines the selected files into one `.zcb.txt` file for your AI chat.

3. **Ask the LLM to work on the project**  
   Upload the bundle to a web-based or desktop AI chat and describe the task.

4. **Use the built-in output prompt**  
   ZCodeBundler includes a ready-to-use prompt that tells the LLM how to return its changed files in the correct format.

### ZDecoder — review and apply the returned changes

5. **Open the LLM response**  
   Add or drop the returned `.zcb.txt` bundle into ZDecoder.

6. **Review the differences**  
   Compare each proposed file with your current source in the side-by-side diff viewer.

7. **Apply what you approve**  
   Write approved changes directly to their original paths or create new files when needed.

```text
Your project
    ↓
Selected files or full codebase
    ↓
Your preferred chat-based LLM
    ↓
Proposed multi-file changes
    ↓
Side-by-side difference review
    ↓
Approve and apply
```

## Works With the Tools You Already Use

ZCodeBundler works with web-based and desktop AI chats that can read and return text files. It does not require a specific LLM provider, IDE, editor, plugin, repository host, or project format.

Because it works with ordinary folders and files, it can be used with many languages and frameworks. It can also sit alongside IDE assistants, plugins, dedicated coding apps, and coding agents as another workflow option.

For some users, this can be a cost-effective way to handle multi-file LLM-assisted coding through an existing general AI chat subscription. Pricing and usage limits vary by provider, and ZCodeBundler does not include model access or an AI subscription.

## Built-In LLM Output Prompt

ZCodeBundler includes a complete prompt for returning proposed changes in ZCodeBundle format.

The prompt tells the LLM to:

- return only files that need to change;
- preserve each file's intended source path;
- return the complete final content of every changed file;
- avoid snippets, patches, partial files, and placeholder text;
- preserve code formatting, quotes, escape sequences, and line breaks;
- produce a response that ZDecoder can open directly.

Open **Default LLM Output Prompt** in ZCodeBundler and include it with your task. You do not need to write the formatting instructions yourself.

## Main Features

### ZCodeBundler

#### Flexible project context

- add files and folders through the picker or drag and drop;
- use tree selection for specific parts of a larger project;
- include a focused set of files or the full codebase;
- include recognised source and text formats automatically;
- optionally review and include unknown file extensions.

### ZDecoder

#### Multi-file change review

ZDecoder restores the LLM response into individual proposed files and compares each one with the current local source.

Each file is assigned a status:

- **Same** — the proposed content already matches the local file;
- **Different** — the local file exists but its content differs;
- **Missing** — the proposed file does not currently exist;
- **InvalidPath** — the proposed target is not a valid absolute path;
- **DuplicateTarget** — multiple proposed files target the same path;
- **SourceReadError** — the local file exists but could not be read.

#### Side-by-side differences

Double-click a decoded file to review:

- the current local source;
- the proposed LLM version;
- line numbers on both sides;
- added, removed, and changed rows;
- the source and bundle paths.

#### Direct application

After review, ZCodeBundler can:

- replace an existing file;
- create a new file;
- create missing parent folders;
- refresh file statuses after changes are written.

Apply actions require confirmation. Invalid paths, duplicate targets, unchanged files, and unreadable source files are not treated as normal writable changes.

### Shared exact content handling

ZCodeBundler preserves the original file content and the LLM's proposed output. It does not reformat code, trim whitespace, or normalise line endings before applying changes.

Each bundled file also keeps its name, absolute source path, detected file type, modified timestamp, and declared content length.

## You Stay in Control

ZCodeBundler does not give the LLM unrestricted access to your local files.

- You choose which files are shared.
- The LLM proposes the changes.
- ZCodeBundler shows the differences.
- You decide what is applied.

Nothing is written to your project until you approve it.

## Getting Started

### Requirements

- Windows
- .NET 8 Desktop Runtime, unless using a self-contained published build

### Build from source

```powershell
dotnet restore
dotnet build -c Release
dotnet publish -c Release
```

The project is configured as a self-contained Windows x64 WPF application with single-file publishing enabled.

## Basic Usage

### Prepare the project context

1. Open ZCodeBundler.
2. Select a folder, or drag files and folders into the application.
3. Enable tree selection if you want to choose specific parts of the project.
4. Review the selected files.
5. Create and save the `.zcb.txt` bundle.
6. Upload the bundle to your AI chat with your task.

### Ask for changes

1. Open **Default LLM Output Prompt** in ZCodeBundler.
2. Include the prompt with your task in the conversation.
3. Ask the LLM to return its changed files as a `.zcb.txt` bundle.

### Review and apply

1. Open the ZDecoder panel.
2. Add or drop the returned `.zcb.txt` bundle.
3. Review the status of each proposed file.
4. Double-click a file to inspect the differences.
5. Apply the changes you approve.

## The `.zcb.txt` Format

ZCodeBundle is a plain-text format that keeps multi-file source context readable and unambiguous.

A bundle contains:

- the format version;
- the selected root path;
- the generation time;
- the number of bundled files;
- reader instructions;
- one structured block for each source file;
- the exact content length of each file.

`CONTENT_LENGTH` allows readers to recover the original source exactly, even when the source itself contains text resembling a bundle delimiter. Older bundles without `CONTENT_LENGTH` remain supported.

## What ZCodeBundler Is Not

ZCodeBundler is not:

- an LLM, chatbot, or AI API client;
- an autonomous coding system;
- an IDE or source-code editor;
- a source-control system;
- a build or test runner.

It is a two-way workflow between your codebase and the chat-based LLM you already use: ZCodeBundler prepares the project context, and ZDecoder reviews and applies the returned changes. It can sit alongside IDE assistants, plugins, coding apps, and coding agents without depending on any of them.

## Project Status

ZCodeBundler is an evolving open-source Windows utility built around one simple workflow:

> **Give the LLM the context you choose, bring its multi-file changes back, review the differences, and apply only what you approve.**

## Contributing

Issues, ideas, and pull requests are welcome. Contributions should keep the workflow clear, keep the user in control, and preserve source content accurately.

# UOX3 DFN Studio

A modern, cross-platform editor for UOX3 DFN files with structured editing, validation, and visual tooling.

Built to make DFN editing faster, clearer, and less error-prone.

---

## Features

### Structured DFN Editing
- Parse DFN sections into a structured view  
- Edit tags without digging through raw text  
- Sync between text editor and structured view  
- Validation messages for common issues  

### Visual Tools
- Item ID picker with live preview (artLegacyMUL.uop + tiledata.mul)  
- Hue preview with color swatches and expanded tooltip  
- Inline preview panel for selected values  

### Smart Editing
- Auto-generate new sections based on folder type:
  - items  
  - npc  
  - creatures  
  - create (crafting)  
  - race  
- Context-aware templates  
- Double-click tag editing (ID picker, hue picker, etc.)  

### Workflow Improvements
- Undo / Redo support  
- Highlight edited vs saved lines  
- Search and navigation tools  
- Cleaner DFN organization  

### Cross Platform
Built with Avalonia:
- Windows  
- Linux  
- macOS  

---

## Screenshots

_Add screenshots here_

---

## Requirements

- .NET SDK (recommended: .NET 8 or newer)  
- UOX3 DFN files  

### For visual previews:
- artLegacyMUL.uop  
- tiledata.mul  

---

## Build Instructions

### Clone the repository

git clone https://github.com/YOUR_USERNAME/UOX3-DfnStudio.git  
cd UOX3-DfnStudio  

---

### Windows

dotnet publish -c Release -r win-x64 --self-contained true  

Output:  
bin/Release/net8.0/win-x64/publish/  

Run:  
UOX3DfnStudio.exe  

---

### Linux

dotnet publish -c Release -r linux-x64 --self-contained true  

Run:  
chmod +x UOX3DfnStudio  
./UOX3DfnStudio  

---

### macOS

dotnet publish -c Release -r osx-x64 --self-contained true  

Run:  
./UOX3DfnStudio  

---

## Usage

1. Click Open DFN Folder  
2. Select your UOX3 DFN directory  
3. Choose a file and section  
4. Edit using:
   - Raw text editor  
   - Structured tag view  
5. Double-click supported tags (like ID) for visual pickers  
6. Save changes  

---

## Supported DFN Types

- Items  
- NPCs  
- Creatures  
- Crafting (create)  
- Race  

---

## License

This project is licensed under the MIT License.  

You are free to use, modify, and distribute this software.

---

## Contributing

Contributions, bug reports, and suggestions are welcome.

When contributing:
- Keep changes focused  
- Follow existing code style  
- Avoid breaking DFN compatibility  

---

## About

UOX3 DFN Studio is built for developers and shard owners who want a better way to work with UOX3 data files without fighting raw text.

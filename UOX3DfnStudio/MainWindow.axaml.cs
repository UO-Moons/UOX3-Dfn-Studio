using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace UOX3DfnStudio;

public partial class MainWindow : Window
{
    private string currentFolder = string.Empty;
    private string currentSelectedFilePath = string.Empty;

    private readonly Dictionary<string, string> folderMap = new Dictionary<string, string>();
    private readonly Dictionary<string, string> fileMap = new Dictionary<string, string>();
    private readonly Dictionary<string, SectionLocation> sectionMap = new Dictionary<string, SectionLocation>();
    private readonly List<SectionSearchEntry> allSectionsForCurrentFile = new List<SectionSearchEntry>();
    private readonly Stack<FileSnapshot> undoStack = new Stack<FileSnapshot>();
    private readonly Stack<FileSnapshot> redoStack = new Stack<FileSnapshot>();

    private readonly Stack<string> editorUndoStack = new Stack<string>();
    private readonly Stack<string> editorRedoStack = new Stack<string>();
    private string lastEditorTextSnapshot = string.Empty;
    private bool suppressEditorUndoTracking = false;

    private readonly List<string> currentSavedSectionLines = new List<string>();
    private HashSet<int> recentlySavedLineIndexes = new HashSet<int>();

    private SectionLocation? currentSection = null;
    private bool suppressSectionSelectionChanged = false;

    private bool suppressStructuredSelectionSync = false;
    private bool suppressEditorToStructuredSync = false;

    private DfnSectionModel? currentParsedSection = null;
    private readonly ObservableCollection<StructuredTagEntry> currentStructuredTags = new ObservableCollection<StructuredTagEntry>();

    private string currentHueFilePath = string.Empty;
    private List<HueFileReader.HueEntry>? cachedHueEntries = null;

    private string currentArtUopFilePath = string.Empty;
    private string currentTileDataFilePath = string.Empty;
    private List<ArtFileReader.ItemArtEntry>? cachedItemArtEntries = null;

    public MainWindow()
    {
        InitializeComponent();

        FolderList.SelectionChanged += OnFolderSelected;
        FileList.SelectionChanged += OnFileSelected;
        SectionList.SelectionChanged += OnSectionSelected;
        SectionSearchBox.TextChanged += OnSectionSearchChanged;
        EditorBox.TextChanged += OnEditorTextChanged;
        EditorBox.PointerReleased += OnEditorPointerReleased;
        EditorBox.GotFocus += OnEditorCaretOrPointerChanged;
        StructuredTagList.SelectionChanged += OnStructuredTagSelected;
        StructuredTagList.DoubleTapped += OnStructuredTagDoubleTapped;
    }

    private string GetSelectedFolderType()
    {
        var selectedFolderKey = FolderList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedFolderKey))
        {
            return string.Empty;
        }

        var normalizedFolderKey = selectedFolderKey
            .Replace('\\', '/')
            .Trim('/')
            .ToLowerInvariant();

        var folderParts = normalizedFolderKey
            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        if (folderParts.Length == 0)
        {
            return string.Empty;
        }

        if (folderParts.Contains("creatures"))
        {
            return "creatures";
        }

        if (folderParts.Contains("create"))
        {
            return "create";
        }

        if (folderParts.Contains("npc"))
        {
            return "npc";
        }

        if (folderParts.Contains("items"))
        {
            return "items";
        }

        if (folderParts.Contains("race"))
        {
            return "race";
        }

        return string.Empty;
    }

    private HashSet<string> GetActiveKnownTagSet()
    {
        return DfnTagRegistry.GetKnownTagsForFolderType(GetSelectedFolderType());
    }

    private Dictionary<string, string> GetActiveTagDescriptions()
    {
        return DfnTagRegistry.GetTagDescriptionsForFolderType(GetSelectedFolderType());
    }

    private bool IsKnownTagName(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return true;
        }

        return GetActiveKnownTagSet().Contains(tagName);
    }

    private string GetClosestKnownTag(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return string.Empty;
        }

        string bestMatch = string.Empty;
        int bestDistance = int.MaxValue;

        foreach (var knownTag in GetActiveKnownTagSet())
        {
            int distance = ComputeLevenshteinDistance(tagName.ToUpperInvariant(), knownTag.ToUpperInvariant());
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestMatch = knownTag;
            }
        }

        if (bestDistance <= 3)
        {
            return bestMatch;
        }

        return string.Empty;
    }

    private string GetTagDescription(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return string.Empty;
        }

        var activeDescriptions = GetActiveTagDescriptions();

        if (activeDescriptions.TryGetValue(tagName, out var description))
        {
            return description;
        }

        return "No description available for this tag in this DFN type yet.";
    }

    private int ComputeLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
        {
            return target.Length;
        }

        if (string.IsNullOrEmpty(target))
        {
            return source.Length;
        }

        int[,] matrix = new int[source.Length + 1, target.Length + 1];

        for (int sourceIndex = 0; sourceIndex <= source.Length; sourceIndex++)
        {
            matrix[sourceIndex, 0] = sourceIndex;
        }

        for (int targetIndex = 0; targetIndex <= target.Length; targetIndex++)
        {
            matrix[0, targetIndex] = targetIndex;
        }

        for (int sourceIndex = 1; sourceIndex <= source.Length; sourceIndex++)
        {
            for (int targetIndex = 1; targetIndex <= target.Length; targetIndex++)
            {
                int cost = source[sourceIndex - 1] == target[targetIndex - 1] ? 0 : 1;

                matrix[sourceIndex, targetIndex] = Math.Min(
                    Math.Min(
                        matrix[sourceIndex - 1, targetIndex] + 1,
                        matrix[sourceIndex, targetIndex - 1] + 1),
                    matrix[sourceIndex - 1, targetIndex - 1] + cost);
            }
        }

        return matrix[source.Length, target.Length];
    }

    private bool IsHuePickerTag(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        switch (tagName.Trim().ToUpperInvariant())
        {
            case "COLOR":
            case "COLOUR":
            case "SKIN":
            case "HAIRCOLOR":
            case "HAIRCOLOUR":
            case "BEARDCOLOR":
            case "BEARDCOLOUR":
            case "EMOTECOLOR":
            case "EMOTECOLOUR":
            case "SAYCOLOR":
            case "SAYCOLOUR":
                return true;
            default:
                return false;
        }
    }

    private void ClearArtCache()
    {
        currentArtUopFilePath = string.Empty;
        currentTileDataFilePath = string.Empty;
        cachedItemArtEntries = null;
    }

    private void ClearHueCache()
    {
        currentHueFilePath = string.Empty;
        cachedHueEntries = null;
    }

    private void EnsureHueEntriesLoadedForPreview()
    {
        if (cachedHueEntries != null && cachedHueEntries.Count > 0)
        {
            return;
        }

        string hueFilePath = TryFindHueFilePathForPreview();
        if (string.IsNullOrWhiteSpace(hueFilePath) || !File.Exists(hueFilePath))
        {
            return;
        }

        var loadedEntries = HueFileReader.LoadHueEntries(hueFilePath);
        if (loadedEntries.Count == 0)
        {
            return;
        }

        currentHueFilePath = hueFilePath;
        cachedHueEntries = loadedEntries;
    }

    private string TryFindHueFilePathForPreview()
    {
        if (!string.IsNullOrWhiteSpace(currentHueFilePath) && File.Exists(currentHueFilePath))
        {
            return currentHueFilePath;
        }

        if (string.IsNullOrWhiteSpace(currentFolder))
        {
            return string.Empty;
        }

        var searchDirectory = new DirectoryInfo(currentFolder);

        while (searchDirectory != null)
        {
            string candidatePath = Path.Combine(searchDirectory.FullName, "hues.mul");
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            searchDirectory = searchDirectory.Parent;
        }

        return string.Empty;
    }

    private bool TryParseHueId(string tagValue, out int hueId)
    {
        hueId = 0;

        if (string.IsNullOrWhiteSpace(tagValue))
        {
            return false;
        }

        string normalizedValue = tagValue.Trim();

        if (normalizedValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(normalizedValue.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out hueId);
        }

        return int.TryParse(normalizedValue, out hueId);
    }

    private HueFileReader.HueEntry? TryGetHueEntryForTagValue(string tagValue)
    {
        EnsureHueEntriesLoadedForPreview();

        if (cachedHueEntries == null || cachedHueEntries.Count == 0)
        {
            return null;
        }

        if (!TryParseHueId(tagValue, out int hueId))
        {
            return null;
        }

        return cachedHueEntries.FirstOrDefault(hueEntry => hueEntry.HueId == hueId);
    }

    private bool IsItemArtPickerTag(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        return string.Equals(tagName.Trim(), "ID", StringComparison.OrdinalIgnoreCase);
    }

    private async void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open DFN Folder",
            AllowMultiple = false
        });

        var selectedFolder = folders.FirstOrDefault();
        if (selectedFolder == null)
        {
            return;
        }

        var folderPath = selectedFolder.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        currentFolder = folderPath;
        ClearHueCache();
        ClearArtCache();
        LoadFolders();
    }

    private void LoadFolders()
    {
        FolderList.ItemsSource = null;
        FileList.ItemsSource = null;
        SectionList.ItemsSource = null;
        SectionSearchBox.Text = string.Empty;
        EditorBox.Text = string.Empty;
        PreviewLineList.ItemsSource = null;

        currentSelectedFilePath = string.Empty;
        currentSection = null;
        currentSavedSectionLines.Clear();
        recentlySavedLineIndexes.Clear();

        ClearStructuredSectionView();

        folderMap.Clear();
        fileMap.Clear();
        sectionMap.Clear();
        allSectionsForCurrentFile.Clear();

        if (!Directory.Exists(currentFolder))
        {
            return;
        }

        var folderEntries = new List<string>();

        folderEntries.Add("(root)");
        folderMap["(root)"] = currentFolder;

        var subFolders = Directory.GetDirectories(currentFolder, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var folderPath in subFolders)
        {
            var relativePath = Path.GetRelativePath(currentFolder, folderPath);
            folderEntries.Add(relativePath);
            folderMap[relativePath] = folderPath;
        }

        FolderList.ItemsSource = folderEntries;
    }

    private void OnFolderSelected(object? sender, SelectionChangedEventArgs e)
    {
        FileList.ItemsSource = null;
        SectionList.ItemsSource = null;
        SectionSearchBox.Text = string.Empty;
        EditorBox.Text = string.Empty;
        PreviewLineList.ItemsSource = null;

        currentSelectedFilePath = string.Empty;
        currentSection = null;
        currentSavedSectionLines.Clear();
        recentlySavedLineIndexes.Clear();

        ClearStructuredSectionView();

        fileMap.Clear();
        sectionMap.Clear();
        allSectionsForCurrentFile.Clear();

        var selectedFolderKey = FolderList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedFolderKey))
        {
            return;
        }

        if (!folderMap.TryGetValue(selectedFolderKey, out var selectedFolderPath))
        {
            return;
        }

        var fileEntries = new List<string>();
        var dfnFiles = Directory.GetFiles(selectedFolderPath, "*.dfn", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in dfnFiles)
        {
            var fileName = Path.GetFileName(filePath);
            fileEntries.Add(fileName);
            fileMap[fileName] = filePath;
        }

        FileList.ItemsSource = fileEntries;
    }

    private void OnFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        SectionList.ItemsSource = null;
        SectionSearchBox.Text = string.Empty;
        EditorBox.Text = string.Empty;
        PreviewLineList.ItemsSource = null;

        currentSection = null;
        currentSavedSectionLines.Clear();
        recentlySavedLineIndexes.Clear();

        ClearStructuredSectionView();

        sectionMap.Clear();
        allSectionsForCurrentFile.Clear();

        var selectedFileKey = FileList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedFileKey))
        {
            return;
        }

        if (!fileMap.TryGetValue(selectedFileKey, out var filePath))
        {
            return;
        }

        currentSelectedFilePath = filePath;

        LoadSectionsFromFile(filePath);
        ApplySectionFilter();
    }

    private void LoadSectionsFromFile(string filePath)
    {
        sectionMap.Clear();
        allSectionsForCurrentFile.Clear();

        var lines = File.ReadAllLines(filePath);

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var trimmedLine = lines[lineIndex].Trim();

            if (!trimmedLine.StartsWith("[") || !trimmedLine.EndsWith("]"))
            {
                continue;
            }

            var headerText = trimmedLine;
            var nameValue = FindFirstNameValue(lines, lineIndex);
            var searchText = BuildSearchText(headerText, nameValue);
            var displayText = BuildDisplayText(headerText, nameValue);

            var sectionEntry = new SectionSearchEntry
            {
                HeaderText = headerText,
                NameValue = nameValue,
                SearchText = searchText,
                DisplayText = displayText
            };

            allSectionsForCurrentFile.Add(sectionEntry);

            sectionMap[displayText] = new SectionLocation
            {
                FilePath = filePath,
                HeaderLineIndex = lineIndex
            };
        }
    }

    private string FindFirstNameValue(string[] lines, int headerLineIndex)
    {
        bool foundOpeningBrace = false;
        int braceDepth = 0;

        for (int lineIndex = headerLineIndex + 1; lineIndex < lines.Length; lineIndex++)
        {
            var currentLine = lines[lineIndex];
            var trimmedLine = currentLine.Trim();

            if (!foundOpeningBrace)
            {
                if (IsOpeningBraceLine(trimmedLine))
                {
                    foundOpeningBrace = true;
                    braceDepth = 1;

                    if (trimmedLine.Length > 1)
                    {
                        var inlineName = trimmedLine.Substring(1).Trim();
                        if (!string.IsNullOrWhiteSpace(inlineName))
                        {
                            return inlineName;
                        }
                    }
                }

                continue;
            }

            if (IsOpeningBraceLine(trimmedLine))
            {
                braceDepth++;
                continue;
            }

            if (IsClosingBraceLine(trimmedLine))
            {
                braceDepth--;

                if (braceDepth == 0)
                {
                    break;
                }

                continue;
            }

            if (trimmedLine.StartsWith("NAME=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmedLine.Substring(5).Trim();
            }
        }

        return string.Empty;
    }

    private string BuildSearchText(string headerText, string nameValue)
    {
        if (string.IsNullOrWhiteSpace(nameValue))
        {
            return headerText;
        }

        return headerText + " " + nameValue;
    }

    private string BuildDisplayText(string headerText, string nameValue)
    {
        if (string.IsNullOrWhiteSpace(nameValue))
        {
            return headerText;
        }

        return headerText + " - " + nameValue;
    }

    private void OnSectionSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySectionFilter();
    }

    private void ApplySectionFilter()
    {
        var searchText = SectionSearchBox.Text ?? string.Empty;
        searchText = searchText.Trim();

        IEnumerable<SectionSearchEntry> filteredSections = allSectionsForCurrentFile;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filteredSections = filteredSections.Where(sectionEntry =>
                sectionEntry.SearchText.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        SectionList.ItemsSource = filteredSections
            .Select(sectionEntry => sectionEntry.DisplayText)
            .ToList();
    }

    private void OnSectionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (suppressSectionSelectionChanged)
        {
            return;
        }

        var selectedDisplayText = SectionList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedDisplayText))
        {
            return;
        }

        if (!sectionMap.TryGetValue(selectedDisplayText, out var sectionLocation))
        {
            return;
        }

        currentSection = sectionLocation;

        var sectionText = ReadSectionBlock(sectionLocation.FilePath, sectionLocation.HeaderLineIndex);
        var sectionLines = NormalizeEditorTextToLines(sectionText);

        currentSavedSectionLines.Clear();
        currentSavedSectionLines.AddRange(sectionLines);

        recentlySavedLineIndexes.Clear();
        editorUndoStack.Clear();
        editorRedoStack.Clear();
        EditorBox.Text = sectionText;

        currentParsedSection = ParseSectionText(sectionText);
        RefreshStructuredSectionView();
        RefreshPreviewLines();
        SyncStructuredSelectionFromEditorCaret();
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (!suppressEditorUndoTracking)
        {
            var currentText = EditorBox.Text ?? string.Empty;

            if (!string.Equals(currentText, lastEditorTextSnapshot, StringComparison.Ordinal))
            {
                editorUndoStack.Push(lastEditorTextSnapshot);
                editorRedoStack.Clear();
                lastEditorTextSnapshot = currentText;
            }
        }

        var editorText = EditorBox.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(editorText))
        {
            ClearStructuredSectionView();
        }
        else
        {
            currentParsedSection = ParseSectionText(editorText);
            RefreshStructuredSectionView();
        }

        RefreshPreviewLines();
        SyncStructuredSelectionFromEditorCaret();
    }

    private bool HasUnsavedEditorChanges()
    {
        var currentText = EditorBox.Text ?? string.Empty;
        var savedText = string.Join(Environment.NewLine, currentSavedSectionLines);

        return !string.Equals(currentText, savedText, StringComparison.Ordinal);
    }

    private void SetEditorTextWithoutTracking(string newText)
    {
        suppressEditorUndoTracking = true;
        EditorBox.Text = newText;
        suppressEditorUndoTracking = false;
        lastEditorTextSnapshot = newText;
    }

    private string ReadSectionBlock(string filePath, int headerLineIndex)
    {
        var lines = File.ReadAllLines(filePath);
        var blockLines = new List<string>();

        bool foundOpeningBrace = false;
        int braceDepth = 0;

        for (int lineIndex = headerLineIndex; lineIndex < lines.Length; lineIndex++)
        {
            var currentLine = lines[lineIndex];
            var trimmedLine = currentLine.Trim();

            blockLines.Add(currentLine);

            if (!foundOpeningBrace)
            {
                if (IsOpeningBraceLine(trimmedLine))
                {
                    foundOpeningBrace = true;
                    braceDepth = 1;
                }

                continue;
            }

            if (lineIndex != headerLineIndex && IsOpeningBraceLine(trimmedLine))
            {
                braceDepth++;
            }
            else if (IsClosingBraceLine(trimmedLine))
            {
                braceDepth--;

                if (braceDepth == 0)
                {
                    break;
                }
            }
        }

        return string.Join(Environment.NewLine, blockLines);
    }

    private void OnSaveSection(object? sender, RoutedEventArgs e)
    {
        if (currentSection == null)
        {
            return;
        }

        if (!File.Exists(currentSection.FilePath))
        {
            return;
        }

        PushUndoSnapshot(currentSection.FilePath);
        redoStack.Clear();

        var filePath = currentSection.FilePath;
        var lines = File.ReadAllLines(filePath).ToList();

        int startIndex = currentSection.HeaderLineIndex;
        int endIndex = currentSection.HeaderLineIndex;

        FindSectionRange(lines, currentSection.HeaderLineIndex, out startIndex, out endIndex);

        var newBlockLines = NormalizeEditorTextToLines(EditorBox.Text);
        var changedIndexes = GetChangedLineIndexes(currentSavedSectionLines, newBlockLines);

        lines.RemoveRange(startIndex, endIndex - startIndex + 1);
        lines.InsertRange(startIndex, newBlockLines);

        File.WriteAllLines(filePath, lines);

        currentSavedSectionLines.Clear();
        currentSavedSectionLines.AddRange(newBlockLines);

        recentlySavedLineIndexes = changedIndexes;
        editorUndoStack.Clear();
        editorRedoStack.Clear();
        lastEditorTextSnapshot = EditorBox.Text ?? string.Empty;

        ReloadCurrentFileAndKeepEditorState(filePath, GetHeaderFromLines(newBlockLines), true);
        RefreshPreviewLines();
    }

    private async void OnNewSection(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(currentSelectedFilePath) || !File.Exists(currentSelectedFilePath))
        {
            return;
        }

        PushUndoSnapshot(currentSelectedFilePath);
        redoStack.Clear();

        var lines = File.ReadAllLines(currentSelectedFilePath).ToList();
        var existingHeaders = ExtractSectionHeaders(lines);

        string selectedFolderType = GetSelectedFolderType();
        string newHeader;

        if (selectedFolderType == "create")
        {
            var createSectionType = await PromptForCreateSectionTypeAsync();
            if (string.IsNullOrWhiteSpace(createSectionType))
            {
                return;
            }

            newHeader = GenerateUniqueSectionHeader(existingHeaders, createSectionType);
        }
        else
        {
            newHeader = GenerateUniqueSectionHeader(existingHeaders);
        }

        var newBlockLines = BuildNewSectionTemplate(newHeader);

        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
        {
            lines.Add(string.Empty);
        }

        lines.AddRange(newBlockLines);

        File.WriteAllLines(currentSelectedFilePath, lines);

        currentSavedSectionLines.Clear();
        currentSavedSectionLines.AddRange(newBlockLines);

        recentlySavedLineIndexes = new HashSet<int>();
        LoadSectionsFromFile(currentSelectedFilePath);
        ApplySectionFilter();
        SelectSectionByHeader(newHeader, false);
    }

    private void OnDeleteSection(object? sender, RoutedEventArgs e)
    {
        if (currentSection == null)
        {
            return;
        }

        if (!File.Exists(currentSection.FilePath))
        {
            return;
        }

        PushUndoSnapshot(currentSection.FilePath);
        redoStack.Clear();

        var filePath = currentSection.FilePath;
        var lines = File.ReadAllLines(filePath).ToList();

        int startIndex = currentSection.HeaderLineIndex;
        int endIndex = currentSection.HeaderLineIndex;

        FindSectionRange(lines, currentSection.HeaderLineIndex, out startIndex, out endIndex);

        lines.RemoveRange(startIndex, endIndex - startIndex + 1);

        while (startIndex < lines.Count && string.IsNullOrWhiteSpace(lines[startIndex]))
        {
            lines.RemoveAt(startIndex);
        }

        File.WriteAllLines(filePath, lines);

        currentSection = null;
        currentSavedSectionLines.Clear();
        recentlySavedLineIndexes.Clear();

        editorUndoStack.Clear();
        editorRedoStack.Clear();
        SetEditorTextWithoutTracking(string.Empty);
        PreviewLineList.ItemsSource = null;

        ClearStructuredSectionView();

        LoadSectionsFromFile(filePath);
        ApplySectionFilter();
    }

    private void OnUndo(object? sender, RoutedEventArgs e)
    {
        if (editorUndoStack.Count > 0 && HasUnsavedEditorChanges())
        {
            var currentText = EditorBox.Text ?? string.Empty;
            var previousText = editorUndoStack.Pop();

            editorRedoStack.Push(currentText);
            SetEditorTextWithoutTracking(previousText);
            RefreshPreviewLines();
            return;
        }

        if (undoStack.Count == 0)
        {
            return;
        }

        var snapshotToRestore = undoStack.Pop();

        if (File.Exists(snapshotToRestore.FilePath))
        {
            redoStack.Push(CaptureSnapshot(snapshotToRestore.FilePath, GetCurrentEditorHeader()));
        }

        RestoreSnapshot(snapshotToRestore);
    }

    private void OnRedo(object? sender, RoutedEventArgs e)
    {
        if (editorRedoStack.Count > 0)
        {
            var currentText = EditorBox.Text ?? string.Empty;
            var redoText = editorRedoStack.Pop();

            editorUndoStack.Push(currentText);
            SetEditorTextWithoutTracking(redoText);
            RefreshPreviewLines();
            return;
        }

        if (redoStack.Count == 0)
        {
            return;
        }

        var snapshotToRestore = redoStack.Pop();

        if (File.Exists(snapshotToRestore.FilePath))
        {
            undoStack.Push(CaptureSnapshot(snapshotToRestore.FilePath, GetCurrentEditorHeader()));
        }

        RestoreSnapshot(snapshotToRestore);
    }

    private void PushUndoSnapshot(string filePath)
    {
        undoStack.Push(CaptureSnapshot(filePath, GetCurrentEditorHeader()));
    }

    private FileSnapshot CaptureSnapshot(string filePath, string selectedHeader)
    {
        return new FileSnapshot
        {
            FilePath = filePath,
            FileLines = File.ReadAllLines(filePath).ToList(),
            SelectedHeader = selectedHeader
        };
    }

    private void RestoreSnapshot(FileSnapshot snapshot)
    {
        File.WriteAllLines(snapshot.FilePath, snapshot.FileLines);

        currentSelectedFilePath = snapshot.FilePath;

        var selectedFileName = Path.GetFileName(snapshot.FilePath);

        suppressSectionSelectionChanged = true;
        FileList.SelectedItem = selectedFileName;
        suppressSectionSelectionChanged = false;

        LoadSectionsFromFile(snapshot.FilePath);
        ApplySectionFilter();

        recentlySavedLineIndexes.Clear();

        if (!string.IsNullOrWhiteSpace(snapshot.SelectedHeader))
        {
            SelectSectionByHeader(snapshot.SelectedHeader, false);
        }
        else
        {
            currentSection = null;
            currentSavedSectionLines.Clear();
            EditorBox.Text = string.Empty;
            PreviewLineList.ItemsSource = null;
            ClearStructuredSectionView();
        }
    }

    private string GetCurrentEditorHeader()
    {
        var editorLines = NormalizeEditorTextToLines(EditorBox.Text);

        if (editorLines.Count == 0)
        {
            return string.Empty;
        }

        return editorLines[0].Trim();
    }

    private void FindSectionRange(List<string> lines, int headerLineIndex, out int startIndex, out int endIndex)
    {
        startIndex = headerLineIndex;
        endIndex = headerLineIndex;

        bool foundOpeningBrace = false;
        int braceDepth = 0;

        for (int lineIndex = headerLineIndex; lineIndex < lines.Count; lineIndex++)
        {
            var trimmedLine = lines[lineIndex].Trim();

            if (!foundOpeningBrace)
            {
                if (IsOpeningBraceLine(trimmedLine))
                {
                    foundOpeningBrace = true;
                    braceDepth = 1;
                }

                continue;
            }

            if (lineIndex != headerLineIndex && IsOpeningBraceLine(trimmedLine))
            {
                braceDepth++;
            }
            else if (IsClosingBraceLine(trimmedLine))
            {
                braceDepth--;

                if (braceDepth == 0)
                {
                    endIndex = lineIndex;
                    break;
                }
            }
        }
    }

    private List<string> NormalizeEditorTextToLines(string? editorText)
    {
        var safeText = editorText ?? string.Empty;

        return safeText
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();
    }

    private List<string> ExtractSectionHeaders(List<string> lines)
    {
        var headers = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                headers.Add(trimmedLine);
            }
        }

        return headers;
    }

    private string GenerateUniqueSectionHeader(List<string> existingHeaders)
    {
        return GenerateUniqueSectionHeader(existingHeaders, string.Empty);
    }

    private string GenerateUniqueSectionHeader(List<string> existingHeaders, string createSectionType)
    {
        if (GetSelectedFolderType() == "creatures")
        {
            int creatureId = 1;

            while (true)
            {
                var candidateHeader = "[CREATURE 0x" + creatureId.ToString("X") + "]";

                if (!existingHeaders.Any(header => header.Equals(candidateHeader, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidateHeader;
                }

                creatureId++;
            }
        }

        if (GetSelectedFolderType() == "create")
        {
            string normalizedCreateSectionType = (createSectionType ?? string.Empty).Trim().ToUpperInvariant();

            if (normalizedCreateSectionType == "SUBMENU")
            {
                int submenuId = 1;

                while (true)
                {
                    var candidateHeader = "[SUBMENU " + submenuId + "]";

                    if (!existingHeaders.Any(header => header.Equals(candidateHeader, StringComparison.OrdinalIgnoreCase)))
                    {
                        return candidateHeader;
                    }

                    submenuId++;
                }
            }

            if (normalizedCreateSectionType == "ITEM")
            {
                int itemId = 1;

                while (true)
                {
                    var candidateHeader = "[ITEM " + itemId + "]";

                    if (!existingHeaders.Any(header => header.Equals(candidateHeader, StringComparison.OrdinalIgnoreCase)))
                    {
                        return candidateHeader;
                    }

                    itemId++;
                }
            }

            int menuEntryId = 1;

            while (true)
            {
                var candidateHeader = "[MENUENTRY " + menuEntryId + "]";

                if (!existingHeaders.Any(header => header.Equals(candidateHeader, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidateHeader;
                }

                menuEntryId++;
            }
        }

        if (GetSelectedFolderType() == "npc")
        {
            int npcNumber = 1;

            while (true)
            {
                var candidateHeader = "[new_npc_" + npcNumber + "]";

                if (!existingHeaders.Any(header => header.Equals(candidateHeader, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidateHeader;
                }

                npcNumber++;
            }
        }

        if (GetSelectedFolderType() == "items")
        {
            int itemId = 1;

            while (true)
            {
                var candidateHeader = "[0x" + itemId.ToString("X4").ToLowerInvariant() + "]";

                if (!existingHeaders.Any(header => header.Equals(candidateHeader, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidateHeader;
                }

                itemId++;
            }
        }

        if (GetSelectedFolderType() == "race")
        {
            int raceId = 0;

            while (true)
            {
                var candidateHeader = "[RACE " + raceId + "]";

                if (!existingHeaders.Any(header => header.Equals(candidateHeader, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidateHeader;
                }

                raceId++;
            }
        }

        int sectionNumber = 1;

        while (true)
        {
            var candidateHeader = "[new_section_" + sectionNumber + "]";

            if (!existingHeaders.Any(header => header.Equals(candidateHeader, StringComparison.OrdinalIgnoreCase)))
            {
                return candidateHeader;
            }

            sectionNumber++;
        }
    }

    private List<string> BuildNewSectionTemplate(string? headerText)
    {
        string safeHeaderText = string.IsNullOrWhiteSpace(headerText) ? "[NEWSECTION]" : headerText;
        string selectedFolderType = GetSelectedFolderType();
        string normalizedHeader = safeHeaderText.Trim().ToUpperInvariant();

        if (selectedFolderType == "creatures")
        {
            return new List<string>
            {
            safeHeaderText,
            "{ New Creature",
            "ICON=0x0000",
            "MOVEMENT=LAND",
            "}",
            };
        }

        if (selectedFolderType == "create")
        {
            if (normalizedHeader.StartsWith("[MENUENTRY ", StringComparison.Ordinal))
            {
                return new List<string>
                {
                safeHeaderText,
                "{",
                "NAME=New Menu Entry",
                "ID=0x0000",
                "SUBMENU=1",
                "}",
                };
            }

            if (normalizedHeader.StartsWith("[SUBMENU ", StringComparison.Ordinal))
            {
                return new List<string>
                {
                safeHeaderText,
                "{",
                "ITEM=1",
                "}",
                };
            }

            if (normalizedHeader.StartsWith("[ITEM ", StringComparison.Ordinal))
            {
                return new List<string>
                {
                safeHeaderText,
                "{",
                "NAME=New Craft Item",
                "ID=0x0000",
                "RESOURCE=0x0000 1",
                "SKILL=0 0 1000",
                "ADDITEM=",
                "MINRANK=1",
                "MAXRANK=10",
                "}",
                };
            }

            return new List<string>
            {
            safeHeaderText,
            "{",
            "NAME=New Entry",
            "ID=0x0000",
            "}",
            };
        }

        if (selectedFolderType == "items")
        {
            return new List<string>
            {
            safeHeaderText,
            "{",
            "GET=base_item",
            "NAME=New Item",
            "ID=0x0000",
            "WEIGHT=100",
            "VALUE=1 1",
            "MOVABLE=1",
            "DECAY=1",
            "}",
            };
        }

        if (selectedFolderType == "npc")
        {
            return new List<string>
            {
            safeHeaderText,
            "{",
            "NAME=New NPC",
            "ID=0x0190",
            "STR=50 60",
            "DEX=50 60",
            "INT=50 60",
            "NPCWANDER=2",
            "NPCAI=2",
            "FLAG=NEUTRAL",
            "}",
            };
        }

        if (selectedFolderType == "race")
        {
            return new List<string>
            {
            safeHeaderText,
            "{",
            "NAME=New Race",
            "STRCAP=100",
            "DEXCAP=100",
            "INTCAP=100",
            "PLAYERRACE=0",
            "HEALTHREGENBONUS=0",
            "STAMINAREGENBONUS=0",
            "MANAREGENBONUS=0",
            "MAXWEIGHTBONUS=0",
            "}",
            };
        }

        return new List<string>
        {
        safeHeaderText,
        "{",
        "NAME=New Section",
        "ID=0x0000",
        "}",
        };
    }

    private void ReloadCurrentFileAndKeepEditorState(string filePath, string headerText, bool keepSavedHighlight)
    {
        LoadSectionsFromFile(filePath);
        ApplySectionFilter();

        SelectSectionByHeader(headerText, keepSavedHighlight);
    }

    private void SelectSectionByHeader(string headerText, bool keepSavedHighlight)
    {
        var matchingEntry = allSectionsForCurrentFile.FirstOrDefault(sectionEntry =>
            sectionEntry.HeaderText.Equals(headerText, StringComparison.OrdinalIgnoreCase));

        if (matchingEntry == null)
        {
            return;
        }

        if (keepSavedHighlight)
        {
            if (!sectionMap.TryGetValue(matchingEntry.DisplayText, out var sectionLocation))
            {
                return;
            }

            currentSection = sectionLocation;

            var sectionText = ReadSectionBlock(sectionLocation.FilePath, sectionLocation.HeaderLineIndex);
            editorUndoStack.Clear();
            editorRedoStack.Clear();
            SetEditorTextWithoutTracking(sectionText);

            currentParsedSection = ParseSectionText(sectionText);
            RefreshStructuredSectionView();
            RefreshPreviewLines();
            SyncStructuredSelectionFromEditorCaret();

            suppressSectionSelectionChanged = true;
            SectionList.SelectedItem = matchingEntry.DisplayText;
            suppressSectionSelectionChanged = false;

            return;
        }

        suppressSectionSelectionChanged = true;
        SectionList.SelectedItem = matchingEntry.DisplayText;
        suppressSectionSelectionChanged = false;

        if (!sectionMap.TryGetValue(matchingEntry.DisplayText, out var location))
        {
            return;
        }

        currentSection = location;

        var loadedSectionText = ReadSectionBlock(location.FilePath, location.HeaderLineIndex);
        var loadedSectionLines = NormalizeEditorTextToLines(loadedSectionText);

        currentSavedSectionLines.Clear();
        currentSavedSectionLines.AddRange(loadedSectionLines);

        editorUndoStack.Clear();
        editorRedoStack.Clear();
        SetEditorTextWithoutTracking(loadedSectionText);

        currentParsedSection = ParseSectionText(loadedSectionText);
        RefreshStructuredSectionView();
        RefreshPreviewLines();
        SyncStructuredSelectionFromEditorCaret();
    }

    private string GetHeaderFromLines(List<string> lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        return lines[0].Trim();
    }

    private HashSet<int> GetChangedLineIndexes(List<string> originalLines, List<string> newLines)
    {
        var changedIndexes = new HashSet<int>();
        int maxLineCount = Math.Max(originalLines.Count, newLines.Count);

        for (int lineIndex = 0; lineIndex < maxLineCount; lineIndex++)
        {
            var originalLine = lineIndex < originalLines.Count ? originalLines[lineIndex] : string.Empty;
            var newLine = lineIndex < newLines.Count ? newLines[lineIndex] : string.Empty;

            if (!string.Equals(originalLine, newLine, StringComparison.Ordinal))
            {
                changedIndexes.Add(lineIndex);
            }
        }

        return changedIndexes;
    }

    private void RefreshPreviewLines()
    {
        var editorLines = NormalizeEditorTextToLines(EditorBox.Text);
        var previewEntries = new List<PreviewLineEntry>();

        for (int lineIndex = 0; lineIndex < editorLines.Count; lineIndex++)
        {
            var currentLine = editorLines[lineIndex];
            var savedLine = lineIndex < currentSavedSectionLines.Count ? currentSavedSectionLines[lineIndex] : string.Empty;

            bool isDifferentFromSaved = !string.Equals(currentLine, savedLine, StringComparison.Ordinal);
            bool isRecentlySaved = recentlySavedLineIndexes.Contains(lineIndex) && !isDifferentFromSaved;

            var lineBrush = Brushes.LimeGreen;

            if (isDifferentFromSaved)
            {
                lineBrush = Brushes.Red;
            }
            else if (isRecentlySaved)
            {
                lineBrush = Brushes.Yellow;
            }

            previewEntries.Add(new PreviewLineEntry
            {
                LineText = currentLine,
                LineBrush = lineBrush
            });
        }

        PreviewLineList.ItemsSource = previewEntries;
    }

    private List<StructuredTagEntry> BuildStructuredTagEntries(DfnSectionModel sectionModel)
    {
        var output = new List<StructuredTagEntry>();
        string selectedFolderType = GetSelectedFolderType();

        foreach (var line in sectionModel.Lines)
        {
            if (!line.IsTagLine)
            {
                continue;
            }

            bool isKnownTag = IsKnownTagName(line.TagName);
            string suggestedTag = isKnownTag ? string.Empty : GetClosestKnownTag(line.TagName);

            string validationMessage = string.Empty;

            if (!isKnownTag)
            {
                if (selectedFolderType == "creatures")
                {
                    validationMessage = "Tag not valid for creatures folder";
                }
                else if (selectedFolderType == "create")
                {
                    validationMessage = "Tag not valid for create folder";
                }
                else if (selectedFolderType == "npc")
                {
                    validationMessage = "Tag not valid for npc folder";
                }
                else if (selectedFolderType == "items")
                {
                    validationMessage = "Tag not valid for items folder";
                }
                else if (selectedFolderType == "race")
                {
                    validationMessage = "Tag not valid for race folder";
                }
                else
                {
                    validationMessage = "Unknown or unregistered tag";
                }

                if (!string.IsNullOrWhiteSpace(suggestedTag))
                {
                    validationMessage += ". Possible match: " + suggestedTag;
                }
            }

            var structuredTagEntry = new StructuredTagEntry
            {
                LineIndex = line.LineIndex,
                TagName = line.TagName,
                TagValue = line.TagValue,
                IsKnownTag = isKnownTag,
                SuggestedTag = suggestedTag,
                ValidationMessage = validationMessage,
                TagBrush = isKnownTag ? Brushes.White : Brushes.OrangeRed,
                ValidationBrush = isKnownTag ? Brushes.Transparent : Brushes.Gold,
                Description = GetTagDescription(line.TagName),
                HasHuePreview = false,
                HuePreviewBrush = Brushes.Transparent,
                HuePreviewTooltip = string.Empty
            };

            if (IsHuePickerTag(line.TagName))
            {
                var hueEntry = TryGetHueEntryForTagValue(line.TagValue);
                if (hueEntry != null && hueEntry.PreviewColors.Count > 0)
                {
                    int previewIndex = Math.Min(8, hueEntry.PreviewColors.Count - 1);

                    structuredTagEntry.HasHuePreview = true;
                    structuredTagEntry.HuePreviewBrush = new SolidColorBrush(hueEntry.PreviewColors[previewIndex]);

                    string hueName = string.IsNullOrWhiteSpace(hueEntry.Name) ? "(no name)" : hueEntry.Name;
                    structuredTagEntry.HuePreviewTooltip = "Hue " + hueEntry.HueId + " - " + hueName;
                }
            }

            output.Add(structuredTagEntry);
        }

        return output;
    }

    private void RefreshStructuredSectionView()
    {
        if (currentParsedSection == null)
        {
            StructuredHeaderText.Text = string.Empty;
            currentStructuredTags.Clear();
            StructuredTagList.ItemsSource = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(currentParsedSection.HeaderArgument))
        {
            StructuredHeaderText.Text = currentParsedSection.HeaderName;
        }
        else
        {
            StructuredHeaderText.Text = currentParsedSection.HeaderName + " " + currentParsedSection.HeaderArgument;
        }

        var newStructuredTags = BuildStructuredTagEntries(currentParsedSection);

        currentStructuredTags.Clear();

        foreach (var structuredTagEntry in newStructuredTags)
        {
            currentStructuredTags.Add(structuredTagEntry);
        }

        if (StructuredTagList.ItemsSource == null)
        {
            StructuredTagList.ItemsSource = currentStructuredTags;
        }

        _ = UpdateSelectedIdPreviewAsync();
    }

    private void ClearStructuredSectionView()
    {
        currentParsedSection = null;
        currentStructuredTags.Clear();
        StructuredHeaderText.Text = string.Empty;
        StructuredTagList.ItemsSource = null;
        ClearSelectedIdPreview();
    }

    private void JumpEditorToLine(int lineIndex)
    {
        var editorText = EditorBox.Text ?? string.Empty;
        var editorLines = NormalizeEditorTextToLines(editorText);

        if (lineIndex < 0 || lineIndex >= editorLines.Count)
        {
            return;
        }

        int caretIndex = 0;

        for (int currentIndex = 0; currentIndex < lineIndex; currentIndex++)
        {
            caretIndex += editorLines[currentIndex].Length + Environment.NewLine.Length;
        }

        EditorBox.Focus();
        EditorBox.CaretIndex = caretIndex;
    }

    private async void OnStructuredTagSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (suppressStructuredSelectionSync)
        {
            return;
        }

        var selectedTagEntry = StructuredTagList.SelectedItem as StructuredTagEntry;
        if (selectedTagEntry == null)
        {
            ClearSelectedIdPreview();
            return;
        }

        suppressEditorToStructuredSync = true;
        JumpEditorToLine(selectedTagEntry.LineIndex);
        suppressEditorToStructuredSync = false;

        await UpdateSelectedIdPreviewAsync();
    }

    private DfnSectionModel ParseSectionText(string sectionText)
    {
        var sectionModel = new DfnSectionModel();
        var lines = NormalizeEditorTextToLines(sectionText);

        if (lines.Count == 0)
        {
            return sectionModel;
        }

        sectionModel.HeaderText = lines[0].Trim();
        ParseHeader(sectionModel);

        for (int lineIndex = 1; lineIndex < lines.Count; lineIndex++)
        {
            var rawLine = lines[lineIndex];
            var trimmedLine = rawLine.Trim();

            if (IsOpeningBraceLine(trimmedLine) || IsClosingBraceLine(trimmedLine))
            {
                continue;
            }

            var lineModel = new DfnLineModel
            {
                LineIndex = lineIndex,
                RawText = rawLine,
                IsBlankLine = string.IsNullOrWhiteSpace(trimmedLine),
                IsCommentLine = trimmedLine.StartsWith("//", StringComparison.Ordinal)
            };

            if (!lineModel.IsBlankLine && !lineModel.IsCommentLine)
            {
                int equalsIndex = trimmedLine.IndexOf('=');
                if (equalsIndex > 0)
                {
                    lineModel.IsTagLine = true;
                    lineModel.TagName = trimmedLine.Substring(0, equalsIndex).Trim().ToUpperInvariant();
                    lineModel.TagValue = trimmedLine.Substring(equalsIndex + 1).Trim();
                }
            }

            sectionModel.Lines.Add(lineModel);
        }

        return sectionModel;
    }

    private void ParseHeader(DfnSectionModel sectionModel)
    {
        var headerText = sectionModel.HeaderText.Trim();

        if (!headerText.StartsWith("[", StringComparison.Ordinal) || !headerText.EndsWith("]", StringComparison.Ordinal))
        {
            return;
        }

        var innerText = headerText.Substring(1, headerText.Length - 2).Trim();

        if (string.IsNullOrWhiteSpace(innerText))
        {
            return;
        }

        int firstSpaceIndex = innerText.IndexOf(' ');
        if (firstSpaceIndex < 0)
        {
            sectionModel.HeaderName = innerText;
            sectionModel.HeaderArgument = string.Empty;
            return;
        }

        sectionModel.HeaderName = innerText.Substring(0, firstSpaceIndex).Trim();
        sectionModel.HeaderArgument = innerText.Substring(firstSpaceIndex + 1).Trim();
    }

    private sealed class SectionLocation
    {
        public string FilePath { get; set; } = string.Empty;
        public int HeaderLineIndex { get; set; }
    }

    private sealed class SectionSearchEntry
    {
        public string HeaderText { get; set; } = string.Empty;
        public string NameValue { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
    }

    private sealed class FileSnapshot
    {
        public string FilePath { get; set; } = string.Empty;
        public List<string> FileLines { get; set; } = new List<string>();
        public string SelectedHeader { get; set; } = string.Empty;
    }

    private async void OnAddTagClicked(object? sender, RoutedEventArgs e)
    {
        if (currentSection == null)
        {
            return;
        }

        var selectedTagName = await ShowAddTagPickerAsync();
        if (string.IsNullOrWhiteSpace(selectedTagName))
        {
            return;
        }

        InsertTagIntoEditor(selectedTagName);
    }

    private void InsertTagIntoEditor(string tagName)
    {
        var editorLines = NormalizeEditorTextToLines(EditorBox.Text);

        if (editorLines.Count == 0)
        {
            return;
        }

        int insertIndex = editorLines.Count;

        for (int lineIndex = editorLines.Count - 1; lineIndex >= 0; lineIndex--)
        {
            if (editorLines[lineIndex].Trim() == "}")
            {
                insertIndex = lineIndex;
                break;
            }
        }

        editorLines.Insert(insertIndex, tagName + "=");

        var newEditorText = string.Join(Environment.NewLine, editorLines);
        SetEditorTextWithoutTracking(newEditorText);

        currentParsedSection = ParseSectionText(newEditorText);
        RefreshStructuredSectionView();
        RefreshPreviewLines();

        JumpEditorToLine(insertIndex);

        var currentCaretIndex = EditorBox.CaretIndex;
        EditorBox.CaretIndex = currentCaretIndex + tagName.Length + 1;
    }

    private bool IsOpeningBraceLine(string trimmedLine)
    {
        return !string.IsNullOrWhiteSpace(trimmedLine) &&
               trimmedLine.StartsWith("{", StringComparison.Ordinal);
    }

    private bool IsClosingBraceLine(string trimmedLine)
    {
        return string.Equals(trimmedLine, "}", StringComparison.Ordinal);
    }

    private async void OnDuplicateSection(object? sender, RoutedEventArgs e)
    {
        if (currentSection == null)
        {
            return;
        }

        var targetFile = await PickTargetFileAsync();
        if (string.IsNullOrWhiteSpace(targetFile))
        {
            return;
        }

        var sourceLines = File.ReadAllLines(currentSection.FilePath).ToList();

        int startIndex;
        int endIndex;
        FindSectionRange(sourceLines, currentSection.HeaderLineIndex, out startIndex, out endIndex);

        var sectionLines = sourceLines.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();

        var targetLines = File.ReadAllLines(targetFile).ToList();
        var existingHeaders = ExtractSectionHeaders(targetLines);

        var newHeader = GenerateDuplicateHeader(sectionLines[0], existingHeaders);
        sectionLines[0] = newHeader;

        targetLines.Add(string.Empty);
        targetLines.AddRange(sectionLines);

        File.WriteAllLines(targetFile, targetLines);

        // reload UI if same file
        if (targetFile == currentSelectedFilePath)
        {
            LoadSectionsFromFile(targetFile);
            ApplySectionFilter();
            SelectSectionByHeader(newHeader, false);
        }
    }

    private string GenerateDuplicateHeader(string originalHeader, List<string> existingHeaders)
    {
        string baseHeader = originalHeader.Trim();

        int counter = 1;

        while (true)
        {
            string newHeader = baseHeader.Replace("]", "_" + counter + "]");

            if (!existingHeaders.Any(h => h.Equals(newHeader, StringComparison.OrdinalIgnoreCase)))
            {
                return newHeader;
            }

            counter++;
        }
    }

    private async void OnNewDfnFile(object? sender, RoutedEventArgs e)
    {
        var selectedFolderKey = FolderList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedFolderKey))
        {
            return;
        }

        if (!folderMap.TryGetValue(selectedFolderKey, out var selectedFolderPath))
        {
            return;
        }

        var newFileName = await PromptForNewDfnFileNameAsync();
        if (string.IsNullOrWhiteSpace(newFileName))
        {
            return;
        }

        newFileName = newFileName.Trim();

        if (!newFileName.EndsWith(".dfn", StringComparison.OrdinalIgnoreCase))
        {
            newFileName += ".dfn";
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            if (newFileName.Contains(invalidChar))
            {
                await ShowMessageAsync("Invalid file name.", "The file name contains invalid characters.");
                return;
            }
        }

        var newFilePath = Path.Combine(selectedFolderPath, newFileName);

        if (File.Exists(newFilePath))
        {
            await ShowMessageAsync("File already exists.", "A DFN file with that name already exists in the selected folder.");
            return;
        }

        File.WriteAllText(newFilePath, string.Empty);

        RefreshCurrentFolderFileList();
        FileList.SelectedItem = newFileName;
    }

    private async void OnDeleteDfnFile(object? sender, RoutedEventArgs e)
    {
        var selectedFileKey = FileList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedFileKey))
        {
            await ShowMessageAsync("No file selected.", "Select a DFN file to delete.");
            return;
        }

        if (!fileMap.TryGetValue(selectedFileKey, out var filePath))
        {
            await ShowMessageAsync("File not found.", "The selected DFN file could not be found.");
            return;
        }

        if (!File.Exists(filePath))
        {
            await ShowMessageAsync("File not found.", "The selected DFN file no longer exists.");
            return;
        }

        bool confirmed = await ConfirmDeleteDfnFileAsync(selectedFileKey);
        if (!confirmed)
        {
            return;
        }

        File.Delete(filePath);

        currentSelectedFilePath = string.Empty;
        currentSection = null;
        currentSavedSectionLines.Clear();
        recentlySavedLineIndexes.Clear();
        editorUndoStack.Clear();
        editorRedoStack.Clear();

        SetEditorTextWithoutTracking(string.Empty);
        PreviewLineList.ItemsSource = null;
        ClearStructuredSectionView();

        FileList.SelectedItem = null;
        SectionList.ItemsSource = null;
        SectionSearchBox.Text = string.Empty;

        RefreshCurrentFolderFileList();

        var remainingFiles = FileList.ItemsSource as IEnumerable<string>;
        if (remainingFiles != null)
        {
            var firstRemainingFile = remainingFiles.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstRemainingFile))
            {
                FileList.SelectedItem = firstRemainingFile;
            }
        }
    }

    private async void OnRenameDfnFile(object? sender, RoutedEventArgs e)
    {
        var selectedFileKey = FileList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedFileKey))
        {
            await ShowMessageAsync("No file selected.", "Select a DFN file to rename.");
            return;
        }

        if (!fileMap.TryGetValue(selectedFileKey, out var originalFilePath))
        {
            await ShowMessageAsync("File not found.", "The selected DFN file could not be found.");
            return;
        }

        if (!File.Exists(originalFilePath))
        {
            await ShowMessageAsync("File not found.", "The selected DFN file no longer exists.");
            return;
        }

        var currentFileName = Path.GetFileName(originalFilePath);
        var newFileName = await PromptForRenameDfnFileNameAsync(currentFileName);

        if (string.IsNullOrWhiteSpace(newFileName))
        {
            return;
        }

        newFileName = newFileName.Trim();

        if (!newFileName.EndsWith(".dfn", StringComparison.OrdinalIgnoreCase))
        {
            newFileName += ".dfn";
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            if (newFileName.Contains(invalidChar))
            {
                await ShowMessageAsync("Invalid file name.", "The file name contains invalid characters.");
                return;
            }
        }

        if (string.Equals(newFileName, currentFileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directoryPath = Path.GetDirectoryName(originalFilePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            await ShowMessageAsync("Rename failed.", "Could not determine the file directory.");
            return;
        }

        var newFilePath = Path.Combine(directoryPath, newFileName);

        if (File.Exists(newFilePath))
        {
            await ShowMessageAsync("File already exists.", "A DFN file with that name already exists in this folder.");
            return;
        }

        File.Move(originalFilePath, newFilePath);

        if (string.Equals(currentSelectedFilePath, originalFilePath, StringComparison.OrdinalIgnoreCase))
        {
            currentSelectedFilePath = newFilePath;
        }

        RefreshCurrentFolderFileList();
        FileList.SelectedItem = newFileName;
    }

    private void RefreshCurrentFolderFileList()
    {
        var selectedFolderKey = FolderList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedFolderKey))
        {
            FileList.ItemsSource = null;
            fileMap.Clear();
            return;
        }

        if (!folderMap.TryGetValue(selectedFolderKey, out var selectedFolderPath))
        {
            FileList.ItemsSource = null;
            fileMap.Clear();
            return;
        }

        if (!Directory.Exists(selectedFolderPath))
        {
            FileList.ItemsSource = null;
            fileMap.Clear();
            return;
        }

        var fileEntries = new List<string>();
        fileMap.Clear();

        var dfnFiles = Directory.GetFiles(selectedFolderPath, "*.dfn", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in dfnFiles)
        {
            var fileName = Path.GetFileName(filePath);
            fileEntries.Add(fileName);
            fileMap[fileName] = filePath;
        }

        FileList.ItemsSource = fileEntries;
    }

    private void OnEditorPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        SyncStructuredSelectionFromEditorCaret();
    }

    private void OnEditorCaretOrPointerChanged(object? sender, EventArgs e)
    {
        SyncStructuredSelectionFromEditorCaret();
    }

    private void SyncStructuredSelectionFromEditorCaret()
    {
        if (suppressEditorToStructuredSync)
        {
            return;
        }

        if (currentStructuredTags.Count == 0)
        {
            return;
        }

        int caretIndex = EditorBox.CaretIndex;
        int currentEditorLineIndex = GetLineIndexFromCaret(EditorBox.Text ?? string.Empty, caretIndex);

        StructuredTagEntry? bestMatch = null;

        foreach (var structuredTagEntry in currentStructuredTags)
        {
            if (structuredTagEntry.LineIndex == currentEditorLineIndex)
            {
                bestMatch = structuredTagEntry;
                break;
            }

            if (structuredTagEntry.LineIndex <= currentEditorLineIndex)
            {
                bestMatch = structuredTagEntry;
            }
        }

        suppressStructuredSelectionSync = true;
        StructuredTagList.SelectedItem = bestMatch;
        suppressStructuredSelectionSync = false;
    }

    private int GetLineIndexFromCaret(string editorText, int caretIndex)
    {
        if (string.IsNullOrEmpty(editorText) || caretIndex <= 0)
        {
            return 0;
        }

        var safeCaretIndex = Math.Min(caretIndex, editorText.Length);
        int lineIndex = 0;

        for (int index = 0; index < safeCaretIndex; index++)
        {
            if (editorText[index] == '\n')
            {
                lineIndex++;
            }
        }

        return lineIndex;
    }

    private async void OnStructuredTagDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var selectedTagEntry = StructuredTagList.SelectedItem as StructuredTagEntry;
        if (selectedTagEntry == null)
        {
            return;
        }

        if (GetSelectedFolderType() == "items")
        {
            if (string.Equals(selectedTagEntry.TagName, "TYPE", StringComparison.OrdinalIgnoreCase))
            {
                var selectedTypeValue = await PromptForItemTypeValueAsync(selectedTagEntry.TagValue);
                if (string.IsNullOrWhiteSpace(selectedTypeValue))
                {
                    return;
                }

                ReplaceTagValueAtLine(selectedTagEntry.LineIndex, selectedTagEntry.TagName, selectedTypeValue);
                return;
            }

            if (string.Equals(selectedTagEntry.TagName, "LAYER", StringComparison.OrdinalIgnoreCase))
            {
                var selectedLayerValue = await PromptForItemLayerValueAsync(selectedTagEntry.TagValue);
                if (string.IsNullOrWhiteSpace(selectedLayerValue))
                {
                    return;
                }

                ReplaceTagValueAtLine(selectedTagEntry.LineIndex, selectedTagEntry.TagName, selectedLayerValue);
                return;
            }
        }

        if (string.Equals(selectedTagEntry.TagName, "ID", StringComparison.OrdinalIgnoreCase))
        {
            var selectedIdValue = await PromptForItemArtValueAsync(selectedTagEntry.TagValue);
            if (string.IsNullOrWhiteSpace(selectedIdValue))
            {
                return;
            }

            ReplaceTagValueAtLine(selectedTagEntry.LineIndex, selectedTagEntry.TagName, selectedIdValue);
            return;
        }

        if (IsLootPickerTag(selectedTagEntry.TagName))
        {
            var selectedLootValue = await PromptForLootListValueAsync(selectedTagEntry.TagValue);
            if (string.IsNullOrWhiteSpace(selectedLootValue))
            {
                return;
            }

            ReplaceTagValueAtLine(selectedTagEntry.LineIndex, selectedTagEntry.TagName, selectedLootValue);
            return;
        }

        if (IsHuePickerTag(selectedTagEntry.TagName))
        {
            var selectedHueValue = await PromptForHueValueAsync(selectedTagEntry.TagValue);
            if (string.IsNullOrWhiteSpace(selectedHueValue))
            {
                return;
            }

            ReplaceTagValueAtLine(selectedTagEntry.LineIndex, selectedTagEntry.TagName, selectedHueValue);
        }
    }

    private void ReplaceTagValueAtLine(int lineIndex, string tagName, string newValue)
    {
        var editorLines = NormalizeEditorTextToLines(EditorBox.Text);

        if (lineIndex < 0 || lineIndex >= editorLines.Count)
        {
            return;
        }

        editorLines[lineIndex] = tagName + "=" + newValue;

        var newEditorText = string.Join(Environment.NewLine, editorLines);
        SetEditorTextWithoutTracking(newEditorText);

        currentParsedSection = ParseSectionText(newEditorText);
        RefreshStructuredSectionView();
        RefreshPreviewLines();

        JumpEditorToLine(lineIndex);

        var caretIndex = GetCaretIndexForLine(editorLines, lineIndex);
        EditorBox.CaretIndex = caretIndex + editorLines[lineIndex].Length;
        EditorBox.Focus();
    }

    private int GetCaretIndexForLine(List<string> editorLines, int lineIndex)
    {
        int caretIndex = 0;

        for (int currentIndex = 0; currentIndex < lineIndex; currentIndex++)
        {
            caretIndex += editorLines[currentIndex].Length + Environment.NewLine.Length;
        }

        return caretIndex;
    }

    private bool IsLootPickerTag(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        return string.Equals(tagName.Trim(), "LOOT", StringComparison.OrdinalIgnoreCase);
    }

    private string GetLootListFilePath()
    {
        if (string.IsNullOrWhiteSpace(currentFolder))
        {
            return string.Empty;
        }

        var searchDirectory = new DirectoryInfo(currentFolder);

        while (searchDirectory != null)
        {
            string candidatePath = Path.Combine(searchDirectory.FullName, "items", "lootlists.dfn");
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            string alternateCandidatePath = Path.Combine(searchDirectory.FullName, "lootlists.dfn");
            if (File.Exists(alternateCandidatePath) &&
                string.Equals(searchDirectory.Name, "items", StringComparison.OrdinalIgnoreCase))
            {
                return alternateCandidatePath;
            }

            searchDirectory = searchDirectory.Parent;
        }

        return string.Empty;
    }

    private async void OnGenerateWeaponClicked(object? sender, RoutedEventArgs e)
    {
        if (GetSelectedFolderType() != "items")
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(currentSelectedFilePath) || !File.Exists(currentSelectedFilePath))
        {
            await ShowMessageAsync("No item file selected", "Select an items DFN file before generating a weapon.");
            return;
        }

        var weaponEntries = LoadWeaponGeneratorEntries();
        if (weaponEntries.Count == 0)
        {
            await ShowMessageAsync("No weapon presets found", "Could not find any weapon DFN sections in the current folder.");
            return;
        }

        var generationResult = await PromptForWeaponGenerationAsync(weaponEntries);
        if (generationResult == null)
        {
            return;
        }

        PushUndoSnapshot(currentSelectedFilePath);
        redoStack.Clear();

        var lines = File.ReadAllLines(currentSelectedFilePath).ToList();
        var existingHeaders = ExtractSectionHeaders(lines);

        string newHeader = GenerateUniqueSectionHeader(existingHeaders);
        var newBlockLines = BuildGeneratedWeaponSection(newHeader, generationResult);

        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
        {
            lines.Add(string.Empty);
        }

        lines.AddRange(newBlockLines);

        File.WriteAllLines(currentSelectedFilePath, lines);

        currentSavedSectionLines.Clear();
        currentSavedSectionLines.AddRange(newBlockLines);
        recentlySavedLineIndexes = new HashSet<int>();

        LoadSectionsFromFile(currentSelectedFilePath);
        ApplySectionFilter();
        SelectSectionByHeader(newHeader, false);
    }

    private List<WeaponGeneratorEntry> LoadWeaponGeneratorEntries()
    {
        var output = new List<WeaponGeneratorEntry>();

        if (string.IsNullOrWhiteSpace(currentFolder) || !Directory.Exists(currentFolder))
        {
            return output;
        }

        var allowedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "archery.dfn",
            "axes.dfn",
            "fencing.dfn",
            "knives_daggers.dfn",
            "maces_hammers.dfn",
            "missile_weapons.dfn",
            "practice_weapons.dfn",
            "staves_polearms.dfn",
            "swords.dfn",
            "throwing.dfn"
        };

        var allDfnFiles = Directory.GetFiles(currentFolder, "*.dfn", SearchOption.AllDirectories)
            .Where(filePath => allowedFileNames.Contains(Path.GetFileName(filePath)))
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var filePath in allDfnFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var lines = File.ReadAllLines(filePath);

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var trimmedLine = lines[lineIndex].Trim();

                if (!trimmedLine.StartsWith("[", StringComparison.Ordinal) || !trimmedLine.EndsWith("]", StringComparison.Ordinal))
                {
                    continue;
                }

                string headerText = trimmedLine;
                string sectionName = ExtractSectionNameFromHeader(headerText);
                string displayName = FindFirstNameValue(lines, lineIndex);

                if (string.IsNullOrWhiteSpace(sectionName))
                {
                    continue;
                }

                var baseTagValues = ReadBaseWeaponTagValues(lines, lineIndex);

                output.Add(new WeaponGeneratorEntry
                {
                    HeaderText = headerText,
                    SectionName = sectionName,
                    DisplayName = displayName,
                    SourceFileName = fileName,
                    SourceFilePath = filePath,
                    HeaderLineIndex = lineIndex,
                    DefaultGeneratedName = BuildDefaultGeneratedWeaponName(sectionName, displayName, fileName),
                    BaseLowDamage = baseTagValues.LowDamage,
                    BaseHighDamage = baseTagValues.HighDamage,
                    BaseSpeed = baseTagValues.Speed,
                    BaseStrength = baseTagValues.StrengthRequirement,
                    BaseValue = baseTagValues.ValueText
                });
            }
        }

        return output
            .OrderBy(entry => entry.DisplayNameOrSectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SourceFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ExtractSectionNameFromHeader(string headerText)
    {
        if (string.IsNullOrWhiteSpace(headerText))
        {
            return string.Empty;
        }

        string trimmedHeader = headerText.Trim();

        if (!trimmedHeader.StartsWith("[", StringComparison.Ordinal) || !trimmedHeader.EndsWith("]", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return trimmedHeader.Substring(1, trimmedHeader.Length - 2).Trim();
    }

    private string BuildDefaultGeneratedWeaponName(string sectionName, string displayName, string sourceFileName)
    {
        string baseName = !string.IsNullOrWhiteSpace(displayName) ? displayName : sectionName;

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = Path.GetFileNameWithoutExtension(sourceFileName);
        }

        return "new " + baseName.ToLowerInvariant();
    }

    private WeaponBaseTagValues ReadBaseWeaponTagValues(string[] lines, int headerLineIndex)
    {
        var output = new WeaponBaseTagValues();

        bool foundOpeningBrace = false;
        int braceDepth = 0;

        for (int lineIndex = headerLineIndex + 1; lineIndex < lines.Length; lineIndex++)
        {
            string trimmedLine = lines[lineIndex].Trim();

            if (!foundOpeningBrace)
            {
                if (IsOpeningBraceLine(trimmedLine))
                {
                    foundOpeningBrace = true;
                    braceDepth = 1;
                }

                continue;
            }

            if (IsOpeningBraceLine(trimmedLine))
            {
                braceDepth++;
                continue;
            }

            if (IsClosingBraceLine(trimmedLine))
            {
                braceDepth--;

                if (braceDepth == 0)
                {
                    break;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            int equalsIndex = trimmedLine.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            string tagName = trimmedLine.Substring(0, equalsIndex).Trim().ToUpperInvariant();
            string tagValue = trimmedLine.Substring(equalsIndex + 1).Trim();

            switch (tagName)
            {
                case "LODAMAGE":
                    output.LowDamage = tagValue;
                    break;
                case "HIDAMAGE":
                    output.HighDamage = tagValue;
                    break;
                case "DAMAGE":
                case "ATT":
                    ParseDamageRange(tagValue, output);
                    break;
                case "SPD":
                    output.Speed = tagValue;
                    break;
                case "STRENGTH":
                case "STR":
                    output.StrengthRequirement = tagValue;
                    break;
                case "VALUE":
                    output.ValueText = tagValue;
                    break;
            }
        }

        return output;
    }

    private void ParseDamageRange(string tagValue, WeaponBaseTagValues output)
    {
        if (string.IsNullOrWhiteSpace(tagValue))
        {
            return;
        }

        var parts = tagValue
            .Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 1 && string.IsNullOrWhiteSpace(output.LowDamage))
        {
            output.LowDamage = parts[0];
        }

        if (parts.Length >= 2 && string.IsNullOrWhiteSpace(output.HighDamage))
        {
            output.HighDamage = parts[1];
        }
    }

    private void AppendGeneratedTagIfValue(List<string> output, string tagName, string tagValue)
    {
        if (string.IsNullOrWhiteSpace(tagValue))
        {
            return;
        }

        output.Add(tagName + "=" + tagValue.Trim());
    }

    private List<string> BuildGeneratedWeaponSection(string headerText, WeaponGenerationResult generationResult)
    {
        var output = new List<string>
        {
            headerText,
            "{",
            "GET=" + generationResult.SelectedWeapon.SectionName
        };

        string generatedName = generationResult.GeneratedName.Trim();
        if (!string.IsNullOrWhiteSpace(generatedName))
        {
            output.Add("NAME=" + generatedName);
        }

        AppendGeneratedTagIfValue(output, "LODAMAGE", generationResult.LowDamage);
        AppendGeneratedTagIfValue(output, "HIDAMAGE", generationResult.HighDamage);
        AppendGeneratedTagIfValue(output, "SPD", generationResult.Speed);
        AppendGeneratedTagIfValue(output, "STRENGTH", generationResult.StrengthRequirement);
        AppendGeneratedTagIfValue(output, "VALUE", generationResult.ValueText);

        output.Add("}");

        return output;
    }

    private ArtFileReader.ItemArtEntry? TryGetCachedItemArtEntryByItemId(int itemId)
    {
        if (cachedItemArtEntries == null || cachedItemArtEntries.Count == 0)
        {
            return null;
        }

        return cachedItemArtEntries.FirstOrDefault(itemArtEntry => itemArtEntry.ItemId == itemId);
    }

    private async System.Threading.Tasks.Task<ArtFileReader.ItemArtEntry?> GetItemArtEntryForTagValueAsync(string tagValue)
    {
        if (!TryParseItemIdValue(tagValue, out int itemId))
        {
            return null;
        }

        var cachedEntry = TryGetCachedItemArtEntryByItemId(itemId);
        if (cachedEntry != null)
        {
            return cachedEntry;
        }

        var loadedEntries = await GetOrLoadItemArtEntriesAsync();
        if (loadedEntries == null || loadedEntries.Count == 0)
        {
            return null;
        }

        return loadedEntries.FirstOrDefault(itemArtEntry => itemArtEntry.ItemId == itemId);
    }

    private void ClearSelectedIdPreview()
    {
        SelectedIdPreviewBorder.IsVisible = false;
        SelectedIdPreviewImage.Source = null;
        SelectedIdPreviewTitle.Text = "Item ID Preview";
        SelectedIdPreviewDetails.Text = string.Empty;
        SelectedIdPreviewHint.Text = "Double-click ID row to change item art.";
    }

    private async System.Threading.Tasks.Task UpdateSelectedIdPreviewAsync()
    {
        var selectedTagEntry = StructuredTagList.SelectedItem as StructuredTagEntry;

        if (selectedTagEntry == null)
        {
            ClearSelectedIdPreview();
            return;
        }

        if (GetSelectedFolderType() != "items" ||
            !string.Equals(selectedTagEntry.TagName, "ID", StringComparison.OrdinalIgnoreCase))
        {
            ClearSelectedIdPreview();
            return;
        }

        var itemArtEntry = await GetItemArtEntryForTagValueAsync(selectedTagEntry.TagValue);
        if (itemArtEntry == null || itemArtEntry.PreviewBitmap == null)
        {
            SelectedIdPreviewBorder.IsVisible = true;
            SelectedIdPreviewImage.Source = null;
            SelectedIdPreviewTitle.Text = "Item ID Preview";
            SelectedIdPreviewDetails.Text =
                "ID: " + selectedTagEntry.TagValue + Environment.NewLine +
                "Preview not available.";
            SelectedIdPreviewHint.Text = "Double-click ID row to change item art.";
            return;
        }

        SelectedIdPreviewBorder.IsVisible = true;
        SelectedIdPreviewImage.Source = itemArtEntry.PreviewBitmap;
        SelectedIdPreviewTitle.Text = string.IsNullOrWhiteSpace(itemArtEntry.TileName)
            ? "0x" + itemArtEntry.ItemId.ToString("X4")
            : "0x" + itemArtEntry.ItemId.ToString("X4") + " - " + itemArtEntry.TileName;

        SelectedIdPreviewDetails.Text =
            "Size: " + itemArtEntry.ImageWidth + "x" + itemArtEntry.ImageHeight + Environment.NewLine +
            "Tile Height: " + itemArtEntry.TileHeight;

        SelectedIdPreviewHint.Text = "Double-click ID row to change item art.";
    }
}

public sealed class WeaponBaseTagValues
{
    public string LowDamage { get; set; } = string.Empty;
    public string HighDamage { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public string StrengthRequirement { get; set; } = string.Empty;
    public string ValueText { get; set; } = string.Empty;
}

public sealed class PreviewLineEntry
{
    public string LineText { get; set; } = string.Empty;
    public IBrush LineBrush { get; set; } = Brushes.White;
}

public sealed class StructuredTagEntry
{
    public int LineIndex { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string TagValue { get; set; } = string.Empty;
    public bool IsKnownTag { get; set; }
    public string SuggestedTag { get; set; } = string.Empty;
    public string ValidationMessage { get; set; } = string.Empty;
    public IBrush TagBrush { get; set; } = Brushes.White;
    public IBrush ValidationBrush { get; set; } = Brushes.Transparent;
    public string Description { get; set; } = string.Empty;

    public bool HasHuePreview { get; set; }
    public IBrush HuePreviewBrush { get; set; } = Brushes.Transparent;
    public string HuePreviewTooltip { get; set; } = string.Empty;

    public string DisplayText
    {
        get
        {
            return TagName + " = " + TagValue;
        }
    }
}

public sealed class DfnSectionModel
{
    public string HeaderText { get; set; } = string.Empty;
    public string HeaderName { get; set; } = string.Empty;
    public string HeaderArgument { get; set; } = string.Empty;
    public List<DfnLineModel> Lines { get; set; } = new List<DfnLineModel>();
}

public sealed class DfnLineModel
{
    public int LineIndex { get; set; }
    public string RawText { get; set; } = string.Empty;
    public bool IsTagLine { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string TagValue { get; set; } = string.Empty;
    public bool IsCommentLine { get; set; }
    public bool IsBlankLine { get; set; }
}

public sealed class TagPickerEntry
{
    public string TagName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string DisplayText
    {
        get
        {
            return TagName + " - " + Description;
        }
    }
}

public sealed class TagPickerResult
{
    public string TagName { get; set; } = string.Empty;
}
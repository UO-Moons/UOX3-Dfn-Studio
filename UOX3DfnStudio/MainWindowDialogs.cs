using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UOX3DfnStudio;

public partial class MainWindow
{
    public sealed class ItemTypeEntry
    {
        public int TypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string DisplayText
        {
            get
            {
                return TypeId + " - " + TypeName;
            }
        }
    }

    public sealed class ItemLayerEntry
    {
        public int LayerId { get; set; }
        public string HexValue { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string DisplayText
        {
            get
            {
                return LayerId + " / " + HexValue + " - " + LayerName;
            }
        }
    }

    public sealed class LootListEntry
    {
        public string HeaderText { get; set; } = string.Empty;
        public string LootListName { get; set; } = string.Empty;
        public int EntryCount { get; set; }
        public List<string> PreviewEntries { get; set; } = new List<string>();

        public string DisplayText
        {
            get
            {
                return LootListName + " (" + EntryCount + " entries)";
            }
        }
    }

    public sealed class WeaponGeneratorEntry
    {
        public string HeaderText { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SourceFileName { get; set; } = string.Empty;
        public string SourceFilePath { get; set; } = string.Empty;
        public int HeaderLineIndex { get; set; }
        public string DefaultGeneratedName { get; set; } = string.Empty;

        public string BaseLowDamage { get; set; } = string.Empty;
        public string BaseHighDamage { get; set; } = string.Empty;
        public string BaseSpeed { get; set; } = string.Empty;
        public string BaseStrength { get; set; } = string.Empty;
        public string BaseValue { get; set; } = string.Empty;

        public string DisplayNameOrSectionName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DisplayName))
                {
                    return DisplayName;
                }

                return SectionName;
            }
        }

        public string DisplayText
        {
            get
            {
                return DisplayNameOrSectionName + " [" + SectionName + "] - " + SourceFileName;
            }
        }
    }

    public sealed class WeaponGenerationResult
    {
        public WeaponGeneratorEntry SelectedWeapon { get; set; } = new WeaponGeneratorEntry();
        public string GeneratedName { get; set; } = string.Empty;
        public string LowDamage { get; set; } = string.Empty;
        public string HighDamage { get; set; } = string.Empty;
        public string Speed { get; set; } = string.Empty;
        public string StrengthRequirement { get; set; } = string.Empty;
        public string ValueText { get; set; } = string.Empty;
    }

    private async Task<WeaponGenerationResult?> PromptForWeaponGenerationAsync(List<WeaponGeneratorEntry> weaponEntries)
    {
        var pickerWindow = new Window
        {
            Title = "Generate Weapon",
            Width = 920,
            Height = 720,
            MinWidth = 760,
            MinHeight = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var searchBox = new TextBox
        {
            Watermark = "Search weapons..."
        };

        var eraText = new TextBlock
        {
            Text = "Era preset: Lord Blackthorn's Revenge",
            FontWeight = FontWeight.Bold,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var infoText = new TextBlock
        {
            Text = "Select a base weapon section. The generated item will use GET= and write override stats below.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 6, 0, 0)
        };

        var weaponListBox = new ListBox();

        var generatedNameTextBox = new TextBox
        {
            Watermark = "Generated NAME value",
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var lowDamageTextBox = new TextBox
        {
            Watermark = "LODAMAGE",
            Margin = new Avalonia.Thickness(0, 8, 8, 0)
        };

        var highDamageTextBox = new TextBox
        {
            Watermark = "HIDAMAGE",
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var speedTextBox = new TextBox
        {
            Watermark = "SPD",
            Margin = new Avalonia.Thickness(0, 8, 8, 0)
        };

        var strengthTextBox = new TextBox
        {
            Watermark = "STRENGTH",
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var valueTextBox = new TextBox
        {
            Watermark = "VALUE",
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var previewText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var generateButton = new Button
        {
            Content = "Generate",
            IsEnabled = false,
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        WeaponGeneratorEntry? currentSelectedEntry = null;

        void UpdatePreview()
        {
            if (currentSelectedEntry == null)
            {
                previewText.Text = string.Empty;
                return;
            }

            var previewLines = new List<string>
            {
                "Section: " + currentSelectedEntry.SectionName,
                "Display Name: " + currentSelectedEntry.DisplayNameOrSectionName,
                "Source File: " + currentSelectedEntry.SourceFileName,
                "",
                "Generated output preview:",
                "GET=" + currentSelectedEntry.SectionName,
                "NAME=" + (generatedNameTextBox.Text ?? string.Empty).Trim()
            };

            string lowDamage = (lowDamageTextBox.Text ?? string.Empty).Trim();
            string highDamage = (highDamageTextBox.Text ?? string.Empty).Trim();
            string speed = (speedTextBox.Text ?? string.Empty).Trim();
            string strength = (strengthTextBox.Text ?? string.Empty).Trim();
            string valueText = (valueTextBox.Text ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(lowDamage))
            {
                previewLines.Add("LODAMAGE=" + lowDamage);
            }

            if (!string.IsNullOrWhiteSpace(highDamage))
            {
                previewLines.Add("HIDAMAGE=" + highDamage);
            }

            if (!string.IsNullOrWhiteSpace(speed))
            {
                previewLines.Add("SPD=" + speed);
            }

            if (!string.IsNullOrWhiteSpace(strength))
            {
                previewLines.Add("STRENGTH=" + strength);
            }

            if (!string.IsNullOrWhiteSpace(valueText))
            {
                previewLines.Add("VALUE=" + valueText);
            }

            previewText.Text = string.Join(Environment.NewLine, previewLines);
        }

        void ApplySelectedWeaponDefaults(WeaponGeneratorEntry selectedEntry)
        {
            currentSelectedEntry = selectedEntry;

            generatedNameTextBox.Text = selectedEntry.DefaultGeneratedName;
            lowDamageTextBox.Text = selectedEntry.BaseLowDamage;
            highDamageTextBox.Text = selectedEntry.BaseHighDamage;
            speedTextBox.Text = selectedEntry.BaseSpeed;
            strengthTextBox.Text = selectedEntry.BaseStrength;
            valueTextBox.Text = selectedEntry.BaseValue;

            generateButton.IsEnabled = true;
            UpdatePreview();
        }

        void RefreshWeaponList()
        {
            string searchText = (searchBox.Text ?? string.Empty).Trim();

            IEnumerable<WeaponGeneratorEntry> filteredEntries = weaponEntries;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredEntries = filteredEntries.Where(entry =>
                    entry.SectionName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    entry.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    entry.SourceFileName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    entry.DefaultGeneratedName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            weaponListBox.SelectedItem = null;
            weaponListBox.ItemsSource = filteredEntries.ToList();

            currentSelectedEntry = null;
            generateButton.IsEnabled = false;
            previewText.Text = string.Empty;
            generatedNameTextBox.Text = string.Empty;
            lowDamageTextBox.Text = string.Empty;
            highDamageTextBox.Text = string.Empty;
            speedTextBox.Text = string.Empty;
            strengthTextBox.Text = string.Empty;
            valueTextBox.Text = string.Empty;
        }

        searchBox.TextChanged += delegate
        {
            RefreshWeaponList();
        };

        weaponListBox.SelectionChanged += delegate
        {
            var selectedEntry = weaponListBox.SelectedItem as WeaponGeneratorEntry;
            if (selectedEntry == null)
            {
                currentSelectedEntry = null;
                generateButton.IsEnabled = false;
                previewText.Text = string.Empty;
                return;
            }

            ApplySelectedWeaponDefaults(selectedEntry);
        };

        generatedNameTextBox.TextChanged += delegate { UpdatePreview(); };
        lowDamageTextBox.TextChanged += delegate { UpdatePreview(); };
        highDamageTextBox.TextChanged += delegate { UpdatePreview(); };
        speedTextBox.TextChanged += delegate { UpdatePreview(); };
        strengthTextBox.TextChanged += delegate { UpdatePreview(); };
        valueTextBox.TextChanged += delegate { UpdatePreview(); };

        generateButton.Click += delegate
        {
            if (currentSelectedEntry == null)
            {
                return;
            }

            pickerWindow.Close(new WeaponGenerationResult
            {
                SelectedWeapon = currentSelectedEntry,
                GeneratedName = (generatedNameTextBox.Text ?? string.Empty).Trim(),
                LowDamage = (lowDamageTextBox.Text ?? string.Empty).Trim(),
                HighDamage = (highDamageTextBox.Text ?? string.Empty).Trim(),
                Speed = (speedTextBox.Text ?? string.Empty).Trim(),
                StrengthRequirement = (strengthTextBox.Text ?? string.Empty).Trim(),
                ValueText = (valueTextBox.Text ?? string.Empty).Trim()
            });
        };

        cancelButton.Click += delegate
        {
            pickerWindow.Close((WeaponGenerationResult?)null);
        };

        weaponListBox.DoubleTapped += delegate
        {
            var selectedEntry = weaponListBox.SelectedItem as WeaponGeneratorEntry;
            if (selectedEntry == null)
            {
                return;
            }

            ApplySelectedWeaponDefaults(selectedEntry);
        };

        weaponListBox.ItemTemplate = new FuncDataTemplate<WeaponGeneratorEntry?>((entry, _) =>
        {
            var panel = new StackPanel
            {
                Spacing = 2,
                Margin = new Avalonia.Thickness(4)
            };

            if (entry == null)
            {
                return panel;
            }

            panel.Children.Add(new TextBlock
            {
                Text = entry.DisplayNameOrSectionName,
                FontWeight = FontWeight.Bold
            });

            panel.Children.Add(new TextBlock
            {
                Text = "[" + entry.SectionName + "] - " + entry.SourceFileName,
                Foreground = Brushes.LightGray,
                FontSize = 11
            });

            return panel;
        });

        RefreshWeaponList();

        var rootPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto,Auto,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootPanel.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        rootPanel.Children.Add(eraText);
        Grid.SetRow(eraText, 1);

        rootPanel.Children.Add(infoText);
        Grid.SetRow(infoText, 2);

        rootPanel.Children.Add(weaponListBox);
        Grid.SetRow(weaponListBox, 3);

        var statsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        statsGrid.Children.Add(generatedNameTextBox);
        Grid.SetRow(generatedNameTextBox, 0);
        Grid.SetColumnSpan(generatedNameTextBox, 2);

        statsGrid.Children.Add(lowDamageTextBox);
        Grid.SetRow(lowDamageTextBox, 1);
        Grid.SetColumn(lowDamageTextBox, 0);

        statsGrid.Children.Add(highDamageTextBox);
        Grid.SetRow(highDamageTextBox, 1);
        Grid.SetColumn(highDamageTextBox, 1);

        statsGrid.Children.Add(speedTextBox);
        Grid.SetRow(speedTextBox, 2);
        Grid.SetColumn(speedTextBox, 0);

        statsGrid.Children.Add(strengthTextBox);
        Grid.SetRow(strengthTextBox, 2);
        Grid.SetColumn(strengthTextBox, 1);

        rootPanel.Children.Add(statsGrid);
        Grid.SetRow(statsGrid, 4);

        rootPanel.Children.Add(valueTextBox);
        Grid.SetRow(valueTextBox, 5);

        rootPanel.Children.Add(previewText);
        Grid.SetRow(previewText, 6);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        buttonPanel.Children.Add(generateButton);
        buttonPanel.Children.Add(cancelButton);

        rootPanel.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 7);

        pickerWindow.Content = rootPanel;

        return await pickerWindow.ShowDialog<WeaponGenerationResult?>(this);
    }

    private async Task<string> PromptForItemArtValueAsync(string currentValue)
    {
        var itemArtEntries = await GetOrLoadItemArtEntriesAsync();
        if (itemArtEntries == null || itemArtEntries.Count == 0)
        {
            await ShowMessageAsync("No item art found", "Could not load item art entries from artLegacyMUL.uop and tiledata.mul.");
            return string.Empty;
        }

        var pickerWindow = new Window
        {
            Title = "Select Item Art ID",
            Width = 980,
            Height = 680,
            MinWidth = 780,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var searchBox = new TextBox
        {
            Watermark = "Search by id or tile name..."
        };

        var currentValueText = new TextBlock
        {
            Text = "Current ID value: " + (string.IsNullOrWhiteSpace(currentValue) ? "(empty)" : currentValue),
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var detailsText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var artListBox = new ListBox();

        var selectButton = new Button
        {
            Content = "Use ID",
            IsEnabled = false,
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        void RefreshItemArtList()
        {
            string searchText = (searchBox.Text ?? string.Empty).Trim();

            IEnumerable<ArtFileReader.ItemArtEntry> filteredEntries = itemArtEntries;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredEntries = filteredEntries.Where(itemArtEntry =>
                    itemArtEntry.ItemId.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    ("0x" + itemArtEntry.ItemId.ToString("X4")).Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    itemArtEntry.TileName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filteredEntries.ToList();
            artListBox.ItemsSource = filteredList;

            selectButton.IsEnabled = false;
            detailsText.Text = string.Empty;

            if (TryParseItemIdValue(currentValue, out int currentItemId))
            {
                var matchingEntry = filteredList.FirstOrDefault(entry => entry.ItemId == currentItemId);
                if (matchingEntry != null)
                {
                    artListBox.SelectedItem = matchingEntry;
                    selectButton.IsEnabled = true;
                    UpdateItemArtDetails(detailsText, matchingEntry);
                    return;
                }
            }

            artListBox.SelectedItem = null;
        }

        searchBox.TextChanged += delegate
        {
            RefreshItemArtList();
        };

        artListBox.SelectionChanged += delegate
        {
            var selectedEntry = artListBox.SelectedItem as ArtFileReader.ItemArtEntry;
            selectButton.IsEnabled = selectedEntry != null;

            if (selectedEntry == null)
            {
                detailsText.Text = string.Empty;
                return;
            }

            UpdateItemArtDetails(detailsText, selectedEntry);
        };

        selectButton.Click += delegate
        {
            var selectedEntry = artListBox.SelectedItem as ArtFileReader.ItemArtEntry;
            if (selectedEntry == null)
            {
                return;
            }

            pickerWindow.Close("0x" + selectedEntry.ItemId.ToString("X4"));
        };

        cancelButton.Click += delegate
        {
            pickerWindow.Close(string.Empty);
        };

        artListBox.DoubleTapped += delegate
        {
            var selectedEntry = artListBox.SelectedItem as ArtFileReader.ItemArtEntry;
            if (selectedEntry == null)
            {
                return;
            }

            pickerWindow.Close("0x" + selectedEntry.ItemId.ToString("X4"));
        };

        artListBox.ItemTemplate = new FuncDataTemplate<ArtFileReader.ItemArtEntry?>((itemArtEntry, _) =>
        {
            var rootPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                Margin = new Avalonia.Thickness(4)
            };

            if (itemArtEntry == null)
            {
                return rootPanel;
            }

            rootPanel.Children.Add(new Border
            {
                Width = 56,
                Height = 56,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Avalonia.Thickness(1),
                Child = new Image
                {
                    Source = itemArtEntry.PreviewBitmap,
                    Width = 48,
                    Height = 48,
                    Stretch = Avalonia.Media.Stretch.Uniform
                }
            });

            var textPanel = new StackPanel
            {
                Spacing = 2
            };

            textPanel.Children.Add(new TextBlock
            {
                Text = "0x" + itemArtEntry.ItemId.ToString("X4") + " - " + (string.IsNullOrWhiteSpace(itemArtEntry.TileName) ? "(no name)" : itemArtEntry.TileName),
                FontWeight = FontWeight.Bold
            });

            textPanel.Children.Add(new TextBlock
            {
                Text = itemArtEntry.ImageWidth + "x" + itemArtEntry.ImageHeight + "  Height=" + itemArtEntry.TileHeight,
                Foreground = Brushes.LightGray,
                FontSize = 11
            });

            rootPanel.Children.Add(textPanel);

            return rootPanel;
        });

        RefreshItemArtList();

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootGrid.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        rootGrid.Children.Add(currentValueText);
        Grid.SetRow(currentValueText, 1);

        rootGrid.Children.Add(artListBox);
        Grid.SetRow(artListBox, 2);

        rootGrid.Children.Add(detailsText);
        Grid.SetRow(detailsText, 3);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        buttonPanel.Children.Add(selectButton);
        buttonPanel.Children.Add(cancelButton);

        rootGrid.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 4);

        pickerWindow.Content = rootGrid;

        var result = await pickerWindow.ShowDialog<string>(this);
        return result ?? string.Empty;
    }

    private void UpdateItemArtDetails(TextBlock detailsText, ArtFileReader.ItemArtEntry entry)
    {
        detailsText.Text =
            "ID: 0x" + entry.ItemId.ToString("X4") +
            Environment.NewLine +
            "Name: " + (string.IsNullOrWhiteSpace(entry.TileName) ? "(no name)" : entry.TileName) +
            Environment.NewLine +
            "Image Size: " + entry.ImageWidth + " x " + entry.ImageHeight +
            Environment.NewLine +
            "Tile Height: " + entry.TileHeight;
    }

    private bool TryParseItemIdValue(string tagValue, out int itemId)
    {
        itemId = 0;

        if (string.IsNullOrWhiteSpace(tagValue))
        {
            return false;
        }

        string normalizedValue = tagValue.Trim();

        if (normalizedValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(
                normalizedValue.Substring(2),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out itemId);
        }

        return int.TryParse(normalizedValue, out itemId);
    }

    private async Task<List<ArtFileReader.ItemArtEntry>?> GetOrLoadItemArtEntriesAsync()
    {
        if (!string.IsNullOrWhiteSpace(currentArtUopFilePath) &&
            !string.IsNullOrWhiteSpace(currentTileDataFilePath) &&
            cachedItemArtEntries != null &&
            cachedItemArtEntries.Count > 0)
        {
            return cachedItemArtEntries;
        }

        string artUopPath = await FindOrPromptForClientDataFileAsync("artLegacyMUL.uop", "UO artLegacyMUL.uop");
        if (string.IsNullOrWhiteSpace(artUopPath))
        {
            return null;
        }

        string tileDataPath = await FindOrPromptForClientDataFileAsync("tiledata.mul", "UO tiledata.mul");
        if (string.IsNullOrWhiteSpace(tileDataPath))
        {
            return null;
        }

        var loadedEntries = ArtFileReader.LoadItemArtEntries(artUopPath, tileDataPath);
        if (loadedEntries.Count == 0)
        {
            await ShowMessageAsync("No item art found", "Could not load item art entries from artLegacyMUL.uop and tiledata.mul.");
            return null;
        }

        currentArtUopFilePath = artUopPath;
        currentTileDataFilePath = tileDataPath;
        cachedItemArtEntries = loadedEntries;

        return cachedItemArtEntries;
    }

    private async Task<string> FindOrPromptForClientDataFileAsync(string fileName, string pickerTitle)
    {
        if (!string.IsNullOrWhiteSpace(currentFolder))
        {
            var searchDirectory = new DirectoryInfo(currentFolder);

            while (searchDirectory != null)
            {
                string candidatePath = Path.Combine(searchDirectory.FullName, fileName);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }

                searchDirectory = searchDirectory.Parent;
            }
        }

        var files = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = pickerTitle,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
            new FilePickerFileType(fileName)
            {
                Patterns = new[] { fileName }
            }
        }
        });

        if (files == null || files.Count == 0)
        {
            return string.Empty;
        }

        return files[0].Path.LocalPath;
    }

    private async Task<string> PromptForLootListValueAsync(string currentValue)
    {
        string lootListFilePath = GetLootListFilePath();
        if (string.IsNullOrWhiteSpace(lootListFilePath) || !File.Exists(lootListFilePath))
        {
            await ShowMessageAsync("Lootlists not found", "Could not find items/lootlists.dfn from the current folder.");
            return string.Empty;
        }

        var allLootLists = LoadLootListsFromFile(lootListFilePath);
        if (allLootLists.Count == 0)
        {
            await ShowMessageAsync("No lootlists found", "No [LOOTLIST ...] sections were found in lootlists.dfn.");
            return string.Empty;
        }

        string currentLootName = ExtractLootListNameFromTagValue(currentValue);
        string currentAmount = ExtractLootAmountFromTagValue(currentValue);

        var pickerWindow = new Window
        {
            Title = "Select Lootlist",
            Width = 860,
            Height = 580,
            MinWidth = 640,
            MinHeight = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var searchBox = new TextBox
        {
            Watermark = "Search lootlists..."
        };

        var currentValueText = new TextBlock
        {
            Text = "Current LOOT value: " + (string.IsNullOrWhiteSpace(currentValue) ? "(empty)" : currentValue),
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var amountTextBox = new TextBox
        {
            Watermark = "Optional amount",
            Text = currentAmount,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var previewText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var lootListBox = new ListBox();

        var selectButton = new Button
        {
            Content = "Use Lootlist",
            IsEnabled = false,
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        void RefreshLootList()
        {
            string searchText = (searchBox.Text ?? string.Empty).Trim();

            IEnumerable<LootListEntry> filteredEntries = allLootLists;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredEntries = filteredEntries.Where(entry =>
                    entry.LootListName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    entry.HeaderText.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    entry.PreviewEntries.Any(previewEntry => previewEntry.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
            }

            var filteredList = filteredEntries.ToList();
            lootListBox.ItemsSource = filteredList;

            LootListEntry? matchingEntry = null;

            if (!string.IsNullOrWhiteSpace(currentLootName))
            {
                matchingEntry = filteredList.FirstOrDefault(entry =>
                    entry.LootListName.Equals(currentLootName, StringComparison.OrdinalIgnoreCase));
            }

            lootListBox.SelectedItem = matchingEntry;
            selectButton.IsEnabled = matchingEntry != null;

            if (matchingEntry != null)
            {
                previewText.Text = BuildLootListPreviewText(matchingEntry);
            }
            else
            {
                previewText.Text = string.Empty;
            }
        }

        searchBox.TextChanged += delegate
        {
            RefreshLootList();
        };

        lootListBox.SelectionChanged += delegate
        {
            var selectedEntry = lootListBox.SelectedItem as LootListEntry;
            selectButton.IsEnabled = selectedEntry != null;

            if (selectedEntry == null)
            {
                previewText.Text = string.Empty;
                return;
            }

            previewText.Text = BuildLootListPreviewText(selectedEntry);
        };

        selectButton.Click += delegate
        {
            var selectedEntry = lootListBox.SelectedItem as LootListEntry;
            if (selectedEntry == null)
            {
                return;
            }

            string selectedValue = selectedEntry.LootListName;

            string amountValue = (amountTextBox.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(amountValue))
            {
                selectedValue += "," + amountValue;
            }

            pickerWindow.Close(selectedValue);
        };

        cancelButton.Click += delegate
        {
            pickerWindow.Close(string.Empty);
        };

        lootListBox.DoubleTapped += delegate
        {
            var selectedEntry = lootListBox.SelectedItem as LootListEntry;
            if (selectedEntry == null)
            {
                return;
            }

            string selectedValue = selectedEntry.LootListName;

            string amountValue = (amountTextBox.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(amountValue))
            {
                selectedValue += "," + amountValue;
            }

            pickerWindow.Close(selectedValue);
        };

        lootListBox.ItemTemplate = new FuncDataTemplate<LootListEntry?>((lootEntry, _) =>
        {
            var panel = new StackPanel
            {
                Spacing = 2,
                Margin = new Avalonia.Thickness(4)
            };

            if (lootEntry == null)
            {
                return panel;
            }

            panel.Children.Add(new TextBlock
            {
                Text = lootEntry.LootListName,
                FontWeight = FontWeight.Bold
            });

            panel.Children.Add(new TextBlock
            {
                Text = lootEntry.EntryCount + " entries",
                Foreground = Brushes.LightGray,
                FontSize = 11
            });

            return panel;
        });

        RefreshLootList();

        var rootPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootPanel.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        rootPanel.Children.Add(currentValueText);
        Grid.SetRow(currentValueText, 1);

        rootPanel.Children.Add(amountTextBox);
        Grid.SetRow(amountTextBox, 2);

        rootPanel.Children.Add(lootListBox);
        Grid.SetRow(lootListBox, 3);

        rootPanel.Children.Add(previewText);
        Grid.SetRow(previewText, 4);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        buttonPanel.Children.Add(selectButton);
        buttonPanel.Children.Add(cancelButton);

        rootPanel.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 5);

        pickerWindow.Content = rootPanel;

        var result = await pickerWindow.ShowDialog<string>(this);
        return result ?? string.Empty;
    }

    private List<LootListEntry> LoadLootListsFromFile(string lootListFilePath)
    {
        var output = new List<LootListEntry>();
        var lines = File.ReadAllLines(lootListFilePath);

        int lineIndex = 0;
        while (lineIndex < lines.Length)
        {
            string trimmedLine = lines[lineIndex].Trim();

            if (!trimmedLine.StartsWith("[LOOTLIST ", StringComparison.OrdinalIgnoreCase) ||
                !trimmedLine.EndsWith("]", StringComparison.Ordinal))
            {
                lineIndex++;
                continue;
            }

            string headerText = trimmedLine;
            string lootListName = trimmedLine.Substring(10, trimmedLine.Length - 11).Trim();

            var previewEntries = new List<string>();
            int entryCount = 0;

            lineIndex++;

            bool insideBlock = false;

            while (lineIndex < lines.Length)
            {
                string blockLine = lines[lineIndex].Trim();

                if (!insideBlock)
                {
                    if (blockLine.StartsWith("{", StringComparison.Ordinal))
                    {
                        insideBlock = true;
                    }

                    lineIndex++;
                    continue;
                }

                if (blockLine == "}")
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(blockLine) && !blockLine.StartsWith("//", StringComparison.Ordinal))
                {
                    entryCount++;

                    if (previewEntries.Count < 12)
                    {
                        previewEntries.Add(blockLine);
                    }
                }

                lineIndex++;
            }

            output.Add(new LootListEntry
            {
                HeaderText = headerText,
                LootListName = lootListName,
                EntryCount = entryCount,
                PreviewEntries = previewEntries
            });

            lineIndex++;
        }

        return output
            .OrderBy(entry => entry.LootListName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ExtractLootListNameFromTagValue(string tagValue)
    {
        if (string.IsNullOrWhiteSpace(tagValue))
        {
            return string.Empty;
        }

        string trimmedValue = tagValue.Trim();
        int commaIndex = trimmedValue.IndexOf(',');

        if (commaIndex < 0)
        {
            return trimmedValue;
        }

        return trimmedValue.Substring(0, commaIndex).Trim();
    }

    private string ExtractLootAmountFromTagValue(string tagValue)
    {
        if (string.IsNullOrWhiteSpace(tagValue))
        {
            return string.Empty;
        }

        string trimmedValue = tagValue.Trim();
        int commaIndex = trimmedValue.IndexOf(',');

        if (commaIndex < 0 || commaIndex >= trimmedValue.Length - 1)
        {
            return string.Empty;
        }

        return trimmedValue.Substring(commaIndex + 1).Trim();
    }

    private string BuildLootListPreviewText(LootListEntry entry)
    {
        var textLines = new List<string>
        {
            entry.HeaderText,
            entry.EntryCount + " entries"
        };

        if (entry.PreviewEntries.Count > 0)
        {
            textLines.Add(string.Empty);
            textLines.AddRange(entry.PreviewEntries);
        }

        return string.Join(Environment.NewLine, textLines);
    }

    private async Task<string> PromptForHueValueAsync(string currentValue)
    {
        var hueEntries = await GetOrLoadHueEntriesAsync();
        if (hueEntries == null || hueEntries.Count == 0)
        {
            return string.Empty;
        }

        var pickerWindow = new Window
        {
            Title = "Select Hue",
            Width = 900,
            Height = 620,
            MinWidth = 700,
            MinHeight = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var searchBox = new TextBox
        {
            Watermark = "Search hues by id or name..."
        };

        var currentValueText = new TextBlock
        {
            Text = "Current hue value: " + (string.IsNullOrWhiteSpace(currentValue) ? "(empty)" : currentValue),
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var descriptionText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var hueListBox = new ListBox();

        var selectButton = new Button
        {
            Content = "Use Hue",
            IsEnabled = false,
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        void RefreshHueList()
        {
            var searchText = (searchBox.Text ?? string.Empty).Trim();

            IEnumerable<HueFileReader.HueEntry> filteredEntries = hueEntries;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredEntries = filteredEntries.Where(hueEntry =>
                    hueEntry.HueId.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    hueEntry.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filteredEntries.ToList();
            hueListBox.ItemsSource = filteredList;

            selectButton.IsEnabled = false;
            descriptionText.Text = string.Empty;

            if (TryParseHueId(currentValue, out int currentHueId))
            {
                var matchingEntry = filteredList.FirstOrDefault(hueEntry => hueEntry.HueId == currentHueId);
                if (matchingEntry != null)
                {
                    hueListBox.SelectedItem = matchingEntry;
                    selectButton.IsEnabled = true;

                    string hueName = string.IsNullOrWhiteSpace(matchingEntry.Name) ? "(no name)" : matchingEntry.Name;
                    descriptionText.Text =
                        "Hue " + matchingEntry.HueId +
                        Environment.NewLine +
                        "Name: " + hueName +
                        Environment.NewLine +
                        "Range: " + matchingEntry.TableStart + " - " + matchingEntry.TableEnd;
                    return;
                }
            }

            hueListBox.SelectedItem = null;
        }

        searchBox.TextChanged += delegate
        {
            RefreshHueList();
        };

        hueListBox.SelectionChanged += delegate
        {
            var selectedEntry = hueListBox.SelectedItem as HueFileReader.HueEntry;
            selectButton.IsEnabled = selectedEntry != null;

            if (selectedEntry == null)
            {
                descriptionText.Text = string.Empty;
                return;
            }

            string hueName = string.IsNullOrWhiteSpace(selectedEntry.Name) ? "(no name)" : selectedEntry.Name;
            descriptionText.Text =
                "Hue " + selectedEntry.HueId +
                Environment.NewLine +
                "Name: " + hueName +
                Environment.NewLine +
                "Range: " + selectedEntry.TableStart + " - " + selectedEntry.TableEnd;
        };

        selectButton.Click += delegate
        {
            var selectedEntry = hueListBox.SelectedItem as HueFileReader.HueEntry;
            if (selectedEntry == null)
            {
                return;
            }

            pickerWindow.Close(selectedEntry.HueId.ToString());
        };

        cancelButton.Click += delegate
        {
            pickerWindow.Close(string.Empty);
        };

        hueListBox.DoubleTapped += delegate
        {
            var selectedEntry = hueListBox.SelectedItem as HueFileReader.HueEntry;
            if (selectedEntry == null)
            {
                return;
            }

            pickerWindow.Close(selectedEntry.HueId.ToString());
        };

        hueListBox.ItemTemplate = new FuncDataTemplate<HueFileReader.HueEntry?>((hueEntry, _) =>
        {
            var rootPanel = new StackPanel
            {
                Spacing = 4,
                Margin = new Avalonia.Thickness(4)
            };

            if (hueEntry == null)
            {
                return rootPanel;
            }

            string hueName = string.IsNullOrWhiteSpace(hueEntry.Name) ? "(no name)" : hueEntry.Name;

            rootPanel.Children.Add(new TextBlock
            {
                Text = hueEntry.HueId + " - " + hueName,
                FontWeight = FontWeight.Bold
            });

            var swatchPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 1
            };

            int previewCount = Math.Min(16, hueEntry.PreviewColors.Count);
            for (int colorIndex = 0; colorIndex < previewCount; colorIndex++)
            {
                swatchPanel.Children.Add(new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = new SolidColorBrush(hueEntry.PreviewColors[colorIndex]),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Avalonia.Thickness(1)
                });
            }

            rootPanel.Children.Add(swatchPanel);

            return rootPanel;
        });

        RefreshHueList();

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootGrid.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        rootGrid.Children.Add(currentValueText);
        Grid.SetRow(currentValueText, 1);

        rootGrid.Children.Add(hueListBox);
        Grid.SetRow(hueListBox, 2);

        rootGrid.Children.Add(descriptionText);
        Grid.SetRow(descriptionText, 3);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        buttonPanel.Children.Add(selectButton);
        buttonPanel.Children.Add(cancelButton);

        rootGrid.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 4);

        pickerWindow.Content = rootGrid;

        var result = await pickerWindow.ShowDialog<string>(this);
        return result ?? string.Empty;
    }

    private async Task<List<HueFileReader.HueEntry>?> GetOrLoadHueEntriesAsync()
    {
        if (!string.IsNullOrWhiteSpace(currentHueFilePath) && cachedHueEntries != null && cachedHueEntries.Count > 0)
        {
            return cachedHueEntries;
        }

        string hueFilePath = await FindOrPromptForHueFileAsync();
        if (string.IsNullOrWhiteSpace(hueFilePath) || !File.Exists(hueFilePath))
        {
            return null;
        }

        var loadedEntries = HueFileReader.LoadHueEntries(hueFilePath);
        if (loadedEntries.Count == 0)
        {
            await ShowMessageAsync("Hue load failed", "Could not read any hue entries from hues.mul.");
            return null;
        }

        currentHueFilePath = hueFilePath;
        cachedHueEntries = loadedEntries;
        return cachedHueEntries;
    }

    private async Task<string> FindOrPromptForHueFileAsync()
    {
        if (!string.IsNullOrWhiteSpace(currentHueFilePath) && File.Exists(currentHueFilePath))
        {
            return currentHueFilePath;
        }

        if (!string.IsNullOrWhiteSpace(currentFolder))
        {
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
        }

        var files = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select hues.mul",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Hue file")
                {
                    Patterns = new[] { "hues.mul" }
                }
            }
        });

        if (files == null || files.Count == 0)
        {
            return string.Empty;
        }

        return files[0].Path.LocalPath;
    }

    private async Task<string> PromptForItemLayerValueAsync(string currentValue)
    {
        var pickerWindow = new Window
        {
            Title = "Select Item Layer",
            Width = 820,
            Height = 560,
            MinWidth = 600,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var searchBox = new TextBox
        {
            Watermark = "Search item layers..."
        };

        var currentValueText = new TextBlock
        {
            Text = "Current LAYER value: " + (string.IsNullOrWhiteSpace(currentValue) ? "(empty)" : currentValue),
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var descriptionText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var itemLayerListBox = new ListBox();

        var selectButton = new Button
        {
            Content = "Use Layer",
            IsEnabled = false,
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        var allItemLayers = GetAllItemLayers();

        void RefreshItemLayerList()
        {
            var searchText = (searchBox.Text ?? string.Empty).Trim();

            IEnumerable<ItemLayerEntry> filteredEntries = allItemLayers;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredEntries = filteredEntries.Where(itemLayerEntry =>
                    itemLayerEntry.LayerId.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    itemLayerEntry.HexValue.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    itemLayerEntry.LayerName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    itemLayerEntry.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            itemLayerListBox.SelectedItem = null;
            itemLayerListBox.ItemsSource = filteredEntries.ToList();
            selectButton.IsEnabled = false;
            descriptionText.Text = string.Empty;
        }

        searchBox.TextChanged += delegate
        {
            RefreshItemLayerList();
        };

        itemLayerListBox.SelectionChanged += delegate
        {
            var selectedEntry = itemLayerListBox.SelectedItem as ItemLayerEntry;
            selectButton.IsEnabled = selectedEntry != null;

            if (selectedEntry == null)
            {
                descriptionText.Text = string.Empty;
                return;
            }

            descriptionText.Text =
                selectedEntry.LayerId + " / " + selectedEntry.HexValue + " - " + selectedEntry.LayerName +
                Environment.NewLine + Environment.NewLine +
                selectedEntry.Description;
        };

        selectButton.Click += delegate
        {
            var selectedEntry = itemLayerListBox.SelectedItem as ItemLayerEntry;
            if (selectedEntry == null)
            {
                return;
            }

            pickerWindow.Close(selectedEntry.LayerId.ToString());
        };

        cancelButton.Click += delegate
        {
            pickerWindow.Close(string.Empty);
        };

        itemLayerListBox.DoubleTapped += delegate
        {
            var selectedEntry = itemLayerListBox.SelectedItem as ItemLayerEntry;
            if (selectedEntry == null)
            {
                return;
            }

            pickerWindow.Close(selectedEntry.LayerId.ToString());
        };

        itemLayerListBox.ItemTemplate = new FuncDataTemplate<ItemLayerEntry?>((itemLayerEntry, _) =>
        {
            var panel = new StackPanel
            {
                Spacing = 2,
                Margin = new Avalonia.Thickness(4)
            };

            if (itemLayerEntry == null)
            {
                return panel;
            }

            panel.Children.Add(new TextBlock
            {
                Text = itemLayerEntry.LayerId + " / " + itemLayerEntry.HexValue + " - " + itemLayerEntry.LayerName,
                FontWeight = FontWeight.Bold
            });

            panel.Children.Add(new TextBlock
            {
                Text = itemLayerEntry.Description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.LightGray,
                FontSize = 11
            });

            return panel;
        });

        RefreshItemLayerList();

        var rootPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootPanel.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        rootPanel.Children.Add(currentValueText);
        Grid.SetRow(currentValueText, 1);

        rootPanel.Children.Add(itemLayerListBox);
        Grid.SetRow(itemLayerListBox, 2);

        rootPanel.Children.Add(descriptionText);
        Grid.SetRow(descriptionText, 3);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        buttonPanel.Children.Add(selectButton);
        buttonPanel.Children.Add(cancelButton);

        rootPanel.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 4);

        pickerWindow.Content = rootPanel;

        var result = await pickerWindow.ShowDialog<string>(this);
        return result ?? string.Empty;
    }

    private List<ItemLayerEntry> GetAllItemLayers()
    {
        return new List<ItemLayerEntry>
        {
            new ItemLayerEntry { LayerId = 0, HexValue = "0x00", LayerName = "None", Description = "No layer." },
            new ItemLayerEntry { LayerId = 1, HexValue = "0x01", LayerName = "Right Hand", Description = "Single-hand item or weapon." },
            new ItemLayerEntry { LayerId = 2, HexValue = "0x02", LayerName = "Left Hand", Description = "Two-hand item or weapon, including shields." },
            new ItemLayerEntry { LayerId = 3, HexValue = "0x03", LayerName = "Feet", Description = "Footwear, foot covering, or foot armor." },
            new ItemLayerEntry { LayerId = 4, HexValue = "0x04", LayerName = "Legs", Description = "Leg covering, including pants, shorts, and some armor legs." },
            new ItemLayerEntry { LayerId = 5, HexValue = "0x05", LayerName = "Chest", Description = "Chest clothing or female chest armor." },
            new ItemLayerEntry { LayerId = 6, HexValue = "0x06", LayerName = "Head", Description = "Head covering or helmet." },
            new ItemLayerEntry { LayerId = 7, HexValue = "0x07", LayerName = "Hands", Description = "Hand covering or armor." },
            new ItemLayerEntry { LayerId = 8, HexValue = "0x08", LayerName = "Ring", Description = "Ring layer." },
            new ItemLayerEntry { LayerId = 9, HexValue = "0x09", LayerName = "Talisman", Description = "Talisman layer." },
            new ItemLayerEntry { LayerId = 10, HexValue = "0x0A", LayerName = "Neck", Description = "Neck covering or armor." },
            new ItemLayerEntry { LayerId = 11, HexValue = "0x0B", LayerName = "Hair", Description = "Hair layer." },
            new ItemLayerEntry { LayerId = 12, HexValue = "0x0C", LayerName = "Waist", Description = "Waist layer, such as half-aprons." },
            new ItemLayerEntry { LayerId = 13, HexValue = "0x0D", LayerName = "Torso Inner", Description = "Inner torso layer, including chest armor." },
            new ItemLayerEntry { LayerId = 14, HexValue = "0x0E", LayerName = "Bracelet", Description = "Bracelet layer." },
            new ItemLayerEntry { LayerId = 15, HexValue = "0x0F", LayerName = "Face", Description = "Face layer." },
            new ItemLayerEntry { LayerId = 16, HexValue = "0x10", LayerName = "Facial Hair", Description = "Facial hair layer." },
            new ItemLayerEntry { LayerId = 17, HexValue = "0x11", LayerName = "Torso Middle", Description = "Middle torso layer, such as surcoats, tunics, aprons, and sashes." },
            new ItemLayerEntry { LayerId = 18, HexValue = "0x12", LayerName = "Earrings", Description = "Earrings layer." },
            new ItemLayerEntry { LayerId = 19, HexValue = "0x13", LayerName = "Arms", Description = "Arm covering or armor." },
            new ItemLayerEntry { LayerId = 20, HexValue = "0x14", LayerName = "Back", Description = "Back layer, such as cloaks." },
            new ItemLayerEntry { LayerId = 21, HexValue = "0x15", LayerName = "BackPack", Description = "Backpack layer." },
            new ItemLayerEntry { LayerId = 22, HexValue = "0x16", LayerName = "Torso Outer", Description = "Outer torso layer, such as robes." },
            new ItemLayerEntry { LayerId = 23, HexValue = "0x17", LayerName = "Legs Outer", Description = "Outer leg layer, such as skirts or kilts." },
            new ItemLayerEntry { LayerId = 24, HexValue = "0x18", LayerName = "Legs Inner", Description = "Inner leg layer, such as leg armor." },
            new ItemLayerEntry { LayerId = 25, HexValue = "0x19", LayerName = "Mount", Description = "Mount layer, such as horses or ostards." },
            new ItemLayerEntry { LayerId = 26, HexValue = "0x1A", LayerName = "Sell Container", Description = "NPC buy restock container." },
            new ItemLayerEntry { LayerId = 27, HexValue = "0x1B", LayerName = "Bought Container", Description = "NPC buy no-restock container." },
            new ItemLayerEntry { LayerId = 28, HexValue = "0x1C", LayerName = "Buy Container", Description = "NPC sell container." },
            new ItemLayerEntry { LayerId = 29, HexValue = "0x1D", LayerName = "Bank Box", Description = "Bank box layer." }
        };
    }

    private async Task<string> PromptForItemTypeValueAsync(string currentValue)
    {
        var pickerWindow = new Window
        {
            Title = "Select Item Type",
            Width = 820,
            Height = 560,
            MinWidth = 600,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var searchBox = new TextBox
        {
            Watermark = "Search item types..."
        };

        var currentValueText = new TextBlock
        {
            Text = "Current TYPE value: " + (string.IsNullOrWhiteSpace(currentValue) ? "(empty)" : currentValue),
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var descriptionText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var itemTypeListBox = new ListBox();

        var selectButton = new Button
        {
            Content = "Use Type",
            IsEnabled = false,
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        var allItemTypes = GetAllItemTypes();

        void RefreshItemTypeList()
        {
            var searchText = (searchBox.Text ?? string.Empty).Trim();

            IEnumerable<ItemTypeEntry> filteredEntries = allItemTypes;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredEntries = filteredEntries.Where(itemTypeEntry =>
                    itemTypeEntry.TypeId.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    itemTypeEntry.TypeName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    itemTypeEntry.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            itemTypeListBox.SelectedItem = null;
            itemTypeListBox.ItemsSource = filteredEntries.ToList();
            selectButton.IsEnabled = false;
            descriptionText.Text = string.Empty;
        }

        searchBox.TextChanged += delegate
        {
            RefreshItemTypeList();
        };

        itemTypeListBox.SelectionChanged += delegate
        {
            var selectedEntry = itemTypeListBox.SelectedItem as ItemTypeEntry;
            selectButton.IsEnabled = selectedEntry != null;

            if (selectedEntry == null)
            {
                descriptionText.Text = string.Empty;
                return;
            }

            descriptionText.Text = selectedEntry.TypeId + " - " + selectedEntry.TypeName + Environment.NewLine + Environment.NewLine + selectedEntry.Description;
        };

        selectButton.Click += delegate
        {
            var selectedEntry = itemTypeListBox.SelectedItem as ItemTypeEntry;
            if (selectedEntry == null)
            {
                return;
            }

            pickerWindow.Close(selectedEntry.TypeId.ToString());
        };

        cancelButton.Click += delegate
        {
            pickerWindow.Close(string.Empty);
        };

        itemTypeListBox.DoubleTapped += delegate
        {
            var selectedEntry = itemTypeListBox.SelectedItem as ItemTypeEntry;
            if (selectedEntry == null)
            {
                return;
            }

            pickerWindow.Close(selectedEntry.TypeId.ToString());
        };

        itemTypeListBox.ItemTemplate = new FuncDataTemplate<ItemTypeEntry?>((itemTypeEntry, _) =>
        {
            var panel = new StackPanel
            {
                Spacing = 2,
                Margin = new Avalonia.Thickness(4)
            };

            if (itemTypeEntry == null)
            {
                return panel;
            }

            panel.Children.Add(new TextBlock
            {
                Text = itemTypeEntry.TypeId + " - " + itemTypeEntry.TypeName,
                FontWeight = FontWeight.Bold
            });

            panel.Children.Add(new TextBlock
            {
                Text = itemTypeEntry.Description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.LightGray,
                FontSize = 11
            });

            return panel;
        });

        RefreshItemTypeList();

        var rootPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootPanel.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        rootPanel.Children.Add(currentValueText);
        Grid.SetRow(currentValueText, 1);

        rootPanel.Children.Add(itemTypeListBox);
        Grid.SetRow(itemTypeListBox, 2);

        rootPanel.Children.Add(descriptionText);
        Grid.SetRow(descriptionText, 3);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        buttonPanel.Children.Add(selectButton);
        buttonPanel.Children.Add(cancelButton);

        rootPanel.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 4);

        pickerWindow.Content = rootPanel;

        var result = await pickerWindow.ShowDialog<string>(this);
        return result ?? string.Empty;
    }

    private List<ItemTypeEntry> GetAllItemTypes()
    {
        return new List<ItemTypeEntry>
        {
            new ItemTypeEntry { TypeId = 0, TypeName = "IT_NOTYPE", Description = "No item type specified." },
            new ItemTypeEntry { TypeId = 1, TypeName = "IT_CONTAINER", Description = "Container." },
            new ItemTypeEntry { TypeId = 2, TypeName = "IT_CASTLEGATEOPENER", Description = "Opens a linked castle gate." },
            new ItemTypeEntry { TypeId = 3, TypeName = "IT_CASTLEGATE", Description = "Castle gate." },
            new ItemTypeEntry { TypeId = 6, TypeName = "IT_TELEPORTITEM", Description = "Teleporter item or rune." },
            new ItemTypeEntry { TypeId = 7, TypeName = "IT_KEY", Description = "Key used to lock or unlock objects with matching MORE values." },
            new ItemTypeEntry { TypeId = 8, TypeName = "IT_LOCKEDCONTAINER", Description = "Locked container." },
            new ItemTypeEntry { TypeId = 9, TypeName = "IT_SPELLBOOK", Description = "Spellbook." },
            new ItemTypeEntry { TypeId = 10, TypeName = "IT_MAP", Description = "In-game world map." },
            new ItemTypeEntry { TypeId = 11, TypeName = "IT_BOOK", Description = "Writable or readable book." },
            new ItemTypeEntry { TypeId = 12, TypeName = "IT_DOOR", Description = "Unlocked door." },
            new ItemTypeEntry { TypeId = 13, TypeName = "IT_LOCKEDDOOR", Description = "Locked door." },
            new ItemTypeEntry { TypeId = 14, TypeName = "IT_FOOD", Description = "Food item." },
            new ItemTypeEntry { TypeId = 15, TypeName = "IT_MAGICWAND", Description = "Magic wand." },
            new ItemTypeEntry { TypeId = 16, TypeName = "IT_RESURRECTOBJECT", Description = "Resurrection object." },
            new ItemTypeEntry { TypeId = 17, TypeName = "IT_CRYSTALBALL", Description = "Unused item type." },
            new ItemTypeEntry { TypeId = 18, TypeName = "IT_POTION", Description = "Magic potion." },
            new ItemTypeEntry { TypeId = 19, TypeName = "IT_TRADEWINDOW", Description = "Secure trade container." },
            new ItemTypeEntry { TypeId = 35, TypeName = "IT_TOWNSTONE", Description = "Townstone or townstone deed." },
            new ItemTypeEntry { TypeId = 50, TypeName = "IT_RECALLRUNE", Description = "Recall rune." },
            new ItemTypeEntry { TypeId = 51, TypeName = "IT_GATE", Description = "Two-way gate." },
            new ItemTypeEntry { TypeId = 60, TypeName = "IT_OBJTELEPORTER", Description = "One-way object teleporter." },
            new ItemTypeEntry { TypeId = 61, TypeName = "IT_ITEMSPAWNER", Description = "Item spawner object." },
            new ItemTypeEntry { TypeId = 62, TypeName = "IT_NPCSPAWNER", Description = "NPC spawner object." },
            new ItemTypeEntry { TypeId = 63, TypeName = "IT_SPAWNCONT", Description = "Spawner container." },
            new ItemTypeEntry { TypeId = 64, TypeName = "IT_LOCKEDSPAWNCONT", Description = "Locked spawner container." },
            new ItemTypeEntry { TypeId = 65, TypeName = "IT_UNLOCKABLESPAWNCONT", Description = "Unlockable spawner container." },
            new ItemTypeEntry { TypeId = 69, TypeName = "IT_AREASPAWNER", Description = "Area NPC spawner." },
            new ItemTypeEntry { TypeId = 80, TypeName = "IT_ADVANCEGATE", Description = "Single-use advancement gate." },
            new ItemTypeEntry { TypeId = 81, TypeName = "IT_MULTIADVANCEGATE", Description = "Multi-use advancement gate." },
            new ItemTypeEntry { TypeId = 82, TypeName = "IT_MONSTERGATE", Description = "Monster gate." },
            new ItemTypeEntry { TypeId = 83, TypeName = "IT_RACEGATE", Description = "Race gate." },
            new ItemTypeEntry { TypeId = 85, TypeName = "IT_DAMAGEOBJECT", Description = "Damage object or trap." },
            new ItemTypeEntry { TypeId = 87, TypeName = "IT_TRASHCONT", Description = "Trash container." },
            new ItemTypeEntry { TypeId = 88, TypeName = "IT_SOUNDOBJECT", Description = "Sound object." },
            new ItemTypeEntry { TypeId = 89, TypeName = "IT_MAPCHANGEOBJECT", Description = "Map change object." },
            new ItemTypeEntry { TypeId = 90, TypeName = "IT_WORLDCHANGEGATE", Description = "World change gate." },
            new ItemTypeEntry { TypeId = 101, TypeName = "IT_MORPHOBJECT", Description = "Morph object." },
            new ItemTypeEntry { TypeId = 102, TypeName = "IT_UNMORPHOBJECT", Description = "Unmorph object." },
            new ItemTypeEntry { TypeId = 105, TypeName = "IT_DRINK", Description = "Drinkable item." },
            new ItemTypeEntry { TypeId = 106, TypeName = "IT_STANDINGHARP", Description = "Standing harp instrument." },
            new ItemTypeEntry { TypeId = 111, TypeName = "IT_ZEROKILLSGATE", Description = "Zero kills gate." },
            new ItemTypeEntry { TypeId = 117, TypeName = "IT_PLANK", Description = "Boat plank." },
            new ItemTypeEntry { TypeId = 118, TypeName = "IT_FIREWORKSWAND", Description = "Fireworks wand." },
            new ItemTypeEntry { TypeId = 119, TypeName = "IT_SPELLCHANNELING", Description = "Spell Channeling." },
            new ItemTypeEntry { TypeId = 125, TypeName = "IT_ESCORTNPCSPAWNER", Description = "Escort NPC spawner." },
            new ItemTypeEntry { TypeId = 186, TypeName = "IT_RENAMEDEED", Description = "Rename deed." },
            new ItemTypeEntry { TypeId = 190, TypeName = "IT_LEATHERREPAIRTOOL", Description = "Leather repair tool." },
            new ItemTypeEntry { TypeId = 191, TypeName = "IT_BOWREPAIRTOOL", Description = "Bow repair tool." },
            new ItemTypeEntry { TypeId = 200, TypeName = "IT_TILLER", Description = "Boat tillerman." },
            new ItemTypeEntry { TypeId = 201, TypeName = "IT_HOUSEADDON", Description = "House addon item." },
            new ItemTypeEntry { TypeId = 202, TypeName = "IT_GUILDSTONE", Description = "Guildstone or guildstone deed." },
            new ItemTypeEntry { TypeId = 203, TypeName = "IT_HOUSESIGN", Description = "House sign." },
            new ItemTypeEntry { TypeId = 204, TypeName = "IT_TINKERTOOL", Description = "Tinker tool." },
            new ItemTypeEntry { TypeId = 205, TypeName = "IT_METALREPAIRTOOL", Description = "Metal repair tool." },
            new ItemTypeEntry { TypeId = 207, TypeName = "IT_FORGE", Description = "Forge." },
            new ItemTypeEntry { TypeId = 208, TypeName = "IT_DYE", Description = "Dye item." },
            new ItemTypeEntry { TypeId = 209, TypeName = "IT_DYEVAT", Description = "Dye vat or dye tub." },
            new ItemTypeEntry { TypeId = 210, TypeName = "IT_MODELMULTI", Description = "Boat deed style multi item." },
            new ItemTypeEntry { TypeId = 211, TypeName = "IT_ARCHERYBUTTE", Description = "Archery butte." },
            new ItemTypeEntry { TypeId = 212, TypeName = "IT_DRUM", Description = "Drum instrument." },
            new ItemTypeEntry { TypeId = 213, TypeName = "IT_TAMBOURINE", Description = "Tambourine instrument." },
            new ItemTypeEntry { TypeId = 214, TypeName = "IT_HARP", Description = "Harp instrument." },
            new ItemTypeEntry { TypeId = 215, TypeName = "IT_LUTE", Description = "Lute instrument." },
            new ItemTypeEntry { TypeId = 216, TypeName = "IT_AXE", Description = "Axe item type." },
            new ItemTypeEntry { TypeId = 217, TypeName = "IT_PLAYERVENDORDEED", Description = "Player vendor deed." },
            new ItemTypeEntry { TypeId = 218, TypeName = "IT_SMITHYTOOL", Description = "Smithing tool." },
            new ItemTypeEntry { TypeId = 219, TypeName = "IT_CARPENTRYTOOL", Description = "Carpentry tool." },
            new ItemTypeEntry { TypeId = 220, TypeName = "IT_MININGTOOL", Description = "Mining tool." },
            new ItemTypeEntry { TypeId = 221, TypeName = "IT_EMPTYVIAL", Description = "Empty vial." },
            new ItemTypeEntry { TypeId = 222, TypeName = "IT_UNSPUNFABRIC", Description = "Unspun wool." },
            new ItemTypeEntry { TypeId = 223, TypeName = "IT_UNCOOKEDFISH", Description = "Uncooked fish." },
            new ItemTypeEntry { TypeId = 224, TypeName = "IT_UNCOOKEDMEAT", Description = "Uncooked meat." },
            new ItemTypeEntry { TypeId = 225, TypeName = "IT_SPUNFABRIC", Description = "Spun wool or yarn." },
            new ItemTypeEntry { TypeId = 226, TypeName = "IT_FLETCHINGTOOL", Description = "Fletching tool." },
            new ItemTypeEntry { TypeId = 227, TypeName = "IT_CANNONBALL", Description = "Unused item type." },
            new ItemTypeEntry { TypeId = 228, TypeName = "IT_WATERPITCHER", Description = "Water pitcher." },
            new ItemTypeEntry { TypeId = 229, TypeName = "IT_UNCOOKEDDOUGH", Description = "Unused item type." },
            new ItemTypeEntry { TypeId = 230, TypeName = "IT_SEWINGKIT", Description = "Sewing kit." },
            new ItemTypeEntry { TypeId = 231, TypeName = "IT_ORE", Description = "Ore." },
            new ItemTypeEntry { TypeId = 232, TypeName = "IT_MESSAGEBOARD", Description = "Message board." },
            new ItemTypeEntry { TypeId = 233, TypeName = "IT_SWORD", Description = "Sword or bladed weapon." },
            new ItemTypeEntry { TypeId = 234, TypeName = "IT_CAMPING", Description = "Kindling or camping item." },
            new ItemTypeEntry { TypeId = 235, TypeName = "IT_MAGICSTATUE", Description = "Magic statue." },
            new ItemTypeEntry { TypeId = 236, TypeName = "IT_GUILLOTINE", Description = "Guillotine." },
            new ItemTypeEntry { TypeId = 238, TypeName = "IT_FLOURSACK", Description = "Sack of flour." },
            new ItemTypeEntry { TypeId = 239, TypeName = "IT_OPENFLOURSACK", Description = "Open sack of flour." },
            new ItemTypeEntry { TypeId = 240, TypeName = "IT_FISHINGPOLE", Description = "Fishing pole." },
            new ItemTypeEntry { TypeId = 241, TypeName = "IT_CLOCK", Description = "Clock." },
            new ItemTypeEntry { TypeId = 242, TypeName = "IT_MORTAR", Description = "Mortar and pestle." },
            new ItemTypeEntry { TypeId = 243, TypeName = "IT_SCISSORS", Description = "Scissors." },
            new ItemTypeEntry { TypeId = 244, TypeName = "IT_BANDAGE", Description = "Bandage." },
            new ItemTypeEntry { TypeId = 245, TypeName = "IT_SEXTANT", Description = "Sextant." },
            new ItemTypeEntry { TypeId = 246, TypeName = "IT_HAIRDYE", Description = "Hair dye." },
            new ItemTypeEntry { TypeId = 247, TypeName = "IT_LOCKPICK", Description = "Lockpick." },
            new ItemTypeEntry { TypeId = 248, TypeName = "IT_COTTONPLANT", Description = "Cotton plant." },
            new ItemTypeEntry { TypeId = 249, TypeName = "IT_TINKERAXLE", Description = "Axles and gears." },
            new ItemTypeEntry { TypeId = 250, TypeName = "IT_TINKERAWG", Description = "Springs, bolts and misc tinker parts." },
            new ItemTypeEntry { TypeId = 251, TypeName = "IT_TINKERCLOCK", Description = "Clock parts." },
            new ItemTypeEntry { TypeId = 252, TypeName = "IT_TINKERSEXTANT", Description = "Sextant parts." },
            new ItemTypeEntry { TypeId = 253, TypeName = "IT_TRAININGDUMMY", Description = "Training dummy." },
            new ItemTypeEntry { TypeId = 255, TypeName = "IT_COUNT", Description = "Final item type marker." }
        };
    }

    private async Task<string> PromptForCreateSectionTypeAsync()
    {
        var dialogWindow = new Window
        {
            Title = "New Create Section",
            Width = 420,
            Height = 220,
            MinWidth = 420,
            MinHeight = 220,
            MaxHeight = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var infoText = new TextBlock
        {
            Text = "Select the type of create.dfn section to add.",
            TextWrapping = TextWrapping.Wrap
        };

        var sectionTypeComboBox = new ComboBox
        {
            ItemsSource = new List<string>
            {
                "MENUENTRY",
                "SUBMENU",
                "ITEM"
            },
            SelectedIndex = 0,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        var createButton = new Button
        {
            Content = "Create",
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        createButton.Click += delegate
        {
            var selectedType = sectionTypeComboBox.SelectedItem as string ?? string.Empty;
            dialogWindow.Close(selectedType);
        };

        cancelButton.Click += delegate
        {
            dialogWindow.Close(string.Empty);
        };

        var rootPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootPanel.Children.Add(infoText);
        Grid.SetRow(infoText, 0);

        rootPanel.Children.Add(sectionTypeComboBox);
        Grid.SetRow(sectionTypeComboBox, 1);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 16, 0, 0)
        };

        buttonPanel.Children.Add(createButton);
        buttonPanel.Children.Add(cancelButton);

        rootPanel.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 2);

        dialogWindow.Content = rootPanel;

        var result = await dialogWindow.ShowDialog<string>(this);
        return result ?? string.Empty;
    }

    private async Task<string> ShowAddTagPickerAsync()
    {
        var pickerWindow = new Window
        {
            Title = "Add Tag",
            Width = 700,
            Height = 500,
            MinWidth = 500,
            MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var searchBox = new TextBox
        {
            Watermark = "Search tags..."
        };

        var descriptionText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var tagListBox = new ListBox();
        var addButton = new Button
        {
            Content = "Add Tag",
            IsEnabled = false,
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        var allTagEntries = GetActiveKnownTagSet()
            .OrderBy(tagName => tagName, StringComparer.OrdinalIgnoreCase)
            .Select(tagName => new TagPickerEntry
            {
                TagName = tagName,
                Description = GetTagDescription(tagName)
            })
            .ToList();

        void RefreshTagPickerList()
        {
            var searchText = searchBox.Text ?? string.Empty;
            searchText = searchText.Trim();

            IEnumerable<TagPickerEntry> filteredEntries = allTagEntries;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredEntries = filteredEntries.Where(tagEntry =>
                    tagEntry.TagName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    tagEntry.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            tagListBox.SelectedItem = null;
            tagListBox.ItemsSource = filteredEntries.ToList();
            addButton.IsEnabled = false;
            descriptionText.Text = string.Empty;
        }

        searchBox.TextChanged += delegate
        {
            RefreshTagPickerList();
            addButton.IsEnabled = tagListBox.SelectedItem is TagPickerEntry;
        };

        tagListBox.SelectionChanged += delegate
        {
            var selectedEntry = tagListBox.SelectedItem as TagPickerEntry;
            addButton.IsEnabled = selectedEntry != null;

            if (selectedEntry == null)
            {
                descriptionText.Text = string.Empty;
                return;
            }

            descriptionText.Text = selectedEntry.TagName + Environment.NewLine + selectedEntry.Description;
        };

        addButton.Click += delegate
        {
            var selectedEntry = tagListBox.SelectedItem as TagPickerEntry;
            if (selectedEntry == null)
            {
                return;
            }

            pickerWindow.Close(new TagPickerResult
            {
                TagName = selectedEntry.TagName
            });
        };

        cancelButton.Click += delegate
        {
            pickerWindow.Close((TagPickerResult?)null);
        };

        tagListBox.DoubleTapped += delegate
        {
            var selectedEntry = tagListBox.SelectedItem as TagPickerEntry;
            if (selectedEntry == null)
            {
                return;
            }

            pickerWindow.Close(new TagPickerResult
            {
                TagName = selectedEntry.TagName
            });
        };

        tagListBox.ItemTemplate = new FuncDataTemplate<TagPickerEntry?>((tagEntry, _) =>
        {
            var panel = new StackPanel
            {
                Spacing = 2,
                Margin = new Avalonia.Thickness(4)
            };

            if (tagEntry == null)
            {
                return panel;
            }

            panel.Children.Add(new TextBlock
            {
                Text = tagEntry.TagName ?? string.Empty,
                FontWeight = FontWeight.Bold
            });

            panel.Children.Add(new TextBlock
            {
                Text = tagEntry.Description ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.LightGray,
                FontSize = 11
            });

            return panel;
        });

        RefreshTagPickerList();

        var rootPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootPanel.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        rootPanel.Children.Add(tagListBox);
        Grid.SetRow(tagListBox, 1);

        rootPanel.Children.Add(descriptionText);
        Grid.SetRow(descriptionText, 2);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        buttonPanel.Children.Add(addButton);
        buttonPanel.Children.Add(cancelButton);

        rootPanel.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 3);

        pickerWindow.Content = rootPanel;

        var dialogResult = await pickerWindow.ShowDialog<TagPickerResult?>(this);
        if (dialogResult == null)
        {
            return string.Empty;
        }

        return dialogResult.TagName;
    }

    private async Task<string> PickTargetFileAsync()
    {
        var files = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Target DFN File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("DFN Files")
                {
                    Patterns = new[] { "*.dfn" }
                }
            }
        });

        if (files == null || files.Count == 0)
        {
            return string.Empty;
        }

        return files[0].Path.LocalPath;
    }

    private async Task<string> PromptForNewDfnFileNameAsync()
    {
        var dialogWindow = new Window
        {
            Title = "New DFN File",
            Width = 420,
            Height = 160,
            MinWidth = 420,
            MinHeight = 160,
            MaxHeight = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var fileNameTextBox = new TextBox
        {
            Watermark = "Enter file name",
            Text = "new_file.dfn"
        };

        var okButton = new Button
        {
            Content = "Create",
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        okButton.Click += delegate
        {
            dialogWindow.Close(fileNameTextBox.Text ?? string.Empty);
        };

        cancelButton.Click += delegate
        {
            dialogWindow.Close(string.Empty);
        };

        var rootPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootPanel.Children.Add(fileNameTextBox);
        Grid.SetRow(fileNameTextBox, 0);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        rootPanel.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 1);

        dialogWindow.Content = rootPanel;

        var result = await dialogWindow.ShowDialog<string>(this);
        return result ?? string.Empty;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialogWindow = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            MinWidth = 420,
            MinHeight = 180,
            MaxHeight = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        };

        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Width = 80
        };

        okButton.Click += delegate
        {
            dialogWindow.Close();
        };

        var rootPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootPanel.Children.Add(messageText);
        Grid.SetRow(messageText, 0);

        rootPanel.Children.Add(okButton);
        Grid.SetRow(okButton, 1);

        dialogWindow.Content = rootPanel;

        await dialogWindow.ShowDialog(this);
    }

    private async Task<bool> ConfirmDeleteDfnFileAsync(string fileName)
    {
        var dialogWindow = new Window
        {
            Title = "Delete DFN File",
            Width = 420,
            Height = 180,
            MinWidth = 420,
            MinHeight = 180,
            MaxHeight = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var messageText = new TextBlock
        {
            Text = "Delete DFN file '" + fileName + "'?\n\nThis cannot be undone unless you restore it from backup or source control.",
            TextWrapping = TextWrapping.Wrap
        };

        var deleteButton = new Button
        {
            Content = "Delete",
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        deleteButton.Click += delegate
        {
            dialogWindow.Close(true);
        };

        cancelButton.Click += delegate
        {
            dialogWindow.Close(false);
        };

        var rootPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootPanel.Children.Add(messageText);
        Grid.SetRow(messageText, 0);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        buttonPanel.Children.Add(deleteButton);
        buttonPanel.Children.Add(cancelButton);

        rootPanel.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 1);

        dialogWindow.Content = rootPanel;

        var result = await dialogWindow.ShowDialog<bool>(this);
        return result;
    }

    private async Task<string> PromptForRenameDfnFileNameAsync(string currentFileName)
    {
        var dialogWindow = new Window
        {
            Title = "Rename DFN File",
            Width = 420,
            Height = 160,
            MinWidth = 420,
            MinHeight = 160,
            MaxHeight = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var fileNameTextBox = new TextBox
        {
            Watermark = "Enter new file name",
            Text = currentFileName
        };

        var okButton = new Button
        {
            Content = "Rename",
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        okButton.Click += delegate
        {
            dialogWindow.Close(fileNameTextBox.Text ?? string.Empty);
        };

        cancelButton.Click += delegate
        {
            dialogWindow.Close(string.Empty);
        };

        var rootPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        rootPanel.Children.Add(fileNameTextBox);
        Grid.SetRow(fileNameTextBox, 0);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        rootPanel.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 1);

        dialogWindow.Content = rootPanel;

        var result = await dialogWindow.ShowDialog<string>(this);
        return result ?? string.Empty;
    }
}
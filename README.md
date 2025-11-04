# Beef's Recipes

<p align="center" width="100%">
<img alt="Beef's Notes Logo" src="./About/thumb.png" width="45%" />
</p>

Note-taking panel for keeping track of recipes, tasks, and reminders in-game

Notes are saved per-save-file with support for markdown formatting, interactive checkboxes, and colors

## Features

### Panel Controls
- Hover over the right edge of your screen to peek at the panel
- Click the right edge to pin it open
- Click the chevron (◀) to expand into full edit mode
- Click the chevron (▶) to collapse back to pinned mode
- Click the right edge when pinned to hide completely
- Press Escape to exit edit mode

### Section Controls
- Green **+** button adds new sections
- Red **X** button deletes sections
- Tab to move between title and content fields
- Click and drag the blue line on the left edge to rearrange sections
- Double-click the blue line to collapse a section (turns red)
- Double-click the red line to expand it again

### Text Controls
- `**bold text**` for **bold text**
- `*italic text*` for *italic text*
- `- [ ]` creates checkboxes you can check
- `- [x]` is a checked checkbox 

>[!NOTE]
> Checkboxes can be clicked to check/uncheck them in peek and pinned modes

### Customization
- Double-click or right-click text to open color picker (titles and content colored separately)
- Ctrl + Scroll to change font size
- Drag top or bottom handles to resize panel height
- Click and drag the ↕ button to move panel vertically

### Save System
- Notes saved per-save-file as `WorldName/notes/SaveName_notes.json`
- Automatically syncs when you save your game
- Old note files cleaned up when game deletes old saves
- JSON format allows manual editing
- Safe to remove mod without breaking saves

## Requirements

**WARNING:** This is a BepInEx Plugin Mod. It requires BepInEx to be installed.

See: [https://github.com/StationeersLaunchPad/StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad)

## Installation

1. Ensure you have BepInEx and StationeersLaunchPad installed.
2. Install from the Steam Workshop, or manually place the folder with DLL file into your `/BepInEx/plugins/` folder.

## Usage

Hover your mouse over the right edge of the screen to reveal the notes panel. Notes automatically save when you save your game.

## Changelog

>### Version 1.0.0
>- Initial release

## Known Issues

## Roadmap

## Source Code

The source code is available on GitHub: https://github.com/TheRealBeef/Beefs-Stationeers-Notes
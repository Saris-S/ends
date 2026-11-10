# Easy Node Dialogue System - Changelog

## Version 2.0.2

### [Updates]

- **Documentation:** Updated the documentation and moved screenshots to GitHub.

## Version 2.0.1

### [Fixes]

- **AI Graph Layout:** AI agents using MCP now arrange dialogue graphs with clearer spacing and easier-to-follow branches.
- **Graph Editor Performance:** Improved editor responsiveness when working with large dialogue graphs.

### [Added]

- **Script Icons:** ENDS runtime scripts now have a custom icon.

## Version 2.0.0

**Upgrade note:** Back up your project before updating. Export a fresh CSV from version 2.0 before continuing localization, as the text key format has changed.

### [MCP and collaboration]

- **MCP Integration:** Connect a compatible client for AI-assisted dialogue inspection and editing alongside the visual editor.
- **Multiple Clients:** Let clients work on separate dialogs with editing ownership and revision checks.
- **Client Activity:** View connected sessions, request activity, and recent changes.
- **Editing Control:** Reclaim client-owned dialogs through Take control.

### [Graph and workflow]

- **Changes Panel:** Review added, removed, and modified nodes, choices, conditions, consequences, and translations.
- **AI Highlights:** Identify pending and saved AI edits directly on graph fields.
- **Selective Saving:** Save or discard selected dialogs and shared data while leaving other work pending.
- **Undo & Redo:** Manual and MCP edits share Unity Undo/Redo.
- **Performance:** Improved editor responsiveness on large databases.

### [Localization and runtime]

- **Updated CSV Localization:** Readable dot-separated text keys and stable option IDs keep translations attached when options are reordered.
- **Numeric Consequences:** Added += and -= operations for numeric variables in choices.
- **Sprite Previews:** Fixed previews to display the selected sprite within a sprite sheet.
- **Redirect Targets:** Default target selection is now stored consistently for runtime playback.
- **Loop Protection:** Added a step limit to stop endless automatic node transitions.
- **Condition Diagnostics:** Increased the overlap check limit to 1024 scenarios per category and added a Console warning when the check is incomplete.

### [Upgrading and usability]

- **Package Upgrade:** Added automatic migration from the previous folder layout while preserving file GUIDs.
- **Legacy Colors:** Preserved automatic conversion of name colors from older databases.
- **Documentation:** Updated the user manual.
- **Release Notes:** Added version highlights at Unity startup and an optional review request when opening ENDS.

## Version 1.0.0

- **First release.**

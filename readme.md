## Fresh Tools
### Description
v.0.7.5

This is a collection of tools and code archives for doing usefull stuff that can be controlled via notification icon. 

### Features
- Hotkeys to manage windows (like WinsplitRevolution), with hotkeys for moving and scaling windows across monitors and positioning windows in screen quadrants.
- Hotkeys to adjust window transparency and send to back.
- Tool for saving and restoring all window positions.
- Option to run at startup (via registry key)
- Control windows volume with right mouse button and mouse wheel. (disabled by default in config)

### Hotkeys 
Defaults - Editable in config with [AHK modifiers](https://autohotkey.com/docs/Hotkeys.htm) and Case Sensitive [Key Names](https://msdn.microsoft.com/en-us/library/system.windows.forms.keys(v=vs.110).aspx)

Group | HotKey | Action
--- | --- | ---
General | CTRL SHIFT A | Move window left 1 screen
General | CTRL SHIFT S | Move window right 1 screen
General | CTRL ALT + | Decrease window transparency
General | CTRL ALT - | Increase window transparency
General | CTRL ALT W | Send window to back
Snap | CTRL ALT SHIFT 1 | Save Layout 1
Snap | CTRL ALT SHIFT 2 | Save Layout 2
Snap | CTRL ALT SHIFT 3 | Save Layout 3
Snap | CTRL ALT 1 | Restore Layout 1
Snap | CTRL ALT 2 | Restore Layout 2
Snap | CTRL ALT 3 | Restore Layout 3
Snap | CTRL ALT Num1 | Snap window to bottom left corner
Snap | CTRL ALT Num2 | Snap window to bottom
Snap | CTRL ALT Num3 | Snap window to bottom right corner
Snap | CTRL ALT Num4 | Snap window to left
Snap | CTRL ALT Num5 | Snap window to center
Snap | CTRL ALT Num6 | Snap window to right
Snap | CTRL ALT Num7 | Snap window to top left corner
Snap | CTRL ALT Num8 | Snap window to top
Snap | CTRL ALT Num9 | Snap window to top right corner
Snap | CTRL ALT Home | Move window to top left corner
Snap | CTRL ALT PageUp | Move window to top right corner
Snap | CTRL ALT End | Move window to bottom left corner
Snap | CTRL ALT PageDown | Move window to bottom right corner
Snap | CTRL ALT Multiply | Move window to Center
Snap Alt | CTRL ALT comma | Snap window to bottom left corner
Snap Alt | CTRL ALT period | Snap window to bottom
Snap Alt | CTRL ALT slash | Snap window to bottom right corner
Snap Alt | CTRL ALT K | Snap window to left
Snap Alt | CTRL ALT L | Snap window to center
Snap Alt | CTRL ALT semicolon | Snap window to right
Snap Alt | CTRL ALT I | Snap window to top left corner
Snap Alt | CTRL ALT O | Snap window to top
Snap Alt | CTRL ALT P | Snap window to top right corner

### Code Archives
- Code for creating global hotkeys linked strait to events.
- Code for global mouse listener (currently wheel only)
- Code for static LogSystem that can write to a set of scoped and tagged rolling log files.
- Code for using FixedLengthArrays
- Code for reading and writing from internal 'script' files. Used for doccumented config files.
- Code for profiling code
- Code for checking for and eliminating existing instances on current process
- Code to check windows ver (up to Windows 10)

### Future:
- Tool to monitoring a set of websites tracking up and down times.

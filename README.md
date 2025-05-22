# Winhance - Windows Enhancement Utility 🚀

**Winhance** is a C# application designed to optimize and customize your Windows experience. From software management to system optimizations and customization, Winhance provides functions to enhance Windows 10 and 11 systems.

**Winhance** features most of the same enhancements as [UnattendedWinstall](https://github.com/memstechtips/UnattendedWinstall) without needing to do a clean install of Windows.

## Requirements 💻
- Windows 10/11
  - *Tested on Windows 10 x64 22H2 and Windows 11 24H2*

## Installation 📥

Download from [winhance.net](https://winhance.net) or the [Releases](https://github.com/memstechtips/Winhance/releases) section of this repository.

The `Winhance.Installer.exe` includes an Installable and Portable version during setup.

## 🔐 Winhance v25.05.22 Security Info

**Important:** Please verify your download using the information below. Any file with different values for this particular version is not from the official source.

- **Winhance.Installer.exe**
  - Size: 131223680 bytes : 125 MiB  
  - SHA256: 5f20b7be5741ce37a8663041ae8228c28e45b32f7ca260036c34c38e436e634c  

- **Winhance.exe**
  - Size: 165248 bytes : 161 KiB  
  - SHA256: 58e1fc0707f25e71738388817b397cdb98aa8037b0c275424b6f56c74bc56b05  

> [!NOTE]
> This tool is currently in development. Any issues can be reported using the Issues tab.<br>
> Also, I'm not a developer, I'm just enjoying learning more about scripting/programming and learning as I go.<br><br>
> Please also understand that I prefer to develop and work on these projects independently.<br>I do value other people's insights and appreciate any feedback, but don't take it personally if a pull request is not accepted.

## Support the developer

It really does make a big difference, and is very much appreciated. Thanks<br>
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/memstechtips)

## Current Features 🛠️

### Software & Apps 💿
- **Windows Apps & Features Section**
  - Searchable interface with explanatory legend
  - Organized sections for Windows Apps, Legacy Capabilities, and Optional Features
  - One-click removal and installation of selected items
- **External Apps Section**
  - Install various useful applications via WinGet
  - Categories include Browsers, Multimedia utilities, Document viewers, and more

### Optimize 🚀
- Searchable interface with status indicators
- Toggle switches for each setting for better control
- Set UAC Notification Level
- Privacy Settings
- Gaming and Performance Optimizations
- Windows Updates
- Explorer Optimizations
- Power Settings with Power Plan selection
- Sound Settings
- Notification Preferences

### Customize 🎨
- Searchable interface with status indicators
- Toggle switches for each setting for better control
- Windows Theme selector (Dark/Light Mode) via dropdown
- Taskbar Customization
- Start Menu Customization
- Explorer Customizations

### Other Settings 
- Manage Your Winhance (and Windows) Settings with Wihance Configuration Files:
   - Save settings currently applied in Winhance to a config file for easy importing on a new system or after a fresh Windows install.
- Toggle Winhance's theme (Light/Dark Mode)

## 🗺️ ROADMAP/TODO
The items below are planned changes and features for future releases.

🔜 = Coming Soon
⌛ = In Progress
✅ = Completed

Note: ✅ Completed items are commited to the source code files, but the changes will not be visible in the application until the next release or update is released to the application.

### Winhance Installation
⌛ Add a Winhance Winget package to make Winhance installable via WinGet. Issue #159  
🔜 Add Winhance to the Microsoft Store.  

### Main Window

#### Config Import
🔜 Improve Config Import to have checkboxes for sub sections in each category 
🔜 Add a Invert Select option #168


### Software & Apps Screen

🔜 Add detection of installed apps and update notifications for those apps     
🔜 Add an option to enable and Activate Windows Photo Viewer. Issue #135  
⌛ Refactor app removal implementation to increase speed of app removals.  
⌛ Fix incorrect (failure) dialog being shown when a single app installation is cancelled.  

#### Windows Apps & Features
🔜 Add Icons next to the "Winhance Removal Status" that when clicked, deletes the scripts and scheduled tasks that are present when Winhance was previously run (ie. BloatRemoval.ps1, EdgeRemoval.ps1 and OneDriveRemoval.ps1)  
🔜 Rework EdgeRemoval script so it doesn't uninstall WebView. Also, update WebView installation  
🔜 Fix "We can't open this 'microsoft-edge' link" due to edge removal and no default browser found. Issue #38  
⌛ Features/apps still auto-removed even after I select and (re)install them from Winhance #175

#### External Software
🔜 For app installations, give users the option to choose a location to install the application. Issue #160  
🔜 Add a "website" icon next to each app in external software that will take the user to the specific app's webpage so users can get more info about the app before installing it. Issue #152  
🔜 Status Feature for External Software: Similar to Windows software, add a status feature for external applications to indicate whether they are installed. If installed, show if updates are available (updates indicator for windows softwares as well). Issue #142  
🔜 Indicator for App Purchases: Include an indicator for apps to show if they are completely free, partially free/paid, and completely paid. Issue #142  
🔜 Add ability to select the programs that users currently have installed on their computers to the external apps section and that they can be added to the config file. Issue #165 
🔜 Add some Software #170   
🔜 Add Meld to Development Apps #149  


### Optimize Screen

#### Power Management
🔜 Improve the power section to detect all power plans on the pc and add indicators next to each power plan in the dropdown menu showing what currently exists  
🔜 Improve the power section to have toggles that toggle the powercfg commands on the currently applied power plan (currently, importing the Ultimate Performance powerplan just automatically applies all "recommended" powercfg commands but there is no control over it) This will also fix #84  

#### Gaming & Performance
🔜 Review the "Mouse" related settings and why Enhance mouse precision won't work anymore due to mouse related tweaks in the gaming section. Issue #13  
🔜 Consider adding preset options in optimizations tab like "Minimal, recommended and extreme optimizations" (This can also be done with preset config files if developed and shared.)  

#### Explorer Improvements 
🔜 Toggle to remove the "open in terminal" option from right click context menu. Issue #162  
🔜 Add toggles to remove "-Shortcut" Text, Disable Always Ask Before Opening. Issue #135   

### Customize Screen

#### Taskbar
🔜 Fix News & Interests/Widgets/Weather icon not being removed from the Taskbar due to being a protected registry key.

#### Explorer
⌛ FR: enable end task to taskbar right click menu in win 11 #177  
⌛ Disable translucent selection rectangle not working in windows 11 file explorer #173  


### Other General Requests, Features & Issues
🔜 Debloat ink handwriting main store. Issue #65  
🔜 Add old F8 Menu to startup screen  
🔜 Can't type in Start Menu search bar. (This is due to tweaks included in the first version of Winhance, need to investigate). Issue #25  
🔜 Add translations for different languages (not currently a top priority). Issue #51  
⌛ Posting Template & Wiki. Issue #91  
  - Posting Template Implemented. Wiki will be created in the future.
🔜 Disable windows search indexing tool. Issue #74 
🔜 Add the Commandline Run option #172  
🔜 Add an Option to change the Mousepointer Size and Color #171



#### UI Improvements
🔜 Update tooltips to be more descriptive about what a toggle does  


## Feedback and Community

If you have feedback, suggestions, or need help with Winhance, please join the discussion on GitHub or our Discord community:

[![Join the Discussion](https://img.shields.io/badge/Join-the%20Discussion-2D9F2D?style=for-the-badge&logo=github&logoColor=white)](https://github.com/memstechtips/Winhance/discussions/183)
[![Join Discord Community](https://img.shields.io/badge/Join-Discord%20Community-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://www.discord.gg/zWGANV8QAX)